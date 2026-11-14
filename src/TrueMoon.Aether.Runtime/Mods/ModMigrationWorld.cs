using TrueMoon.Aether.Abstractions.Ecs;
using TrueMoon.Aether.Abstractions.Mods;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Loading;

namespace TrueMoon.Aether.Runtime.Mods;

// Group-owned World/scheduler and persistent identities. No registry/factory is exposed to callers.
internal sealed class ModMigrationWorld
{
    internal SceneWorld? Scene;
    internal EcsScheduler? Scheduler;
    private ModEntityMap? _importMap;
    private readonly Dictionary<EntityId, EntityHandle> _entities = [];
    private readonly Dictionary<(EntityId, ComponentTypeId), ComponentId> _components = [];
    private readonly Dictionary<EntityId, EntityId?> _parents = [];
    internal World World => Scene!.World;
    internal EntityHandle Entity(EntityId id) => _entities.TryGetValue(id, out var value) && World.IsAlive(value) ? value : throw new InvalidDataException("Unknown or dead persistent entity.");
    internal void TrackEntity(EntityId id, EntityHandle handle, EntityId? parent)
    {
        if (id.Value == Guid.Empty || !World.IsAlive(handle) || _entities.ContainsKey(id) || _entities.Values.Contains(handle)) throw new ArgumentException("Invalid/duplicate persistent entity identity.");
        _entities.Add(id, handle); _parents.Add(id, parent);
    }
    internal void TrackComponent(EntityId entity, ComponentTypeId type, ComponentId component)
    {
        _ = Entity(entity);
        if (type.Value == Guid.Empty || component.Value == Guid.Empty || _components.Values.Contains(component) || !_components.TryAdd((entity, type), component))
            throw new ArgumentException("Invalid/duplicate persistent component identity.");
    }
    internal void Load(ModWorldSnapshot snapshot, ModComponentCatalog catalog, IFactory<SceneWorldLoader, ComponentRegistry> loaders)
    {
        try { Scene = loaders.Create(catalog.Build(context => _importMap ??= new ModEntityMap(context.EntityMap))).Load(snapshot.Scene); }
        catch (SceneWorldLoadException error) { Scene = error.CleanupOwner; throw; }
        foreach (var pair in Scene.Entities)
        {
            _entities.Add(pair.Key, pair.Value); var entity = Scene.Scene.Entities[pair.Key]; _parents.Add(pair.Key, entity.ParentId);
            foreach (var component in entity.Components.Values) _components.Add((pair.Key, component.TypeId), component.Id);
        }
    }
    internal ModWorldSnapshot Capture(ModComponentCatalog catalog)
    {
        var live = World.MigrationEntities(); var entities = _entities.Where(p => World.IsAlive(p.Value)).ToDictionary(p => p.Key, p => p.Value);
        if (live.Length != entities.Count || live.Any(e => !entities.Values.Contains(e))) throw new InvalidDataException("Every live entity requires explicit persistent identity.");
        if (World.MigrationComponentTypes().Any(t => !catalog.Bindings.Values.Any(b => b.Type == t))) throw new InvalidDataException("World contains unexportable component storage.");
        var map = new ModEntityMap(entities); var instances = new List<EntityInstance>(); var definitions = new List<EntityDefinition>();
        var schemas = new Dictionary<ComponentTypeId, ComponentSchema>();
        using (World.BeginReloadPreparation())
        {
            foreach (var pair in entities.OrderBy(p => p.Key.Value))
            {
                var values = new List<ComponentData>();
                foreach (var binding in catalog.Bindings.Values)
                {
                    if (!World.MigrationHasComponent(pair.Value, binding.Type)) continue;
                    if (!_components.TryGetValue((pair.Key, binding.Schema.TypeId), out var id)) throw new InvalidDataException("Live component requires explicit persistent occurrence identity.");
                    var data = binding.Capture(World, pair.Value, id, map)!; values.Add(data); schemas[binding.Schema.TypeId] = binding.Schema;
                }
                var definition = new EntityDefinition(new(pair.Key.Value), new EntityPatch(addedComponents: values)); definitions.Add(definition);
                var original = Scene!.Scene.Entities.GetValueOrDefault(pair.Key);
                instances.Add(new(pair.Key, definition.Id, null, _parents.GetValueOrDefault(pair.Key), original?.Name, original?.SiblingOrder));
            }
        }
        return new(new(Scene!.Scene.Id, Scene.Scene.Revision, instances, definitions, schemas.Values), Scheduler!.CaptureReloadState(), Scheduler.TickNumber);
    }
    internal void Dispose()
    {
        Scheduler?.Dispose(); Scheduler = null;
        if (Scene != null) { Scene.World.SanitizeModuleFailure(); Scene.Dispose(); Scene = null; }
        _entities.Clear(); _components.Clear(); _parents.Clear(); _importMap = null;
    }
}
