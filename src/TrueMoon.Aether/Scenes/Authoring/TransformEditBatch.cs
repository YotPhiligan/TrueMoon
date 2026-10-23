using System.Collections.Immutable;

namespace TrueMoon.Aether.Scenes.Authoring;

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

/// <summary>Local built-in v1 Transform: position, pitch/yaw/roll in radians and positive scale.</summary>
/// <param name="X">Local X coordinate.</param>
/// <param name="Y">Local Y coordinate.</param>
/// <param name="Z">Local Z coordinate.</param>
/// <param name="Pitch">Pitch in radians.</param>
/// <param name="Yaw">Yaw in radians.</param>
/// <param name="Roll">Roll in radians.</param>
/// <param name="ScaleX">Positive X scale.</param>
/// <param name="ScaleY">Positive Y scale.</param>
/// <param name="ScaleZ">Positive Z scale.</param>
public readonly record struct TransformValue(double X, double Y, double Z, double Pitch = 0, double Yaw = 0,
    double Roll = 0, double ScaleX = 1, double ScaleY = 1, double ScaleZ = 1)
{
    internal IEnumerable<(string Key, double Value)> Properties()
    {
        yield return ("x", X); yield return ("y", Y); yield return ("z", Z);
        yield return ("pitch", Pitch); yield return ("yaw", Yaw); yield return ("roll", Roll);
        yield return ("sx", ScaleX); yield return ("sy", ScaleY); yield return ("sz", ScaleZ);
    }

    internal bool IsValid() => Properties().All(p => float.IsFinite((float)p.Value)) && (float)ScaleX > 0 && (float)ScaleY > 0 && (float)ScaleZ > 0;
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
