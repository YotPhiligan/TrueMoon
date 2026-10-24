using System.Collections.Immutable;

namespace TrueMoon.Aether.Scenes.Authoring;

/// <summary>Changes a scene parent while preserving all local component data and inheritance.</summary>
/// <param name="EntityId">Existing scene occurrence.</param>
/// <param name="ParentId">Existing new parent, or null to move to the scene root.</param>
public readonly record struct ReparentEdit(EntityId EntityId, EntityId? ParentId);

/// <summary>Frozen atomic parent changes using the document's shared revision, receipts and Undo/Redo.</summary>
public sealed class ReparentBatch
{
    /// <summary>Captures one to 4096 parent changes; the session may impose a smaller batch limit.</summary>
    /// <param name="operation">Identity, actor and expected revision.</param><param name="edits">One change per entity.</param>
    public ReparentBatch(AuthoringOperation operation, IEnumerable<ReparentEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(operation); ArgumentNullException.ThrowIfNull(edits);
        var values = ImmutableArray.CreateBuilder<ReparentEdit>();
        foreach (var edit in edits)
        {
            if (values.Count == 4096) throw new ArgumentOutOfRangeException(nameof(edits), "At most 4096 edits are supported.");
            values.Add(edit);
        }
        if (values.Count == 0) throw new ArgumentException("A reparent batch must not be empty.", nameof(edits));
        Operation = operation; Edits = values.ToImmutable();
    }
    /// <summary>Gets the operation identity and optimistic concurrency boundary.</summary>
    public AuthoringOperation Operation { get; }
    /// <summary>Gets parent changes in caller order; their combined graph must be valid.</summary>
    public ImmutableArray<ReparentEdit> Edits { get; }
}
