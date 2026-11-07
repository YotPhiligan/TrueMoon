using System.Numerics;

namespace TrueMoon.Aether.Rendering;

/// <summary>Finite inclusive axis-aligned bounds; flat bounds are valid.</summary>
/// <param name="Minimum">Inclusive minimum.</param><param name="Maximum">Inclusive maximum.</param>
public readonly record struct RenderBounds(Vector3 Minimum, Vector3 Maximum)
{
    /// <summary>Checks finite ordered endpoints.</summary>
    public void Validate() => ContentValidation.Require(ContentValidation.Finite(Minimum) && ContentValidation.Finite(Maximum) &&
        Minimum.X <= Maximum.X && Minimum.Y <= Maximum.Y && Minimum.Z <= Maximum.Z,
        RenderContentError.InvalidBounds, "Bounds must be finite and ordered.");
    /// <summary>Conservatively transforms all eight corners, preserving rotation/nonuniform scale/shear.</summary>
    /// <param name="world">Finite nonsingular affine row-vector matrix.</param><returns>Detached world-space bounds.</returns>
    public RenderBounds Transform(Matrix4x4 world)
    {
        Validate();
        ContentValidation.Require(ContentValidation.Affine(world), RenderContentError.InvalidBounds, "Invalid affine bounds transform.");
        var minimum = new Vector3(float.PositiveInfinity); var maximum = new Vector3(float.NegativeInfinity);
        for (var corner = 0; corner < 8; corner++)
        {
            var x = (corner & 1) == 0 ? Minimum.X : Maximum.X;
            var y = (corner & 2) == 0 ? Minimum.Y : Maximum.Y;
            var z = (corner & 4) == 0 ? Minimum.Z : Maximum.Z;
            var transformed = new Vector3(Convert((double)x * world.M11 + (double)y * world.M21 + (double)z * world.M31 + world.M41),
                Convert((double)x * world.M12 + (double)y * world.M22 + (double)z * world.M32 + world.M42),
                Convert((double)x * world.M13 + (double)y * world.M23 + (double)z * world.M33 + world.M43));
            minimum = Vector3.Min(minimum, transformed); maximum = Vector3.Max(maximum, transformed);
        }
        return new(minimum, maximum);
    }
    private static float Convert(double value)
    {
        ContentValidation.Require(double.IsFinite(value) && value is >= -float.MaxValue and <= float.MaxValue,
            RenderContentError.InvalidBounds, "Transformed bounds are not representable.");
        return (float)value;
    }
}
