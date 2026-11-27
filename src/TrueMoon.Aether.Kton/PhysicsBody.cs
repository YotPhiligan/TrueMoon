using System.Numerics;
using TrueMoon.Aether.Ecs;
using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Shapes;

namespace TrueMoon.Aether.Kton;

/// <summary>Collider profile. Zero/default is invalid.</summary>
public enum PhysicsColliderShape
{
    /// <summary>Size contains full XYZ side lengths.</summary>
    Box = 1,
    /// <summary>Size.X is radius; Y/Z must be zero.</summary>
    Sphere,
    /// <summary>Size.X is radius, Y cylinder length, Z zero. Local axis is +Y.</summary>
    Capsule,
    /// <summary>Authored static triangles resolved by asset ID. All dimensions must be zero.</summary>
    StaticMesh = 4
}

/// <summary>Reference-free body authoring data. Physical pose uses root LocalTransform, unit scale and centre-of-mass origin.</summary>
/// <param name="Motion">Static, dynamic or kinematic ownership.</param><param name="Shape">Collider profile.</param>
/// <param name="Size">Shape-specific metres.</param><param name="Mass">Positive dynamic mass, zero for static/kinematic.</param>
public readonly record struct PhysicsBody(PhysicsBodyMotion Motion, PhysicsColliderShape Shape, Vector3 Size, float Mass) : IComponent
{
    /// <summary>Body friction, combined by the provider.</summary>
    public PhysicsMaterial Material { get; init; } = PhysicsMaterial.Default;
    /// <summary>Symmetric collision filter.</summary>
    public PhysicsCollisionFilter Filter { get; init; } = PhysicsCollisionFilter.All;
    /// <summary>Generate contacts without solver response.</summary>
    public bool IsSensor { get; init; }
    /// <summary>Stable collision asset ID for an authored static mesh; empty for primitives.</summary>
    public Guid MeshAssetId { get; init; }
    /// <summary>Captures a neutral primitive definition, rejecting unsupported/unused shape fields.</summary>
    /// <returns>Immutable definition; live limits/body validation remain the runtime provider's responsibility.</returns>
    public PhysicsShapeDefinition CaptureShape()
    {
        if (MeshAssetId != Guid.Empty) throw new InvalidDataException("Primitive colliders cannot reference a mesh asset.");
        return Shape switch
        {
            PhysicsColliderShape.Box => new PhysicsBox(Size),
            PhysicsColliderShape.Sphere when Size.Y == 0 && Size.Z == 0 => new PhysicsSphere(Size.X),
            PhysicsColliderShape.Capsule when Size.Z == 0 => new PhysicsCapsule(Size.X, Size.Y),
            _ => throw new InvalidDataException("Unsupported physics collider or nonzero unused dimensions.")
        };
    }
}
