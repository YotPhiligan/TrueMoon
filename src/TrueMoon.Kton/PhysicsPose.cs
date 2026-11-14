using System.Numerics;

namespace TrueMoon.Kton;

/// <summary>World-space centre-of-mass pose in metres; right-handed Y-up, quaternion XYZW, unit scale.</summary>
/// <param name="Position">World position.</param><param name="Orientation">Unit quaternion; no implicit normalization.</param>
public readonly record struct PhysicsPose(Vector3 Position, Quaternion Orientation)
{
    /// <summary>Origin with identity orientation. Default struct orientation is invalid.</summary>
    public static PhysicsPose Identity => new(Vector3.Zero, Quaternion.Identity);
}
