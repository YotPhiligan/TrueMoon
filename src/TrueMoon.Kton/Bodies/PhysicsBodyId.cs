namespace TrueMoon.Kton.Bodies;

/// <summary>World-local body identity. Slots may be reused only with a newer generation.</summary>
/// <param name="World">Issuing world incarnation.</param>
/// <param name="Slot">Nonnegative provider-owned slot, not a BEPU handle.</param>
/// <param name="Generation">Starts at one. Retire the slot instead of wrapping uint.MaxValue.</param>
public readonly record struct PhysicsBodyId(PhysicsWorldId World, int Slot, uint Generation)
{
    /// <summary>Structural validity only. World operations also check ownership and the live generation.</summary>
    public bool IsValid => World.IsValid && Slot >= 0 && Generation != 0;
}
