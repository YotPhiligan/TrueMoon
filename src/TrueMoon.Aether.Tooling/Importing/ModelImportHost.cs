namespace TrueMoon.Aether.Tooling.Importing;

/// <summary>App-owned admission and shutdown owner. Published candidates and idle factory products remain caller-owned.</summary>
public sealed class ModelImportHost : IAsyncDisposable
{
    private readonly object _gate = new();
    private IAsyncDisposable? _active;
    private bool _closed;

    /// <summary>Zero or one admitted operations, including a child whose cleanup needs retry.</summary>
    public int ActiveOperationCount { get { lock (_gate) return _active == null ? 0 : 1; } }
    internal void Enter(IAsyncDisposable operation)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_closed, this);
            if (_active != null) throw new InvalidOperationException("Model import admission is 1 active / 0 queued.");
            _active = operation;
        }
    }
    internal void Exit(IAsyncDisposable operation) { lock (_gate) { if (ReferenceEquals(_active, operation)) _active = null; } }
    /// <summary>Closes admission and waits for actual worker exit, pipe drain and scratch cleanup. Failed cleanup is retryable.</summary>
    public async ValueTask DisposeAsync()
    {
        IAsyncDisposable? active;
        lock (_gate) { _closed = true; active = _active; }
        if (active != null) await active.DisposeAsync().ConfigureAwait(false);
    }
}

internal sealed class ModelImportLifetime(ModelImportHost host) : IStoppable
{
    public Task StopAsync(CancellationToken cancellationToken = default) => host.DisposeAsync().AsTask();
}
