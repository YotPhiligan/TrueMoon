namespace TrueMoon.Aether.Ecs;

/// <summary>Transient entity identity in one World. Never persist a slot or generation as an authoring ID.</summary>
/// <remarks>The default handle is invalid. Reusing a slot does not revive its previous handles.</remarks>
public readonly record struct EntityHandle
{
    internal EntityHandle(Guid worldId, int index, uint generation)
    { WorldId = worldId; Index = index; Generation = generation; }

    /// <summary>Gets the identity of the World that issued this handle.</summary>
    public Guid WorldId { get; }
    /// <summary>Gets the slot index within that World; this is not an iteration order or persistent ID.</summary>
    public int Index { get; }
    /// <summary>Gets the slot generation, starting at one. Exhausted generations retire their slot.</summary>
    public uint Generation { get; }
}
