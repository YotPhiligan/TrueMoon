using System.Collections.Immutable;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Ecs.Loading;

/// <summary>Stateless factory for complete caller-owned SceneWorld results. No partially populated World is returned.</summary>
public sealed class SceneWorldLoader : IFactory<SceneWorld, SceneWorldLoadRequest>
{
    private readonly ComponentRegistry _registry;
    private readonly IFactory<World> _worlds;
    private readonly SceneResolutionService _resolution;
    private readonly SceneWorldLoadOptions _options;
    /// <summary>Gets the exact frozen registry generation used by this factory.</summary>
    public Guid RegistryId => _registry.Id;
    /// <summary>Creates a loader with borrowed dependencies and copied limits. No World/importer is activated.</summary>
    /// <param name="registry">Frozen exact bindings.</param><param name="worlds">Factory for fresh empty unowned Worlds.</param>
    /// <param name="resolution">Borrowed neutral authoring resolver.</param><param name="options">Optional copied limits.</param>
    public SceneWorldLoader(ComponentRegistry registry, IFactory<World> worlds, SceneResolutionService resolution, SceneWorldLoadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(registry); ArgumentNullException.ThrowIfNull(worlds); ArgumentNullException.ThrowIfNull(resolution);
        _registry = registry; _worlds = worlds; _resolution = resolution; _options = (options ?? new()).Snapshot();
    }
    /// <summary>Loads complete versioned data through the same path as Load.</summary><param name="arguments">Payload and runtime cancellation.</param><returns>New caller-owned scene.</returns>
    public SceneWorld Create(SceneWorldLoadRequest arguments)
    { ArgumentNullException.ThrowIfNull(arguments); return Load(arguments.Data, arguments.CancellationToken); }
    /// <summary>Loads an immutable source document as current version data.</summary><param name="scene">Complete source.</param><param name="cancellationToken">Preparation cancellation.</param><returns>New caller-owned scene.</returns>
    public SceneWorld Load(SceneDocument scene, CancellationToken cancellationToken = default) => Load(new SceneWorldData(scene), cancellationToken);
    /// <summary>Validates all source data, prepares private values/resources, then publishes complete storage.</summary>
    /// <param name="data">Complete neutral versioned data.</param><param name="cancellationToken">Cooperative cancellation.</param><returns>New caller-owned scene.</returns>
    public SceneWorld Load(SceneWorldData data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data); cancellationToken.ThrowIfCancellationRequested();
        if (data.FormatVersion != SceneWorldData.CurrentFormatVersion)
            throw Failure(SceneWorldLoadError.UnsupportedFormat, "Unsupported World scene format.", data.Scene.Id);
        ValidateSource(data.Scene, cancellationToken);
        try { SceneWorldCodec.ValidateStrings(data.Scene, cancellationToken); }
        catch (InvalidDataException error) { throw Failure(SceneWorldLoadError.InvalidValue, "Scene strings are not representable in the load format.", data.Scene.Id, inner: error); }
        ResolvedSceneSnapshot scene;
        try { scene = _resolution.Resolve(data.Scene, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception error) { throw Failure(SceneWorldLoadError.InvalidScene, "Authoring scene resolution failed.", data.Scene.Id, inner: error); }
        var plans = ValidateResolved(scene, cancellationToken);
        var owner = new SceneWorld(scene, _options.MaximumPreparedResources);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            World world;
            try
            {
                world = _worlds.Create() ?? throw new InvalidOperationException("World factory returned null.");
                owner.Attach(world);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception error) { throw Failure(SceneWorldLoadError.InvalidWorldFactory, "World factory must return fresh empty healthy unowned storage.", scene.Id, inner: error); }
            if (!world.SceneFits(owner, scene.Entities.Count, plans.Select(p => p.Binding.RuntimeType).Distinct().Count()))
                throw Failure(SceneWorldLoadError.LimitExceeded, "The scene exceeds its World factory's slot/type limits.", scene.Id);
            var handles = ImmutableDictionary.CreateBuilder<EntityId, EntityHandle>();
            foreach (var id in scene.HierarchyOrder)
            { cancellationToken.ThrowIfCancellationRequested(); handles.Add(id, world.CreateSceneEntity(owner)); }
            owner.SetEntities(handles.ToImmutable());
            var prepared = new List<IPreparedComponent>(plans.Count);
            foreach (var plan in plans)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var context = new ComponentLoadContext(owner, plan.Entity, plan.Component, plan.Binding, cancellationToken);
                try { prepared.Add(plan.Binding.Prepare(context)); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (SceneWorldLoadException) { throw; }
                catch (Exception error) { throw Failure(SceneWorldLoadError.ImportFailed, "Component importer failed.", scene.Id, plan.Entity.Id, plan.Component.Id, inner: error); }
                finally { context.Deactivate(); }
            }
            foreach (var value in prepared) { cancellationToken.ThrowIfCancellationRequested(); value.Apply(world, owner); }
            cancellationToken.ThrowIfCancellationRequested(); owner.Publish(); return owner;
        }
        catch (Exception error)
        {
            try { owner.Dispose(); }
            catch (Exception cleanup)
            {
                throw new SceneWorldLoadException(SceneWorldLoadError.CleanupFailed, "Scene preparation and cleanup failed; retry CleanupOwner.Dispose.",
                    scene.Id, inner: new AggregateException(error, cleanup), cleanup: owner);
            }
            throw;
        }
    }

    private List<Plan> ValidateResolved(ResolvedSceneSnapshot scene, CancellationToken token)
    {
        var schemas = scene.ComponentSchemas.ToDictionary(s => (s.TypeId, s.Version));
        var bindings = new Dictionary<(ComponentTypeId, int), IComponentBinding>();
        var plans = new List<Plan>(); long properties = 0;
        var assets = new Dictionary<Guid, (EntityId Entity, ComponentId Component, string Property)>();
        foreach (var id in scene.HierarchyOrder)
        {
            token.ThrowIfCancellationRequested(); var entity = scene.Entities[id];
            var seenTypes = new HashSet<Type>();
            foreach (var component in entity.Components.Values.OrderBy(c => c.Id.Value))
            {
                token.ThrowIfCancellationRequested();
                var key = (component.TypeId, component.SchemaVersion);
                if (!bindings.TryGetValue(key, out var binding))
                {
                    try { binding = _registry.Binding(schemas[key], scene.Id); }
                    catch (SceneWorldLoadException error) { throw Failure(error.Error, error.Message, scene.Id, entity.Id, component.Id, inner: error); }
                    bindings.Add(key, binding);
                }
                if (!seenTypes.Add(binding.RuntimeType)) throw Failure(SceneWorldLoadError.UnsupportedMultiplicity, "Runtime singleton type occurs twice on one entity.", scene.Id, entity.Id, component.Id);
                properties += component.Properties.Count;
                if (plans.Count == _options.MaximumComponentInstances || properties > _options.MaximumPropertyValues)
                    throw Failure(SceneWorldLoadError.LimitExceeded, "Resolved component/property budget exceeded.", scene.Id, entity.Id, component.Id);
                foreach (var reference in binding.References)
                {
                    var value = component.Properties[reference.Key].Value;
                    if (value.Kind == PropertyValueKind.Null) continue;
                    if (value.Kind != PropertyValueKind.String || value.GetString().Length != 36 || !Guid.TryParseExact(value.GetString(), "D", out var target) || target == Guid.Empty)
                        throw Failure(SceneWorldLoadError.InvalidReference, "A reference requires a nonempty GUID in D format.", scene.Id, entity.Id, component.Id, reference.Key);
                    if (reference.Value == ComponentReferenceKind.Entity)
                    {
                        if (!scene.Entities.ContainsKey(new(target))) throw Failure(SceneWorldLoadError.InvalidReference, "Dangling scene entity reference.", scene.Id, entity.Id, component.Id, reference.Key);
                    }
                    else assets.TryAdd(target, (entity.Id, component.Id, reference.Key));
                }
                plans.Add(new(entity, component, binding));
            }
        }
        foreach (var asset in assets.OrderBy(a => a.Key))
        {
            token.ThrowIfCancellationRequested();
            var location = asset.Value;
            if (_registry.AssetValidator == null) throw Failure(SceneWorldLoadError.InvalidReference, "Asset references require an explicit asset validator.", scene.Id, location.Entity, location.Component, location.Property);
            try { _registry.AssetValidator(asset.Key, token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception error) { throw Failure(SceneWorldLoadError.InvalidReference, "Asset reference validation failed.", scene.Id, location.Entity, location.Component, location.Property, error); }
        }
        foreach (var plan in plans)
        {
            token.ThrowIfCancellationRequested();
            var context = new ComponentValidationContext(scene, plan.Entity, plan.Component, plan.Binding, token);
            try { plan.Binding.Validate(context); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (SceneWorldLoadException) { throw; }
            catch (Exception error) { throw Failure(SceneWorldLoadError.InvalidValue, "Component validation failed.", scene.Id, plan.Entity.Id, plan.Component.Id, inner: error); }
            finally { context.Deactivate(); }
        }
        token.ThrowIfCancellationRequested(); return plans;
    }

    private void ValidateSource(SceneDocument scene, CancellationToken token)
    {
        if (scene.Entities.Length > _options.MaximumEntities || scene.Definitions.Length > SceneFileCodec.MaximumDefinitions || scene.Schemas.Length > SceneFileCodec.MaximumSchemas)
            throw Failure(SceneWorldLoadError.LimitExceeded, "Source scene catalog budget exceeded.", scene.Id);
        long values = 0; long components = 0;
        foreach (var schema in scene.Schemas)
        {
            token.ThrowIfCancellationRequested();
            if (schema.Properties.Count > 256 || schema.RequiredTypes.Count > 256) throw Failure(SceneWorldLoadError.LimitExceeded, "Source schema budget exceeded.", scene.Id);
            values += schema.Properties.Count;
        }
        foreach (var patch in scene.Definitions.Select(d => d.Changes).Concat(scene.Entities.Select(e => e.Changes)))
        {
            token.ThrowIfCancellationRequested();
            if (patch.AddedComponents.Length > 256 || patch.RemovedComponents.Count > 256 || patch.RestoredComponents.Count > 256 || patch.PropertyOverrides.Length > 1024)
                throw Failure(SceneWorldLoadError.LimitExceeded, "Source patch budget exceeded.", scene.Id);
            components += patch.AddedComponents.Length; values += patch.PropertyOverrides.Length;
            foreach (var component in patch.AddedComponents)
            {
                if (component.Properties.Count > 256) throw Failure(SceneWorldLoadError.LimitExceeded, "Source component property budget exceeded.", scene.Id);
                values += component.Properties.Count;
            }
            if (components > 65536 || values > 262144) throw Failure(SceneWorldLoadError.LimitExceeded, "Source value budget exceeded.", scene.Id);
        }
        if (values > 262144) throw Failure(SceneWorldLoadError.LimitExceeded, "Source value budget exceeded.", scene.Id);
    }
    private static SceneWorldLoadException Failure(SceneWorldLoadError error, string message, SceneId scene,
        EntityId? entity = null, ComponentId? component = null, string? property = null, Exception? inner = null)
        => new(error, message, scene, entity, component, property, inner);
    private sealed record Plan(ResolvedEntity Entity, ResolvedComponent Component, IComponentBinding Binding);
}
