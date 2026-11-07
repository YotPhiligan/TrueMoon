namespace TrueMoon.Aether.Ecs;

internal interface IComponentPool
{
    int Count { get; }
    int EntityAt(int denseIndex);
    bool Contains(int entityIndex);
    bool Remove(int entityIndex);
}

// A zero sparse entry means absent; other entries contain dense index + 1.
// Only World calls this pool, after validating entity identity and component value policy.
internal sealed class ComponentPool<T>(int maximumEntities) : IComponentPool where T : struct
{
    private int[] _sparse = [];
    private int[] _entities = [];
    private T[] _values = [];
    private int _count;

    public int Count => _count;
    public int EntityAt(int denseIndex) => _entities[denseIndex];
    public bool Contains(int entityIndex) => entityIndex < _sparse.Length && _sparse[entityIndex] != 0;
    internal ref T Reference(int entityIndex) => ref _values[_sparse[entityIndex] - 1];

    internal void Add(int entityIndex, T value)
    {
        if (Contains(entityIndex)) throw new InvalidOperationException("The entity already has this component type.");

        // Prepare all growth before publishing arrays or changing membership/counts.
        var sparse = _sparse;
        var entities = _entities;
        var values = _values;
        if (entityIndex >= sparse.Length)
        {
            sparse = new int[Capacity(sparse.Length, entityIndex + 1)];
            _sparse.CopyTo(sparse, 0);
        }
        if (_count == values.Length)
        {
            var capacity = Capacity(values.Length, _count + 1);
            entities = new int[capacity]; values = new T[capacity];
            _entities.CopyTo(entities, 0); _values.CopyTo(values, 0);
        }
        _sparse = sparse; _entities = entities; _values = values;
        _entities[_count] = entityIndex; _values[_count] = value;
        _sparse[entityIndex] = ++_count;
    }

    internal void Set(int entityIndex, T value) => _values[_sparse[entityIndex] - 1] = value;

    internal bool TryGet(int entityIndex, out T value)
    {
        if (Contains(entityIndex)) { value = _values[_sparse[entityIndex] - 1]; return true; }
        value = default; return false;
    }

    public bool Remove(int entityIndex)
    {
        if (!Contains(entityIndex)) return false;
        var removed = _sparse[entityIndex] - 1;
        var last = --_count;
        if (removed != last)
        {
            var movedEntity = _entities[last];
            _entities[removed] = movedEntity; _values[removed] = _values[last];
            _sparse[movedEntity] = removed + 1;
        }
        _sparse[entityIndex] = 0; _entities[last] = 0; _values[last] = default;
        return true;
    }

    private int Capacity(int current, int required)
        => Math.Min(maximumEntities, Math.Max(required, current == 0 ? 4 : current * 2));
}
