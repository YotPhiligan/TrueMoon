using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.Json;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;

internal static partial class AsyncCloseProbe
{
    internal static async Task RunAsync(string[] args)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60)); var token = deadline.Token;
        var messages = new ConcurrentQueue<string>(); var reports = new List<object>();
        foreach (var openGL in new[] { true, false })
        {
            using var gl = new HostedUiSessionFactory(new SkiaOpenGLRenderBackend());
            using var vk = new HostedUiSessionFactory(new SkiaVulkanRenderBackend());
            await using var scope = new HostedUiDispatcher();
            await using var first = new Live(openGL ? gl : vk, openGL, scope, args.Contains("--validation") ? messages.Enqueue : null);
            await using var sibling = new Live(openGL ? vk : gl, !openGL, scope, args.Contains("--validation") ? messages.Enqueue : null);
            await first.Start(token); await sibling.Start(token);
            var mode = 0; var calls = 0; var reasons = new ConcurrentQueue<UiCloseReason>();
            var saveError = new IOException("expected save failure"); var save = Gate(); var entered = Gate();
            using var releaseSave = new Cleanup(() => save.TrySetResult(true));
            first.Window.Closing += async reason =>
            {
                Require(Environment.CurrentManagedThreadId == first.Owner, "Closing did not start on owner.");
                reasons.Enqueue(reason); Interlocked.Increment(ref calls);
                if (Volatile.Read(ref mode) == 0) return false;
                if (Volatile.Read(ref mode) == 1) throw saveError;
                entered.TrySetResult(true);
                await save.Task;
                await first.Session!.InvokeAsync(() => Require(!first.Root.IsDisposed, "UI disposed during save/drain."));
                return true;
            };
            NativeClose(first.Handle);
            await Until(() => Volatile.Read(ref calls) == 1 && !first.Window.IsCloseRequested, token);
            Require(!first.Window.Completion.IsCompleted && !first.Root.IsDisposed && reasons.Last() == UiCloseReason.Native, "Native veto closed window.");
            Volatile.Write(ref mode, 1); NativeClose(first.Handle);
            await Until(() => ReferenceEquals(first.Window.CloseError, saveError), token);
            Require(!first.Window.Completion.IsCompleted && !first.Root.IsDisposed, "Failed native save closed window.");

            Volatile.Write(ref mode, 2); var request = first.Window.RequestCloseAsync(); var joined = first.Window.RequestCloseAsync();
            await entered.Task.WaitAsync(token);
            using (var cancel = new CancellationTokenSource())
            {
                var wait = first.Window.RequestCloseAsync(cancel.Token); cancel.Cancel();
                Require(await Observe(() => wait) is OperationCanceledException, "Close wait cancellation failed.");
            }
            await sibling.Session!.InvokeAsync(() => sibling.Root.Background = new Color(23, 45, 67)).WaitAsync(token);
            first.Window.Resize(340, 220); first.Window.Minimize();
            await Until(() => first.Window.Diagnostics!.Snapshot.State == UiSessionState.Suspended, token);
            await first.Session!.InvokeAsync(() => first.Root.Background = new Color(12, 34, 56)).WaitAsync(token);
            first.Window.Restore();
            await Until(() => first.Window.Diagnostics!.Snapshot.State == UiSessionState.Active, token);
            Require(!request.IsCompleted && !joined.IsCompleted && !first.Root.IsDisposed && calls == 3, "Concurrent close did not join or drained too early.");
            save.SetResult(true);
            Require(await request.WaitAsync(token) && await joined.WaitAsync(token), "Approved request did not complete.");
            Require(first.Window.IsClosed && first.Root.IsDisposed && first.CleanupOwner == first.Owner && !sibling.Root.IsDisposed, "Close cleanup affinity/sibling lifetime failed.");
            Require(await first.Window.RequestCloseAsync(token), "Repeated closed request failed.");

            var drain = Gate(); var draining = Gate(); var remaining = 0;
            using var releaseDrain = new Cleanup(() => drain.TrySetResult(true));
            sibling.Window.Closing += async reason =>
            {
                Require(reason == UiCloseReason.Native, "Expected native origin before shutdown upgrade.");
                draining.SetResult(true); await drain.Task; return false;
            };
            sibling.Window.Closing += reason =>
            {
                Require(reason == UiCloseReason.Shutdown, "App Stop did not upgrade remaining handler.");
                Interlocked.Increment(ref remaining); return Task.FromResult(false);
            };
            NativeClose(sibling.Handle); await draining.Task.WaitAsync(token);
            var appStop = scope.StopAsync(token);
            Require(!appStop.IsCompleted && !sibling.Root.IsDisposed, "App Stop freed resources before drain.");
            await sibling.Session.InvokeAsync(() => Require(!sibling.Root.IsDisposed, "Pending shutdown lost UI access.")).WaitAsync(token);
            drain.SetResult(true); await appStop;
            Require(sibling.Window.IsClosed && sibling.Root.IsDisposed && remaining == 1 && sibling.CleanupOwner == sibling.Owner, "Forced shutdown did not drain/ignore veto/clean on owner.");

            await using var failureScope = new HostedUiDispatcher();
            var cleanupError = new IOException("expected cleanup failure"); var handlerError = new IOException("expected forced drain failure");
            var commandError = new IOException("expected native command failure"); var failureDrain = Gate(); var failureEntered = Gate(); var commandEntered = Gate();
            using var releaseFailureDrain = new Cleanup(() => failureDrain.TrySetResult(true));
            var failed = new Live(openGL ? gl : vk, openGL, failureScope, args.Contains("--validation") ? messages.Enqueue : null, cleanupError);
            await failed.Start(token);
            failed.Window.Closing += async _ => { failureEntered.SetResult(true); await failureDrain.Task; return false; };
            failed.Window.Closing += _ => Task.FromException<bool>(handlerError);
            var failedRequest = failed.Window.RequestCloseAsync(token); await failureEntered.Task.WaitAsync(token);
            failed.Window.PostWindow(_ => { commandEntered.SetResult(true); throw commandError; }); await commandEntered.Task.WaitAsync(token);
            Require(!failed.Window.Completion.IsCompleted && !failed.Root.IsDisposed, "Native failure freed resources during drain.");
            failureDrain.SetResult(true);
            var failure = await Observe(() => failedRequest);
            Require(failure != null && Flatten(failure).Contains(cleanupError) && Flatten(failure).Contains(handlerError)
                && Flatten(failure).Contains(commandError), "Shutdown lost native/handler/cleanup errors.");
            Require(!failed.Window.IsClosed && failed.Window.Completion.IsFaulted && failed.Root.IsDisposed && failed.CleanupOwner == failed.Owner,
                "Faulted cleanup reported closed success or leaked root.");
            Require(await Observe(() => failed.Window.RequestCloseAsync(token)) != null, "Repeated close masked failed cleanup.");
            Require(gl.ActiveSessionCount == 0 && vk.ActiveSessionCount == 0, "Async close retained sessions.");
            reports.Add(new { OpenGL = openGL, NativeVeto = true, SaveFailureRetry = true, Concurrent = calls == 3, WaitCancellation = true,
                ResizeSuspendResume = true, OwnerProgress = true, ForcedDrain = true, CleanupFailure = true, Registry = 0 });
        }
        Require(messages.IsEmpty && Win32WindowDpi.ActiveHooks == 0 && Win32WindowChrome.ActiveHooks == 0 && Win32TransparentFramebuffer.ActiveHooks == 0,
            "Close retained hooks or produced validation messages.");
        var index = Array.IndexOf(args, "--close-output");
        var output = Path.GetFullPath(index < 0 ? "TestResults/AlloyClose/report.json" : args[index + 1]);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, JsonSerializer.Serialize(new { Completed = true, Cases = reports, ValidationMessages = messages.Count, Hooks = 0 }));
        Console.WriteLine("Async close passed: native veto/save failure/retry, concurrent/canceled wait, responsive owner, resize/suspend/resume, forced App Stop, cleanup errors, GL/Vulkan.");
    }

    private static TaskCompletionSource<bool> Gate() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static IEnumerable<Exception> Flatten(Exception error) => error is AggregateException aggregate
        ? aggregate.InnerExceptions.SelectMany(Flatten) : [error];
    private static async Task<Exception?> Observe(Func<Task> operation) { try { await operation(); return null; } catch (Exception error) { return error; } }
    private static async Task Until(Func<bool> condition, CancellationToken token) { while (!condition()) await Task.Delay(5, token); }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void NativeClose(nint handle) => SendMessageW(handle, 0x0010, 0, 0);
    [LibraryImport("user32.dll")] private static partial nint SendMessageW(nint handle, uint message, nuint wparam, nint lparam);

    private sealed class Live : IAsyncDisposable
    {
        internal readonly HostedUiWindow Window;
        internal readonly Panel Root;
        internal UiSession? Session;
        internal nint Handle;
        internal int Owner, CleanupOwner;
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Live(HostedUiSessionFactory factory, bool openGL, HostedUiDispatcher scope, Action<string>? validation, Exception? cleanupFailure = null)
        {
            Root = new Panel { Background = new Color(70, 80, 90) };
            Root.Own(new Cleanup(() => { CleanupOwner = Environment.CurrentManagedThreadId; if (cleanupFailure != null) throw cleanupFailure; }));
            Window = new HostedUiWindow(new SilkUiWindowOptions { OpenGL = openGL, FrameMode = UiFrameMode.OnDemand, EnableDiagnostics = true,
                Width = 260, Height = 160, Title = "INF4 async close", ValidationMessage = validation }, factory,
                () => { Owner = Environment.CurrentManagedThreadId; return Root; }, null, scope);
            Window.PostWindow(host => Handle = host.NativeWindow.Native!.Win32!.Value.Hwnd);
            Window.FramePresented += session => { Session = session; _ready.TrySetResult(); };
        }
        internal async Task Start(CancellationToken token) { await Window.StartAsync(token); await _ready.Task.WaitAsync(token); }
        public async ValueTask DisposeAsync() => await Window.DisposeAsync();
    }
    private sealed class Cleanup(Action dispose) : IDisposable { public void Dispose() => dispose(); }
}
