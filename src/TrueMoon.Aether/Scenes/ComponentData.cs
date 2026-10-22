using System.Collections.Immutable;

namespace TrueMoon.Aether.Scenes;

/// <summary>Immutable component occurrence with explicit values; omitted properties use schema defaults.</summary>
public sealed class ComponentData
{
    /// <summary>Creates component data and snapshots the supplied values.</summary>
    /// <param name="id">Persistent occurrence identity.</param>
    /// <param name="typeId">Persistent component type.</param>
    /// <param name="schemaVersion">Positive exact schema version.</param>
    /// <param name="properties">Explicit values; null means all properties inherit schema defaults.</param>
    public ComponentData(ComponentId id, ComponentTypeId typeId, int schemaVersion,
        IEnumerable<KeyValuePair<string, PropertyValue>>? properties = null)
    {
        AuthoringValidation.Id(id.Value, nameof(id)); AuthoringValidation.Id(typeId.Value, nameof(typeId));
        if (schemaVersion < 1) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        Id = id; TypeId = typeId; SchemaVersion = schemaVersion; Properties = AuthoringValidation.Values(properties);
    }
    /// <summary>Gets occurrence identity, independent of ordering and type.</summary>
    public ComponentId Id { get; }
    /// <summary>Gets the schema type.</summary>
    public ComponentTypeId TypeId { get; }
    /// <summary>Gets the exact required schema version.</summary>
    public int SchemaVersion { get; }
    /// <summary>Gets explicit property values, including explicit null/default.</summary>
    public ImmutableDictionary<string, PropertyValue> Properties { get; }
}
