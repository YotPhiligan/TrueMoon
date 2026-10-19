namespace TrueMoon.Alloy.Hosting;

// Request is thread-safe; Poll and handler invocation belong to the window owner.
// No continuation touches a window/session. It only wakes the owner for the next poll.
internal sealed class UiCloseCoordinator(Action wake)
{
    private readonly object _gate = new();
    private Attempt? _attempt;
    private bool _finished;
    private Exception? _lastError;

    internal bool IsRequested { get { lock (_gate) return _attempt != null; } }
    internal Exception? LastError { get { lock (_gate) return _lastError; } }

    internal Task<bool> Request(UiCloseReason reason)
    {
        Task<bool> result;
        lock (_gate)
        {
            if (_finished) return Task.FromResult(true);
            if (_attempt == null)
            {
                _attempt = new Attempt(reason); _lastError = null;
            }
            else if (reason == UiCloseReason.Shutdown) _attempt.Forced = true;
            result = _attempt.Result.Task;
        }
        wake();
        return result;
    }

    // True means cleanup is allowed. Forced handler failures are rethrown only
    // after all handlers drain, so one failed save cannot skip another drain.
    internal bool Poll(Func<Func<UiCloseReason, Task<bool>>[]> snapshot)
    {
        Attempt? attempt;
        lock (_gate) attempt = _attempt;
        if (attempt == null) return false;
        attempt.Handlers ??= snapshot();
        for (var budget = 0; budget < 64; budget++)
        {
            if (attempt.Pending != null)
            {
                if (!attempt.Pending.IsCompleted) return false;
                bool accepted = false; Exception? error = null;
                try { accepted = attempt.Pending.GetAwaiter().GetResult(); }
                catch (Exception cause) { error = cause; }
                attempt.Pending = null;
                lock (_gate)
                {
                    if (!attempt.Forced && (error != null || !accepted))
                    {
                        _attempt = null; _lastError = error;
                        if (error != null)
                        {
                            attempt.Result.TrySetException(error);
                            _ = attempt.Result.Task.Exception; // Native requests have no awaiting caller.
                        }
                        else attempt.Result.TrySetResult(false);
                        return false;
                    }
                    if (error != null) attempt.Errors.Add(error);
                }
            }
            if (attempt.Next == attempt.Handlers.Length)
            {
                Exception? error = attempt.Errors.Count switch
                {
                    0 => null, 1 => attempt.Errors[0], _ => new AggregateException(attempt.Errors)
                };
                lock (_gate) _lastError = error;
                attempt.Result.TrySetResult(true);
                if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
                return true;
            }
            UiCloseReason reason;
            lock (_gate) reason = attempt.Forced ? UiCloseReason.Shutdown : attempt.Reason;
            try
            {
                attempt.Pending = attempt.Handlers[attempt.Next++](reason)
                    ?? Task.FromException<bool>(new InvalidOperationException("A Closing handler returned null."));
            }
            catch (Exception error) { attempt.Pending = Task.FromException<bool>(error); }
            if (!attempt.Pending.IsCompleted)
                _ = attempt.Pending.ContinueWith(_ => wake(), CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        wake();
        return false;
    }

    // Also releases an attempt interrupted by initialization/native/cleanup failure.
    // Public callers still await the owner's Completion before seeing success.
    internal void Finish()
    {
        lock (_gate)
        {
            _finished = true;
            _attempt?.Result.TrySetResult(true);
            _attempt = null;
        }
    }

    private sealed class Attempt(UiCloseReason reason)
    {
        internal readonly UiCloseReason Reason = reason;
        internal bool Forced = reason == UiCloseReason.Shutdown;
        internal readonly TaskCompletionSource<bool> Result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Func<UiCloseReason, Task<bool>>[]? Handlers;
        internal Task<bool>? Pending;
        internal int Next;
        internal readonly List<Exception> Errors = [];
    }
}
