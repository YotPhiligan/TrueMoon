using System.Collections.Immutable;

namespace TrueMoon.Aether.Scenes;

/// <summary>Immutable component contract; schemas can be inspected without loading game code.</summary>
public sealed class ComponentSchema
{
    /// <summary>Creates an exact version, property defaults, multiplicity and required-type contract.</summary>
    /// <param name="typeId">Persistent nonempty type identity.</param>
    /// <param name="version">Positive exact schema version.</param>
    /// <param name="properties">Property contracts with distinct ordinal IDs.</param>
    /// <param name="allowMultiple">Whether multiple occurrences can coexist.</param>
    /// <param name="requiredTypes">Distinct types required on the final effective entity.</param>
    public ComponentSchema(ComponentTypeId typeId, int version, IEnumerable<ComponentPropertySchema> properties,
        bool allowMultiple = false, IEnumerable<ComponentTypeId>? requiredTypes = null)
    {
        AuthoringValidation.Id(typeId.Value, nameof(typeId));
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
        ArgumentNullException.ThrowIfNull(properties);
        var map = ImmutableDictionary.CreateBuilder<string, ComponentPropertySchema>(StringComparer.Ordinal);
        foreach (var property in properties)
        {
            ArgumentNullException.ThrowIfNull(property);
            if (map.ContainsKey(property.Id)) throw new ArgumentException("Duplicate property ID.", nameof(properties));
            map.Add(property.Id, property);
        }
        var required = ImmutableHashSet.CreateBuilder<ComponentTypeId>();
        foreach (var dependency in requiredTypes ?? [])
        {
            AuthoringValidation.Id(dependency.Value, nameof(requiredTypes));
            if (dependency == typeId || !required.Add(dependency)) throw new ArgumentException("Invalid or duplicate component dependency.", nameof(requiredTypes));
        }
        TypeId = typeId; Version = version; Properties = map.ToImmutable(); AllowMultiple = allowMultiple; RequiredTypes = required.ToImmutable();
    }
    /// <summary>Gets the persistent component type ID.</summary>
    public ComponentTypeId TypeId { get; }
    /// <summary>Gets the exact schema version. No implicit migration occurs.</summary>
    public int Version { get; }
    /// <summary>Gets the property's stable-ID lookup.</summary>
    public ImmutableDictionary<string, ComponentPropertySchema> Properties { get; }
    /// <summary>Gets whether multiple occurrences of this type are supported on one entity.</summary>
    public bool AllowMultiple { get; }
    /// <summary>Gets types that must be present on the effective entity.</summary>
    public ImmutableHashSet<ComponentTypeId> RequiredTypes { get; }
}
