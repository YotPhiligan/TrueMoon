namespace TrueMoon.Kton.Joints;

/// <summary>Identity of a world-owned joint; deleting either body requires removing this joint first.</summary>
/// <param name="World">Issuing world incarnation.</param>
/// <param name="Slot">Nonnegative provider-owned slot.</param>
/// <param name="Generation">Starts at one; exhausted slots are retired.</param>
public readonly record struct PhysicsJointId(PhysicsWorldId World, int Slot, uint Generation)
{
    /// <summary>Structural validity only, not a live-joint lookup.</summary>
    public bool IsValid => World.IsValid && Slot >= 0 && Generation != 0;
}
