using System.Collections.Immutable;

namespace TrueMoon.Aether.Scenes.Authoring;

/// <summary>One scalar instance override, or removal of the instance value to inherit again.</summary>
/// <param name="EntityId">Instance identity.</param><param name="ComponentId">Occurrence identity.</param>
/// <param name="PropertyId">Exact schema property key.</param><param name="Value">Desired value, ignored for reset.</param>
/// <param name="Reset">Remove the instance override and any explicit value in a locally added occurrence.</param>
public readonly record struct PropertyEdit(EntityId EntityId, ComponentId ComponentId, string PropertyId,
    PropertyValue Value = default, bool Reset = false);

/// <summary>Instance component presence operations; inherited definitions are never mutated.</summary>
public enum ComponentChangeKind
{
    /// <summary>Introduce a fresh occurrence with an exact schema.</summary>
    Add,
    /// <summary>Delete a local occurrence or mask an inherited one; local property edits are cleared.</summary>
    Remove,
    /// <summary>Make a hidden occurrence visible, clearing a local mask or restoring an ancestor mask.</summary>
    Restore,
    /// <summary>Clear only this instance's removal mask; ancestor masks still apply.</summary>
    ResetRemoval
}

/// <summary>One component presence change, addressed independently from its display name.</summary>
/// <param name="EntityId">Instance identity.</param><param name="ComponentId">Occurrence identity.</param>
/// <param name="Kind">Presence operation.</param><param name="Addition">Exact immutable data, required only for Add.</param>
public readonly record struct ComponentEdit(EntityId EntityId, ComponentId ComponentId, ComponentChangeKind Kind,
    ComponentData? Addition = null);

/// <summary>Frozen atomic scalar/presence edits sharing the scene's existing receipt journal and history.</summary>
public sealed class ComponentEditBatch
{
    /// <summary>Snapshots one to 4096 combined edits. Structural edits are applied before properties.</summary>
    /// <param name="operation">Shared identity, expected revision and actor.</param>
    /// <param name="properties">Scalar sets/resets; may be empty when changing component presence.</param>
    /// <param name="components">Presence changes; omitted for property-only edits.</param>
    public ComponentEditBatch(AuthoringOperation operation, IEnumerable<PropertyEdit> properties,
        IEnumerable<ComponentEdit>? components = null)
    {
        ArgumentNullException.ThrowIfNull(operation); ArgumentNullException.ThrowIfNull(properties);
        Operation = operation;
        var values = ImmutableArray.CreateBuilder<PropertyEdit>();
        foreach (var edit in properties)
        {
            if (values.Count == 4096) throw new ArgumentOutOfRangeException(nameof(properties));
            values.Add(edit);
        }
        var presence = ImmutableArray.CreateBuilder<ComponentEdit>();
        foreach (var edit in components ?? [])
        {
            if (values.Count + presence.Count == 4096) throw new ArgumentOutOfRangeException(nameof(components));
            presence.Add(edit);
        }
        if (values.Count + presence.Count == 0) throw new ArgumentException("A batch must contain an edit.", nameof(properties));
        Properties = values.ToImmutable(); Components = presence.ToImmutable();
    }
    /// <summary>Gets immutable operation metadata.</summary>
    public AuthoringOperation Operation { get; }
    /// <summary>Gets the frozen scalar changes.</summary>
    public ImmutableArray<PropertyEdit> Properties { get; }
    /// <summary>Gets the frozen presence changes.</summary>
    public ImmutableArray<ComponentEdit> Components { get; }

    internal bool SameChanges(ComponentEditBatch other) =>
        Properties.SequenceEqual(other.Properties) && Components.Length == other.Components.Length &&
        Components.Zip(other.Components).All(pair =>
            pair.First.EntityId == pair.Second.EntityId && pair.First.ComponentId == pair.Second.ComponentId &&
            pair.First.Kind == pair.Second.Kind && SameData(pair.First.Addition, pair.Second.Addition));
    private static bool SameData(ComponentData? first, ComponentData? second) =>
        ReferenceEquals(first, second) || first != null && second != null && first.Id == second.Id &&
        first.TypeId == second.TypeId && first.SchemaVersion == second.SchemaVersion &&
        first.Properties.Count == second.Properties.Count &&
        first.Properties.All(p => second.Properties.TryGetValue(p.Key, out var value) && value == p.Value);
}
