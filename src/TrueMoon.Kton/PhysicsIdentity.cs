using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Joints;
using TrueMoon.Kton.Shapes;

namespace TrueMoon.Kton;

/// <summary>Pure ID admission checks. The provider must first look up the actual live slot; supplied IDs are not proof of liveness.</summary>
public static class PhysicsIdentity
{
    /// <summary>Checks a requested body against the world and its current live slot identity.</summary>
    /// <param name="world">Owner world.</param><param name="requested">Input ID.</param>
    /// <param name="current">Actual live slot ID, or default for an absent slot.</param>
    /// <returns>InvalidIdentifier, ForeignWorld, StaleIdentifier, or success in that order.</returns>
    public static PhysicsError Validate(PhysicsWorldId world, PhysicsBodyId requested, PhysicsBodyId current)
        => Check(world, requested.World, requested.IsValid, requested == current && current.IsValid);

    /// <summary>Checks a shape against the actual live slot.</summary>
    /// <param name="world">Owner world.</param><param name="requested">Input ID.</param><param name="current">Live ID or default.</param>
    /// <returns>Identity validation result.</returns>
    public static PhysicsError Validate(PhysicsWorldId world, PhysicsShapeId requested, PhysicsShapeId current)
        => Check(world, requested.World, requested.IsValid, requested == current && current.IsValid);

    /// <summary>Checks a joint against the actual live slot.</summary>
    /// <param name="world">Owner world.</param><param name="requested">Input ID.</param><param name="current">Live ID or default.</param>
    /// <returns>Identity validation result.</returns>
    public static PhysicsError Validate(PhysicsWorldId world, PhysicsJointId requested, PhysicsJointId current)
        => Check(world, requested.World, requested.IsValid, requested == current && current.IsValid);

    private static PhysicsError Check(PhysicsWorldId world, PhysicsWorldId issuing, bool valid, bool current)
    {
        if (!world.IsValid || !valid) return new(PhysicsErrorCode.InvalidIdentifier, "Id");
        if (issuing != world) return new(PhysicsErrorCode.ForeignWorld, "Id.World");
        return current ? default : new(PhysicsErrorCode.StaleIdentifier, "Id.Generation");
    }
}
