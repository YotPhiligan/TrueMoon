using System.Collections.Immutable;

namespace TrueMoon.Aether.Scenes;

/// <summary>Origin in a definition revision or an instance; schema defaults additionally carry IsSchemaDefault.</summary>
public readonly record struct EntityValueSource
{
    internal EntityValueSource(EntityDefinitionId? definitionId, long? definitionRevision, EntityId? entityId)
    { DefinitionId = definitionId; DefinitionRevision = definitionRevision; EntityId = entityId; }
    /// <summary>Gets the originating template/variant, or null for instance data.</summary>
    public EntityDefinitionId? DefinitionId { get; }
    /// <summary>Gets the exact originating definition revision.</summary>
    public long? DefinitionRevision { get; }
    /// <summary>Gets the originating instance, or null for definition data.</summary>
    public EntityId? EntityId { get; }
}

/// <summary>Effective scalar value and the source that supplied it.</summary>
public sealed class ResolvedProperty
{
    internal ResolvedProperty(PropertyValue value, EntityValueSource source, bool isSchemaDefault)
    { Value = value; Source = source; IsSchemaDefault = isSchemaDefault; }
    /// <summary>Gets the resolved value.</summary>
    public PropertyValue Value { get; }
    /// <summary>Gets its authoring source.</summary>
    public EntityValueSource Source { get; }
    /// <summary>Gets whether the value came from the component schema default.</summary>
    public bool IsSchemaDefault { get; }
}

/// <summary>Immutable effective component with property and presence provenance.</summary>
public sealed class ResolvedComponent
{
    internal ResolvedComponent(ComponentId id, ComponentSchema schema, EntityValueSource source, EntityValueSource presenceSource,
        ImmutableDictionary<string, ResolvedProperty> properties)
    { Id = id; TypeId = schema.TypeId; SchemaVersion = schema.Version; Source = source; PresenceSource = presenceSource; Properties = properties; }
    /// <summary>Gets stable occurrence identity.</summary>
    public ComponentId Id { get; }
    /// <summary>Gets stable component type.</summary>
    public ComponentTypeId TypeId { get; }
    /// <summary>Gets the exact resolved schema version.</summary>
    public int SchemaVersion { get; }
    /// <summary>Gets the source introducing this occurrence.</summary>
    public EntityValueSource Source { get; }
    /// <summary>Gets the source that introduced or most recently restored the occurrence.</summary>
    public EntityValueSource PresenceSource { get; }
    /// <summary>Gets all schema properties, including defaults, using ordinal IDs.</summary>
    public ImmutableDictionary<string, ResolvedProperty> Properties { get; }
}

/// <summary>Immutable effective instance; no runtime objects or game assemblies are required.</summary>
public sealed class ResolvedEntity
{
    internal ResolvedEntity(EntityInstance instance, ImmutableArray<EntityDefinitionId> chain,
        ImmutableDictionary<ComponentId, ResolvedComponent> components)
    { Id = instance.Id; ParentId = instance.ParentId; DefinitionId = instance.DefinitionId; DefinitionChain = chain; Components = components; Name = instance.Name; SiblingOrder = instance.SiblingOrder; }
    /// <summary>Gets instance identity.</summary>
    public EntityId Id { get; }
    /// <summary>Gets scene parent independently of inheritance resolution.</summary>
    public EntityId? ParentId { get; }
    /// <summary>Instance display name; null retains an ID-based fallback.</summary>
    public string? Name { get; }
    /// <summary>Optional sibling rank; missing/tied ranks resolve by GUID.</summary>
    public int? SiblingOrder { get; }
    /// <summary>Gets the selected definition.</summary>
    public EntityDefinitionId DefinitionId { get; }
    /// <summary>Gets definitions in base-to-variant application order.</summary>
    public ImmutableArray<EntityDefinitionId> DefinitionChain { get; }
    /// <summary>Gets visible effective component occurrences.</summary>
    public ImmutableDictionary<ComponentId, ResolvedComponent> Components { get; }
}
