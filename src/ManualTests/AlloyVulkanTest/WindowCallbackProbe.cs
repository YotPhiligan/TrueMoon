using System.Runtime.InteropServices;
using System.Text.Json;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;

namespace AlloyVulkanTest;

// Directed messages target only owned HWNDs; no global hooks, input injection or settings changes.
internal static partial class WindowCallbackProbe
{
    [StructLayout(LayoutKind.Sequential)] private struct NativeSize { internal int Width, Height; }
    private sealed record Result(string Scope, bool OpenGL, WindowTransparencyMode Mode, string Fault, int Frames);

    internal static async Task RunAsync(string[] args)
    {
        var results = new List<Result>();
        foreach (var kind in new[] { "mouse", "key", "text", "focus", "render", "dpi" })
            Direct(kind);
        foreach (var openGL in new[] { true, false })
        foreach (var mode in Enum.GetValues<WindowTransparencyMode>())
        foreach (var fault in new[] { "subscriber", "dpi" })
            results.Add(await HostedAsync(openGL, mode, fault, args.Contains("--validation")));
        VerifyNoHooks();
        var index = Array.IndexOf(args, "--callback-output");
        if (index >= 0 && index + 1 == args.Length) throw new ArgumentException("--callback-output requires a path.");
        var path = Path.GetFullPath(index >= 0 ? args[index + 1] : "TestResults/AlloyCallbacks/callbacks.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(new { Utc = DateTime.UtcNow, DirectCases = 6,
            HostedCases = results.Count, RecreatedWindows = results.Count, Results = results,
            DpiHooks = Win32WindowDpi.ActiveHooks, ChromeHooks = Win32WindowChrome.ActiveHooks,
            TransparencyHooks = Win32TransparentFramebuffer.ActiveHooks,
            Scope = "Directed native callback faults and managed render dispatch, not physical DPI/device loss or VRAM soak." },
            new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Callback fault proof passed: 6 direct cases, {results.Count} hosted failures + healthy recreations; all hooks/registry zero; validation zero. Report: {path}");
    }

    private static void Direct(string kind)
    {
        var host = new SilkWindowHost(400, 240, "Owned callback fault " + kind, true,
            new WindowAppearance { Decorated = false, Transparency = WindowTransparencyMode.PerPixel },
            new WindowChromeOptions());
        var hwnd = host.NativeWindow.Native!.Win32!.Value.Hwnd;
        var expected = new InvalidOperationException("subscriber " + kind);
        var calls = 0;
        void Input(UiInput _) { calls++; throw expected; }
        host.Input += Input;
        host.RenderRequested += () => { calls++; throw expected; };
        try
        {
            Require(Win32WindowDpi.ActiveHooks == 1 && Win32WindowChrome.ActiveHooks == 1
                && Win32TransparentFramebuffer.ActiveHooks == 1, "Expected all three native hooks.");
            Trigger(host, kind);
            var error = Observe(host.VerifyWindowAccess);
            if (kind == "dpi")
            {
                Require(error is ArgumentOutOfRangeException { ParamName: "dpi" }
                    && error.StackTrace!.Contains("SetScaledSize", StringComparison.Ordinal), "Not a real DPI callback fault.");
                Require(calls == 0, "DPI injection unexpectedly dispatched input.");
            }
            else Require(calls == 1 && ReferenceEquals(error, expected), "Native subscriber fault was lost/swallowed.");
            // Pointer normalization must not rethrow the DPI hook fault across GLFW, and
            // subsequent key/text/focus/render callbacks must stop before user code.
            var before = calls;
            foreach (var subsequent in new[] { "key", "text", "focus", "render", "mouse", "dpi" }) Trigger(host, subsequent);
            Require(calls == before && ReferenceEquals(Observe(host.VerifyWindowAccess), error), "Later callback replaced the first cause/dispatched user code.");
        }
        finally { host.Dispose(); host.Dispose(); }
        Require(IsWindow(hwnd) == 0, "Owned HWND survived cleanup.");
        VerifyNoHooks();
        Console.WriteLine($"Direct {kind}: normal native return, deferred first cause, later handlers suppressed, HWND/hooks released.");
    }

