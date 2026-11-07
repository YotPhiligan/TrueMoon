using System.Numerics;
using TrueMoon.Aether.Ecs.Transforms;

namespace TrueMoon.Aether.Ecs.Motion;

/// <summary>Finite translation velocity in parent coordinates, in local-position units per second.</summary>
/// <param name="Value">Parent-space displacement rate; the node's own rotation is not applied to it.</param>
public readonly record struct LinearVelocity(Vector3 Value)
{
    /// <summary>Rejects nonfinite rates.</summary>
    public void Validate() => TransformMath.ValidateVector(Value);
}
