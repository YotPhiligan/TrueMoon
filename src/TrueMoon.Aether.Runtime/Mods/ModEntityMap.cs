using TrueMoon.Aether.Abstractions.Ecs;
using TrueMoon.Aether.Abstractions.Mods;
using TrueMoon.Aether.Abstractions.Scenes;

namespace TrueMoon.Aether.Runtime.Mods;

internal sealed class ModEntityMap : IModEntityMap
{
    private readonly IReadOnlyDictionary<EntityId, EntityHandle> _entities;
    private readonly Dictionary<EntityHandle, EntityId> _reverse;
    internal ModEntityMap(IReadOnlyDictionary<EntityId, EntityHandle> entities)
    { _entities = entities; _reverse = entities.ToDictionary(p => p.Value, p => p.Key); }
    public EntityHandle Resolve(EntityId id) => _entities.TryGetValue(id, out var handle) ? handle : throw new InvalidDataException("Unknown persistent entity reference.");
    public EntityId Persistent(EntityHandle handle) => _reverse.TryGetValue(handle, out var id) ? id : throw new InvalidDataException("Foreign, stale or dangling entity reference.");
}
