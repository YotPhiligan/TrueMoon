using System.Runtime.InteropServices;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;

namespace AlloyVulkanTest;

/// <summary>Exercises production dispatcher concurrency, scope isolation and cancellation on real windows.</summary>
internal static partial class HostedDispatcherProbe
{
    internal static async Task RunAsync(bool validation)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var glFactory = new HostedUiSessionFactory(new SkiaOpenGLRenderBackend());
        using var vkFactory = new HostedUiSessionFactory(new SkiaVulkanRenderBackend());
        var messages = new List<string>();
        var owners = new HashSet<int>();
        foreach (var (firstGL, siblingGL) in new[] { (true, true), (false, false), (true, false) })
        {
            await using var scope = new HostedUiDispatcher();
            await using var siblingScope = new HostedUiDispatcher();
            var firstFactory = firstGL ? glFactory : vkFactory;
            var siblingFactory = siblingGL ? glFactory : vkFactory;
            await using var first = new Live(firstFactory, firstGL, scope, "First", validation ? messages.Add : null);
            await using var sibling = new Live(siblingFactory, siblingGL, siblingScope, "Sibling", validation ? messages.Add : null);
            await first.Start(timeout.Token); await sibling.Start(timeout.Token);
            Require(first.Owner == sibling.Owner && first.Handle != sibling.Handle, "Windows did not share one owner with independent HWNDs.");
            owners.Add(first.Owner);
            Require(glFactory.ActiveSessionCount + vkFactory.ActiveSessionCount == 2 && Win32WindowDpi.ActiveHooks == 2, "Concurrent sessions/hooks mismatch.");
            first.Window.Resize(480, 300); sibling.Window.Resize(420, 280);
            await first.WaitFrames(first.Frames + 3, timeout.Token); await sibling.WaitFrames(sibling.Frames + 3, timeout.Token);
            var refreshed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            first.Window.PostWindow(host =>
            {
                try
                {
                    var before = first.Frames; SendMessageW(first.Handle, 0xF, 0, 0);
                    Require(first.Frames > before, "Native paint did not render on the shared loop.");
                    // Paint a sibling while the first's command/context is current.
                    before = sibling.Frames; SendMessageW(sibling.Handle, 0xF, 0, 0);
                    Require(sibling.Frames > before, "Sibling native refresh was not routed to its context.");
                    first.Window.PostWindow(_ =>
                    {
                        try
                        {
                            if (firstGL) Require(CurrentGLContext() == first.Context, "Next FIFO command inherited a sibling GL context.");
                            refreshed.TrySetResult();
                        }
                        catch (Exception error) { refreshed.TrySetException(error); throw; }
                    });
                }
                catch (Exception error) { refreshed.TrySetException(error); throw; }
            });
            await refreshed.Task.WaitAsync(timeout.Token);
            var beforeClose = sibling.Frames;
            first.Window.Close(); await first.Window.Completion.WaitAsync(timeout.Token);
            await sibling.WaitFrames(beforeClose + 3, timeout.Token);
            Require(first.Session!.IsDisposed && glFactory.ActiveSessionCount + vkFactory.ActiveSessionCount == 1, "Closing first affected sibling or retained its session.");

            await using var replacement = new Live(firstFactory, firstGL, scope, "Replacement", validation ? messages.Add : null);
            await replacement.Start(timeout.Token);
            Require(replacement.Owner == sibling.Owner, "Recreation changed owner thread.");
            var commandFault = new ArgumentException("owned command fault");
            replacement.Window.PostWindow(_ => throw commandFault);
            Require(ReferenceEquals(await Observe(() => replacement.Window.Completion.WaitAsync(timeout.Token)), commandFault), "Command fault identity changed.");
            beforeClose = sibling.Frames;
            await sibling.WaitFrames(beforeClose + 3, timeout.Token);

            var startupFault = new ArgumentException("owned startup fault");
            var badStartup = new HostedUiWindow(new SilkUiWindowOptions { OpenGL = firstGL,
                Width = 320, Height = 200, ValidationMessage = validation ? messages.Add : null }, firstFactory, () => throw startupFault, closed: null, dispatcher: scope);
            Require(ReferenceEquals(await Observe(() => badStartup.StartAsync(timeout.Token)), startupFault), "Startup fault identity changed.");
            Require(ReferenceEquals(await Observe(() => badStartup.Completion.WaitAsync(timeout.Token)), startupFault), "Startup Completion lost cause.");
            // Expected-fault windows are explicitly observed and are not disposed with
            // await using (DisposeAsync intentionally reports their Completion fault).
            await scope.StopAsync(timeout.Token);
            beforeClose = sibling.Frames;
            await sibling.WaitFrames(beforeClose + 3, timeout.Token);
            await siblingScope.StopAsync(timeout.Token);
            Require(sibling.Session!.IsDisposed, "Scope shutdown returned before cleanup.");
            Require(glFactory.ActiveSessionCount + vkFactory.ActiveSessionCount == 0, "Shutdown retained registered sessions.");
            VerifyHooks();
            Console.WriteLine($"Hosted pair GL={firstGL}/{siblingGL}: shared owner {first.Owner}, resize/selection, close/recreate, command/startup fault isolation, independent scopes passed.");
        }
        Require(owners.Count == 1, "Application scopes created new owner threads.");
        await Cancellation(glFactory, timeout.Token);
        VerifyHooks(); Require(glFactory.ActiveSessionCount == 0 && vkFactory.ActiveSessionCount == 0, "Cancellation retained sessions.");
        Require(messages.Count == 0, "Vulkan validation reported messages.");
        Console.WriteLine("Hosted dispatcher passed: GL/GL, Vulkan/Vulkan, GL/Vulkan; one process owner across scopes; startup/shutdown cancellation; registry/hooks zero; validation " + (validation ? "zero." : "not requested."));
    }

    private static async Task Cancellation(HostedUiSessionFactory factory, CancellationToken timeout)
    {
        await using var scope = new HostedUiDispatcher();
        var neverStarted = new HostedUiWindow(new SilkUiWindowOptions { OpenGL = true }, factory,
            () => throw new Exception("Closed-before-start root must not run"), closed: null, dispatcher: scope);
        neverStarted.Close();
        Require(neverStarted.Completion.IsCompletedSuccessfully
            && await Observe(() => neverStarted.StartAsync(timeout)) is OperationCanceledException,
            "Close before Start did not finish without allocating.");
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        var roots = 0;
        var window = new HostedUiWindow(new SilkUiWindowOptions { OpenGL = true, Width = 320, Height = 200 }, factory, () =>
        {
            entered.Set(); Require(release.Wait(TimeSpan.FromSeconds(10)), "Startup release timed out.");
            roots++; return new Text("Startup cancellation keeps accepted window");
        }, closed: null, dispatcher: scope);
        try
        {
            var start = window.StartAsync(cancellation.Token);
            Require(entered.Wait(TimeSpan.FromSeconds(10)), "Startup did not enter owner thread.");
            cancellation.Cancel();
            Require(await Observe(() => start) is OperationCanceledException && !window.Completion.IsCompleted, "Startup cancellation closed the window or ignored cancellation.");
            release.Set(); await window.StartAsync(timeout);
            Require(roots == 1, "Repeated Start allocated another root.");
            entered.Reset(); release.Reset();
            window.PostWindow(_ => { entered.Set(); Require(release.Wait(TimeSpan.FromSeconds(10)), "Shutdown release timed out."); });
            Require(entered.Wait(TimeSpan.FromSeconds(10)), "Shutdown command did not enter owner.");
            Require(await Observe(() => scope.StopAsync(cancellation.Token)) is OperationCanceledException, "Canceled shutdown wait was not canceled.");
            Require(!window.Completion.IsCompleted, "Shutdown returned before blocked command finished.");
            release.Set(); await scope.StopAsync(timeout); await window.Completion.WaitAsync(timeout);
            var rejected = new HostedUiWindow(new SilkUiWindowOptions { OpenGL = true }, factory, () => throw new Exception("Rejected root must not run"), closed: null, dispatcher: scope);
            Require(await Observe(() => rejected.StartAsync(timeout)) is ObjectDisposedException, "Stopped scope accepted a new window.");
            Require(await Observe(() => rejected.Completion) is ObjectDisposedException, "Rejected Completion did not report startup rejection.");
            Require(await Observe(() => window.PostAsTask()) is ObjectDisposedException, "Closed window accepted a command.");
            Console.WriteLine("Hosted cancellation: startup wait only, idempotent Start, shutdown continues after canceled wait, rejected starts/posts passed.");
        }
        finally { release.Set(); await window.StopAsync(timeout); }
    }

    private static Task PostAsTask(this HostedUiWindow window) { window.PostWindow(_ => { }); return Task.CompletedTask; }
    private static async Task<Exception?> Observe(Func<Task> operation)
    { try { await operation(); return null; } catch (Exception error) { return error; } }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    private static void VerifyHooks() => Require(Win32WindowDpi.ActiveHooks == 0 && Win32WindowChrome.ActiveHooks == 0
        && Win32TransparentFramebuffer.ActiveHooks == 0, "Dispatcher retained native hooks.");
    [LibraryImport("user32.dll")] private static partial nint SendMessageW(nint hwnd, uint message, nuint wparam, nint lparam);
    [LibraryImport("opengl32.dll", EntryPoint = "wglGetCurrentContext")] private static partial nint CurrentGLContext();

    private sealed class Live : IAsyncDisposable
    {
        internal readonly HostedUiWindow Window;
        private readonly TaskCompletionSource _firstFrame = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<(int Count, TaskCompletionSource Signal)> _waiters = [];
        private TextBox? _editor;
        private int _frames;
        internal int Frames => Volatile.Read(ref _frames);
        internal int Owner;
        internal nint Handle;
        internal nint Context;
        internal UiSession? Session;
        internal Live(HostedUiSessionFactory factory, bool openGL, HostedUiDispatcher scope, string title, Action<string>? validation)
        {
            Window = new HostedUiWindow(new SilkUiWindowOptions { OpenGL = openGL, Width = 400, Height = 240,
                Title = "Owned dispatcher " + title, ValidationMessage = validation }, factory,
                () => new Panel().WithChildren(_editor = new TextBox { Value = title + " 🧑‍💻", Width = 240, Height = 40 }), closed: null, dispatcher: scope);
            Window.PostWindow(host => { Owner = Environment.CurrentManagedThreadId; Handle = host.NativeWindow.Native!.Win32!.Value.Hwnd; Context = openGL ? CurrentGLContext() : 0; });
            Window.FramePresented += session =>
            {
                Require(Environment.CurrentManagedThreadId == Owner, "Frame owner changed.");
                if (Session == null)
                {
                    Session = session; session.Focus(_editor!);
                    session.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.End));
                    session.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Left, Shift: true));
                }
                Require(_editor!.Value == title + " 🧑‍💻" && _editor.SelectionLength == "🧑‍💻".Length, "Concurrent resize lost editor state.");
                Interlocked.Increment(ref _frames); _firstFrame.TrySetResult();
                foreach (var waiter in _waiters.Where(waiter => waiter.Count <= Frames).ToArray())
                { waiter.Signal.TrySetResult(); _waiters.Remove(waiter); }
            };
        }
        internal async Task Start(CancellationToken token)
        { await Window.StartAsync(token); await Wait(_firstFrame.Task, token); }
        internal Task WaitFrames(int count, CancellationToken token)
        {
            var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Window.PostWindow(_ => { if (Frames >= count) signal.TrySetResult(); else _waiters.Add((count, signal)); });
            return Wait(signal.Task, token);
        }
        private async Task Wait(Task signal, CancellationToken token)
        {
            if (await Task.WhenAny(signal, Window.Completion).WaitAsync(token) == Window.Completion)
            { await Window.Completion; throw new InvalidOperationException("Window closed before expected frame."); }
            await signal;
        }
        public async ValueTask DisposeAsync()
        {
            // Expected faults have already been asserted; healthy paths still wait for cleanup.
            if (!Window.Completion.IsFaulted) await Window.StopAsync();
        }
    }
}
