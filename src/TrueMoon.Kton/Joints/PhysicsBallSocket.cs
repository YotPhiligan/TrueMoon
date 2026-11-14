using System.Numerics;
using TrueMoon.Kton.Bodies;

namespace TrueMoon.Kton.Joints;

/// <summary>Ball socket with local centre-of-mass offsets; no angular limits or ragdoll abstraction in v1.</summary>
/// <param name="BodyA">First live non-static body.</param><param name="BodyB">Second distinct live non-static body.</param>
/// <param name="LocalAnchorA">Metres in A's local frame.</param><param name="LocalAnchorB">Metres in B's local frame.</param>
public readonly record struct PhysicsBallSocket(PhysicsBodyId BodyA, PhysicsBodyId BodyB, Vector3 LocalAnchorA, Vector3 LocalAnchorB)
{
    /// <summary>Spring frequency in Hz; v1 is fixed to the PHYS0 value 30.</summary>
    public float SpringFrequency => 30;
    /// <summary>Damping ratio, fixed to one.</summary>
    public float DampingRatio => 1;
    /// <summary>Connected bodies do not collide; provider combines this with layer filtering.</summary>
    public bool CollideConnected => false;
}
