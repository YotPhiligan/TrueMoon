using System.Numerics;
using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Shapes;

namespace TrueMoon.Kton.Queries;

/// <summary>Query-only filter, separate from symmetric body/body filtering.</summary>
/// <param name="LayerMask">Included layers.</param><param name="IncludeSensors">Whether to include sensors.</param>
/// <param name="ExcludeBody">Optional live body to exclude; default means no exclusion.</param>
public readonly record struct PhysicsQueryFilter(uint LayerMask, bool IncludeSensors, PhysicsBodyId ExcludeBody = default)
{
    /// <summary>All layers and sensors, no excluded body.</summary>
    public static PhysicsQueryFilter All => new(uint.MaxValue, true);
}

/// <summary>Ray with unit direction and positive distance, in metres. Mesh backfaces are culled.</summary>
/// <param name="Origin">World-space start.</param><param name="Direction">Unit direction.</param><param name="MaximumDistance">Distance in metres.</param>
public sealed record PhysicsRay(Vector3 Origin, Vector3 Direction, float MaximumDistance)
{
    /// <summary>Query-only filter.</summary>
    public PhysicsQueryFilter Filter { get; init; } = PhysicsQueryFilter.All;
}

/// <summary>Translational sweep of a live convex shape, without angular sweep.</summary>
/// <param name="Shape">Sphere, box or capsule in this world.</param><param name="Pose">Initial pose.</param>
/// <param name="Direction">Unit direction.</param><param name="MaximumDistance">Positive metres.</param>
public sealed record PhysicsSweep(PhysicsShapeId Shape, PhysicsPose Pose, Vector3 Direction, float MaximumDistance)
{
    /// <summary>Query-only filter.</summary>
    public PhysicsQueryFilter Filter { get; init; } = PhysicsQueryFilter.All;
}

/// <summary>Exact convex shape overlap; broadphase AABB candidates alone are not hits.</summary>
/// <param name="Shape">Live sphere, box or capsule.</param><param name="Pose">World pose.</param>
public sealed record PhysicsOverlap(PhysicsShapeId Shape, PhysicsPose Pose)
{
    /// <summary>Query-only filter.</summary>
    public PhysicsQueryFilter Filter { get; init; } = PhysicsQueryFilter.All;
}

/// <summary>Copied ray/sweep hit. StartedInside is distance zero with HasNormal=false and unspecified Point/Normal.</summary>
/// <param name="Body">Hit identity.</param><param name="Shape">Hit shape.</param><param name="Subshape">Triangle index, or -1 for a convex shape.</param>
/// <param name="Distance">Metres along the query.</param><param name="Point">World point when HasNormal.</param><param name="Normal">Outward world normal when HasNormal.</param>
/// <param name="StartedInside">Query starts overlapping this shape.</param><param name="HasNormal">Whether Point/Normal are available.</param>
public readonly record struct PhysicsHit(PhysicsBodyId Body, PhysicsShapeId Shape, int Subshape, float Distance,
    Vector3 Point, Vector3 Normal, bool StartedInside, bool HasNormal);

/// <summary>Copied overlap identity; does not invent contact point/depth information.</summary>
/// <param name="Body">Hit body.</param><param name="Shape">Hit shape.</param><param name="Subshape">Triangle index or -1.</param>
public readonly record struct PhysicsOverlapHit(PhysicsBodyId Body, PhysicsShapeId Shape, int Subshape);

/// <summary>Caller-owned output result. Truncation is observable and does not fault the world.</summary>
/// <param name="WrittenCount">Initialized prefix length only; unused output elements remain untouched.</param>
/// <param name="Truncated">More hits existed than min(output.Length, MaxQueryHits); total count is not promised.</param>
public readonly record struct PhysicsQueryResult(int WrittenCount, bool Truncated);
