using System.Numerics;
using TrueMoon.Aether.Ecs;

namespace TrueMoon.Aether.Kton;

/// <summary>Initial/commanded velocities and subsequent physics writeback. Static bodies require zero.</summary>
/// <param name="Linear">Metres per second.</param><param name="Angular">Radians per second.</param>
public readonly record struct PhysicsVelocity(Vector3 Linear, Vector3 Angular = default) : IComponent;
