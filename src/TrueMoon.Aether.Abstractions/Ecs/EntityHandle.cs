namespace TrueMoon.Aether.Ecs;

/// <summary>Transient value identity. Default is invalid; a slot is meaningful only with its World and generation.</summary>
/// <remarks>Construction does not prove liveness. The host validates every use, including wrong-World and stale identities.</remarks>
public readonly record struct EntityHandle
{
    /// <summary>Describes a host-issued identity without allocating an entity facade.</summary>
    /// <param name="worldId">Nonempty World identity.</param><param name="index">Nonnegative slot.</param><param name="generation">Nonzero generation.</param>
    public EntityHandle(Guid worldId, int index, uint generation)
    {
        if (worldId == Guid.Empty) throw new ArgumentException("World identity is required.", nameof(worldId));
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        if (generation == 0) throw new ArgumentOutOfRangeException(nameof(generation));
        WorldId = worldId; Index = index; Generation = generation;
    }
    /// <summary>Issuing World, never a persistent authoring ID.</summary>
    public Guid WorldId { get; }
    /// <summary>Transient slot.</summary>
    public int Index { get; }
    /// <summary>Generation; exhausted generations must retire rather than wrap.</summary>
    public uint Generation { get; }
}
