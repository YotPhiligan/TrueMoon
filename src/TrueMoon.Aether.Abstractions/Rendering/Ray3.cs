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
        Span<double> origin = stackalloc double[3];
        Span<double> direction = stackalloc double[3];
        if (!TryTransformInverse(inverse, origin, direction)) return false;
        var start = double.NegativeInfinity; var end = double.PositiveInfinity;
        for (var axis = 0; axis < 3; axis++)
            if (!ClipAxis(origin[axis], direction[axis], -.5, .5, ref start, ref end)) return true;
        var hit = start >= 0 ? start : end;
        if (double.IsFinite(hit) && hit >= 0 && hit <= MaximumDistance) distance = hit;
        return true;
    }

    internal bool TryIntersectMeshInverse(Matrix4x4 inverse, MeshDefinition mesh, double maximumDistance, out double? distance)
    {
        distance = null;
        Span<double> origin = stackalloc double[3];
        Span<double> direction = stackalloc double[3];
        if (!TryTransformInverse(inverse, origin, direction)) return false;
        var cap = Math.Min(MaximumDistance, maximumDistance);
        var bounds = mesh.Bounds;
        double start = 0, end = cap;
        if (!ClipAxis(origin[0], direction[0], bounds.Minimum.X, bounds.Maximum.X, ref start, ref end) ||
            !ClipAxis(origin[1], direction[1], bounds.Minimum.Y, bounds.Maximum.Y, ref start, ref end) ||
            !ClipAxis(origin[2], direction[2], bounds.Minimum.Z, bounds.Maximum.Z, ref start, ref end))
            return true;

        // The slab interval is only a broad phase. In particular, flat bounds may round
        // differently from the triangle intersection along the same ray.
        var d = (X: direction[0], Y: direction[1], Z: direction[2]);
        var vertices = mesh.Vertices.AsSpan();
        var indices = mesh.Indices.AsSpan();
        for (var i = 0; i < indices.Length; i += 3)
        {
            ref readonly var a = ref vertices[(int)indices[i]];
            ref readonly var b = ref vertices[(int)indices[i + 1]];
            ref readonly var c = ref vertices[(int)indices[i + 2]];
            var e1 = ((double)b.X - a.X, (double)b.Y - a.Y, (double)b.Z - a.Z);
            var e2 = ((double)c.X - a.X, (double)c.Y - a.Y, (double)c.Z - a.Z);
            var p = Cross(d, e2);
            var det = Dot(e1, p);
            if (!double.IsFinite(det)) return false;
            if (det == 0) continue;
            var s = (origin[0] - a.X, origin[1] - a.Y, origin[2] - a.Z);
            var q = Cross(s, e1);
            var u = Dot(s, p) / det;
            var v = Dot(d, q) / det;
            var t = Dot(e2, q) / det;
            if (!double.IsFinite(u) || !double.IsFinite(v) || !double.IsFinite(t)) return false;
            if (u >= 0 && v >= 0 && u + v <= 1 && t >= 0 && t <= cap && (distance == null || t < cap))
            {
                distance = t;
                cap = t;
            }
        }
        return true;
    }

    private bool TryTransformInverse(Matrix4x4 inverse, Span<double> origin, Span<double> direction)
    {
        if (MaximumDistance <= 0) return false;
        // Double affine transforms avoid Single overflow and retain world t under scale/shear.
        origin[0] = Transform(Origin, inverse.M11, inverse.M21, inverse.M31, inverse.M41);
        origin[1] = Transform(Origin, inverse.M12, inverse.M22, inverse.M32, inverse.M42);
        origin[2] = Transform(Origin, inverse.M13, inverse.M23, inverse.M33, inverse.M43);
        direction[0] = Transform(Direction, inverse.M11, inverse.M21, inverse.M31, 0);
        direction[1] = Transform(Direction, inverse.M12, inverse.M22, inverse.M32, 0);
        direction[2] = Transform(Direction, inverse.M13, inverse.M23, inverse.M33, 0);
        for (var axis = 0; axis < 3; axis++)
            if (!double.IsFinite(origin[axis]) || !double.IsFinite(direction[axis])) return false;
        return direction[0] != 0 || direction[1] != 0 || direction[2] != 0;
    }

    private static bool ClipAxis(double origin, double direction, double minimum, double maximum, ref double start, ref double end)
    {
        if (direction == 0) return origin >= minimum && origin <= maximum;
        var a = (minimum - origin) / direction;
        var b = (maximum - origin) / direction;
        start = Math.Max(start, Math.Min(a, b));
        end = Math.Min(end, Math.Max(a, b));
        return start <= end;
    }

    private static (double X, double Y, double Z) Cross((double X, double Y, double Z) a, (double X, double Y, double Z) b)
        => (a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    private static double Dot((double X, double Y, double Z) a, (double X, double Y, double Z) b)
        => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

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
