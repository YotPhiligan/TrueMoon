namespace TrueMoon.Aether.Ecs;

/// <summary>Invocation-stamped value context. Copies cannot revive when the host starts a later invocation.</summary>
/// <remarks>The borrowed host binding validates ownership and declared access. No concrete World or service resolver is returned.</remarks>
public readonly struct EcsSystemContext
{
    private readonly IEcsSystemAccess? _access;
    private readonly ulong _invocation, _tick;
    private readonly TimeSpan _delta;
    private readonly EcsPhase _phase;
    private readonly CancellationToken _cancellation;
    /// <summary>Captures a host invocation; default context is invalid.</summary>
    /// <param name="access">Borrowed host access binding.</param><param name="invocation">Nonzero monotonic stamp.</param>
    /// <param name="tick">Target tick, starting at one.</param><param name="delta">Positive fixed step.</param>
    /// <param name="phase">Current ordered phase.</param><param name="cancellationToken">Cooperative invocation cancellation.</param>
    public EcsSystemContext(IEcsSystemAccess access, ulong invocation, ulong tick, TimeSpan delta, EcsPhase phase, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(access);
        if (invocation == 0 || tick == 0) throw new ArgumentOutOfRangeException(nameof(invocation));
        if (delta <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(delta));
        if (!Enum.IsDefined(phase)) throw new ArgumentOutOfRangeException(nameof(phase));
        cancellationToken.ThrowIfCancellationRequested(); access.ValidateInvocation(invocation);
        _access = access; _invocation = invocation; _tick = tick; _delta = delta; _phase = phase; _cancellation = cancellationToken;
    }
    private IEcsSystemAccess Access => EcsAccess.Require(_access, _invocation);
    /// <summary>Issuing World identity, never mutable World access.</summary>
    public Guid WorldId => Access.GetWorldId(_invocation);
    /// <summary>Target tick; the scheduler publishes it only after successful phase completion.</summary>
    public ulong TickNumber { get { _ = Access; return _tick; } }
    /// <summary>Caller-supplied fixed step.</summary>
    public TimeSpan DeltaTime { get { _ = Access; return _delta; } }
    /// <summary>Cooperative cancellation, not forced abort.</summary>
    public CancellationToken CancellationToken { get { _ = Access; return _cancellation; } }
    /// <summary>Current phase.</summary>
    public EcsPhase Phase { get { _ = Access; return _phase; } }
    /// <summary>Recording-only writer bound to this invocation.</summary>
    public EcsCommandWriter Commands => new(Access, _invocation);
    /// <summary>Checks current transient liveness through the host.</summary><param name="entity">Handle.</param><returns>Current liveness.</returns>
    public bool IsAlive(EntityHandle entity) => Access.IsAlive(_invocation, entity);
    /// <summary>Checks declared component membership.</summary><typeparam name="T">Reference-free value type.</typeparam><param name="entity">Handle.</param><returns>Membership.</returns>
    public bool HasComponent<T>(EntityHandle entity) where T : struct { ComponentStorage.Validate<T>(); return Access.HasComponent<T>(_invocation, entity); }
    /// <summary>Copies a declared component without exposing a long-lived ref.</summary><typeparam name="T">Reference-free value type.</typeparam><param name="entity">Handle.</param><param name="value">Detached value.</param><returns>Whether present.</returns>
    public bool TryGetComponent<T>(EntityHandle entity, out T value) where T : struct { ComponentStorage.Validate<T>(); return Access.TryGetComponent(_invocation, entity, out value); }
    /// <summary>Creates a stamped typed query; no per-entity interface storage is introduced.</summary><typeparam name="T">Required component.</typeparam><returns>Invocation query.</returns>
    public EcsQuery<T> Query<T>() where T : struct { ComponentStorage.Validate<T>(); return new(Access, _invocation, null); }
    /// <summary>Creates a query requiring two distinct types.</summary><typeparam name="T1">First component.</typeparam><typeparam name="T2">Second component.</typeparam><returns>Invocation query.</returns>
    public EcsQuery<T1, T2> Query<T1, T2>() where T1 : struct where T2 : struct
    {
        ComponentStorage.Validate<T1>(); ComponentStorage.Validate<T2>();
        if (typeof(T1) == typeof(T2)) throw new ArgumentException("Query component types must be distinct.");
        return new(Access, _invocation, null);
    }
    /// <summary>Requests terminal stop after this callback, preserving the host's phase-boundary policy.</summary>
    public void RequestStop() => Access.RequestStop(_invocation);
}

internal static class EcsAccess
{
    internal static IEcsSystemAccess Require(IEcsSystemAccess? access, ulong invocation)
    {
        if (access == null || invocation == 0) throw new InvalidOperationException("The default ECS access is invalid.");
        access.ValidateInvocation(invocation); return access;
    }
    internal static Type[] Exclude<T>(Type[]? current, Type first, Type? second) where T : struct
    {
        ComponentStorage.Validate<T>(); var type = typeof(T);
        if (type == first || type == second) throw new ArgumentException("A required component cannot also be excluded.");
        if (current?.Contains(type) == true) return current;
        if (current?.Length == 8) throw new InvalidOperationException("Queries support at most eight excluded types.");
        return [.. current ?? [], type];
    }
}
