namespace TrueMoon.Aether.Rendering.Resources;

/// <summary>Counted mesh cache and bounded preparation factory for one session/world/device generation.</summary>
/// <remarks>The owner borrows its CPU catalog, owns the injected backend factory result and tracks operation products until released.</remarks>
public sealed class RenderDeviceOwner : IAsyncDisposable, IFactory<RenderPreparation, RenderManifestLease>
{
    private readonly Lock _gate = new();
    private readonly SemaphoreSlim _disposeGate = new(1, 1), _backendGate = new(1, 1);
    private readonly RenderContentCatalog _catalog;
    private readonly IFactory<IRenderResourceBackend, RenderDeviceRequest> _backends;
    private readonly RenderDeviceRequest _request;
    private readonly RenderResourceOptions _options;
    private readonly Dictionary<RenderAssetKey, Entry> _entries = [];
    private readonly List<Slot> _slots = [];
    private readonly HashSet<RenderPreparation> _operations = [];
    private readonly LinkedList<RenderPreparation> _queue = [];
    private readonly List<IAsyncDisposable> _failedBackends = [];
    private readonly CancellationTokenSource _end = new();
    private IRenderResourceBackend? _backend;
    private int _active;
    private bool _closed;
    /// <summary>Creates lazy ownership without activating the backend.</summary>
    /// <param name="catalog">Borrowed CPU owner.</param><param name="backends">Injected caller-owned backend factory.</param>
    /// <param name="request">Exact device/identity arguments.</param><param name="options">Copied limits.</param>
    public RenderDeviceOwner(RenderContentCatalog catalog, IFactory<IRenderResourceBackend, RenderDeviceRequest> backends,
        RenderDeviceRequest request, RenderResourceOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(catalog); ArgumentNullException.ThrowIfNull(backends); ArgumentNullException.ThrowIfNull(request);
        request.Identity.Validate(); ResourceGuard.Require(request.DeviceGeneration != 0, RenderResourceError.InvalidIdentity, "Device generation is zero.");
        _catalog = catalog; _backends = backends; _request = request; _options = (options ?? new()).Snapshot(); AllocationBudget = new(_options);
    }
    /// <summary>Gets this cache owner's identity.</summary>
    public Guid Id { get; } = Guid.NewGuid();
    /// <summary>Gets the immutable resource identity.</summary>
    public RenderResourceIdentity Identity => _request.Identity;
    /// <summary>Gets the device generation.</summary>
    public ulong Generation => _request.DeviceGeneration;
    /// <summary>Gets the actual allocation budget.</summary>
    public RenderAllocationBudget AllocationBudget { get; }
    /// <summary>Whether all operations, resources and backend released.</summary>
    public bool IsReleased { get; private set; }
    /// <summary>Gets counters without invoking the driver.</summary>
    public RenderDeviceCounts Counts { get { lock (_gate) return new(_entries.Count, _active, _queue.Count, _operations.Count, _entries.Values.Count(static e => e.References == 0), _closed); } }
    /// <summary>Creates a caller-owned preparation with default cancellation.</summary><param name="arguments">Live CPU scope.</param><returns>Tracked operation owner.</returns>
    public RenderPreparation Create(RenderManifestLease arguments) => Create(arguments, default);
    /// <summary>Admits a bounded asynchronous operation, retaining a CPU lease before starting work.</summary>
    /// <param name="content">Same-catalog identity-compatible CPU scope.</param><param name="cancellationToken">Operation cancellation.</param><returns>Caller-owned preparation.</returns>
    public RenderPreparation Create(RenderManifestLease content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content); RenderPreparation operation; bool start;
        lock (_gate)
        {
            EnsureOpen(); ValidateContent(content);
            start = _active < _options.MaximumActivePreparations;
            ResourceGuard.Require(start || _queue.Count < _options.MaximumQueuedPreparations, RenderResourceError.LimitExceeded, "Preparation queue admission exhausted.");
            operation = new(this, content.Retain(), cancellationToken);
            _operations.Add(operation);
            if (start) _active++; else operation.QueueNode = _queue.AddLast(operation);
        }
        operation.RegisterCancellation();
        if (start) _ = Task.Run(operation.RunAsync);
        else if (operation.Token.IsCancellationRequested) operation.Cancel();
        return operation;
    }
    internal void ValidateContent(RenderManifestLease content)
    {
        content.CheckAdmission();
        ResourceGuard.Require(ReferenceEquals(content.Owner, _catalog), RenderResourceError.InvalidIdentity, "Foreign CPU catalog lease.");
        var manifest = content.Manifest;
        ResourceGuard.Require(manifest.ProjectId == Identity.ProjectId && manifest.BuildId == Identity.BuildId && manifest.Generation == Identity.CatalogGeneration,
            RenderResourceError.InvalidIdentity, "Stale/foreign project, build or catalog generation.");
    }
    internal void PublishReady(RenderPreparation operation)
    {
        lock (_gate) { EnsureOpen(); operation.Token.ThrowIfCancellationRequested(); ValidateContent(operation.Content); operation.SetReady(); }
    }
    internal RenderSnapshotResources RetainSnapshot(RenderPreparation operation, ulong revision)
    {
        lock (_gate)
        {
            EnsureOpen();
            ResourceGuard.Require(operation.CanRetainSnapshot && _operations.Contains(operation), RenderResourceError.Closed, "A live ready binding is required.");
            var content = operation.Content.Retain(); var leases = new List<EntryLease>();
            try
            {
                foreach (var asset in content.Manifest.Entries)
                {
                    if (asset.Definition is not MeshDefinition) continue;
                    ResourceGuard.Require(_entries.TryGetValue(asset.Key, out var entry) && entry.References > 0 && entry.Error == null && entry.Completion.Task.IsCompletedSuccessfully,
                        RenderResourceError.StaleHandle, "Required mesh is not completely ready.");
                    var lease = new EntryLease(this, entry!); leases.Add(lease); entry!.References++;
                }
                return new(this, content, leases, revision);
            }
            catch
            {
                // The preparation still owns every entry under this gate: rollback cannot be last release.
                foreach (var lease in leases) lease.Value.References--;
                content.Dispose(); throw;
            }
        }
    }
    internal EntryLease Acquire(RenderAssetEntry mesh)
    {
        lock (_gate)
        {
            EnsureOpen();
            if (_entries.TryGetValue(mesh.Key, out var existing))
            {
                ResourceGuard.Require(existing.References > 0 && existing.Error == null, RenderResourceError.Conflict, "Mesh is retiring or failed; cleanup must finish before retry.");
                existing.References++; return new(this, existing);
            }
            ResourceGuard.Require(_entries.Count < _options.MaximumGpuEntries, RenderResourceError.LimitExceeded, "Mesh entry admission exhausted.");
            var slotIndex = _slots.FindIndex(static slot => slot.Entry == null && !slot.Retired);
            if (slotIndex < 0)
            {
                ResourceGuard.Require(_slots.Count < _options.MaximumGpuEntries, RenderResourceError.LimitExceeded, "Mesh slots retired their generation capacity.");
                slotIndex = _slots.Count; _slots.Add(new());
            }
            var slot = _slots[slotIndex];
            var entry = new Entry(mesh, new(Id, Generation, slotIndex, slot.Generation), CancellationTokenSource.CreateLinkedTokenSource(_end.Token));
            slot.Entry = entry; _entries.Add(mesh.Key, entry); entry.References = 1;
            _ = Task.Run(() => UploadAsync(entry));
            return new(this, entry);
        }
    }
    private async Task UploadAsync(Entry entry)
    {
        try
        {
            await _backendGate.WaitAsync(entry.End.Token).ConfigureAwait(false);
            try
            {
                if (_backend == null)
                {
                    try { _backend = _backends.Create(_request) ?? throw new RenderResourceException(RenderResourceError.BackendFailure, "Backend factory returned null."); }
                    catch (RenderResourceCreationException failure) { lock (_gate) _failedBackends.Add(failure.CleanupOwner); throw; }
                }
                try { entry.Upload = _backend.MeshUploads.Create(new(entry.Value, AllocationBudget)) ?? throw new RenderResourceException(RenderResourceError.BackendFailure, "Upload factory returned null."); entry.Owner = entry.Upload; }
                catch (RenderResourceCreationException failure) { entry.Owner = failure.CleanupOwner; throw; }
            }
            finally { _backendGate.Release(); }
            var upload = entry.Upload ?? throw new RenderResourceException(RenderResourceError.BackendFailure, "Missing upload owner.");
            await upload.PrepareAsync(entry.End.Token).ConfigureAwait(false);
            entry.End.Token.ThrowIfCancellationRequested();
            ResourceGuard.Require(entry.Upload.IsReady, RenderResourceError.BackendFailure, "Upload returned without confirmed readiness.");
            lock (_gate) { EnsureOpen(); entry.Completion.TrySetResult(); }
        }
        catch (Exception error)
        {
            lock (_gate)
            {
                entry.Error = error;
                if (error is RenderResourceException { Error: RenderResourceError.UnknownOutcome }) { _closed = true; AllocationBudget.Close(); }
            }
            if (error is OperationCanceledException) entry.Completion.TrySetCanceled(entry.End.Token); else entry.Completion.TrySetException(error);
        }
    }
    internal void Finished(RenderPreparation operation, bool wasActive)
    {
        RenderPreparation? next = null;
        lock (_gate)
        {
            if (wasActive) _active--;
            if (operation.IsReleased) _operations.Remove(operation);
            if (!_closed && _active < _options.MaximumActivePreparations && _queue.First is { } node) { next = node.Value; _queue.RemoveFirst(); next.QueueNode = null; _active++; }
        }
        if (next != null) _ = Task.Run(next.RunAsync);
    }
    internal void Released(RenderPreparation operation) { lock (_gate) { if (operation.Completion.IsCompleted) _operations.Remove(operation); } }
    internal void CancelQueued(RenderPreparation operation)
    {
        lock (_gate)
        {
            if (operation.QueueNode == null) return;
            _queue.Remove(operation.QueueNode); operation.QueueNode = null;
        }
        operation.FinishQueuedCancellation();
    }
    /// <summary>Validates a non-owning handle without resolving native data or acquiring ownership.</summary><param name="handle">Exact cache handle.</param><returns>Its pinned logical key.</returns>
    public RenderAssetKey ValidateHandle(RenderResourceHandle handle)
    {
        lock (_gate)
        {
            EnsureOpen();
            ResourceGuard.Require(handle.OwnerId == Id && handle.DeviceGeneration == Generation && handle.Slot >= 0 && handle.Slot < _slots.Count,
                RenderResourceError.StaleHandle, "Foreign or invalid mesh handle.");
            var slot = _slots[handle.Slot]; var entry = slot.Entry;
            ResourceGuard.Require(!slot.Retired && slot.Generation == handle.SlotGeneration && entry != null && entry.References > 0 && entry.Error == null &&
                entry.Completion.Task.IsCompletedSuccessfully && entry.Upload is { IsReady: true, IsReleased: false }, RenderResourceError.StaleHandle, "Retired or unready mesh handle.");
            return entry!.Value.Key;
        }
    }
    private async ValueTask ReleaseAsync(EntryLease lease)
    {
        var entry = lease.Value; bool cleanup;
        lock (_gate)
        {
            if (!lease.ReferenceReleased) { lease.ReferenceReleased = true; entry.References--; }
            cleanup = entry.References == 0 && !entry.Released;
        }
        if (!cleanup) return;
        await entry.CleanupGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (entry.Released) return;
            try { entry.End.Cancel(); } catch (Exception error) { entry.CancellationError = error; }
            try { await entry.Completion.Task.ConfigureAwait(false); } catch { /* The owner still requires cleanup after failed upload. */ }
            if (entry.Owner != null)
            {
                await entry.Owner.DisposeAsync().ConfigureAwait(false);
                ResourceGuard.Require(entry.Upload == null || entry.Upload.IsReleased, RenderResourceError.CleanupFailed, "Upload cleanup retained ownership.");
                entry.Owner = null;
            }
            lock (_gate)
            {
                var slot = _slots[entry.Handle.Slot]; slot.Entry = null;
                if (slot.Generation == uint.MaxValue) slot.Retired = true; else slot.Generation++;
                _entries.Remove(entry.Value.Key); entry.Released = true;
            }
            entry.End.Dispose();
        }
        finally { entry.CleanupGate.Release(); }
    }
    internal void EnsureOpen() => ResourceGuard.Require(!Volatile.Read(ref _closed), RenderResourceError.Closed, "Device preparation/use admission is closed.");
    internal bool IsAdmissionClosed => Volatile.Read(ref _closed);
    internal void FenceSnapshotGeneration()
    {
        // Close allocation before cancellation callbacks; accepted uses still own their retained entries.
        lock (_gate) _closed = true;
        AllocationBudget.Close(); BeginStop();
    }
    /// <summary>Starts cleanup; cancellation only stops this caller's wait, not actual resource retirement.</summary><param name="cancellationToken">Wait cancellation.</param><returns>Confirmed cleanup or retained failure.</returns>
    public async Task StopAsync(CancellationToken cancellationToken = default) => await DisposeAsync().AsTask().WaitAsync(cancellationToken).ConfigureAwait(false);
    internal void BeginStop()
    {
        RenderPreparation[] operations;
        lock (_gate) { _closed = true; operations = _operations.ToArray(); }
        foreach (var operation in operations) operation.Cancel();
        try { _end.Cancel(); } catch { /* Cancellation callbacks cannot discard actual resource owners. */ }
        AllocationBudget.Close();
    }
    /// <summary>Closes admission, drains operations, retries retained owners and releases backend last.</summary>
    public async ValueTask DisposeAsync()
    {
        if (IsReleased) return;
        BeginStop(); RenderPreparation[] operations;
        await _disposeGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (IsReleased) return;
            var errors = new List<Exception>();
            lock (_gate) operations = _operations.ToArray();
            foreach (var operation in operations) { try { await operation.DisposeAsync().ConfigureAwait(false); } catch (Exception error) { errors.Add(error); } }
            foreach (var failed in _failedBackends.ToArray()) { try { await failed.DisposeAsync().ConfigureAwait(false); _failedBackends.Remove(failed); } catch (Exception error) { errors.Add(error); } }
            if (errors.Count != 0) throw new AggregateException("Device cleanup retained resources.", errors);
            ResourceGuard.Require(_entries.Count == 0, RenderResourceError.CleanupFailed, "Device still owns mesh entries.");
            if (_backend != null) { await _backend.DisposeAsync().ConfigureAwait(false); ResourceGuard.Require(_backend.IsReleased, RenderResourceError.CleanupFailed, "Backend context was not released."); _backend = null; }
            IsReleased = true;
        }
        finally { _disposeGate.Release(); }
    }
    internal sealed class Entry(RenderAssetEntry value, RenderResourceHandle handle, CancellationTokenSource end)
    {
        internal RenderAssetEntry Value { get; } = value; internal RenderResourceHandle Handle { get; } = handle;
        internal CancellationTokenSource End { get; } = end; internal SemaphoreSlim CleanupGate { get; } = new(1, 1);
        internal TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int References; internal IRenderMeshUpload? Upload; internal IAsyncDisposable? Owner; internal Exception? Error, CancellationError; internal bool Released;
    }
    internal sealed class EntryLease(RenderDeviceOwner owner, Entry value)
    { internal Entry Value { get; } = value; internal bool ReferenceReleased; internal ValueTask ReleaseAsync() => owner.ReleaseAsync(this); }
    private sealed class Slot { internal uint Generation = 1; internal Entry? Entry; internal bool Retired; }
}
