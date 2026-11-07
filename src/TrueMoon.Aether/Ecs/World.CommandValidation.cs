namespace TrueMoon.Aether.Ecs;

public sealed partial class World
{
    internal CommandValidation BeginCommandValidation() => new(this);

    // Touched state only: the original free stack is read through a private cursor until commit starts.
    internal sealed class CommandValidation(World world)
    {
        private int _nextIndex = world._nextIndex;
        private int _originalFreeCount = world._freeCount;
        private readonly Stack<int> _freed = [];
        private readonly Dictionary<int, (uint Generation, bool Alive)> _slots = [];
        private readonly Dictionary<(EntityHandle Entity, Type Type), bool> _memberships = [];
        private readonly HashSet<Type> _newTypes = [];

        internal EntityHandle Create()
        {
            int index;
            if (_freed.Count != 0) index = _freed.Pop();
            else if (_originalFreeCount != 0) index = world._freeIndices[--_originalFreeCount];
            else
            {
                if (_nextIndex == world._maximumEntities) throw new InvalidOperationException("The command batch exhausts entity slots.");
                index = _nextIndex++;
            }
            var generation = _slots.TryGetValue(index, out var slot) ? slot.Generation :
                index < world._nextIndex ? world._slots[index].Generation : 1u;
            _slots[index] = (generation, true);
            return new(world.Id, index, generation);
        }

        internal void CheckAlive(EntityHandle entity)
        {
            var alive = entity.WorldId == world.Id && (uint)entity.Index < (uint)_nextIndex &&
                (_slots.TryGetValue(entity.Index, out var slot) ? slot.Alive && slot.Generation == entity.Generation : world.Matches(entity));
            if (!alive) throw new ArgumentException("The command target is stale or destroyed earlier in this batch.", nameof(entity));
        }

        internal void Destroy(EntityHandle entity)
        {
            if (entity.Generation < world._generationLimit)
            { _slots[entity.Index] = (entity.Generation + 1, false); _freed.Push(entity.Index); }
            else _slots[entity.Index] = (entity.Generation, false);
        }
        private bool Has(EntityHandle entity, Type type)
            => _memberships.TryGetValue((entity, type), out var present) ? present :
                world.Matches(entity) && world.FindPool(type)?.Contains(entity.Index) == true;
        internal void Add(EntityHandle entity, Type type)
        {
            if (Has(entity, type)) throw new InvalidOperationException("A component is added twice in the command batch.");
            if (!world._pools.ContainsKey(type) && !_newTypes.Contains(type))
            {
                if (world._pools.Count + _newTypes.Count == world._maximumComponentTypes)
                    throw new InvalidOperationException("The command batch exhausts component types.");
                _newTypes.Add(type);
            }
            _memberships[(entity, type)] = true;
        }
        internal void Set(EntityHandle entity, Type type)
        { if (!Has(entity, type)) throw new KeyNotFoundException("A command replaces a missing component."); }
        internal void Remove(EntityHandle entity, Type type) => _memberships[(entity, type)] = false;
    }
}
