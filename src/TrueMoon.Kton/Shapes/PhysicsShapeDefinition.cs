using System.Numerics;

namespace TrueMoon.Kton.Shapes;

/// <summary>Immutable shape definition. CreateShape consumes borrowed mesh input synchronously and owns its captured copy.</summary>
public abstract record PhysicsShapeDefinition;

/// <summary>Sphere, radius in metres.</summary><param name="Radius">Positive radius.</param>
public sealed record PhysicsSphere(float Radius) : PhysicsShapeDefinition;

/// <summary>Box with full side lengths, in metres; no scale transform.</summary><param name="Size">Positive XYZ lengths.</param>
public sealed record PhysicsBox(Vector3 Size) : PhysicsShapeDefinition;

/// <summary>Capsule along local +Y. Full height is CylinderLength + 2*Radius.</summary>
/// <param name="Radius">Positive hemisphere radius.</param><param name="CylinderLength">Positive centre-to-centre segment length, not half-length.</param>
public sealed record PhysicsCapsule(float Radius, float CylinderLength) : PhysicsShapeDefinition;

/// <summary>Static-only indexed triangle mesh. Backfaces are culled; front winding is clockwise when viewed from the front.</summary>
/// <param name="Vertices">Borrowed during CreateShape only; caller must not mutate until it returns.</param>
/// <param name="Indices">Borrowed index triples with valid, nondegenerate clockwise triangles.</param>
public sealed record PhysicsStaticMesh(ReadOnlyMemory<Vector3> Vertices, ReadOnlyMemory<int> Indices) : PhysicsShapeDefinition;
