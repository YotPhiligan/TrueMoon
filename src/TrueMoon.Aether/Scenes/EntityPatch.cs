using System.Collections.Immutable;

namespace TrueMoon.Aether.Scenes;

/// <summary>Immutable local changes. A missing override inherits; null/default remains an explicit override.</summary>
public sealed class EntityPatch
{
    /// <summary>Gets an empty patch.</summary>
    public static EntityPatch Empty { get; } = new();
    /// <summary>Snapshots additions, removals, restorations and property overrides.</summary>
    /// <remarks>Remove hides an inherited component. Restore makes a hidden occurrence visible with its inherited values.
    /// Each component can have only one structural operation in a layer; property overrides apply afterwards.</remarks>
    /// <param name="addedComponents">New occurrence IDs and data.</param>
    /// <param name="removedComponents">Inherited occurrences to hide.</param>
    /// <param name="propertyOverrides">Distinct occurrence/property changes.</param>
    /// <param name="restoredComponents">Hidden occurrences to restore.</param>
    public EntityPatch(IEnumerable<ComponentData>? addedComponents = null, IEnumerable<ComponentId>? removedComponents = null,
        IEnumerable<ComponentPropertyOverride>? propertyOverrides = null, IEnumerable<ComponentId>? restoredComponents = null)
    {
        AddedComponents = (addedComponents ?? []).ToImmutableArray();
        RemovedComponents = AuthoringValidation.Components(removedComponents);
        RestoredComponents = AuthoringValidation.Components(restoredComponents);
        PropertyOverrides = (propertyOverrides ?? []).ToImmutableArray();
        var additions = new HashSet<ComponentId>();
        foreach (var item in AddedComponents)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (!additions.Add(item.Id)) throw new ArgumentException("Duplicate added component identity.", nameof(addedComponents));
        }
        if (RemovedComponents.Overlaps(RestoredComponents) || additions.Overlaps(RemovedComponents) || additions.Overlaps(RestoredComponents))
            throw new ArgumentException("A component has conflicting structural changes.");
        var addresses = new HashSet<(ComponentId, string)>();
        foreach (var item in PropertyOverrides)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (!addresses.Add((item.ComponentId, item.PropertyId))) throw new ArgumentException("Duplicate property override.", nameof(propertyOverrides));
        }
    }
    /// <summary>Gets locally introduced occurrences.</summary>
    public ImmutableArray<ComponentData> AddedComponents { get; }
    /// <summary>Gets explicit masks of inherited occurrences.</summary>
    public ImmutableHashSet<ComponentId> RemovedComponents { get; }
    /// <summary>Gets explicit reinstatements of masked occurrences.</summary>
    public ImmutableHashSet<ComponentId> RestoredComponents { get; }
    /// <summary>Gets local property overrides.</summary>
    public ImmutableArray<ComponentPropertyOverride> PropertyOverrides { get; }
    /// <summary>Returns a new patch inheriting the specified property again; other changes are retained.</summary>
    /// <param name="componentId">Target occurrence identity.</param>
    /// <param name="propertyId">Property whose local override is removed.</param>
    public EntityPatch WithoutPropertyOverride(ComponentId componentId, string propertyId)
    {
        AuthoringValidation.Id(componentId.Value, nameof(componentId)); AuthoringValidation.Key(propertyId, nameof(propertyId));
        return new(AddedComponents, RemovedComponents, PropertyOverrides.Where(p => p.ComponentId != componentId || p.PropertyId != propertyId), RestoredComponents);
    }
    /// <summary>Returns a new patch without the local removal mask; ancestor masks remain effective.</summary>
    /// <param name="componentId">Occurrence whose local mask is removed.</param>
    public EntityPatch WithoutComponentRemoval(ComponentId componentId)
    {
        AuthoringValidation.Id(componentId.Value, nameof(componentId));
        return new(AddedComponents, RemovedComponents.Remove(componentId), PropertyOverrides, RestoredComponents);
    }
}
