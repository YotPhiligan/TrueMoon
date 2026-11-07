namespace TrueMoon.Aether.Rendering.Resources;

/// <summary>Actual owned allocation and CPU staging counters.</summary>
/// <param name="GeometryBytes">Device allocation requirements.</param><param name="UploadStagingBytes">Upload/readback allocation requirements.</param><param name="CpuStagingBytes">Owned temporary CPU buffers.</param>
public sealed record RenderAllocationCounts(long GeometryBytes, long UploadStagingBytes, long CpuStagingBytes);

/// <summary>Atomic pre-allocation reservations retained until actual resource cleanup.</summary>
public sealed class RenderAllocationBudget
{
    private readonly Lock _gate = new();
    private readonly RenderResourceOptions _options;
    private long _geometry, _upload, _cpu;
    private bool _closed;
    /// <summary>Copies bounded allocation options.</summary><param name="options">Optional lower budgets.</param>
    public RenderAllocationBudget(RenderResourceOptions? options = null) => _options = (options ?? new()).Snapshot();
    /// <summary>Gets counters without native calls.</summary>
    public RenderAllocationCounts Counts { get { lock (_gate) return new(_geometry, _upload, _cpu); } }
    /// <summary>Reserves all byte classes or none before allocating.</summary>
    /// <param name="geometryBytes">Actual device requirements.</param><param name="uploadStagingBytes">Actual staging requirements.</param><param name="cpuStagingBytes">CPU buffer bytes.</param><returns>Exclusive idempotent reservation.</returns>
    public RenderAllocationReservation Reserve(long geometryBytes, long uploadStagingBytes, long cpuStagingBytes)
    {
        if (geometryBytes < 0 || uploadStagingBytes < 0 || cpuStagingBytes < 0) throw new ArgumentOutOfRangeException(nameof(geometryBytes));
        lock (_gate)
        {
            ResourceGuard.Require(!_closed, RenderResourceError.Closed, "Device allocation admission is closed.");
            ResourceGuard.Require(geometryBytes <= _options.MaximumGeometryBytes - _geometry && uploadStagingBytes <= _options.MaximumUploadStagingBytes - _upload &&
                cpuStagingBytes <= _options.MaximumCpuStagingBytes - _cpu, RenderResourceError.LimitExceeded, "Allocation peak budget exhausted.");
            _geometry += geometryBytes; _upload += uploadStagingBytes; _cpu += cpuStagingBytes;
            return new(this, geometryBytes, uploadStagingBytes, cpuStagingBytes);
        }
    }
    internal void Close() { lock (_gate) _closed = true; }
    internal void Release(long geometry, long upload, long cpu) { lock (_gate) { _geometry -= geometry; _upload -= upload; _cpu -= cpu; } }
}
/// <summary>One exclusive byte reservation; release only after freeing the matching buffers.</summary>
public sealed class RenderAllocationReservation : IDisposable
{
    private readonly RenderAllocationBudget _owner;
    private readonly long _geometry, _upload, _cpu;
    private int _released;
    internal RenderAllocationReservation(RenderAllocationBudget owner, long geometry, long upload, long cpu)
    { _owner = owner; _geometry = geometry; _upload = upload; _cpu = cpu; }
    /// <summary>Releases all reserved classes exactly once.</summary>
    public void Dispose() { if (Interlocked.Exchange(ref _released, 1) == 0) _owner.Release(_geometry, _upload, _cpu); }
}
