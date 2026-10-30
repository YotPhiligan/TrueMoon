using System.Collections.Immutable;

namespace TrueMoon.Aether.Scenes.Authoring;

/// <summary>Instance-name change; null restores the ID-based fallback. Names need not be unique.</summary>
/// <param name="EntityId">Persistent occurrence.</param><param name="Name">Candidate name, validated atomically by the session.</param>
public readonly record struct EntityNameEdit(EntityId EntityId, string? Name);
/// <summary>Position within the destination parent.</summary>
public enum EntityPlacement { Last, Before, After }
/// <summary>Ordered placement; relative anchors must be siblings at the time this edit is prepared.</summary>
/// <param name="EntityId">Moved occurrence.</param><param name="ParentId">Destination parent, null for roots.</param>
/// <param name="Placement">Append or position relative to AnchorId.</param><param name="AnchorId">Sibling anchor for Before/After; null for Last.</param>
public readonly record struct EntityPlacementEdit(EntityId EntityId, EntityId? ParentId, EntityPlacement Placement = EntityPlacement.Last, EntityId? AnchorId = null);
/// <summary>Atomic names/placements in shared revision/history. Placement edits are prepared in caller order.</summary>
public sealed class HierarchyEditBatch
{
    /// <summary>Freezes one to 4096 edits. One entity may have one name and one placement in the same transaction.</summary>
    /// <param name="operation">Operation identity and expected revision.</param><param name="names">Optional names.</param><param name="placements">Optional placements.</param>
    public HierarchyEditBatch(AuthoringOperation operation, IEnumerable<EntityNameEdit>? names = null, IEnumerable<EntityPlacementEdit>? placements = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        Operation = operation; Names = Freeze(names ?? []); Placements = Freeze(placements ?? []);
        if (Names.Length + Placements.Length is 0 or > 4096) throw new ArgumentOutOfRangeException(nameof(names), "A hierarchy transaction requires 1..4096 edits.");
    }
    /// <summary>Shared operation identity.</summary>
    public AuthoringOperation Operation { get; }
    /// <summary>Frozen name edits.</summary>
    public ImmutableArray<EntityNameEdit> Names { get; }
    /// <summary>Frozen ordered placements.</summary>
    public ImmutableArray<EntityPlacementEdit> Placements { get; }
    internal bool SameChanges(HierarchyEditBatch other) => Names.SequenceEqual(other.Names) && Placements.SequenceEqual(other.Placements);
    private static ImmutableArray<T> Freeze<T>(IEnumerable<T> source)
    {
        var values = ImmutableArray.CreateBuilder<T>();
        foreach (var value in source) { if (values.Count == 4096) throw new ArgumentOutOfRangeException(nameof(source)); values.Add(value); }
        return values.ToImmutable();
    }
}
