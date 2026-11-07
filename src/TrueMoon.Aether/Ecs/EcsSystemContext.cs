namespace TrueMoon.Aether.Ecs;

/// <summary>A cached invocation context valid only during its own synchronous Execute call.</summary>
public sealed class EcsSystemContext
{
    private readonly EcsScheduler _owner;
    private readonly World _world;
    private readonly SystemPlan _plan;
    private bool _active;
    private ulong _invocation;
    private ulong _tick;
    private TimeSpan _delta;
    private CancellationToken _cancellation;
    internal EcsSystemContext(EcsScheduler owner, World world, SystemPlan plan) { _owner = owner; _world = world; _plan = plan; }
    /// <summary>Gets the borrowed World for this invocation.</summary>
    public World World { get { CheckActive(); return _world; } }
    /// <summary>Gets the current target tick, starting at one. Published scheduler tick advances only on success.</summary>
    public ulong TickNumber { get { CheckActive(); return _tick; } }
    /// <summary>Gets the caller-supplied fixed step.</summary>
    public TimeSpan DeltaTime { get { CheckActive(); return _delta; } }
    /// <summary>Gets cooperative cancellation for this invocation.</summary>
    public CancellationToken CancellationToken { get { CheckActive(); return _cancellation; } }
    /// <summary>Gets this system's ordered phase.</summary>
    public EcsPhase Phase { get { CheckActive(); return _plan.Phase; } }
    /// <summary>Gets a scoped recording facade; it cannot play, clear or dispose another system's phase commands.</summary>
    public EcsCommandWriter Commands { get { CheckActive(); return new(this, _invocation); } }
    /// <summary>Requests terminal cooperative stop after this callback, discarding pending phase commands.</summary>
    public void RequestStop() { CheckActive(); _owner.RequestStop(); }
    internal void Activate(ulong tick, TimeSpan delta, CancellationToken cancellation)
    { _invocation = checked(_invocation + 1); _tick = tick; _delta = delta; _cancellation = cancellation; _active = true; }
    internal void Deactivate() { _active = false; _cancellation = default; }
    internal EntityCommandBuffer GetBuffer(ulong invocation)
    {
        CheckActive();
        if (invocation != _invocation) throw new InvalidOperationException("The command writer belongs to an earlier invocation.");
        return _owner.Commands;
    }
    private void CheckActive() { if (!_active) throw new InvalidOperationException("The system context is not active."); }
}

/// <summary>Recording access tied to one active system invocation. Default/retained inactive writers are invalid.</summary>
public readonly struct EcsCommandWriter
{
    private readonly EcsSystemContext? _context;
    private readonly ulong _invocation;
    internal EcsCommandWriter(EcsSystemContext context, ulong invocation) { _context = context; _invocation = invocation; }
    private EntityCommandBuffer Buffer => (_context ?? throw new InvalidOperationException("The default command writer is invalid.")).GetBuffer(_invocation);
    /// <summary>Gets pending commands in this phase.</summary>
    public int Count => Buffer.Count;
    /// <summary>Reserves an entity in this phase.</summary><returns>Batch-local reservation.</returns>
    public DeferredEntity CreateEntity() => Buffer.CreateEntity();
    /// <summary>Records destruction.</summary><param name="entity">Live or phase-local target.</param>
    public void DestroyEntity(CommandTarget entity) => Buffer.DestroyEntity(entity);
    /// <summary>Records addition with declared write access.</summary><typeparam name="T">Component type.</typeparam><param name="entity">Target.</param><param name="value">Copied value.</param>
    public void AddComponent<T>(CommandTarget entity, T value) where T : struct => Buffer.AddComponent(entity, value);
    /// <summary>Records replacement with declared write access.</summary><typeparam name="T">Component type.</typeparam><param name="entity">Target.</param><param name="value">Copied value.</param>
    public void SetComponent<T>(CommandTarget entity, T value) where T : struct => Buffer.SetComponent(entity, value);
    /// <summary>Records removal with declared write access.</summary><typeparam name="T">Component type.</typeparam><param name="entity">Target.</param>
    public void RemoveComponent<T>(CommandTarget entity) where T : struct => Buffer.RemoveComponent<T>(entity);
}
