namespace TrueMoon.Aether.Rendering.Resources;

/// <summary>Preparation publication/lifetime states.</summary>
public enum RenderPreparationState
{
    /// <summary>Queued or preparing.</summary>
    Pending,
    /// <summary>The entire required mesh closure is ready.</summary>
    Ready,
    /// <summary>Preparation failed; cleanup may require retry.</summary>
    Failed,
    /// <summary>Cancelled without publishing a ready binding.</summary>
    Cancelled,
    /// <summary>Explicit disposal completed.</summary>
    Disposed
}
/// <summary>Caller-owned preparation and ready resource lease. Completion does not transfer a second owner.</summary>
public sealed class RenderPreparation : IAsyncDisposable
{
    private readonly RenderDeviceOwner _owner;
    private readonly CancellationTokenSource _end;
    private readonly CancellationToken _external;
    private readonly CancellationToken _token;
    private CancellationTokenRegistration _registration;
    private readonly SemaphoreSlim _cleanupGate = new(1, 1);
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<RenderDeviceOwner.EntryLease> _entries = [];
    private int _state, _disposing;
    internal RenderManifestLease Content { get; }
    internal CancellationToken Token => _token;
    internal LinkedListNode<RenderPreparation>? QueueNode;
    internal RenderPreparation(RenderDeviceOwner owner, RenderManifestLease content, CancellationToken external)
    { _owner = owner; Content = content; _external = external; _end = CancellationTokenSource.CreateLinkedTokenSource(external); _token = _end.Token; }
    internal void RegisterCancellation() => _registration = _external.Register(static state => ((RenderPreparation)state!).Cancel(), this);
    /// <summary>Gets complete preparation or the original preparation/cleanup failure.</summary>
    public Task Completion => _completion.Task;
    /// <summary>Gets the current publication state.</summary>
    public RenderPreparationState State => (RenderPreparationState)Volatile.Read(ref _state);
    /// <summary>Gets captured immutable data while the operation owns it.</summary>
    public RenderContentManifest Manifest => Content.Manifest;
    /// <summary>Gets whether every CPU/GPU reference was released.</summary>
    public bool IsReleased { get; private set; }
    /// <summary>Gets the preparation or retained cleanup error.</summary>
    public Exception? Error { get; private set; }
    internal async Task RunAsync()
    {
        try
        {
            Token.ThrowIfCancellationRequested();
            foreach (var entry in Manifest.Entries)
            {
                if (entry.Definition is not MeshDefinition) continue;
                Token.ThrowIfCancellationRequested();
                var lease = _owner.Acquire(entry); _entries.Add(lease);
                await lease.Value.Completion.Task.WaitAsync(Token).ConfigureAwait(false);
            }
            _owner.PublishReady(this);
        }
        catch (Exception error)
        {
            Error = error; Volatile.Write(ref _state, (int)(error is OperationCanceledException ? RenderPreparationState.Cancelled : RenderPreparationState.Failed));
            try { await CleanupAsync().ConfigureAwait(false); }
            catch (Exception cleanup) { Error = new AggregateException(error, cleanup); }
        }
        finally { _owner.Finished(this, true); }
        if (State == RenderPreparationState.Ready) _completion.TrySetResult();
        else if (Error is OperationCanceledException && IsReleased) _completion.TrySetCanceled(Token);
        else _completion.TrySetException(Error ?? new RenderResourceException(RenderResourceError.BackendFailure, "Preparation failed without an outcome."));
    }
    internal void SetReady() => Volatile.Write(ref _state, (int)RenderPreparationState.Ready);
    internal void FinishQueuedCancellation()
    {
        Content.Dispose(); IsReleased = true; Volatile.Write(ref _state, (int)RenderPreparationState.Cancelled);
        _registration.Dispose(); _end.Dispose();
        _owner.Finished(this, false); _completion.TrySetCanceled(Token);
    }
    /// <summary>Requests cancellation of pending work; ready leases retain their validity until disposal.</summary>
    public void Cancel()
    {
        if (State != RenderPreparationState.Pending) return;
        try { _end.Cancel(); } catch (Exception error) { Error ??= error; }
        _owner.CancelQueued(this);
    }
    /// <summary>Gets a non-owning mesh handle from this complete live ready binding.</summary><param name="key">Exact mesh key.</param><returns>Validated cache-local handle.</returns>
    public RenderResourceHandle GetMeshHandle(RenderAssetKey key)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposing) != 0 || IsReleased, this);
        ResourceGuard.Require(State == RenderPreparationState.Ready, RenderResourceError.Closed, "Preparation is not completely ready.");
        var lease = _entries.FirstOrDefault(item => item.Value.Value.Key == key);
        ResourceGuard.Require(lease != null, RenderResourceError.InvalidIdentity, "Mesh is outside this ready binding.");
        _owner.ValidateHandle(lease!.Value.Handle); return lease.Value.Handle;
    }
    private async ValueTask CleanupAsync()
    {
        await _cleanupGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (IsReleased) return;
            var errors = new List<Exception>();
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                try { await _entries[i].ReleaseAsync().ConfigureAwait(false); _entries.RemoveAt(i); }
                catch (Exception error) { errors.Add(error); }
            }
            if (errors.Count != 0) throw new AggregateException("Ready/preparation references remain owned.", errors);
            Content.Dispose(); IsReleased = true; _registration.Dispose(); _end.Dispose(); _owner.Released(this);
        }
        finally { _cleanupGate.Release(); }
    }
    /// <summary>Cancels/drains before releasing resources; failed disposal retains ownership for retry.</summary>
    public async ValueTask DisposeAsync()
    {
        Volatile.Write(ref _disposing, 1); Cancel();
        try { await Completion.ConfigureAwait(false); } catch { /* Cleanup remains necessary after preparation failure. */ }
        await CleanupAsync().ConfigureAwait(false);
        _registration.Dispose(); _end.Dispose(); Volatile.Write(ref _state, (int)RenderPreparationState.Disposed);
    }
}
