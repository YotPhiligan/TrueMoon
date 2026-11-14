using System.Numerics;

namespace TrueMoon.Kton.Bodies;

/// <summary>Explicit owner-thread operation. Unused payload fields are ignored; default command is invalid.</summary>
public enum PhysicsCommandKind
{
    /// <summary>Invalid/default.</summary>
    None,
    /// <summary>Sets both velocities and wakes Dynamic/Kinematic bodies.</summary>
    SetVelocity,
    /// <summary>One linear impulse at centre of mass; Dynamic only, wakes the body.</summary>
    ApplyLinearImpulse,
    /// <summary>One tick's force at centre of mass, applied as F*fixedDelta before the next step; Dynamic only.</summary>
    ApplyLinearForce,
    /// <summary>Sets a pose, clears old contact identity and resets interpolation; preserves velocity and wakes the body.</summary>
    Teleport,
    /// <summary>Wakes a Dynamic body.</summary>
    Wake,
    /// <summary>Requests sleep, zeros Dynamic velocities and clears pending forces. Existing contact identities remain.</summary>
    Sleep
}

/// <summary>One bounded command value; no delegates or application references.</summary>
/// <param name="Kind">Operation.</param><param name="Body">Live target.</param>
/// <param name="Linear">Linear velocity, impulse or force according to Kind.</param>
/// <param name="Angular">Angular velocity for SetVelocity only.</param><param name="Pose">Pose for Teleport only.</param>
public readonly record struct PhysicsBodyCommand(PhysicsCommandKind Kind, PhysicsBodyId Body,
    Vector3 Linear = default, Vector3 Angular = default, PhysicsPose Pose = default);
