using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Alloy.Hosting;

namespace TrueMoon.Aether.Editor.UI;

/// <summary>App-owned controls for a captured deployment. Borrows Runtime; never opens projects, builds or writes scenes.</summary>
public sealed class RuntimeEditorSession : IStoppable, IAsyncDisposable
{
    private readonly RuntimeEngine _engine;
    private readonly PlayerStart _start;
    private readonly RuntimeViewportBinding _viewport;
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _end;
    private CancellationTokenSource? _command;
    private Task _pending = Task.CompletedTask, _presentation = Task.CompletedTask;
    private Task? _stop;
    private bool _closing, _disposed;
    private Exception? _error;
    private WeakReference<EditorView>? _presentedView;

    internal RuntimeEditorSession(RuntimeEngine engine, PlayerStart start, RuntimeViewportBinding viewport, CancellationToken lifetime)
    { _engine = engine; _start = start; _viewport = viewport; _end = CancellationTokenSource.CreateLinkedTokenSource(lifetime); }

    /// <summary>Current detached runtime observation.</summary>
    public SessionStatus Status => _engine.Supervisor.Status;
    /// <summary>Accepted command completion. Failures are exposed through Error.</summary>
    public Task Pending { get { lock (_gate) return _pending; } }
    internal bool IsClosed { get { lock (_gate) return _closing; } }
    /// <summary>Last command or frame transport failure.</summary>
    public Exception? Error { get { lock (_gate) return _error ?? _engine.Frames.TransportError; } }

    /// <summary>Initializes the standard shared GPU binding on a borrowed live hosted window. Custom transports need no initialization.</summary>
    public Task InitializeViewportAsync(HostedUiWindow window, CancellationToken token = default) => _viewport.InitializeAsync(window, _engine.Frames, token);

    /// <summary>Fresh admission; authoring/build/replace capabilities are absent.</summary>
    public bool CanExecute(EditorCommand command)
    { lock (_gate) return CanExecuteCore(command); }
    private bool CanExecuteCore(EditorCommand command)
    {
        if (_closing || _disposed) return false;
        if (command == EditorCommand.Cancel) return !_pending.IsCompleted;
        if (!_pending.IsCompleted) return false;
        return command switch
        {
            EditorCommand.Start => Status.State == SessionState.Stopped,
            EditorCommand.Play or EditorCommand.Step => Status.State == SessionState.Paused,
            EditorCommand.Pause => Status.State == SessionState.Playing,
            EditorCommand.Stop => Status.State is SessionState.Paused or SessionState.Playing or SessionState.Faulted or SessionState.CleanupFailed,
            _ => false
        };
    }

    /// <summary>Admits at most one command; unavailable actions have no side effects.</summary>
    public bool Execute(EditorCommand command) => Execute(command, false);
    internal bool RunSimulation() => Execute(Status.State == SessionState.Stopped ? EditorCommand.Start : EditorCommand.Play, true);
    private bool Execute(EditorCommand command, bool playAfterStart)
    {
        lock (_gate)
        {
            if (!CanExecuteCore(command)) return false;
            if (command == EditorCommand.Cancel) { _command?.Cancel(); return true; }
            _error = null;
            _command?.Dispose(); _command = CancellationTokenSource.CreateLinkedTokenSource(_end.Token);
            var token = _command.Token;
            _pending = Task.Run(() => ExecuteAsync(command, playAfterStart, token));
            return true;
        }
    }
    private async Task ExecuteAsync(EditorCommand command, bool playAfterStart, CancellationToken token)
    {
        try
        {
            switch (command)
            {
                case EditorCommand.Start:
                    await _viewport.Ready.WaitAsync(token).ConfigureAwait(false);
                    if (_start.World is { } world)
                        await _engine.Frames.StartWorldAsync(world, _start.Artifact, 640, 360, token: token).ConfigureAwait(false);
                    else await _engine.Frames.StartAsync(_start.Snapshot!, _start.Artifact, 640, 360, token: token).ConfigureAwait(false);
                    if (playAfterStart) await _engine.Supervisor.SetPlayingAsync(true, token).ConfigureAwait(false);
                    break;
                case EditorCommand.Play: await _engine.Supervisor.SetPlayingAsync(true, token).ConfigureAwait(false); break;
                case EditorCommand.Pause: await _engine.Supervisor.SetPlayingAsync(false, token).ConfigureAwait(false); break;
                case EditorCommand.Step: await _engine.Supervisor.StepAsync(token).ConfigureAwait(false); break;
                case EditorCommand.Stop: await _engine.Frames.StopAsync().ConfigureAwait(false); break;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { lock (_gate) _error = error; }
    }

    /// <summary>Called by an embedded or standard host on the UI owner, after actual presentation.</summary>
    public void Present(UiSession ui, EditorView view)
    {
        ArgumentNullException.ThrowIfNull(ui); ArgumentNullException.ThrowIfNull(view); view.VerifyAccess();
        if (view.DataContext is not RuntimeEditorViewModel model || !ReferenceEquals(model.Session, this))
            throw new ArgumentException("View belongs to another runtime editor App.", nameof(view));
        lock (_gate)
        {
            if (!_closing)
            {
                if (_presentedView != null && _presentedView.TryGetTarget(out var active) && !active.IsDisposed && !ReferenceEquals(active, view))
                    throw new InvalidOperationException("One live runtime EditorView presents this App. Dispose it before attaching another view.");
                if (_presentedView == null) _presentedView = new(view); else _presentedView.SetTarget(view);
                _viewport.Observe(ui, view);
                if (_presentation.IsCompleted && view.Canvas.CurrentDraw is {} draw)
                    _presentation = PresentAsync(draw.Frame);
            }
        }
        model.Refresh();
        view.Canvas.EmptyText = "Start the captured game to display its viewport";
    }
    private async Task PresentAsync(TrueMoon.Aether.Runtime.Frames.ViewportFrame frame)
    {
        try { await _engine.Frames.PresentedAsync(frame).ConfigureAwait(false); }
        catch (Exception error)
        {
            lock (_gate) _error = error;
            await _engine.Supervisor.ReportFrameFaultAsync(frame.Presentation.Surface.Identity, "RuntimeEditorPresentation", error).ConfigureAwait(false);
        }
    }

    /// <summary>Closes admission, cancels/drains commands and releases the runtime surface while the host context is alive.</summary>
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_stop is { IsCompletedSuccessfully: true } or { IsCompleted: false }) return _stop;
            _closing = true; _end.Cancel();
            return _stop = StopCoreAsync();
        }
    }
    private async Task StopCoreAsync()
    {
        await _pending.ConfigureAwait(false);
        List<Exception> failures = [];
        try { await _presentation.ConfigureAwait(false); } catch (Exception error) { failures.Add(error); }
        try { await _engine.Frames.StopAsync().ConfigureAwait(false); } catch (Exception error) { failures.Add(error); }
        _viewport.Forget(); _presentedView = null;
        if (failures.Count != 0) throw new AggregateException("Runtime editor cleanup failed.", failures);
    }
    /// <summary>Drains before releasing only this session's cancellation owners.</summary>
    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true; _command?.Dispose(); _end.Dispose();
        }
    }
}
