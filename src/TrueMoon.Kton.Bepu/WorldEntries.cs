using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Joints;
using TrueMoon.Kton.Shapes;

namespace TrueMoon.Kton.Bepu;

internal sealed class ShapeEntry(PhysicsShapeId id, PhysicsShapeDefinition definition, TypedIndex backend)
{
    internal readonly PhysicsShapeId Id = id;
    internal readonly PhysicsShapeDefinition Definition = definition;
    internal readonly TypedIndex Backend = backend;
    internal int BodyReferences;
    internal BodyInertia Inertia(float mass) => Definition switch
    {
        PhysicsSphere s => new Sphere(s.Radius).ComputeInertia(mass),
        PhysicsBox b => new Box(b.Size.X, b.Size.Y, b.Size.Z).ComputeInertia(mass),
        PhysicsCapsule c => new Capsule(c.Radius, c.CylinderLength).ComputeInertia(mass),
        _ => throw BepuPhysicsWorldFactory.Error(PhysicsErrorCode.UnsupportedGeometry, "Body.Shape")
    };
}
internal sealed class BodyEntry(PhysicsBodyId id, PhysicsBodyDescription description, ShapeEntry shape)
{
    internal readonly PhysicsBodyId Id = id;
    internal readonly PhysicsBodyDescription Description = description;
    internal readonly ShapeEntry Shape = shape;
    internal BodyHandle Handle;
    internal StaticHandle StaticHandle;
    internal CollidableReference Collider;
    internal int JointReferences;
    internal Vector3 PendingImpulse;
}
internal sealed class JointEntry(PhysicsJointId id, PhysicsBallSocket description, ConstraintHandle backend)
{
    internal readonly PhysicsJointId Id = id;
    internal readonly PhysicsBallSocket Description = description;
    internal readonly ConstraintHandle Backend = backend;
}
internal struct PreparedBody
{
    internal BodyEntry? Entry;
    internal PhysicsPose Pose;
    internal Vector3 Linear, Angular, PendingImpulse;
    internal bool Awake, Teleported;
}
