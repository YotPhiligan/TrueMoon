using System.Diagnostics;
using System.ComponentModel;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;

namespace AlloyVulkanTest;

/// <summary>Serial native thread-lifetime diagnostics with an optional direct OpenGL UI control.</summary>
internal static partial class WindowNativeResourceProbe
{
    internal static void Run(string[] args)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("USER/GDI control requires Windows.");
        var singleThread = args.Contains("--native-single-thread");
        var openGL = !args.Contains("--native-vulkan-window");
        var directSilk = args.Contains("--native-direct-silk");
        var rawWgl = args.Contains("--native-raw-wgl");
        var rawWindow = args.Contains("--native-raw-window");
        var uiControl = args.Contains("--native-ui-control");
        if (uiControl && (rawWgl || rawWindow || directSilk || !openGL))
            throw new ArgumentException("UI control requires the TrueMoon OpenGL host.");
        if (rawWgl && rawWindow) throw new ArgumentException("Choose either raw WGL or raw window control.");
        if ((rawWgl || rawWindow) && (directSilk || args.Contains("--native-vulkan-window")))
            throw new ArgumentException("Raw controls cannot be combined with Silk/Vulkan controls.");
        openGL = !rawWindow && openGL;
        var cycles = ReadInt(args, "--soak-cycles", 24);
        var workers = ReadInt(args, "--native-workers", 0);
        if (cycles is < 1 or > 10000 || workers is < 0 or > 8 || singleThread && workers != 0)
            throw new ArgumentOutOfRangeException(nameof(args), "Use 1..10000 cycles; workers 0..8 (exclusive with single-thread).");
        var outputIndex = Array.IndexOf(args, "--soak-output");
        if (outputIndex >= 0 && (outputIndex + 1 == args.Length || args[outputIndex + 1].StartsWith("--", StringComparison.Ordinal)))
            throw new ArgumentException("--soak-output requires a path.");
        var path = Path.GetFullPath(outputIndex >= 0 ? args[outputIndex + 1] : "TestResults/AlloyLifecycle/native-control.json");
        var samples = new List<object>();
        var windows = new List<object>();
        var released = new List<WeakReference>();
        using var process = Process.GetCurrentProcess();
        var pool = Enumerable.Range(0, workers).Select(_ => new Worker()).ToArray();
        Exception? runFailure = null;
        object Sample(int cycle, string phase)
        {
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            process.Refresh();
            var ownWindows = NativeWindowDiagnostics.OwnWindows();
            var sample = new
            {
                Cycle = cycle,
                Phase = phase,
                User = GuiCount(process.Handle, 1),
                Gdi = GuiCount(process.Handle, 0),
                Handles = process.HandleCount,
                PrivateBytes = process.PrivateMemorySize64,
                ManagedBytes = GC.GetTotalMemory(false),
                ThreadIds = process.Threads.Cast<ProcessThread>().Select(t => t.Id).ToArray(),
                OwnWindows = ownWindows,
                RetainedUiReferences = released.Count(reference => reference.IsAlive)
            };
            Console.WriteLine($"Native control {cycle}/{phase}: USER/GDI {sample.User}/{sample.Gdi}; private {sample.PrivateBytes}; HWNDs {ownWindows.Count}.");
            return sample;
        }
        try
        {
            samples.Add(Sample(0, "initial"));
            for (var cycle = 0; cycle < cycles; cycle++)
            {
                uint owner = 0;
                object? graphics = null;
                object? afterInitialize = null;
                object? beforeThreadExit = null;
                void Create()
                {
                    owner = NativeWindowDiagnostics.ThreadId;
                    void Initialized(object? info)
                    {
                        graphics = info;
                        afterInitialize = NativeWindowDiagnostics.OwnWindows();
                    }
                    if (rawWgl || rawWindow)
                    {
                        NativeWindowDiagnostics.RunRaw(rawWgl, Initialized);
                    }
                    else
                    {
                        if (directSilk)
                        {
                            var options = openGL ? WindowOptions.Default : WindowOptions.DefaultVulkan;
                            options.Size = new Vector2D<int>(320, 200); options.Title = "Direct Silk resource control";
                            using var window = Window.Create(options);
                            window.Initialize(); var directFrames = 0;
                            Initialized(openGL ? NativeWindowDiagnostics.GraphicsInfo() : null);
                            window.Render += _ => { window.GLContext?.SwapBuffers(); if (++directFrames == 3) window.Close(); };
                            window.Run();
                        }
                        else
                        {
                            using var host = new SilkWindowHost(320, 200, "Owned native resource control", openGL);
                            Initialized(openGL ? NativeWindowDiagnostics.GraphicsInfo() : null);
                            if (uiControl) RunUi(host, released);
                            else
                            {
                                var frames = 0;
                                host.RenderRequested += () => { host.NativeWindow.GLContext?.SwapBuffers(); if (++frames == 3) host.Close(); };
                                host.Run();
                            }
                        }
                    }
                    beforeThreadExit = NativeWindowDiagnostics.OwnWindows();
                }
                if (singleThread) Create();
                else if (workers != 0) pool[cycle % workers].Invoke(Create);
                else
                {
                    Exception? failure = null;
                    var thread = new Thread(() => { try { Create(); } catch (Exception error) { failure = error; } });
                    thread.IsBackground = true; thread.Start();
                    if (!thread.Join(TimeSpan.FromSeconds(30))) throw new TimeoutException("Native control thread did not finish.");
                    if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
                }
                windows.Add(new
                {
                    Cycle = cycle + 1,
                    OwnerThread = owner,
                    Graphics = graphics,
                    AfterInitialize = afterInitialize,
                    AfterDisposeOnOwner = beforeThreadExit
                });
                samples.Add(Sample(cycle + 1, "after-window"));
                if (uiControl && (released.Any(reference => reference.IsAlive) || Win32WindowDpi.ActiveHooks != 0
                    || Win32WindowChrome.ActiveHooks != 0 || Win32TransparentFramebuffer.ActiveHooks != 0))
                    throw new InvalidOperationException("UI control retained a completed window graph or hooks.");
            }
        }
        catch (Exception error) { runFailure = error; }
        finally
        {
            void Cleanup(Action action)
            {
                try { action(); }
                catch (Exception error) { runFailure = runFailure == null ? error : new AggregateException(runFailure, error); }
            }
            foreach (var worker in pool) Cleanup(worker.Dispose);
            Cleanup(() => samples.Add(Sample(windows.Count, "workers-joined")));
            // A fixed quiescence observation, not a retry-until-pass loop.
            Thread.Sleep(1000);
            Cleanup(() => samples.Add(Sample(windows.Count, "quiescent")));
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(new
                {
                    Utc = DateTime.UtcNow,
                    Completed = runFailure == null,
                    Failure = runFailure?.ToString(),
                    RequestedCycles = cycles,
                    OpenGL = openGL,
                    SingleThread = singleThread,
                    DirectSilk = directSilk,
                    RawWgl = rawWgl,
                    RawWindow = rawWindow,
                    UiControl = uiControl,
                    TrackedUiReferences = released.Count,
                    Workers = workers,
                    Windows = windows,
                    Samples = samples,
                    Scope = "Serial thread-lifetime diagnostic; workers are reused round-robin, not concurrent windows. Raw WGL uses a hidden STATIC HWND and legacy context. Optional UI control creates OpenGL Skia/session/editor, 12 frames and 3 resizes per window; it does not exercise HostedUiWindow scheduling, Vulkan, chrome/alpha, minimize, VRAM or full K7. HWND census does not enumerate all USER objects. No new production thread contract."
                },
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception error) when (runFailure != null) { Console.Error.WriteLine($"Native report/cleanup failed: {error}"); }
        }
        if (runFailure != null) ExceptionDispatchInfo.Capture(runFailure).Throw();
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void RunUi(SilkWindowHost host, List<WeakReference> released)
    {
        using var factory = new HostedUiSessionFactory(new SkiaOpenGLRenderBackend());
        var editor = new TextBox { Value = "Ирина 🧑‍💻", Width = 220, Height = 36 };
        var root = new Panel().WithChildren(editor);
        using var session = factory.Create(root, OpenGLProbe.Target(host), host.Viewport, host);
        var owner = NativeWindowDiagnostics.ThreadId;
        var frames = 0;
        Exception? failure = null, closingFailure = null;
        void Closing()
        {
            try { session.Dispose(); }
            catch (Exception error) { closingFailure = error; }
        }
        void Render()
        {
            if (owner != NativeWindowDiagnostics.ThreadId) throw new InvalidOperationException("UI owner changed.");
            if (session.Viewport != host.Viewport) session.Resize(host.Viewport);
            session.Update(); session.PresentOpenGL(); host.NativeWindow.GLContext!.SwapBuffers();
            if (++frames == 1)
            {
                session.Focus(editor);
                session.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.End));
                session.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Left, Shift: true));
            }
            if (frames == 3) host.Resize(480, 300);
            if (frames == 6) host.Resize(360, 260);
            if (frames == 9) host.Resize(440, 280);
            if (frames >= 12)
            {
                if (editor.Value != "Ирина 🧑‍💻" || editor.SelectionLength == 0 || !editor.IsFocused)
                    throw new InvalidOperationException("UI resize lost text/focus/selection.");
                host.Close();
            }
        }
        host.NativeWindow.Closing += Closing;
        host.RenderRequested += Render;
        try { host.Run(); }
        catch (Exception error) { failure = error; }
        finally
        {
            host.NativeWindow.Closing -= Closing; host.RenderRequested -= Render;
            UiCleanup.Complete(failure,
                () => { if (closingFailure != null) ExceptionDispatchInfo.Capture(closingFailure).Throw(); }, session.Dispose);
        }
        if (frames < 12 || factory.ActiveSessionCount != 0 || !root.IsDisposed || !session.IsDisposed)
            throw new InvalidOperationException("UI control did not finish/dispose.");
        released.Add(new WeakReference(host)); released.Add(new WeakReference(session));
        released.Add(new WeakReference(root)); released.Add(new WeakReference(editor));
    }
    private static int ReadInt(string[] args, string name, int fallback)
    {
        var index = Array.IndexOf(args, name);
        if (index < 0) return fallback;
        if (index + 1 == args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException($"{name} requires a number.");
        return int.Parse(args[index + 1]);
    }
    private sealed class Worker : IDisposable
    {
        private readonly BlockingCollection<Action> _work = new();
        private readonly Thread _thread;
        internal Worker()
        {
            _thread = new Thread(() => { foreach (var operation in _work.GetConsumingEnumerable()) operation(); })
            { IsBackground = true, Name = "Native control reusable worker" };
            _thread.Start();
        }
        internal void Invoke(Action operation)
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _work.Add(() => { try { operation(); done.SetResult(); } catch (Exception error) { done.SetException(error); } });
            done.Task.WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
        }
        public void Dispose()
        {
            _work.CompleteAdding();
            if (!_thread.Join(TimeSpan.FromSeconds(30))) throw new TimeoutException("Native worker did not stop.");
            _work.Dispose();
        }
    }
    private static uint GuiCount(nint process, uint flags)
    {
        var count = GetGuiResources(process, flags);
        if (count == 0 && Marshal.GetLastPInvokeError() is var error && error != 0) throw new Win32Exception(error);
        return count;
    }
    [LibraryImport("user32.dll", SetLastError = true)] private static partial uint GetGuiResources(nint process, uint flags);
}
