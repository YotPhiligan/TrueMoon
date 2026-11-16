namespace TrueMoon.Kton.Characters;

/// <summary>World-local, generation-safe identity of a controller borrowing a capsule body.</summary>
public readonly record struct PhysicsCharacterId(PhysicsWorldId World, int Slot, uint Generation)
{
    /// <summary>Structural validity; the world also checks the live generation.</summary>
    public bool IsValid => World.IsValid && Slot >= 0 && Generation != 0;
}
