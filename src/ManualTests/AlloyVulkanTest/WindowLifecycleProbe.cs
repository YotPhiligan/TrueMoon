using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;

namespace AlloyVulkanTest;

/// <summary>Bounded native window recreation/resize probe; process memory is observational, not a VRAM budget.</summary>
internal sealed partial class WindowLifecycleProbe
{
    private readonly List<(string Name, WeakReference Reference)> _released = [];
    private readonly List<WindowResult> _windows = [];
    private readonly List<ProcessSample> _samples = [];
    private readonly Publisher _publisher = new();
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private ProcessSample? _warm;
    private bool _guiLimitExceeded;
    private sealed record WindowResult(int Cycle, string Backend, WindowTransparencyMode Transparency,
        bool CustomFrame, int Frames, int Resizes, int MinimizeRestore, int SwapchainGenerations, float NativeScale);
    private sealed record ProcessSample(int Windows, double Seconds, int Handles, int Threads, uint UserObjects,
        uint GdiObjects, long PrivateBytes, long WorkingSetBytes, long ManagedBytes, int RetainedReferences);

    internal static async Task RunAsync(string[] args)
    {
        var cycles = ReadValue(args, "--soak-cycles", int.Parse, 240);
        if (cycles is < 1 or > 10000) throw new ArgumentOutOfRangeException("--soak-cycles");
        var path = Path.GetFullPath(ReadValue(args, "--soak-output", value => value,
            "TestResults/AlloyLifecycle/window-lifecycle.json"));
        var probe = new WindowLifecycleProbe();
        Exception? failure = null;
        try { await probe.RunCoreAsync(cycles, args.Contains("--validation")); }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(new
                {
                    Utc = DateTime.UtcNow, RequestedWindows = cycles, ValidationRequested = args.Contains("--validation"), Completed = failure == null,
                    Failure = failure?.ToString(), ElapsedSeconds = probe._elapsed.Elapsed.TotalSeconds,
                    Windows = probe._windows, Samples = probe._samples,
                    DpiHooks = Win32WindowDpi.ActiveHooks, ChromeHooks = Win32WindowChrome.ActiveHooks,
                    TransparencyHooks = Win32TransparentFramebuffer.ActiveHooks, Subscriptions = probe._publisher.Count,
                    TrackedReferences = probe._released.Count, RetainedReferences = probe._released.Count(item => item.Reference.IsAlive),
                    GuiLimitExceeded = probe._guiLimitExceeded,
                    Scope = "Sequential owned HWNDs: GL/Vulkan x Opaque/Opacity/PerPixel x standard/custom; 3 logical resizes, native minimize/restore and explicit zero-size UI suspension per window. Weak refs cover window/host/session/tree/controls; registry and subscriptions checked each cycle. USER/GDI limited to warm checkpoint +2/+4. Process handles/bytes/threads are observations; no VRAM/driver profile, physical DPI transition or device-loss injection."
                }, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception error) when (failure != null)
            { Console.Error.WriteLine($"Lifecycle report could not be saved: {error}"); }
        }
        Console.WriteLine($"Window lifecycle passed: {cycles} HWNDs, {probe._windows.Sum(w => w.Frames)} frames, " +
            $"{probe._windows.Sum(w => w.Resizes)} resizes, {probe._windows.Sum(w => w.MinimizeRestore)} minimize/restore; " +
            $"hooks/registry/subscriptions/retained references zero; {(args.Contains("--validation") ? "Vulkan validation zero" : "validation not requested")}. Report: {path}");
    }

    private async Task RunCoreAsync(int cycles, bool validation)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Native lifecycle soak requires Windows.");
        VerifyNoHooks();
        _samples.Add(Sample(0));
        using var glFactory = new HostedUiSessionFactory(new SkiaOpenGLRenderBackend());
        using var vkFactory = new HostedUiSessionFactory(new SkiaVulkanRenderBackend());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(120, cycles * 5)));
        var modes = Enum.GetValues<WindowTransparencyMode>();
        for (var cycle = 0; cycle < cycles; cycle++)
        {
            var openGL = cycle % 2 == 0;
            var custom = cycle / (2 * modes.Length) % 2 != 0;
            _windows.Add(RunWindow(cycle, openGL ? glFactory : vkFactory, openGL,
                modes[cycle / 2 % modes.Length], custom, validation, timeout.Token));
            Require(glFactory.ActiveSessionCount == 0 && vkFactory.ActiveSessionCount == 0 && _publisher.Count == 0,
                "Registry or subscriptions retained after closing a window.");
            VerifyNoHooks();
            _publisher.Raise(); // A stale callback would access a disposed tree and fail here.
            if ((cycle + 1) % 12 == 0 || cycle + 1 == cycles)
            {
                await VerifyCollectionAsync(timeout.Token);
                var sample = Sample(cycle + 1); _samples.Add(sample);
                _warm ??= sample;
                if (sample.UserObjects > _warm.UserObjects + 2 || sample.GdiObjects > _warm.GdiObjects + 4)
                {
                    _guiLimitExceeded = true;
                    Console.Error.WriteLine($"USER/GDI exceeded warm limits: {sample.UserObjects}/{sample.GdiObjects}, warm {_warm.UserObjects}/{_warm.GdiObjects}.");
                }
                Console.WriteLine($"Lifecycle progress: {cycle + 1}/{cycles}; hooks/registry/subscriptions/roots 0; " +
                    $"USER/GDI {sample.UserObjects}/{sample.GdiObjects}; handles {sample.Handles}; " +
                    $"private {sample.PrivateBytes / 1048576.0:F1} MiB; managed {sample.ManagedBytes / 1048576.0:F1} MiB.");
            }
        }
        // Finish the bounded run to preserve the trend; an exceeded checkpoint still fails the result.
        Require(!_guiLimitExceeded, "USER/GDI exceeded the warm +2/+4 limits; see all checkpoints in the report.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private WindowResult RunWindow(int cycle, HostedUiSessionFactory factory, bool openGL,
        WindowTransparencyMode mode, bool custom, bool validation, CancellationToken token)
    {
        var messages = new List<string>();
        var options = new SilkUiWindowOptions { OpenGL = openGL, Width = 400, Height = 240,
            Title = $"Owned lifecycle {cycle}", Appearance = new WindowAppearance { Decorated = !custom,
                Transparency = mode, Opacity = mode == WindowTransparencyMode.Opacity ? .6f : 1 },
            Chrome = custom ? new WindowChromeOptions() : null, ValidationMessage = validation ? messages.Add : null,
            TitleBarFactory = custom ? _ => new HStack { Height = 30, WindowRegion = WindowRegionRole.Caption }
                .WithChildren(new Text("Lifecycle"), new Button("Client")) : null };
        UiSession? session = null; Panel? root = null; TextBox? editor = null; Button? button = null;
        nint hwnd = 0; var frames = 0; var phase = 0; var resizes = 0; var minimizations = 0;
        var restored = false; float scale = 0; var closed = 0;
        var window = new HostedUiWindow(options, factory, () =>
        {
            editor = new TextBox { Value = "Ирина 🧑‍💻", Width = 220, Height = 36 };
            button = new Button("Capture") { Width = 100, Height = 36, Margin = new Thickness(0, 60, 0, 0) };
            root = new Panel().WithChildren(editor, button);
            var ownedRoot = root;
            root.Own(_publisher.Subscribe(() => ownedRoot.Invalidate()));
            return root;
        }, () => closed++);
        window.PostWindow(host =>
        {
            hwnd = host.NativeWindow.Native!.Win32!.Value.Hwnd;
            Track(cycle, "host", host);
            scale = host.Viewport.Scale;
            Require(Win32WindowDpi.ActiveHooks == 1 && Win32WindowChrome.ActiveHooks == (custom ? 1 : 0)
                && Win32TransparentFramebuffer.ActiveHooks == (mode == WindowTransparencyMode.PerPixel ? 1 : 0), "Active native hook count mismatch.");
            double? restoreAt = null;
            void Update(double delta)
            {
                if (restoreAt == null && phase == 4 && minimizations == 0)
                {
                    host.Minimize();
                    Require(host.State == UiWindowState.Minimized, "Native minimize failed.");
                    session!.Resize(new UiViewport(0, 0, scale));
                    Require(!session.Update(), "Zero-size UI did not suspend.");
                    minimizations++; restoreAt = _elapsed.Elapsed.TotalSeconds + .05;
                }
                if (restoreAt is { } at && _elapsed.Elapsed.TotalSeconds >= at)
                {
                    host.Restore();
                    Require(host.State == UiWindowState.Normal, "Native restore failed.");
                    restored = true; restoreAt = null;
                }
            }
            host.NativeWindow.Update += Update;
            root!.Own(new CallbackSubscription(() => host.NativeWindow.Update -= Update));
        });
        window.FramePresented += ui =>
        {
            frames++;
            if (phase == 0)
            {
                session = ui;
                Require(factory.ActiveSessionCount == 1 && _publisher.Count == 1, "Active registry/subscription mismatch.");
                ui.Focus(editor!);
                ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.End));
                ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Left, Shift: true));
                Track(cycle, "session", ui); Track(cycle, "session-root", ui.Root);
                Track(cycle, "content-root", root!); Track(cycle, "editor", editor!); Track(cycle, "button", button!);
                window.Resize(480, 300); resizes++; phase = 1;
            }
            else if (phase == 1 && frames >= 5)
            { VerifyResize(ui, editor!, 480, 300, focused: true); window.Resize(360, 260); resizes++; phase = 2; }
            else if (phase == 2 && frames >= 10)
            { VerifyResize(ui, editor!, 360, 260, focused: true); window.Resize(440, 280); resizes++; phase = 3; }
            else if (phase == 3 && frames >= 15)
            { VerifyResize(ui, editor!, 440, 280, focused: true); phase = 4; }
            else if (phase == 4 && restored)
            {
                VerifyResize(ui, editor!, 440, 280, focused: false);
                _publisher.Raise();
                var bounds = button!.Bounds;
                Require(ui.HandleInput(new UiInput(InputKind.PointerDown, bounds.X + bounds.Width / 2,
                    bounds.Y + bounds.Height / 2)).PointerCaptured && button.IsPressed, "Capture was not active before close.");
                phase = 5; window.Close();
            }
        };
        Exception? windowFailure = null;
        try
        {
            window.StartAsync(token).GetAwaiter().GetResult();
            window.Completion.WaitAsync(token).GetAwaiter().GetResult();
            window.StopAsync(token).GetAwaiter().GetResult();
            Require(phase == 5 && resizes == 3 && minimizations == 1 && closed == 1 && frames >= 16, "Window cycle was incomplete.");
            Require(session!.IsDisposed && root!.IsDisposed && editor!.IsDisposed && button!.IsDisposed
                && !root.IsAttached && !editor.IsFocused && !button.IsPressed, "Tree/focus/capture cleanup failed.");
            Require(hwnd != 0 && IsWindow(hwnd) == 0, "Owned HWND survived window completion.");
            Require(messages.Count == 0, "Vulkan validation: " + string.Join(Environment.NewLine, messages));
            if (!openGL) Require(window.SwapchainGenerations >= 4, "Resize did not recreate Vulkan swapchains.");
            Track(cycle, "window", window);
            return new(cycle, openGL ? "OpenGL" : "Vulkan", mode, custom, frames, resizes, minimizations,
                window.SwapchainGenerations, scale);
        }
        catch (Exception error) { windowFailure = error; throw; }
        finally
        {
            window.Close();
            if (!window.Completion.IsCompleted)
                try { window.StopAsync().WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult(); }
                catch (Exception error) when (windowFailure != null) { throw new AggregateException(windowFailure, error); }
        }
    }

    private static void VerifyResize(UiSession ui, TextBox editor, int width, int height, bool focused)
    {
        Require(Math.Abs(ui.Viewport.Width / ui.Viewport.Scale - width) <= 1 / ui.Viewport.Scale
            && Math.Abs(ui.Viewport.Height / ui.Viewport.Scale - height) <= 1 / ui.Viewport.Scale, "Logical client resize mismatch.");
        Require(editor.Value == "Ирина 🧑‍💻" && editor.SelectionStart == "Ирина ".Length
            && editor.SelectionLength == "🧑‍💻".Length && (!focused || editor.IsFocused), "Resize lost editor value/selection/focus.");
    }
    private void Track(int cycle, string name, object value) => _released.Add(($"{cycle}:{name}", new WeakReference(value)));
    private async Task VerifyCollectionAsync(CancellationToken token)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await Task.Delay(50, token); // Completion can precede the UI thread's return by a few instructions.
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            if (_released.All(item => !item.Reference.IsAlive)) return;
        }
        throw new InvalidOperationException("Retained after GC: " + string.Join(", ", _released.Where(item => item.Reference.IsAlive).Select(item => item.Name)));
    }
    private ProcessSample Sample(int windows)
    {
        using var process = Process.GetCurrentProcess(); process.Refresh();
        var gdi = GuiCount(process.Handle, 0); var user = GuiCount(process.Handle, 1);
        return new(windows, _elapsed.Elapsed.TotalSeconds, process.HandleCount, process.Threads.Count,
            user, gdi, process.PrivateMemorySize64, process.WorkingSet64, GC.GetTotalMemory(false),
            _released.Count(item => item.Reference.IsAlive));
    }
    private static uint GuiCount(nint process, uint flags)
    {
        var count = GetGuiResources(process, flags);
        if (count == 0 && Marshal.GetLastPInvokeError() is var error && error != 0) throw new Win32Exception(error);
        return count;
    }
    private static void VerifyNoHooks() => Require(Win32WindowDpi.ActiveHooks == 0 && Win32WindowChrome.ActiveHooks == 0
        && Win32TransparentFramebuffer.ActiveHooks == 0, "Native hooks retained after teardown.");
    private static T ReadValue<T>(string[] args, string flag, Func<string, T> parse, T fallback)
    {
        var index = Array.IndexOf(args, flag);
        if (index < 0) return fallback;
        if (index + 1 == args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException(flag + " requires a value.");
        return parse(args[index + 1]);
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    private sealed class Publisher
    {
        private event Action? Tick;
        internal int Count { get; private set; }
        internal IDisposable Subscribe(Action callback)
        { Tick += callback; Count++; return new Subscription(() => { Tick -= callback; Count--; }); }
        internal void Raise() => Tick?.Invoke();
        private sealed class Subscription(Action unsubscribe) : IDisposable
        {
            private Action? _unsubscribe = unsubscribe;
            public void Dispose() => Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
        }
    }
    private sealed class CallbackSubscription(Action unsubscribe) : IDisposable
    {
        private Action? _unsubscribe = unsubscribe;
        public void Dispose() => Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
    }
    [LibraryImport("user32.dll")] private static partial int IsWindow(nint hwnd);
    [LibraryImport("user32.dll", SetLastError = true)] private static partial uint GetGuiResources(nint process, uint flags);
}
