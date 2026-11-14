namespace TrueMoon.Kton.Bepu;

/// <summary>Low-level resource-free factory. Use UseBepuPhysics for App integration; results are caller-owned.</summary>
/// <remarks>Dispose cleans only an unpublished failed candidate. It never disposes published caller-owned worlds.</remarks>
public sealed class BepuPhysicsWorldFactory : IFactory<IPhysicsWorld, PhysicsWorldOptions>, IDisposable
{
    private readonly FaultInjection _faults;
    private WorldResources? _pending;
    private int _gate, _disposed, _owners, _published, _cleanups, _poolBudgetExceededOwners;
    private long _maximumPoolBytes, _maximumAfterCleanup;
    /// <summary>Creates a resource-free factory. No world or worker is started here.</summary>
    public BepuPhysicsWorldFactory() : this(new FaultInjection()) { }
    internal BepuPhysicsWorldFactory(FaultInjection faults) => _faults = faults;
    /// <summary>Ownership diagnostics; pooled bytes do not cover every process/GC allocation.</summary>
    public BepuResourceSnapshot Resources => new(Volatile.Read(ref _owners), Volatile.Read(ref _published), _pending is not null,
        Volatile.Read(ref _cleanups), Interlocked.Read(ref _maximumPoolBytes), Interlocked.Read(ref _maximumAfterCleanup), Volatile.Read(ref _poolBudgetExceededOwners));
    /// <summary>Validates/snapshots options, then creates one independent world. A failed candidate remains owned until cleanup completes.</summary>
    /// <param name="arguments">Immutable world options.</param><returns>Caller-owned world, independent of other factory products.</returns>
    public IPhysicsWorld Create(PhysicsWorldOptions arguments)
    {
        Enter();
        try
        {
            if (_disposed != 0) throw Error(PhysicsErrorCode.Disposed, "Factory");
            if (_pending is not null) throw Error(PhysicsErrorCode.Faulted, "Factory.PendingCleanup");
            PhysicsValidation.Validate(arguments).ThrowIfFailure();
            var options = arguments with { Limits = arguments.Limits with { } };
            var resources = new WorldResources(this, _faults);
            try
            {
                var world = new BepuWorld(this, resources, options, _faults);
                world.Initialize(); Interlocked.Increment(ref _published); return world;
            }
            catch (Exception original)
            {
                try { resources.Dispose(); }
                catch (Exception cleanup)
                {
                    _pending = resources;
                    throw new PhysicsException(new(PhysicsErrorCode.Faulted, "Factory.CreationCleanup"), new AggregateException(original, cleanup));
                }
                throw new PhysicsException(new(PhysicsErrorCode.Faulted, "Factory.Creation"), original);
            }
        }
        finally { Volatile.Write(ref _gate, 0); }
    }
    /// <summary>Retries retained failed-creation cleanup, then closes this factory. Published worlds are unaffected.</summary>
    public void Dispose()
    {
        Enter();
        try { _pending?.Dispose(); _pending = null; Volatile.Write(ref _disposed, 1); }
        catch (Exception error) { throw new PhysicsException(new(PhysicsErrorCode.Faulted, "Factory.Cleanup"), error); }
        finally { Volatile.Write(ref _gate, 0); }
    }
    private void Enter() { if (Interlocked.CompareExchange(ref _gate, 1, 0) != 0) throw Error(PhysicsErrorCode.Busy, "Factory"); }
    internal static PhysicsException Error(PhysicsErrorCode code, string field) => new(new(code, field));
    internal void Acquired() => Interlocked.Increment(ref _owners);
    internal void WorldReleased() => Interlocked.Decrement(ref _published);
    internal void Sample(long bytes) => Maximum(ref _maximumPoolBytes, bytes);
    internal void PoolBudgetExceeded() => Interlocked.Increment(ref _poolBudgetExceededOwners);
    internal void Released(long bytes)
    {
        Maximum(ref _maximumAfterCleanup, bytes); Interlocked.Decrement(ref _owners); Interlocked.Increment(ref _cleanups);
    }
    private static void Maximum(ref long target, long value)
    {
        long before;
        do { before = Interlocked.Read(ref target); if (value <= before) return; }
        while (Interlocked.CompareExchange(ref target, value, before) != before);
    }
}
