using TrueMoon.Aether.Abstractions.Mods;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Loading;
using TrueMoon.Aether.Runtime.Scenes;

namespace TrueMoon.Aether.Runtime.Mods;

public sealed partial class ModGroup
{
    private readonly List<ModMigrationWorld> _migrationWorlds = [];
    private readonly List<IModComponentMigrator?> _migrationConverters = [];
    private void ValidateMigration()
    {
        if (_registration!.Migration.Bindings.Count != _registration.ComponentCount || _modules.Any(m => m.Value is not IModReloadParticipant))
            throw new ModFailure(ModLoadError.MigrationNotSupported, "Migration requires reversible entries and bidirectional component bindings for every component.");
        _ = _registration.Migration.Build(_ => throw new InvalidOperationException("Import mapping is only available during preparation."));
    }
    internal ModWorldSnapshot MigrateSnapshot(ModWorldSnapshot source, SceneResolutionService resolution, ModWorldMigrationOptions options)
    {
        var resolved = resolution.Resolve(source.Scene); var oldSchemas = resolved.ComponentSchemas.ToDictionary(s => (s.TypeId, s.Version));
        var instances = new List<EntityInstance>(); var definitions = new List<EntityDefinition>(); var schemas = new Dictionary<ComponentTypeId, ComponentSchema>();
        var converters = new Dictionary<(ComponentTypeId, int, int), IModComponentMigrator>();
        foreach (var entity in resolved.Entities.Values.OrderBy(e => e.Id.Value))
        {
            var components = new List<ComponentData>();
            foreach (var component in entity.Components.Values)
            {
                if (!_registration!.Migration.Bindings.TryGetValue(component.TypeId, out var binding)) throw new ModFailure(ModLoadError.MigrationNotSupported, "Unknown component type; implicit component drop is forbidden.");
                var from = oldSchemas[(component.TypeId, component.SchemaVersion)]; var to = binding.Schema;
                var data = ModComponentCatalog.Binding.Values(component);
                if (from.Version == to.Version)
                {
                    if (!ModComponentCatalog.SameSchema(from, to)) throw new ModFailure(ModLoadError.MigrationFailed, "Schema changed without a new version.");
                }
                else
                {
                    var key = (from.TypeId, from.Version, to.Version);
                    if (!_registration.Migration.Rules.TryGetValue(key, out var rule)) throw new ModFailure(ModLoadError.MigrationNotSupported, "An exact explicit component migrator is required.");
                    var removed = from.Properties.Keys.Except(to.Properties.Keys, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
                    if (!removed.SetEquals(rule.Removed) || removed.Count != 0 && !options.AllowFieldRemoval) throw new ModFailure(ModLoadError.MigrationNotSupported, "Removed fields require exact declaration and explicit host admission.");
                    if (!converters.TryGetValue(key, out var converter))
                    {
                        converter = rule.Factory.Create() ?? throw new InvalidOperationException("Migration factory returned null.");
                        if (_migrationConverters.Any(c => ReferenceEquals(c, converter)) || _modules.Any(m => ReferenceEquals(m.Value, converter)) || _services.Any(s => ReferenceEquals(s.Value, converter)))
                            throw new InvalidOperationException("Migrator result aliases an owned resource.");
                        _migrationConverters.Add(converter); converters.Add(key, converter);
                    }
                    data = converter.Migrate(new(from, to, data)) ?? throw new InvalidDataException("Migration returned null.");
                }
                if (data.Id != component.Id || data.TypeId != component.TypeId || data.SchemaVersion != to.Version) throw new InvalidDataException("Migration changed persistent identity or returned an invalid schema version.");
                schemas[to.TypeId] = to; components.Add(data);
            }
            var definition = new EntityDefinition(new(entity.Id.Value), new EntityPatch(addedComponents: components)); definitions.Add(definition);
            instances.Add(new(entity.Id, definition.Id, null, entity.ParentId, entity.Name, entity.SiblingOrder));
        }
        return new(new(resolved.Id, resolved.Revision, instances, definitions, schemas.Values), source.Systems, source.TickNumber);
    }
    internal ModMigrationWorld PrepareMigrationWorld(ModWorldSnapshot snapshot, IFactory<SceneWorldLoader, ComponentRegistry> loaders, bool restore, EcsScheduler? previous)
    {
        var owner = new ModMigrationWorld(); _migrationWorlds.Add(owner); owner.Load(snapshot, _registration!.Migration, loaders);
        var scheduler = EcsScheduler.PrepareDetached(owner.World, _registration.Systems, value => owner.Scheduler = value, previous);
        scheduler.RejectAliases(previous);
        if (restore || snapshot.Systems.Count != 0) scheduler.RestoreReloadState(snapshot.Systems);
        scheduler.SetMigrationTick(snapshot.TickNumber);
        return owner;
    }
    internal ModWorldSnapshot CaptureMigrationWorld(ModMigrationWorld owner) => owner.Capture(_registration!.Migration);
    internal void ReleaseMigrationConverters()
    {
        for (var i = _migrationConverters.Count - 1; i >= 0; i--)
        { _migrationConverters[i]?.Dispose(); _migrationConverters[i] = null; }
        _migrationConverters.Clear();
    }
}