    private static async Task<Result> HostedAsync(bool openGL, WindowTransparencyMode mode, string fault, bool validation)
    {
        var messages = new List<string>();
        var options = new SilkUiWindowOptions { OpenGL = openGL, Width = 400, Height = 240,
            Title = "Owned hosted callback fault", Appearance = new WindowAppearance { Decorated = false,
                Transparency = mode, Opacity = mode == WindowTransparencyMode.Opacity ? .6f : 1 },
            Chrome = new WindowChromeOptions(), ValidationMessage = validation ? messages.Add : null };
        using var factory = new HostedUiSessionFactory(openGL ? new SkiaOpenGLRenderBackend() : new SkiaVulkanRenderBackend());
        var expected = new InvalidOperationException("hosted input subscriber");
        var cleanup = new InvalidOperationException("controlled close cleanup");
        var ready = new TaskCompletionSource<UiSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        TextBox? editor = null; Panel? root = null; var closed = 0; nint hwnd = 0;
        var window = new HostedUiWindow(options, factory, () =>
        {
            editor = new TextBox { Value = "retained", Width = 180, Height = 36 };
            root = new Panel().WithChildren(editor); return root;
        }, () => { closed++; throw cleanup; });
        window.FramePresented += ui => ready.TrySetResult(ui);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await window.StartAsync(timeout.Token);
        var session = await ready.Task.WaitAsync(timeout.Token);
        var injected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.PostWindow(host =>
        {
            try
            {
                hwnd = host.NativeWindow.Native!.Win32!.Value.Hwnd;
                session.Focus(editor!); session.Capture(editor!);
                if (fault == "subscriber") host.Input += _ => throw expected;
                Trigger(host, fault == "dpi" ? "dpi" : "mouse");
                // Force normalization after a stored hook error while still inside the owned command.
                if (fault == "dpi") Trigger(host, "mouse");
                injected.TrySetResult();
            }
            catch (Exception error) { injected.TrySetException(error); throw; }
        });
        await injected.Task.WaitAsync(timeout.Token);
        var completion = await ObserveAsync(() => window.Completion.WaitAsync(timeout.Token));
        var errors = ((AggregateException)completion).Flatten().InnerExceptions;
        Require(errors.Count == 2 && ReferenceEquals(errors[1], cleanup), "Cleanup error masked/reordered native first cause.");
        if (fault == "subscriber") Require(ReferenceEquals(errors[0], expected), "Input subscriber cause identity lost.");
        else Require(errors[0] is ArgumentOutOfRangeException { ParamName: "dpi" }
            && errors[0].StackTrace!.Contains("SetScaledSize", StringComparison.Ordinal), "Completion lost the DPI callback cause.");
        Require(ReferenceEquals(await ObserveAsync(() => window.StopAsync(timeout.Token)), completion), "StopAsync changed Completion cause identity.");
        Require(closed == 1 && session.IsDisposed && root!.IsDisposed && editor!.IsDisposed && !root.IsAttached
            && !editor.IsFocused && factory.ActiveSessionCount == 0 && IsWindow(hwnd) == 0, "Faulted window/session/tree/registry cleanup failed.");
        VerifyNoHooks();
        var healthy = new HostedUiWindow(options, factory, () => new Panel());
        var frames = 0;
        healthy.FramePresented += _ => { if (++frames == 3) healthy.Close(); };
        await healthy.StartAsync(timeout.Token); await healthy.Completion.WaitAsync(timeout.Token); await healthy.StopAsync(timeout.Token);
        Require(frames >= 3 && factory.ActiveSessionCount == 0 && messages.Count == 0, "Healthy recreation or Vulkan validation failed.");
        VerifyNoHooks();
        Console.WriteLine($"Hosted {(openGL ? "OpenGL" : "Vulkan")}/{mode}/{fault}: original + cleanup errors, Completion/StopAsync identity, tree/registry/hooks zero, healthy recreation {frames} frames, validation zero.");
        return new("Hosted", openGL, mode, fault, frames);
    }

    private static unsafe void Trigger(SilkWindowHost host, string kind)
    {
        var hwnd = host.NativeWindow.Native!.Win32!.Value.Hwnd;
        switch (kind)
        {
            case "mouse": SendMessageW(hwnd, 0x201, 1, (nint)(40 | 80 << 16)); SendMessageW(hwnd, 0x202, 0, (nint)(40 | 80 << 16)); break;
            case "key": SendMessageW(hwnd, 0x100, 0x41, 1); SendMessageW(hwnd, 0x101, 0x41, unchecked((nint)0xC0000001u)); break;
            case "text": SendMessageW(hwnd, 0x102, 0x58, 1); break;
            case "focus": SendMessageW(hwnd, 0x8, 0, 0); break;
            case "render": host.NativeWindow.DoRender(); break;
            case "dpi": var size = new NativeSize { Width = 400, Height = 240 }; SendMessageW(hwnd, 0x2E4, 0, (nint)(&size)); break;
            default: throw new ArgumentException("Unknown fault kind.", nameof(kind));
        }
    }
    private static Exception Observe(Action operation)
    { try { operation(); } catch (Exception error) { return error; } throw new InvalidOperationException("Expected deferred failure."); }
    private static async Task<Exception> ObserveAsync(Func<Task> operation)
    { try { await operation(); } catch (Exception error) { return error; } throw new InvalidOperationException("Expected asynchronous failure."); }
    private static void VerifyNoHooks() => Require(Win32WindowDpi.ActiveHooks == 0 && Win32WindowChrome.ActiveHooks == 0
        && Win32TransparentFramebuffer.ActiveHooks == 0, "Native hooks retained after teardown.");
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [LibraryImport("user32.dll")] private static partial nint SendMessageW(nint hwnd, uint message, nuint wparam, nint lparam);
    [LibraryImport("user32.dll")] private static partial int IsWindow(nint hwnd);
}
