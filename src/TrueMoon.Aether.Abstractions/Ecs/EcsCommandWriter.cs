namespace TrueMoon.Aether.Ecs;

/// <summary>Stamped recording-only value. It cannot play/reset/dispose the host's shared phase buffer.</summary>
public readonly struct EcsCommandWriter
{
    private readonly IEcsSystemAccess? _access;
    private readonly ulong _invocation;
    internal EcsCommandWriter(IEcsSystemAccess access, ulong invocation) { _access = access; _invocation = invocation; }
    private IEcsSystemAccess Access => EcsAccess.Require(_access, _invocation);
    /// <summary>Pending phase commands.</summary>
    public int Count => Access.CommandCount(_invocation);
    /// <summary>Reserves an entity; no structural change occurs yet.</summary><returns>Batch-local reservation.</returns>
    public DeferredEntity CreateEntity() => Access.CreateEntity(_invocation);
    /// <summary>Records destruction.</summary><param name="entity">Live or batch-local target.</param>
    public void DestroyEntity(CommandTarget entity) => Access.DestroyEntity(_invocation, entity);
    /// <summary>Records typed addition with declared write access.</summary><typeparam name="T">Value type.</typeparam><param name="entity">Target.</param><param name="value">Copied value.</param>
    public void AddComponent<T>(CommandTarget entity, T value) where T : struct { ComponentStorage.Validate<T>(); Access.AddComponent(_invocation, entity, value); }
    /// <summary>Records typed replacement with declared write access.</summary><typeparam name="T">Value type.</typeparam><param name="entity">Target.</param><param name="value">Copied value.</param>
    public void SetComponent<T>(CommandTarget entity, T value) where T : struct { ComponentStorage.Validate<T>(); Access.SetComponent(_invocation, entity, value); }
    /// <summary>Records typed removal with declared write access.</summary><typeparam name="T">Value type.</typeparam><param name="entity">Target.</param>
    public void RemoveComponent<T>(CommandTarget entity) where T : struct { ComponentStorage.Validate<T>(); Access.RemoveComponent<T>(_invocation, entity); }
}
