using A = TrueMoon.Aether.Ecs;
using CommandTarget = TrueMoon.Aether.Ecs.CommandTarget;
using DeferredEntity = TrueMoon.Aether.Ecs.DeferredEntity;
using Ecs_EcsSystemContext = TrueMoon.Aether.Ecs.EcsSystemContext;
using EcsPhase = TrueMoon.Aether.Ecs.EcsPhase;
using EntityHandle = TrueMoon.Aether.Ecs.EntityHandle;
using IEcsSystemAccess = TrueMoon.Aether.Ecs.IEcsSystemAccess;

namespace TrueMoon.Aether.Runtime.Ecs;

// One binding per owned system. No container, component boxing or independent storage.
// Retained neutral descriptors keep this small binding, whose World reference is cleared after cleanup.
internal sealed class EcsContractAccess(World world) : IEcsSystemAccess
{
    private World? _world = world;
    private EcsSystemContext? _context;
    private ulong _invocation;
    private int _thread;
    private CancellationToken _cancellation;
    internal Ecs_EcsSystemContext Activate(EcsSystemContext context)
    {
        if (_context != null) throw new InvalidOperationException("Contract invocation is reentrant.");
        if (_world == null) throw new ObjectDisposedException(nameof(EcsContractAccess));
        if (!ReferenceEquals(context.World, _world)) throw new InvalidOperationException("Contract system World mismatch.");
        _invocation = checked(_invocation + 1); _thread = Environment.CurrentManagedThreadId;
        _context = context; _cancellation = context.CancellationToken;
        try { return new(this, _invocation, context.TickNumber, context.DeltaTime, (EcsPhase)context.Phase, _cancellation); }
        catch { Deactivate(); throw; }
    }
    internal void Deactivate() { _context = null; _cancellation = default; }
    internal void Release() { Deactivate(); _world = null; }
    public void ValidateInvocation(ulong invocation)
    {
        if (_context == null || invocation == 0 || invocation != _invocation || Environment.CurrentManagedThreadId != _thread)
            throw new InvalidOperationException("Expired ECS invocation or wrong owner thread.");
        ObjectDisposedException.ThrowIf(_world == null || _world.IsDisposed, this);
        if (_world.IsFaulted) throw new InvalidOperationException("The bound World is faulted.");
        _cancellation.ThrowIfCancellationRequested();
    }
    public Guid GetWorldId(ulong invocation) { ValidateInvocation(invocation); return _world!.Id; }
    public bool IsAlive(ulong invocation, EntityHandle entity) { ValidateInvocation(invocation); return _world!.IsAlive(Legacy(entity)); }
    public bool HasComponent<T>(ulong invocation, EntityHandle entity) where T : struct
    { ValidateInvocation(invocation); return _world!.HasComponent<T>(Legacy(entity)); }
    public bool TryGetComponent<T>(ulong invocation, EntityHandle entity, out T value) where T : struct
    { ValidateInvocation(invocation); return _world!.TryGetComponent(Legacy(entity), out value); }
    public void ForEach<T>(ulong invocation, ReadOnlySpan<Type> excluded, A.QueryReadAction<T> action) where T : struct
    { ArgumentNullException.ThrowIfNull(action); Visit(invocation, excluded, action, null); }
    public void ForEachWrite<T>(ulong invocation, ReadOnlySpan<Type> excluded, A.QueryWriteAction<T> action) where T : struct
    { ArgumentNullException.ThrowIfNull(action); Visit(invocation, excluded, null, action); }
    private void Visit<T>(ulong invocation, ReadOnlySpan<Type> excluded, A.QueryReadAction<T>? read, A.QueryWriteAction<T>? write) where T : struct
    {
        ValidateInvocation(invocation); var owner = _world!;
        using var scope = owner.BeginQuery(typeof(T), null, write != null, false, Exclusions(excluded, typeof(T), null));
        var pool = owner.FindPool<T>(); if (pool == null) return;
        for (var i = 0; i < pool.Count; i++)
        {
            ValidateInvocation(invocation); var index = pool.EntityAt(i); if (!scope.Includes(index)) continue;
            ref var value = ref pool.Reference(index); var entity = Neutral(owner.HandleAt(index));
            if (read != null) read(entity, in value); else write!(entity, ref value);
        }
    }
    public void ForEach<T1, T2>(ulong invocation, ReadOnlySpan<Type> excluded, A.QueryReadAction<T1, T2> action) where T1 : struct where T2 : struct
    { ArgumentNullException.ThrowIfNull(action); Visit(invocation, excluded, action, null, null); }
    public void ForEachWrite<T1, T2>(ulong invocation, ReadOnlySpan<Type> excluded, A.QueryWriteAction<T1, T2> action) where T1 : struct where T2 : struct
    { ArgumentNullException.ThrowIfNull(action); Visit(invocation, excluded, null, action, null); }
    public void ForEachWriteBoth<T1, T2>(ulong invocation, ReadOnlySpan<Type> excluded, A.QueryWriteBothAction<T1, T2> action) where T1 : struct where T2 : struct
    { ArgumentNullException.ThrowIfNull(action); Visit(invocation, excluded, null, null, action); }
    private void Visit<T1, T2>(ulong invocation, ReadOnlySpan<Type> excluded, A.QueryReadAction<T1, T2>? read, A.QueryWriteAction<T1, T2>? write, A.QueryWriteBothAction<T1, T2>? both) where T1 : struct where T2 : struct
    {
        ValidateInvocation(invocation); var owner = _world!;
        if (typeof(T1) == typeof(T2)) throw new ArgumentException("Required query types must be distinct.");
        using var scope = owner.BeginQuery(typeof(T1), typeof(T2), write != null || both != null, both != null, Exclusions(excluded, typeof(T1), typeof(T2)));
        var first = owner.FindPool<T1>(); var second = owner.FindPool<T2>(); if (first == null || second == null) return;
        var candidates = first.Count <= second.Count ? (IComponentPool)first : second;
        for (var i = 0; i < candidates.Count; i++)
        {
            ValidateInvocation(invocation); var index = candidates.EntityAt(i);
            if (!first.Contains(index) || !second.Contains(index) || !scope.Includes(index)) continue;
            ref var a = ref first.Reference(index); ref var b = ref second.Reference(index); var entity = Neutral(owner.HandleAt(index));
            if (read != null) read(entity, in a, in b);
            else if (write != null) write(entity, ref a, in b);
            else both!(entity, ref a, ref b);
        }
    }
    private static QueryExclusions? Exclusions(ReadOnlySpan<Type> excluded, Type first, Type? second)
    {
        if (excluded.IsEmpty) return null;
        if (excluded.Length > 8) throw new ArgumentException("Too many exclusions.", nameof(excluded));
        var copy = excluded.ToArray();
        if (copy.Any(t => t == null || t == first || t == second) || copy.Distinct().Count() != copy.Length)
            throw new ArgumentException("Invalid query exclusions.", nameof(excluded));
        return new(copy);
    }
    public int CommandCount(ulong invocation) { ValidateInvocation(invocation); return _context!.Commands.Count; }
    public DeferredEntity CreateEntity(ulong invocation)
    { ValidateInvocation(invocation); var token = _context!.Commands.CreateEntity(); return new(token.BufferId, token.BatchId, token.Index); }
    public void DestroyEntity(ulong invocation, CommandTarget entity) { ValidateInvocation(invocation); _context!.Commands.DestroyEntity(Legacy(entity)); }
    public void AddComponent<T>(ulong invocation, CommandTarget entity, T value) where T : struct
    { ValidateInvocation(invocation); _context!.Commands.AddComponent(Legacy(entity), value); }
    public void SetComponent<T>(ulong invocation, CommandTarget entity, T value) where T : struct
    { ValidateInvocation(invocation); _context!.Commands.SetComponent(Legacy(entity), value); }
    public void RemoveComponent<T>(ulong invocation, CommandTarget entity) where T : struct
    { ValidateInvocation(invocation); _context!.Commands.RemoveComponent<T>(Legacy(entity)); }
    public void RequestStop(ulong invocation) { ValidateInvocation(invocation); _context!.RequestStop(); }
    private static EntityHandle Legacy(EntityHandle entity) => entity;
    private static EntityHandle Neutral(EntityHandle entity) => entity;
    private static CommandTarget Legacy(CommandTarget target) => target.IsDeferred
        ? new DeferredEntity(target.Deferred.BufferId, target.Deferred.BatchId, target.Deferred.Index) : Legacy(target.Live);
}
