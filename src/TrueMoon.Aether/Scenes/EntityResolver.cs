using System.Collections.Immutable;

namespace TrueMoon.Aether.Scenes;

/// <summary>Pure, reusable resolver over immutable authoring definitions and exact component schemas.</summary>
public sealed class EntityResolver
{
    /// <summary>Maximum number of definitions, including the root, in one inheritance chain.</summary>
    public const int MaximumInheritanceDepth = 64;
    private readonly ImmutableDictionary<EntityDefinitionId, EntityDefinition> _definitions;
    private readonly ImmutableDictionary<(ComponentTypeId, int), ComponentSchema> _schemas;

    /// <summary>Snapshots the catalogs; duplicate identities/versions are rejected without loading game code.</summary>
    /// <param name="definitions">Immutable templates and variants.</param>
    /// <param name="schemas">Exact component contracts keyed by type and version.</param>
    public EntityResolver(IEnumerable<EntityDefinition> definitions, IEnumerable<ComponentSchema> schemas)
    {
        ArgumentNullException.ThrowIfNull(definitions); ArgumentNullException.ThrowIfNull(schemas);
        var definitionMap = ImmutableDictionary.CreateBuilder<EntityDefinitionId, EntityDefinition>();
        foreach (var definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);
            if (definitionMap.ContainsKey(definition.Id)) throw Error(EntityResolutionError.DuplicateDefinition,
                DefinitionPath(definition.Id), "Duplicate definition identity.");
            definitionMap.Add(definition.Id, definition);
        }
        var schemaMap = ImmutableDictionary.CreateBuilder<(ComponentTypeId, int), ComponentSchema>();
        foreach (var schema in schemas)
        {
            ArgumentNullException.ThrowIfNull(schema);
            var key = (schema.TypeId, schema.Version);
            if (schemaMap.ContainsKey(key)) throw Error(EntityResolutionError.DuplicateSchema,
                $"schemas/{schema.TypeId.Value:D}/{schema.Version}", "Duplicate schema identity/version.");
            schemaMap.Add(key, schema);
        }
        _definitions = definitionMap.ToImmutable(); _schemas = schemaMap.ToImmutable();
    }

    /// <summary>Resolves an instance without modifying inputs. Final multiplicity and dependencies must hold.</summary>
    /// <param name="instance">Instance with a definition reference and local changes.</param>
    /// <returns>Effective values and their provenance, detached from every other resolution.</returns>
    /// <exception cref="EntityResolutionException">A graph, schema or effective component invariant is invalid.</exception>
    public ResolvedEntity Resolve(EntityInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var chain = FindChain(instance);
        var state = new Dictionary<ComponentId, WorkingComponent>();
        foreach (var definition in chain)
            Apply(definition.Changes, new(definition.Id, definition.Revision, null), DefinitionPath(definition.Id), state);
        var instancePath = $"entities/{instance.Id.Value:D}";
        Apply(instance.Changes, new(null, null, instance.Id), instancePath, state);
        var visible = state.Values.Where(c => c.Visible).OrderBy(c => c.Id.Value).ToArray();
        var typeCounts = visible.GroupBy(c => c.Schema.TypeId).ToDictionary(g => g.Key, g => g.Count());
        foreach (var component in visible)
        {
            var path = ComponentPath(instancePath, component.Id);
            if (!component.Schema.AllowMultiple && typeCounts[component.Schema.TypeId] > 1)
                throw Error(EntityResolutionError.ComponentMultiplicity, path, "Component type does not allow multiple occurrences.");
            foreach (var dependency in component.Schema.RequiredTypes.OrderBy(t => t.Value))
                if (!typeCounts.ContainsKey(dependency))
                    throw Error(EntityResolutionError.MissingComponentDependency, path, $"Missing required type {dependency.Value:D}.");
        }
        return new(instance, chain.Select(d => d.Id).ToImmutableArray(), visible.ToImmutableDictionary(c => c.Id,
            c => new ResolvedComponent(c.Id, c.Schema, c.Source, c.PresenceSource, c.Properties.ToImmutableDictionary(StringComparer.Ordinal))));
    }

    private List<EntityDefinition> FindChain(EntityInstance instance)
    {
        var chain = new List<EntityDefinition>(); var visited = new HashSet<EntityDefinitionId>();
        var current = instance.DefinitionId; var path = $"entities/{instance.Id.Value:D}/definition";
        while (true)
        {
            if (!visited.Add(current)) throw Error(EntityResolutionError.InheritanceCycle, path, $"Definition cycle at {current.Value:D}.");
            if (chain.Count == MaximumInheritanceDepth) throw Error(EntityResolutionError.InheritanceDepthExceeded, path, "Definition depth limit exceeded.");
            if (!_definitions.TryGetValue(current, out var definition)) throw Error(EntityResolutionError.MissingDefinition, path, $"Missing definition {current.Value:D}.");
            chain.Add(definition);
            if (definition.BaseDefinitionId is not { } parent) break;
            path = $"{DefinitionPath(definition.Id)}/base/{parent.Value:D}"; current = parent;
        }
        chain.Reverse(); return chain;
    }

    private void Apply(EntityPatch patch, EntityValueSource source, string path, Dictionary<ComponentId, WorkingComponent> state)
    {
        foreach (var data in patch.AddedComponents.OrderBy(c => c.Id.Value))
        {
            var componentPath = ComponentPath(path, data.Id);
            if (state.ContainsKey(data.Id)) throw Error(EntityResolutionError.ComponentAlreadyExists, componentPath, "Occurrence identity is already used, even if hidden.");
            if (!_schemas.TryGetValue((data.TypeId, data.SchemaVersion), out var schema))
                throw Error(_schemas.Keys.Any(k => k.Item1 == data.TypeId) ? EntityResolutionError.SchemaVersionMismatch : EntityResolutionError.UnknownSchema,
                    componentPath, $"No exact schema {data.TypeId.Value:D}/{data.SchemaVersion}.");
            var values = schema.Properties.Values.ToDictionary(p => p.Id, p => new ResolvedProperty(p.DefaultValue, source, true), StringComparer.Ordinal);
            foreach (var property in data.Properties.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                ValidateProperty(schema, property.Key, property.Value, componentPath);
                values[property.Key] = new(property.Value, source, false);
            }
            state.Add(data.Id, new(data.Id, schema, source, values));
        }
        foreach (var id in patch.RemovedComponents.OrderBy(id => id.Value))
        {
            var component = FindComponent(state, id, path);
            if (!component.Visible) throw Error(EntityResolutionError.ComponentAlreadyRemoved, ComponentPath(path, id), "Occurrence is already removed.");
            component.Visible = false;
        }
        foreach (var id in patch.RestoredComponents.OrderBy(id => id.Value))
        {
            var component = FindComponent(state, id, path);
            if (component.Visible) throw Error(EntityResolutionError.ComponentNotRemoved, ComponentPath(path, id), "Only a removed occurrence can be restored.");
            component.Visible = true; component.PresenceSource = source;
        }
        foreach (var property in patch.PropertyOverrides.OrderBy(p => p.ComponentId.Value).ThenBy(p => p.PropertyId, StringComparer.Ordinal))
        {
            var component = FindComponent(state, property.ComponentId, path);
            var componentPath = ComponentPath(path, property.ComponentId);
            if (!component.Visible) throw Error(EntityResolutionError.ComponentAlreadyRemoved, componentPath, "Restore an occurrence before overriding its properties.");
            ValidateProperty(component.Schema, property.PropertyId, property.Value, componentPath);
            component.Properties[property.PropertyId] = new(property.Value, source, false);
        }
    }

    private static WorkingComponent FindComponent(Dictionary<ComponentId, WorkingComponent> state, ComponentId id, string path)
        => state.TryGetValue(id, out var component) ? component : throw Error(EntityResolutionError.ComponentNotFound, ComponentPath(path, id), "Unknown occurrence identity.");

    private static void ValidateProperty(ComponentSchema schema, string id, PropertyValue value, string path)
    {
        var propertyPath = $"{path}/properties/{id}";
        if (!schema.Properties.TryGetValue(id, out var property)) throw Error(EntityResolutionError.UnknownProperty, propertyPath, "Property is not declared in the exact schema.");
        if (!property.Accepts(value)) throw Error(EntityResolutionError.PropertyTypeMismatch, propertyPath, "Value kind/nullability does not satisfy the schema.");
    }

    private static string DefinitionPath(EntityDefinitionId id) => $"definitions/{id.Value:D}";
    private static string ComponentPath(string path, ComponentId id) => $"{path}/components/{id.Value:D}";
    private static EntityResolutionException Error(EntityResolutionError code, string path, string message) => new(code, path, message);

    private sealed class WorkingComponent(ComponentId id, ComponentSchema schema, EntityValueSource source, Dictionary<string, ResolvedProperty> properties)
    {
        internal ComponentId Id { get; } = id;
        internal ComponentSchema Schema { get; } = schema;
        internal EntityValueSource Source { get; } = source;
        internal EntityValueSource PresenceSource { get; set; } = source;
        internal Dictionary<string, ResolvedProperty> Properties { get; } = properties;
        internal bool Visible { get; set; } = true;
    }
}
