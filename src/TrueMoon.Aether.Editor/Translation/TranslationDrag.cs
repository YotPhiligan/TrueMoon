using System.Numerics;
using TrueMoon.Aether.Rendering;

namespace TrueMoon.Aether.Editor.Translation;

/// <summary>Frozen world-axis/plane drag math; candidates have no authoring or UI side effects.</summary>
public sealed class TranslationDrag
{
    private readonly double _x, _y;
    private readonly Vector3 _start;
    private TranslationDrag(TranslationGeometry geometry, TranslationHandle handle, double x, double y, Vector3 start)
    { Geometry = geometry; Handle = handle; _x = x; _y = y; _start = start; }
    /// <summary>Original frozen view and pivot.</summary>
    public TranslationGeometry Geometry { get; }
    /// <summary>Admitted world constraint.</summary>
    public TranslationHandle Handle { get; }
    /// <summary>Starts only a projected nondegenerate constraint with a valid ray intersection.</summary>
    /// <param name="geometry">Frozen actual view.</param><param name="handle">Projected constraint.</param>
    /// <param name="x">Original logical X.</param><param name="y">Original logical Y.</param>
    /// <param name="drag">Detached gesture data.</param><returns>False for invalid/parallel/behind-camera input.</returns>
    public static bool TryCreate(TranslationGeometry geometry, TranslationHandle handle, double x, double y, out TranslationDrag? drag)
    {
        ArgumentNullException.ThrowIfNull(geometry); drag = null;
        if (!geometry.Handles.Any(h => h.Kind == handle) || !Parameter(geometry, handle, x, y, out var start)) return false;
        drag = new(geometry, handle, x, y, start); return true;
    }
    /// <summary>Computes a world delta from frozen start; finite outside-image movement is allowed under caller's capture.</summary>
    /// <param name="x">Current logical X.</param><param name="y">Current logical Y.</param>
    /// <param name="delta">Constrained finite delta; zero for the exact original pointer.</param><returns>False when a candidate is invalid; callers keep the prior valid preview but reject an invalid release.</returns>
    public bool TryMove(double x, double y, out Vector3 delta)
    {
        delta = default;
        if (x == _x && y == _y) return true;
        if (!Parameter(Geometry, Handle, x, y, out var position)) return false;
        delta = position - _start; return Finite(delta);
    }
    private static bool Parameter(TranslationGeometry geometry, TranslationHandle handle, double x, double y, out Vector3 parameter)
    {
        parameter = default;
        if (!Ray(geometry, x, y, out var ray)) return false;
        var axis = TranslationGeometry.Axis(handle); var offset = ray.Origin - geometry.Origin;
        if (axis != Vector3.Zero)
        {
            var c = Dot(axis, ray.Direction); var denominator = 1 - c * c;
            if (denominator < 1e-8) return false;
            var s = (c * Dot(axis, offset) - Dot(ray.Direction, offset)) / denominator;
            if (!double.IsFinite(s) || s < 0 || s > ray.MaximumDistance) return false;
            var t = (Dot(axis, offset) - c * Dot(ray.Direction, offset)) / denominator;
            parameter = axis * (float)t;
        }
        else
        {
            var (a, b) = TranslationGeometry.PlaneAxes(handle); var normal = Vector3.Cross(a, b);
            var denominator = Dot(normal, ray.Direction);
            if (Math.Abs(denominator) < 1e-8) return false;
            var t = -Dot(normal, offset) / denominator;
            if (!double.IsFinite(t) || t < 0 || t > ray.MaximumDistance) return false;
            parameter = new((float)(offset.X + t * ray.Direction.X), (float)(offset.Y + t * ray.Direction.Y), (float)(offset.Z + t * ray.Direction.Z));
            parameter *= a + b;
        }
        return Finite(parameter);
    }
    private static bool Ray(TranslationGeometry geometry, double x, double y, out Ray3 ray)
    {
        ray = default;
        if (!double.IsFinite(x) || !double.IsFinite(y)) return false;
        var image = geometry.Image; var nx = (float)(2 * (x - image.Left) / image.Width - 1); var ny = (float)(2 * (y - image.Top) / image.Height - 1);
        if (!geometry.Projection.TryUnproject(new(nx, ny, 0), out var near) || !geometry.Projection.TryUnproject(new(nx, ny, 1), out var far)) return false;
        var dx = (double)far.X - near.X; var dy = (double)far.Y - near.Y; var dz = (double)far.Z - near.Z;
        var length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (!double.IsFinite(length) || length <= 0) return false;
        ray = new(near, new((float)(dx / length), (float)(dy / length), (float)(dz / length)), length); return true;
    }
    internal static bool Finite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    private static double Dot(Vector3 a, Vector3 b) => (double)a.X * b.X + (double)a.Y * b.Y + (double)a.Z * b.Z;
}
