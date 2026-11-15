using TrueMoon.Aether.Abstractions.Ecs;
using TrueMoon.Kton;

namespace TrueMoon.Aether.Kton;

/// <summary>Explicit pose discontinuity. Sequence is positive and increases per attached entity incarnation.</summary>
/// <param name="Sequence">Monotonic command identity; equal identity requires identical payload.</param>
/// <param name="Pose">Root centre-of-mass pose, metres and unit quaternion.</param>
/// <param name="ClearVelocity">Zero dynamic/kinematic velocity before the next step.</param>
public readonly record struct PhysicsTeleport(ulong Sequence, PhysicsPose Pose, bool ClearVelocity = false) : IComponent;
