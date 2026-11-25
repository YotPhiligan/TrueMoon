namespace TrueMoon.Aether.Ecs;

/// <summary>Detached immutable reservation mapping. Handles may already have been destroyed after creation.</summary>
public sealed class CommandPlaybackResult
{
    internal static readonly CommandPlaybackResult Empty = new(Guid.NewGuid(), Guid.NewGuid(), Array.Empty<EntityHandle>());
    private readonly Guid _bufferId, _batchId;
    private readonly EntityHandle[] _created;
    /// <summary>Copies a successful host batch result.</summary>
    /// <param name="bufferId">Nonempty issuing buffer.</param><param name="batchId">Nonempty batch.</param><param name="created">Created handles in reservation order.</param>
    public CommandPlaybackResult(Guid bufferId, Guid batchId, IReadOnlyList<EntityHandle> created)
    {
        if (bufferId == Guid.Empty || batchId == Guid.Empty) throw new ArgumentException("Buffer and batch identities are required.");
        ArgumentNullException.ThrowIfNull(created);
        if (created.Count > 1048576) throw new ArgumentOutOfRangeException(nameof(created));
        _bufferId = bufferId; _batchId = batchId; _created = created.ToArray();
        if (_created.Any(value => value.WorldId == Guid.Empty || value.Index < 0 || value.Generation == 0))
            throw new ArgumentException("Created identities must be valid values.", nameof(created));
    }
    /// <summary>Number of originally created reservations.</summary>
    public int CreatedEntityCount => _created.Length;
    /// <summary>Resolves only a reservation from this exact buffer/batch.</summary>
    /// <param name="entity">Reservation.</param><returns>The original transient handle; this does not prove current liveness.</returns>
    public EntityHandle Resolve(DeferredEntity entity)
    {
        if (entity.BufferId != _bufferId || entity.BatchId != _batchId || (uint)entity.Index >= (uint)_created.Length)
            throw new ArgumentException("The reservation does not belong to this playback result.", nameof(entity));
        return _created[entity.Index];
    }
}
