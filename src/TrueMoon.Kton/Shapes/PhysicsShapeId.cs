namespace TrueMoon.Kton.Shapes;

/// <summary>Identity of an immutable, world-owned, shareable shape.</summary>
/// <param name="World">Issuing world incarnation.</param>
/// <param name="Slot">Nonnegative provider-owned slot.</param>
/// <param name="Generation">Starts at one; exhausted slots are retired.</param>
public readonly record struct PhysicsShapeId(PhysicsWorldId World, int Slot, uint Generation)
{
    /// <summary>Structural validity only, not a live-shape lookup.</summary>
    public bool IsValid => World.IsValid && Slot >= 0 && Generation != 0;
}
