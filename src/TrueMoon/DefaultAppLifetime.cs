using TrueMoon.Diagnostics;

namespace TrueMoon;

/// <summary>Thread-safe, single-use application cancellation and shutdown notifications.</summary>
public class DefaultAppLifetime : IAppLifetime, IAppLifetimeHandler, IDisposable
{
    private readonly IEventsSource<IAppLifetime> _eventsSource;
    private readonly Lock _sync = new();
    private readonly CancellationTokenSource _cts;
    private readonly CancellationToken _token;
    private readonly bool _ownsCancellationSource;
    private readonly CancellationTokenRegistration _externalCancellation;
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<Action> _stoppingActions = [];
    private readonly List<Action> _stoppedActions = [];
    private bool _cancelRequested;
    private bool _cancelling;
    private bool _stopping;
    private bool _stopped;
    private bool _disposed;

    /// <summary>Creates a lifetime, optionally borrowing an external cancellation source.</summary>
    /// <param name="eventsSource">Diagnostic events.</param>
    /// <param name="appCancellationTokenSourceHandle">A borrowed source; never disposed by this lifetime.</param>
    public DefaultAppLifetime(IEventsSource<IAppLifetime> eventsSource,
        AppCancellationTokenSourceHandle? appCancellationTokenSourceHandle = default)
    {
        ArgumentNullException.ThrowIfNull(eventsSource);
        _eventsSource = eventsSource;
        _ownsCancellationSource = appCancellationTokenSourceHandle == null;
        _cts = appCancellationTokenSourceHandle?.CancellationTokenSource ?? new CancellationTokenSource();
        _token = _cts.Token;
        if (!_ownsCancellationSource)
            _externalCancellation = _token.Register(static state =>
                ((TaskCompletionSource)state!).TrySetResult(), _completion);
    }

    /// <inheritdoc />
    public CancellationToken AppCancellationToken => _token;

    /// <inheritdoc />
    public void Cancel()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_cancelRequested) return;
            _cancelRequested = true;
            _cancelling = true;
        }
        // User callbacks and task continuations must never run while holding _sync.
        try { _cts.Cancel(); }
        finally
        {
            lock (_sync)
            {
                _cancelling = false;
                if (_disposed && _ownsCancellationSource) _cts.Dispose();
            }
            _completion.TrySetResult();
        }
        _eventsSource.Trace();
    }

    /// <inheritdoc />
    public void OnStopped(Action action) => Register(action, false);
    /// <inheritdoc />
    public void OnStopping(Action action) => Register(action, true);

    private void Register(Action action, bool stopping)
    {
        ArgumentNullException.ThrowIfNull(action);
        bool invoke;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            invoke = stopping ? _stopping || _stopped : _stopped;
            if (!invoke) (stopping ? _stoppingActions : _stoppedActions).Add(action);
        }
        if (invoke) action();
    }

    /// <inheritdoc />
    public Task WaitAsync(CancellationToken cancellationToken = default)
        => _completion.Task.WaitAsync(cancellationToken);

    /// <inheritdoc />
    public void Stopping()
    {
        Action[] actions;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_stopping || _stopped) return;
            _stopping = true;
            actions = _stoppingActions.ToArray();
            _stoppingActions.Clear();
        }
        var errors = new List<Exception>();
        AppRunner.TryCleanup(Cancel, errors);
        Invoke(actions, errors);
        _eventsSource.Trace(nameof(Stopping));
        AppRunner.ThrowErrors(errors);
    }

    /// <inheritdoc />
    public void Stopped()
    {
        // Ensure stopping callbacks run even when callers go directly to Stopped.
        var errors = new List<Exception>();
        AppRunner.TryCleanup(Stopping, errors);
        Action[] actions;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_stopped) { AppRunner.ThrowErrors(errors); return; }
            _stopped = true;
            actions = _stoppedActions.ToArray();
            _stoppedActions.Clear();
        }
        Invoke(actions, errors);
        _eventsSource.Trace(nameof(Stopped));
        AppRunner.ThrowErrors(errors);
    }

    private static void Invoke(Action[] actions, List<Exception> errors)
    {
        foreach (var action in actions) AppRunner.TryCleanup(action, errors);
    }

    /// <summary>Releases callbacks and the owned cancellation source; borrowed sources stay usable.</summary>
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            _stoppingActions.Clear();
            _stoppedActions.Clear();
            // A cancellation callback may dispose its own lifetime. Defer source
            // disposal until Cancel has finished running all user callbacks.
            if (_ownsCancellationSource && !_cancelling) _cts.Dispose();
        }
        _completion.TrySetResult();
        _externalCancellation.Dispose();
        GC.SuppressFinalize(this);
    }
}
