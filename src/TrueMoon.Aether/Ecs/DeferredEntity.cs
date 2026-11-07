namespace TrueMoon.Aether.Ecs;

/// <summary>A transient entity reservation valid only in one command buffer recording batch.</summary>
public readonly record struct DeferredEntity
{
    internal DeferredEntity(Guid bufferId, Guid batchId, int index) { BufferId = bufferId; BatchId = batchId; Index = index; }
    /// <summary>Gets the issuing buffer identity.</summary>
    public Guid BufferId { get; }
    /// <summary>Gets the recording batch identity, invalidated by successful playback or Reset.</summary>
    public Guid BatchId { get; }
    /// <summary>Gets the reservation ordinal within its batch.</summary>
    public int Index { get; }
}

/// <summary>A live or deferred command target. Default is invalid.</summary>
public readonly record struct CommandTarget
{
    private CommandTarget(EntityHandle live) { Live = live; Deferred = default; IsDeferred = false; }
    private CommandTarget(DeferredEntity deferred) { Live = default; Deferred = deferred; IsDeferred = true; }
    internal EntityHandle Live { get; }
    internal DeferredEntity Deferred { get; }
    internal bool IsDeferred { get; }
    /// <summary>Targets a live entity.</summary><param name="entity">Live handle.</param><returns>A command target.</returns>
    public static implicit operator CommandTarget(EntityHandle entity) => new(entity);
    /// <summary>Targets a reservation in the current recording batch.</summary><param name="entity">Deferred token.</param><returns>A command target.</returns>
    public static implicit operator CommandTarget(DeferredEntity entity) => new(entity);
}
