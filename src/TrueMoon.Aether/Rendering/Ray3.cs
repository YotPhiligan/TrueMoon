using System.Numerics;

namespace TrueMoon.Aether.Rendering;

/// <summary>A finite near-to-far segment parameterized by world distance. Default is invalid.</summary>
public readonly record struct Ray3
{
    /// <summary>Creates a normalized ray with an inclusive positive length.</summary>
    /// <param name="origin">Finite near origin.</param><param name="direction">Finite nonzero direction; normalized using double intermediates.</param>
    /// <param name="maximumDistance">Positive finite maximum world distance.</param>
    public Ray3(Vector3 origin, Vector3 direction, double maximumDistance)
    {
        var length = Length(direction.X, direction.Y, direction.Z);
        if (!ContentValidation.Finite(origin)) throw new ArgumentOutOfRangeException(nameof(origin));
        if (!ContentValidation.Finite(direction) || !double.IsFinite(length) || length <= 0) throw new ArgumentOutOfRangeException(nameof(direction));
        if (!double.IsFinite(maximumDistance) || maximumDistance <= 0) throw new ArgumentOutOfRangeException(nameof(maximumDistance));
        Origin = origin; Direction = new((float)(direction.X / length), (float)(direction.Y / length), (float)(direction.Z / length));
        MaximumDistance = maximumDistance;
    }

    /// <summary>Gets the near-plane world origin.</summary>
    public Vector3 Origin { get; }
    /// <summary>Gets the unit world direction. Local transformed directions must not be normalized again.</summary>
    public Vector3 Direction { get; }
    /// <summary>Gets the inclusive far-segment distance.</summary>
    public double MaximumDistance { get; }

    /// <summary>Intersects an origin-centered unit cube with its full affine matrix.</summary>
    /// <param name="world">Finite nonsingular affine world matrix with a finite inverse.</param>
    /// <param name="distance">Nearest admitted surface distance, null for a valid miss.</param>
    /// <returns>False for invalid math/geometry, distinct from a valid miss. A near origin inside the cube uses the exit surface.</returns>
    public bool TryIntersectUnitCube(Matrix4x4 world, out double? distance)
    {
        distance = null;
        return ContentValidation.Affine(world) && ContentValidation.TryInvertFinite(world, out var inverse) &&
            TryIntersectInverse(inverse, out distance);
    }

    internal bool TryIntersectInverse(Matrix4x4 inverse, out double? distance)
    {
        distance = null;
        if (MaximumDistance <= 0) return false;
        // Double affine transforms avoid Single overflow and retain the world-distance parameter under scale/shear.
        Span<double> origin = [Transform(Origin, inverse.M11, inverse.M21, inverse.M31, inverse.M41),
            Transform(Origin, inverse.M12, inverse.M22, inverse.M32, inverse.M42), Transform(Origin, inverse.M13, inverse.M23, inverse.M33, inverse.M43)];
        Span<double> direction = [Transform(Direction, inverse.M11, inverse.M21, inverse.M31, 0),
            Transform(Direction, inverse.M12, inverse.M22, inverse.M32, 0), Transform(Direction, inverse.M13, inverse.M23, inverse.M33, 0)];
        var start = double.NegativeInfinity; var end = double.PositiveInfinity;
        for (var axis = 0; axis < 3; axis++)
        {
            var o = origin[axis]; var d = direction[axis];
            if (!double.IsFinite(o) || !double.IsFinite(d)) return false;
            if (d == 0) { if (o < -.5 || o > .5) return true; continue; }
            var a = (-.5 - o) / d; var b = (.5 - o) / d;
            start = Math.Max(start, Math.Min(a, b)); end = Math.Min(end, Math.Max(a, b));
            if (start > end) return true;
        }
        var hit = start >= 0 ? start : end;
        if (double.IsFinite(hit) && hit >= 0 && hit <= MaximumDistance) distance = hit;
        return true;
    }

    internal static bool TryFromSegment(Vector3 near, Vector3 far, out Ray3 ray)
    {
        ray = default;
        var x = (double)far.X - near.X; var y = (double)far.Y - near.Y; var z = (double)far.Z - near.Z;
        var length = Length(x, y, z);
        if (!double.IsFinite(length) || length <= 0) return false;
        ray = new(near, new((float)(x / length), (float)(y / length), (float)(z / length)), length); return true;
    }

    private static double Transform(Vector3 value, float x, float y, float z, float translation)
        => (double)value.X * x + (double)value.Y * y + (double)value.Z * z + translation;
    private static double Length(double x, double y, double z) => Math.Sqrt(x * x + y * y + z * z);
}
