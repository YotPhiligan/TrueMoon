using System.Numerics;
using TrueMoon.Kton.Shapes;

namespace TrueMoon.Kton.Bodies;

/// <summary>Body ownership of motion.</summary>
public enum PhysicsBodyMotion
{
    /// <summary>Fixed collider, zero mass and velocities.</summary>
    Static,
    /// <summary>Solver-owned motion, positive mass.</summary>
    Dynamic,
    /// <summary>Command-owned motion, zero mass, no gravity integration.</summary>
    Kinematic
}

/// <summary>Symmetric collision filtering. A pair is allowed only when both masks include the other layer.</summary>
/// <param name="Layer">Layer index 0..31.</param><param name="Mask">Allowed layer bits; zero disables all body collisions.</param>
public readonly record struct PhysicsCollisionFilter(byte Layer, uint Mask)
{
    /// <summary>Layer zero, all layer bits.</summary>
    public static PhysicsCollisionFilter All => new(0, uint.MaxValue);
    /// <summary>Whether both valid filters admit this pair.</summary>
    /// <param name="other">Other body's filter.</param><returns>Symmetric pair admission.</returns>
    public bool Allows(PhysicsCollisionFilter other) => Layer < 32 && other.Layer < 32
        && (Mask & (1u << other.Layer)) != 0 && (other.Mask & (1u << Layer)) != 0;
}

/// <summary>Body-specific friction. Spring/damping/recovery belong to the world quality profile.</summary>
/// <param name="Friction">Coefficient 0..4; pair combination is min(A,B), independent of callback ordering.</param>
public readonly record struct PhysicsMaterial(float Friction)
{
    /// <summary>PHYS0 friction coefficient.</summary>
    public static PhysicsMaterial Default => new(0.8f);
    /// <summary>Combines validated coefficients symmetrically.</summary>
    /// <param name="other">Other material.</param><returns>Minimum coefficient.</returns>
    public float CombineFriction(PhysicsMaterial other) => MathF.Min(Friction, other.Friction);
}

/// <summary>Neutral body input. Shape must be a live ID in this world; no scene/entity/backend types.</summary>
/// <param name="Shape">Immutable shared shape.</param><param name="Motion">Body mode.</param><param name="Pose">Initial centre-of-mass pose.</param>
public sealed record PhysicsBodyDescription(PhysicsShapeId Shape, PhysicsBodyMotion Motion, PhysicsPose Pose)
{
    /// <summary>Mass in kg, 0 for Static/Kinematic, 0.01..10000 for Dynamic.</summary>
    public float Mass { get; init; } = 1;
    /// <summary>Linear velocity in metres/second, zero for Static.</summary>
    public Vector3 LinearVelocity { get; init; }
    /// <summary>Angular velocity in radians/second; magnitude at most 64, zero for Static.</summary>
    public Vector3 AngularVelocity { get; init; }
    /// <summary>Friction coefficient; no independent contact softness override.</summary>
    public PhysicsMaterial Material { get; init; } = PhysicsMaterial.Default;
    /// <summary>Symmetric layer/mask filter.</summary>
    public PhysicsCollisionFilter Filter { get; init; } = PhysicsCollisionFilter.All;
    /// <summary>Generate contacts without solver response. At least one body must be Dynamic for body/body events in v1.</summary>
    public bool IsSensor { get; init; }
}

/// <summary>Copied body state; no borrowed backend references.</summary>
/// <param name="Body">Live identity at read time.</param><param name="Motion">Mode.</param><param name="Pose">Centre-of-mass pose.</param>
/// <param name="LinearVelocity">Metres/second.</param><param name="AngularVelocity">Radians/second.</param><param name="IsAwake">Solver activity.</param>
public readonly record struct PhysicsBodyState(PhysicsBodyId Body, PhysicsBodyMotion Motion, PhysicsPose Pose,
    Vector3 LinearVelocity, Vector3 AngularVelocity, bool IsAwake);
