using TrueMoon.Aether.Ecs;

namespace TrueMoon.Aether.Runtime.Ecs;

public sealed partial class World
{
    internal EntityHandle[] MigrationEntities()
    {
        CheckPlayback(); var entities = new List<EntityHandle>(_entityCount);
        for (var index = 0; index < _nextIndex; index++) if (_slots[index].Alive) entities.Add(HandleAt(index));
        return entities.ToArray();
    }
    internal Type[] MigrationComponentTypes() { CheckPlayback(); return _pools.Keys.ToArray(); }
    internal bool MigrationHasComponent(EntityHandle entity, Type type) => _pools.GetValueOrDefault(type)?.Contains(entity.Index) == true;
}
