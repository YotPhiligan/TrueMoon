namespace TrueMoon.Aether.Ecs;

/// <summary>Immutable mapping from one successful batch's reservations to their created handles.</summary>
public sealed class CommandPlaybackResult
{
    internal static readonly CommandPlaybackResult Empty = new(Guid.Empty, Guid.Empty, []);
    private readonly Guid _bufferId, _batchId;
    private readonly EntityHandle[] _created;
    internal CommandPlaybackResult(Guid bufferId, Guid batchId, EntityHandle[] created)
    { _bufferId = bufferId; _batchId = batchId; _created = created; }
    /// <summary>Gets the number of reservations created, including entities subsequently destroyed in the same batch.</summary>
    public int CreatedEntityCount => _created.Length;
    /// <summary>Resolves a token from this batch. The handle may already be destroyed; inspect World.IsAlive separately.</summary>
    /// <param name="entity">Token captured while recording this batch.</param><returns>The originally created handle.</returns>
    public EntityHandle Resolve(DeferredEntity entity)
    {
        if (entity.BufferId != _bufferId || entity.BatchId != _batchId || (uint)entity.Index >= (uint)_created.Length)
            throw new ArgumentException("The reservation does not belong to this playback result.", nameof(entity));
        return _created[entity.Index];
    }
}
