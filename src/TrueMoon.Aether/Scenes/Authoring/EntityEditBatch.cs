using System.Collections.Immutable;

namespace TrueMoon.Aether.Scenes.Authoring;

/// <summary>Closed family of atomic entity operations sharing the session's revision, receipts and history.</summary>
public abstract class EntityEditBatch
{
    private protected EntityEditBatch(AuthoringOperation operation) { ArgumentNullException.ThrowIfNull(operation); Operation = operation; }
    /// <summary>Gets the caller's stable operation identity and expected revision.</summary>
    public AuthoringOperation Operation { get; }
    internal abstract bool SameChanges(EntityEditBatch other);
    internal static ImmutableArray<T> Freeze<T>(IEnumerable<T> values, int maximum = 4096)
    {
        ArgumentNullException.ThrowIfNull(values); var result = ImmutableArray.CreateBuilder<T>();
        foreach (var value in values) { if (result.Count == maximum) throw new ArgumentOutOfRangeException(nameof(values)); ArgumentNullException.ThrowIfNull(value); result.Add(value); }
        return result.ToImmutable();
    }
}

/// <summary>Creates instances and optional new catalog entries in one transaction. Instances append to their parents in request order.</summary>
/// <remarks>New instances must have null SiblingOrder; the session assigns ranks. Existing catalogs are never overwritten.</remarks>
public sealed class CreateEntitiesBatch : EntityEditBatch
{
    /// <summary>Freezes a nonempty creation request; definitions and exact schemas may be supplied for an empty scene.</summary>
    /// <param name="operation">Shared operation identity.</param><param name="entities">New stable identities and local data.</param>
    /// <param name="definitions">New definitions only.</param><param name="schemas">New exact schema versions only.</param>
    public CreateEntitiesBatch(AuthoringOperation operation, IEnumerable<EntityInstance> entities,
        IEnumerable<EntityDefinition>? definitions = null, IEnumerable<ComponentSchema>? schemas = null) : base(operation)
    {
        Entities = Freeze(entities); Definitions = Freeze(definitions ?? []); Schemas = Freeze(schemas ?? [], 1024);
        if (Entities.IsEmpty || Entities.Length + Definitions.Length + Schemas.Length > 4096) throw new ArgumentOutOfRangeException(nameof(entities));
    }
    /// <summary>New instances, appended in this order.</summary>
    public ImmutableArray<EntityInstance> Entities { get; }
    /// <summary>New immutable definitions; Undo restores the earlier catalog.</summary>
    public ImmutableArray<EntityDefinition> Definitions { get; }
    /// <summary>New exact component schemas; Undo restores the earlier catalog.</summary>
    public ImmutableArray<ComponentSchema> Schemas { get; }
    internal override bool SameChanges(EntityEditBatch other) => other is CreateEntitiesBatch value &&
        Entities.Length == value.Entities.Length && Entities.Zip(value.Entities).All(pair => EntityIntentEquality.Same(pair.First, pair.Second)) &&
        Definitions.Length == value.Definitions.Length && Definitions.Zip(value.Definitions).All(pair => EntityIntentEquality.Same(pair.First, pair.Second)) &&
        Schemas.Length == value.Schemas.Length && Schemas.Zip(value.Schemas).All(pair => EntityIntentEquality.Same(pair.First, pair.Second));
}

/// <summary>Explicit treatment of descendants when removing scene instances.</summary>
public enum EntityDeletionPolicy
{
    /// <summary>Reject if any direct child would remain after the requested removals.</summary>
    RejectChildren,
    /// <summary>Remove every descendant of each requested root.</summary>
    Subtree
}
/// <summary>Removes instances only; definition/schema catalogs are retained. Arbitrary scalar strings are not interpreted as references.</summary>
public sealed class DeleteEntitiesBatch : EntityEditBatch
{
    /// <summary>Freezes roots and an explicit descendant policy. Expanded affected instances count against the session batch limit.</summary>
    /// <param name="operation">Shared operation identity.</param><param name="roots">Existing roots; overlapping subtrees are allowed.</param>
    /// <param name="policy">Reject dangling children or remove complete subtrees.</param>
    public DeleteEntitiesBatch(AuthoringOperation operation, IEnumerable<EntityId> roots, EntityDeletionPolicy policy) : base(operation)
    { Roots = Freeze(roots); Policy = policy; if (Roots.IsEmpty) throw new ArgumentOutOfRangeException(nameof(roots)); }
    /// <summary>Requested stable identities.</summary>
    public ImmutableArray<EntityId> Roots { get; }
    /// <summary>Explicit descendant policy.</summary>
    public EntityDeletionPolicy Policy { get; }
    internal override bool SameChanges(EntityEditBatch other) => other is DeleteEntitiesBatch value && Policy == value.Policy && Roots.SequenceEqual(value.Roots);
}

/// <summary>Caller-assigned identity for an original instance and its new copy.</summary>
/// <param name="Source">Existing identity.</param><param name="Copy">Fresh nonempty identity.</param>
public readonly record struct EntityCopyId(EntityId Source, EntityId Copy);
/// <summary>Copies a root or complete subtree with explicit IDs. Inheritance, names and local patches are preserved.</summary>
/// <remarks>Only parent links inside the copy are remapped. Component occurrence IDs remain scoped to their entity.
/// The root is placed after its source when sharing a parent, otherwise last under the destination parent.</remarks>
public sealed class DuplicateEntitiesBatch : EntityEditBatch
{
    /// <summary>Freezes the exact ID map; it must cover precisely the requested source set.</summary>
    /// <param name="operation">Shared operation identity.</param><param name="source">Original root.</param>
    /// <param name="copies">Fresh IDs, one per original.</param><param name="parentId">Destination parent for the new root.</param>
    /// <param name="includeDescendants">Whether the map covers the complete subtree.</param>
    public DuplicateEntitiesBatch(AuthoringOperation operation, EntityId source, IEnumerable<EntityCopyId> copies,
        EntityId? parentId, bool includeDescendants = true) : base(operation)
    { Source = source; Copies = Freeze(copies); ParentId = parentId; IncludeDescendants = includeDescendants; if (Copies.IsEmpty) throw new ArgumentOutOfRangeException(nameof(copies)); }
    /// <summary>Original root.</summary>
    public EntityId Source { get; }
    /// <summary>Exact caller-owned identity map.</summary>
    public ImmutableArray<EntityCopyId> Copies { get; }
    /// <summary>Destination parent, null for scene roots.</summary>
    public EntityId? ParentId { get; }
    /// <summary>Whether all descendants must be copied.</summary>
    public bool IncludeDescendants { get; }
    internal override bool SameChanges(EntityEditBatch other) => other is DuplicateEntitiesBatch value && Source == value.Source &&
        ParentId == value.ParentId && IncludeDescendants == value.IncludeDescendants && Copies.SequenceEqual(value.Copies);
}
