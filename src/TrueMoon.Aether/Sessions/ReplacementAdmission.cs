namespace TrueMoon.Aether.Sessions;

// Shared by supervisor and frame session. Tokens are explicit so background monitoring never
// inherits transaction authority through ExecutionContext or AsyncLocal.
internal sealed class ReplacementAdmission
{
    private readonly Lock _gate = new();
    private Lease? _active;
    internal Lease Begin(ReplacementDeadlines deadlines)
    {
        lock (_gate)
        {
            if (_active != null) throw new InvalidOperationException("A replacement already owns this session.");
            return _active = new(this, deadlines);
        }
    }
    internal void Check(Lease? owner)
    {
        lock (_gate)
        {
            if (owner != null)
            {
                owner.Interrupted.Token.ThrowIfCancellationRequested();
                owner.Total.Token.ThrowIfCancellationRequested();
                if (!ReferenceEquals(_active, owner)) throw new OperationCanceledException("Replacement no longer owns the session.");
            }
            else if (_active != null) throw new InvalidOperationException("Replacement owns this session; wait for completion or Stop.");
        }
    }
    internal void Interrupt()
    {
        lock (_gate)
        {
            try { _active?.Interrupted.Cancel(); }
            catch (AggregateException) { /* A cancellation callback cannot prevent ownership cleanup. */ }
        }
    }
    internal Task DrainAsync() { lock (_gate) return _active?.Completion.Task ?? Task.CompletedTask; }
    internal CancellationTokenSource CleanupScope()
    {
        lock (_gate)
        {
            if (_active == null) return new();
            var scope = CancellationTokenSource.CreateLinkedTokenSource(_active.Total.Token);
            scope.CancelAfter(_active.Deadlines.Cleanup); return scope;
        }
    }
    internal sealed class Lease : IDisposable
    {
        private readonly ReplacementAdmission _admission;
        internal ReplacementDeadlines Deadlines { get; }
        internal CancellationTokenSource Total { get; }
        internal CancellationTokenSource Interrupted { get; } = new();
        internal TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Lease(ReplacementAdmission admission, ReplacementDeadlines deadlines)
        { _admission = admission; Deadlines = deadlines; Total = new(deadlines.Total); }
        public void Dispose()
        {
            lock (_admission._gate)
            {
                if (!ReferenceEquals(_admission._active, this)) return;
                _admission._active = null;
                Total.Dispose(); Interrupted.Dispose(); Completion.TrySetResult();
            }
        }
    }
}
