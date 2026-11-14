using System.Collections.Immutable;
using TrueMoon.Aether.Abstractions.Scenes;

namespace TrueMoon.Aether.Tooling.Scenes.Authoring;

/// <summary>Caller-assigned identity and optimistic concurrency boundary for Apply, Undo or Redo.</summary>
public sealed record AuthoringOperation
{
    /// <summary>Creates a request identity. Cancelled/unexpected failures before commit can be retried with the same ID.</summary>
    /// <param name="id">Nonempty operation GUID, unique for a distinct intent within this session.</param>
    /// <param name="expectedRevision">Exact current document revision, not a content checkpoint or simulation tick.</param>
    /// <param name="actor">Nonblank author identity of at most 256 characters.</param>
    public AuthoringOperation(Guid id, long expectedRevision, string actor)
    {
        if (id == Guid.Empty) throw new ArgumentException("Operation identity must be nonempty.", nameof(id));
        if (expectedRevision < 0) throw new ArgumentOutOfRangeException(nameof(expectedRevision));
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        if (actor.Length > 256) throw new ArgumentOutOfRangeException(nameof(actor));
        Id = id; ExpectedRevision = expectedRevision; Actor = actor;
    }
    /// <summary>Gets the session-local operation identity.</summary>
    public Guid Id { get; }
    /// <summary>Gets the expected document revision.</summary>
    public long ExpectedRevision { get; }
    /// <summary>Gets the author identity retained with the receipt.</summary>
    public string Actor { get; }
}

/// <summary>Full local Transform replacement addressed by stable entity and component occurrence IDs.</summary>
/// <param name="EntityId">Instance identity.</param>
/// <param name="ComponentId">Visible built-in Transform occurrence identity.</param>
/// <param name="Value">Desired local Transform; invalid numbers/scales produce an InvalidTransform result.</param>
public readonly record struct TransformEdit(EntityId EntityId, ComponentId ComponentId, TransformValue Value);

/// <summary>Frozen atomic batch. Every target can occur once; all nine properties become explicit instance overrides.</summary>
public sealed class TransformEditBatch
{
    /// <summary>Snapshots at most 4096 edits; session configuration may set a smaller limit.</summary>
    /// <param name="operation">Identity, author and expected revision shared by the whole batch.</param>
    /// <param name="edits">One or more Transform replacements.</param>
    public TransformEditBatch(AuthoringOperation operation, IEnumerable<TransformEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(operation); ArgumentNullException.ThrowIfNull(edits);
        Operation = operation;
        var values = ImmutableArray.CreateBuilder<TransformEdit>();
        foreach (var edit in edits)
        {
            if (values.Count == 4096) throw new ArgumentOutOfRangeException(nameof(edits), "At most 4096 edits are supported.");
            values.Add(edit);
        }
        if (values.Count == 0) throw new ArgumentException("An edit batch must not be empty.", nameof(edits));
        Edits = values.ToImmutable();
    }
    /// <summary>Gets the batch identity and concurrency boundary.</summary>
    public AuthoringOperation Operation { get; }
    /// <summary>Gets immutable edits in caller order.</summary>
    public ImmutableArray<TransformEdit> Edits { get; }
}
