namespace TrueMoon.Aether.Ecs;

/// <summary>A reservation in exactly one structural command batch. Default is invalid.</summary>
public readonly record struct DeferredEntity
{
    /// <summary>Describes a host-issued reservation; construction does not authorize playback.</summary>
    /// <param name="bufferId">Issuing buffer identity.</param><param name="batchId">Current recording batch.</param><param name="index">Reservation ordinal.</param>
    public DeferredEntity(Guid bufferId, Guid batchId, int index)
    {
        if (bufferId == Guid.Empty || batchId == Guid.Empty) throw new ArgumentException("Buffer and batch identities are required.");
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        BufferId = bufferId; BatchId = batchId; Index = index;
    }
    /// <summary>Issuing buffer.</summary>
    public Guid BufferId { get; }
    /// <summary>Batch invalidated by playback, reset or cancellation.</summary>
    public Guid BatchId { get; }
    /// <summary>Reservation ordinal.</summary>
    public int Index { get; }
}

/// <summary>A live or deferred structural target. Default is invalid and must be rejected by the host.</summary>
public readonly record struct CommandTarget
{
    private CommandTarget(EntityHandle live) { Live = live; Deferred = default; IsDeferred = false; }
    private CommandTarget(DeferredEntity deferred) { Live = default; Deferred = deferred; IsDeferred = true; }
    /// <summary>Live target when IsDeferred is false.</summary>
    public EntityHandle Live { get; }
    /// <summary>Reservation when IsDeferred is true.</summary>
    public DeferredEntity Deferred { get; }
    /// <summary>Whether the target is a batch-local reservation.</summary>
    public bool IsDeferred { get; }
    /// <summary>Targets a live value.</summary><param name="entity">Host-issued handle.</param><returns>Live target.</returns>
    public static implicit operator CommandTarget(EntityHandle entity) => new(entity);
    /// <summary>Targets a reservation.</summary><param name="entity">Host-issued reservation.</param><returns>Deferred target.</returns>
    public static implicit operator CommandTarget(DeferredEntity entity) => new(entity);
}
