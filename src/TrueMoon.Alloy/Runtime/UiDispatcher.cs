namespace TrueMoon.Alloy;

internal sealed class UiDispatcher(UiDispatchOptions options)
{
    private readonly object _gate = new();
    private readonly LinkedList<Work> _pending = new();
    private bool _closed;
    private UiRenderingException? _failure;
    private UiDiagnostics? _diagnostics;
    private Action? _updateSignal;
    private volatile bool _frameRequested;

    internal void SetUpdateSignal(Action? signal)
    { lock (_gate) { _updateSignal = _closed ? null : signal; } }

    internal void SignalUpdate() { lock (_gate) _updateSignal?.Invoke(); }

    internal void RequestFrame()
    {
        lock (_gate)
        {
            if (_closed) throw ClosedError();
            if (_frameRequested) return;
            _frameRequested = true; _updateSignal?.Invoke();
        }
    }

    internal bool TakeFrameRequest()
    {
        if (!_frameRequested) return false;
        lock (_gate) { var requested = _frameRequested; _frameRequested = false; return requested; }
    }

    internal void EnableDiagnostics(UiDiagnostics diagnostics)
    {
        lock (_gate) { diagnostics.InitializeQueue(_pending.Count); _diagnostics = diagnostics; }
    }

    internal bool HasPending { get { lock (_gate) return _pending.Count != 0 || _frameRequested; } }

    internal void Post(Action action) => Enqueue(new Work(this, action, null, default));

    internal Task InvokeAsync(Action action, CancellationToken cancellationToken, bool inline)
    {
        try
        {
            lock (_gate)
            {
                ThrowIfClosed();
                if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);
            }
            if (inline) { action(); return Task.CompletedTask; }
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Enqueue(new Work(this, action, completion, cancellationToken));
            return completion.Task;
        }
        catch (UiRenderingException) { throw; }
        catch (Exception error) { return Task.FromException(error); }
    }

    private void ThrowIfClosed()
    {
        if (_closed) { _diagnostics?.Queue(UiQueueEvent.Rejected, _pending.Count); throw ClosedError(); }
    }

    private Exception ClosedError() => _failure is { } failure
        ? new InvalidOperationException("UI rendering has failed. Dispose this session and explicitly create a new tree/session from the model.", failure)
        : new ObjectDisposedException(nameof(UiSession));

    private void Enqueue(Work work)
    {
        lock (_gate)
        {
            ThrowIfClosed();
            if (work.Token.IsCancellationRequested) { work.Completion!.TrySetCanceled(work.Token); return; }
            if (_pending.Count == options.Capacity)
            {
                _diagnostics?.Queue(UiQueueEvent.Rejected, _pending.Count);
                throw new InvalidOperationException($"UI dispatch queue capacity ({options.Capacity}) exceeded.");
            }
            work.Node = _pending.AddLast(work);
            _diagnostics?.Queue(UiQueueEvent.Accepted, _pending.Count);
            // Registration may synchronously cancel; the monitor is reentrant. Never wait for a
            // cancellation callback while holding this gate (registrations are disposed outside).
            if (work.Token.CanBeCanceled)
                work.Registration = work.Token.UnsafeRegister(static state =>
                {
                    var item = (Work)state!;
                    item.Owner.Cancel(item);
                }, work);
            if (_pending.Count != 0) _updateSignal?.Invoke();
        }
    }

    private void Cancel(Work work)
    {
        lock (_gate)
        {
            if (work.Node == null) return; // Already running or closed: cancellation cannot preempt it.
            _pending.Remove(work.Node); work.Node = null; work.Action = null;
            _diagnostics?.Queue(UiQueueEvent.Canceled, _pending.Count);
            work.Completion!.TrySetCanceled(work.Token);
        }
    }

    internal void Drain()
    {
        for (var i = 0; i < options.ActionsPerUpdate; i++)
        {
            Work work;
            UiDiagnostics? diagnostics;
            lock (_gate)
            {
                if (_pending.First is not { } first) return;
                work = first.Value; _pending.RemoveFirst(); work.Node = null;
                diagnostics = _diagnostics;
                diagnostics?.Queue(UiQueueEvent.Started, _pending.Count);
            }
            work.Registration.Dispose();
            var action = work.Action!; work.Action = null;
            try
            {
                action();
                if (diagnostics != null) lock (_gate) diagnostics.Queue(UiQueueEvent.Completed, _pending.Count);
                work.Completion?.TrySetResult();
            }
            catch (Exception error)
            {
                if (diagnostics != null) lock (_gate) diagnostics.Queue(UiQueueEvent.Failed, _pending.Count);
                if (work.Completion == null) throw;
                work.Completion.TrySetException(error);
                if (error is UiRenderingException) throw;
            }
        }
    }

    internal void Close(UiRenderingException? failure = null)
    {
        Work[] pending;
        lock (_gate)
        {
            // Disposal supersedes a rendering failure for subsequent calls, as VerifyAccess does.
            _closed = true; _failure = failure;
            _updateSignal = null; _frameRequested = false;
            pending = _pending.ToArray(); _pending.Clear();
            _diagnostics?.Queue(UiQueueEvent.Discarded, 0, pending.Length);
            foreach (var work in pending)
            {
                work.Node = null; work.Action = null;
                work.Completion?.TrySetException(ClosedError());
            }
        }
        foreach (var work in pending) work.Registration.Dispose();
    }

    private sealed class Work(UiDispatcher owner, Action action, TaskCompletionSource? completion, CancellationToken token)
    {
        internal readonly UiDispatcher Owner = owner;
        internal Action? Action = action;
        internal readonly TaskCompletionSource? Completion = completion;
        internal readonly CancellationToken Token = token;
        internal LinkedListNode<Work>? Node;
        internal CancellationTokenRegistration Registration;
    }
}
