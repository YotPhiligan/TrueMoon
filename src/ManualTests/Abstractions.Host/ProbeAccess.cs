using TrueMoon.Aether.Abstractions.Ecs;

namespace TrueMoon.ManualTests.ContractHost;

/// <summary>Single-callback contract double. It has no World, pools, scheduler or structural playback.</summary>
public sealed class ProbeAccess(EcsSystemMetadata metadata) : IEcsSystemAccess
{
    private readonly int _owner = Environment.CurrentManagedThreadId;
    private readonly Guid _buffer = Guid.NewGuid(), _batch = Guid.NewGuid();
    public EntityHandle Entity { get; } = new(Guid.NewGuid(), 0, 1);
    public ulong Invocation { get; private set; } = 1;
    public bool Active { get; private set; } = true;
    public bool StopRequested { get; private set; }
    public int Commands { get; private set; }
    public int QueryCallbacks { get; private set; }
    public int ValueRecorded { get; private set; }
    public CancellationToken Cancellation { get; set; }
    public void End() => Active = false;
    public void Next() { Active = true; Invocation = checked(Invocation + 1); }
    public void ValidateInvocation(ulong invocation)
    {
        if (!Active || invocation != Invocation || Environment.CurrentManagedThreadId != _owner)
            throw new InvalidOperationException("Expired invocation or wrong owner thread.");
        Cancellation.ThrowIfCancellationRequested();
    }
    private void Read<T>(ulong invocation) where T : struct { ValidateInvocation(invocation); ComponentStorage.Validate<T>(); if (!metadata.CanRead(typeof(T))) throw new InvalidOperationException("Undeclared read."); }
    private void Write<T>(ulong invocation) where T : struct { Read<T>(invocation); if (!metadata.CanWrite(typeof(T))) throw new InvalidOperationException("Undeclared write."); }
    private void Excluded(ReadOnlySpan<Type> types) { foreach (var type in types) if (!metadata.CanRead(type)) throw new InvalidOperationException("Undeclared exclusion."); }
    public bool IsAlive(ulong invocation, EntityHandle entity) { ValidateInvocation(invocation); return entity == Entity; }
    public Guid GetWorldId(ulong invocation) { ValidateInvocation(invocation); return Entity.WorldId; }
    public bool HasComponent<T>(ulong invocation, EntityHandle entity) where T : struct { Read<T>(invocation); RequireEntity(entity); return true; }
    public bool TryGetComponent<T>(ulong invocation, EntityHandle entity, out T value) where T : struct { Read<T>(invocation); RequireEntity(entity); value = default; return true; }
    public void ForEach<T>(ulong invocation, ReadOnlySpan<Type> excluded, QueryReadAction<T> action) where T : struct
    { Read<T>(invocation); Excluded(excluded); T value = default; action(Entity, in value); QueryCallbacks++; }
    public void ForEachWrite<T>(ulong invocation, ReadOnlySpan<Type> excluded, QueryWriteAction<T> action) where T : struct
    { Write<T>(invocation); Excluded(excluded); T value = default; action(Entity, ref value); QueryCallbacks++; }
    public void ForEach<T1, T2>(ulong invocation, ReadOnlySpan<Type> excluded, QueryReadAction<T1, T2> action) where T1 : struct where T2 : struct
    { Read<T1>(invocation); Read<T2>(invocation); Excluded(excluded); T1 first = default; T2 second = default; action(Entity, in first, in second); QueryCallbacks++; }
    public void ForEachWrite<T1, T2>(ulong invocation, ReadOnlySpan<Type> excluded, QueryWriteAction<T1, T2> action) where T1 : struct where T2 : struct
    { Write<T1>(invocation); Read<T2>(invocation); Excluded(excluded); T1 first = default; T2 second = default; action(Entity, ref first, in second); QueryCallbacks++; }
    public void ForEachWriteBoth<T1, T2>(ulong invocation, ReadOnlySpan<Type> excluded, QueryWriteBothAction<T1, T2> action) where T1 : struct where T2 : struct
    { Write<T1>(invocation); Write<T2>(invocation); Excluded(excluded); T1 first = default; T2 second = default; action(Entity, ref first, ref second); QueryCallbacks++; }
    public int CommandCount(ulong invocation) { ValidateInvocation(invocation); return Commands; }
    public DeferredEntity CreateEntity(ulong invocation) { ValidateInvocation(invocation); Commands++; return new(_buffer, _batch, 0); }
    public void DestroyEntity(ulong invocation, CommandTarget entity) { ValidateInvocation(invocation); Target(entity); Commands++; }
    public void AddComponent<T>(ulong invocation, CommandTarget entity, T value) where T : struct { Write<T>(invocation); Target(entity); Commands++; ValueRecorded++; }
    public void SetComponent<T>(ulong invocation, CommandTarget entity, T value) where T : struct { Write<T>(invocation); Target(entity); Commands++; ValueRecorded++; }
    public void RemoveComponent<T>(ulong invocation, CommandTarget entity) where T : struct { Write<T>(invocation); Target(entity); Commands++; }
    public void RequestStop(ulong invocation) { ValidateInvocation(invocation); StopRequested = true; }
    private void Target(CommandTarget target)
    {
        if (target.IsDeferred) { if (target.Deferred.BufferId != _buffer || target.Deferred.BatchId != _batch || target.Deferred.Index != 0) throw new ArgumentException("Wrong reservation."); }
        else RequireEntity(target.Live);
    }
    private void RequireEntity(EntityHandle entity) { if (entity != Entity) throw new ArgumentException("Wrong or stale handle."); }
}
