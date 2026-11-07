using System.Runtime.CompilerServices;

namespace TrueMoon.Aether.Ecs;

/// <summary>Caller-owned, headless storage of entities and singleton value components.</summary>
/// <remarks>
/// Access must be serialized by the simulation/session owner; this type is not thread-safe.
/// Component reads return copies. Components cannot contain managed references and are never
/// disposed by World. Structural commands, queries, authoring materialization and systems are separate stages.
/// Application services obtain Worlds through UseAetherEcs and IFactory; direct construction is the low-level path.
/// </remarks>
public sealed partial class World : IDisposable
{
    private readonly Dictionary<Type, IComponentPool> _pools = [];
    private readonly int _maximumEntities, _maximumComponentTypes;
    private readonly uint _generationLimit;
    private EntitySlot[] _slots;
    private int[] _freeIndices;
    private int _nextIndex, _freeCount, _entityCount;

    /// <summary>Creates independent low-level storage. The caller owns disposal; no services/native resources are activated.</summary>
    /// <param name="options">Optional limits, validated and copied before allocation.</param>
    /// <exception cref="ArgumentOutOfRangeException">One of the storage limits is invalid.</exception>
    public World(WorldOptions? options = null) : this(options ?? new WorldOptions(), uint.MaxValue) { }

    // A bounded generation range permits exercising actual retirement without billions of allocations.
    internal World(WorldOptions options, uint generationLimit)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (generationLimit == 0) throw new ArgumentOutOfRangeException(nameof(generationLimit));
        var snapshot = options.Snapshot();
        _maximumEntities = snapshot.MaximumEntityCount;
        _maximumComponentTypes = snapshot.MaximumComponentTypes;
        _generationLimit = generationLimit;
        _slots = new EntitySlot[snapshot.InitialEntityCapacity];
        _freeIndices = new int[snapshot.InitialEntityCapacity];
    }

    /// <summary>Gets this World's process-local identity. It remains readable after disposal.</summary>
    public Guid Id { get; } = Guid.NewGuid();
    /// <summary>Gets whether disposal has invalidated the entire World.</summary>
    public bool IsDisposed { get; private set; }
    /// <summary>Gets the live entity count. Throws after disposal.</summary>
    public int EntityCount { get { ThrowIfDisposed(); return _entityCount; } }
    /// <summary>Gets the number of retained component types, including empty pools. Throws after disposal.</summary>
    public int ComponentTypeCount { get { ThrowIfDisposed(); return _pools.Count; } }

    /// <summary>Creates an empty entity, reusing a free slot with its advanced generation when possible.</summary>
    /// <returns>A handle valid only in this World until destruction/disposal.</returns>
    /// <exception cref="InvalidOperationException">The slot budget is exhausted, including retired slots.</exception>
    public EntityHandle CreateEntity()
    {
        EnsureMutation(null, true);
        int index;
        if (_freeCount != 0) index = _freeIndices[--_freeCount];
        else
        {
            if (_nextIndex == _maximumEntities) throw new InvalidOperationException("The World entity slot budget is exhausted.");
            EnsureEntityCapacity(_nextIndex + 1);
            index = _nextIndex++;
            _slots[index].Generation = 1;
        }
        _slots[index].Alive = true; _entityCount++;
        return new(Id, index, _slots[index].Generation);
    }

    /// <summary>Tests identity, live membership and generation without accepting a handle from another World.</summary>
    /// <param name="entity">Handle to inspect; default, foreign and stale handles return false.</param>
    /// <returns>Whether the entity is currently alive in this World.</returns>
    public bool IsAlive(EntityHandle entity) { ThrowIfDisposed(); return Matches(entity); }

    /// <summary>Removes all components and invalidates the entity. Exhausted slots are retired instead of wrapping.</summary>
    /// <param name="entity">A live handle issued by this World.</param>
    /// <exception cref="ArgumentException">The handle is invalid, stale or belongs to another World.</exception>
    public void DestroyEntity(EntityHandle entity)
    {
        ValidateEntity(entity);
        EnsureMutation(null, true);
        foreach (var pool in _pools.Values) pool.Remove(entity.Index);
        ref var slot = ref _slots[entity.Index];
        slot.Alive = false; _entityCount--;
        if (slot.Generation < _generationLimit)
        {
            slot.Generation++;
            _freeIndices[_freeCount++] = entity.Index;
        }
    }

    /// <summary>Adds one value component, rejecting duplicate membership and managed references.</summary>
    /// <typeparam name="T">A value type containing no managed references; tags are allowed.</typeparam>
    /// <param name="entity">Live target entity.</param><param name="value">Value to copy into storage.</param>
    /// <exception cref="InvalidOperationException">The component exists or the distinct type budget is exhausted.</exception>
    /// <exception cref="NotSupportedException">T contains managed references.</exception>
    public void AddComponent<T>(EntityHandle entity, T value) where T : struct
    {
        ValidateEntity(entity);
        EnsureMutation(typeof(T), true);
        var pool = FindPool<T>();
        if (pool != null) { pool.Add(entity.Index, value); return; }
        if (_pools.Count == _maximumComponentTypes) throw new InvalidOperationException("The World component type budget is exhausted.");
        pool = new ComponentPool<T>(_maximumEntities);
        pool.Add(entity.Index, value);
        _pools.Add(typeof(T), pool);
    }

    /// <summary>Replaces an existing value. This operation never adds missing membership.</summary>
    /// <typeparam name="T">Supported value component type.</typeparam>
    /// <param name="entity">Live target entity.</param><param name="value">Replacement value.</param>
    /// <exception cref="KeyNotFoundException">The entity does not have this component.</exception>
    public void SetComponent<T>(EntityHandle entity, T value) where T : struct
    {
        ValidateEntity(entity);
        EnsureMutation(typeof(T), false);
        var pool = FindPool<T>();
        if (pool == null || !pool.Contains(entity.Index)) throw MissingComponent<T>();
        pool.Set(entity.Index, value);
    }

    /// <summary>Copies an existing component; changing the returned value does not modify this World.</summary>
    /// <typeparam name="T">Supported value component type.</typeparam>
    /// <param name="entity">Live target entity.</param><returns>A copy of the stored value.</returns>
    /// <exception cref="KeyNotFoundException">The entity does not have this component.</exception>
    public T GetComponent<T>(EntityHandle entity) where T : struct
    {
        if (TryGetComponent<T>(entity, out var value)) return value;
        throw MissingComponent<T>();
    }

    /// <summary>Copies a component if present. Invalid handles still fail; absence does not register a pool.</summary>
    /// <typeparam name="T">Supported value component type.</typeparam>
    /// <param name="entity">Live target entity.</param><param name="value">Stored copy or default when absent.</param>
    /// <returns>Whether the component is present.</returns>
    public bool TryGetComponent<T>(EntityHandle entity, out T value) where T : struct
    {
        ValidateEntity(entity);
        EnsureRead(typeof(T));
        var pool = FindPool<T>();
        if (pool != null) return pool.TryGet(entity.Index, out value);
        value = default; return false;
    }

    /// <summary>Tests component membership without registering a type or changing data.</summary>
    /// <typeparam name="T">Supported value component type.</typeparam>
    /// <param name="entity">Live target entity.</param><returns>Whether the component is present.</returns>
    public bool HasComponent<T>(EntityHandle entity) where T : struct
    {
        ValidateEntity(entity);
        EnsureRead(typeof(T));
        return FindPool<T>()?.Contains(entity.Index) == true;
    }

    /// <summary>Removes membership, preserving all other entities even when dense storage is compacted.</summary>
    /// <typeparam name="T">Supported value component type.</typeparam>
    /// <param name="entity">Live target entity.</param><returns>True if a component was removed; false if absent.</returns>
    public bool RemoveComponent<T>(EntityHandle entity) where T : struct
    {
        ValidateEntity(entity);
        EnsureMutation(typeof(T), true);
        return FindPool<T>()?.Remove(entity.Index) == true;
    }

    /// <summary>Invalidates all handles and releases storage references once. Component values are never disposed.</summary>
    public void Dispose()
    {
        if (IsDisposed) return;
        if (_activeQuery || _schedulerOwner != null || _sceneOwner != null)
            throw new InvalidOperationException("Finish queries/scheduler cleanup and dispose the owning SceneWorld before its World.");
        IsDisposed = true;
        _pools.Clear(); _slots = []; _freeIndices = [];
        _nextIndex = _freeCount = _entityCount = 0;
    }

    internal ComponentPool<T>? FindPool<T>() where T : struct
    {
        ValidateComponent<T>();
        return _pools.TryGetValue(typeof(T), out var pool) ? (ComponentPool<T>)pool : null;
    }

    internal static void ValidateComponent<T>() where T : struct
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            throw new NotSupportedException("ECS components containing managed references require a separate storage/ownership contract.");
    }

    private bool Matches(EntityHandle entity)
        => entity.WorldId == Id && (uint)entity.Index < (uint)_nextIndex &&
           _slots[entity.Index].Alive && _slots[entity.Index].Generation == entity.Generation;

    private void ValidateEntity(EntityHandle entity)
    {
        ThrowIfDisposed();
        if (!Matches(entity)) throw new ArgumentException("The entity is not alive in this World generation.", nameof(entity));
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(IsDisposed, this);
    private static KeyNotFoundException MissingComponent<T>() => new($"The entity has no component of type {typeof(T).FullName}.");

    private void EnsureEntityCapacity(int required)
    {
        if (required <= _slots.Length) return;
        var capacity = Math.Min(_maximumEntities, Math.Max(required, _slots.Length == 0 ? 4 : _slots.Length * 2));
        var slots = new EntitySlot[capacity]; var freeIndices = new int[capacity];
        _slots.CopyTo(slots, 0); _freeIndices.CopyTo(freeIndices, 0);
        _slots = slots; _freeIndices = freeIndices;
    }

    private struct EntitySlot
    {
        internal uint Generation;
        internal bool Alive;
    }
}
