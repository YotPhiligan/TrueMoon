using System.Collections.Immutable;
using System.Numerics;
using TrueMoon.Aether.Rendering;

namespace TrueMoon.Aether.Editor.Translation;

/// <summary>Frozen CPU-only world gizmo projected through one actual view. Does not own UI or render resources.</summary>
public sealed class TranslationGeometry
{
    private TranslationGeometry(CameraProjection projection, ViewportImageBounds image, Vector3 origin,
        float length, ImmutableArray<TranslationHandleGeometry> handles)
    { Projection = projection; Image = image; Origin = origin; Length = length; Handles = handles; }
    /// <summary>Frozen camera and physical extent.</summary>
    public CameraProjection Projection { get; }
    /// <summary>Actual logical image rectangle.</summary>
    public ViewportImageBounds Image { get; }
    /// <summary>Original world-space pivot.</summary>
    public Vector3 Origin { get; }
    /// <summary>World distance corresponding to a72logical-pixel camera-right segment at the pivot depth.</summary>
    public float Length { get; }
    /// <summary>Nondegenerate projected constraints, axes before planes.</summary>
    public ImmutableArray<TranslationHandleGeometry> Handles { get; }

    /// <summary>Captures a visible finite pivot and excludes degenerate constraints.</summary>
    /// <param name="projection">Actual camera.</param><param name="image">Actual logical image.</param>
    /// <param name="origin">World pivot.</param><param name="geometry">Detached frozen geometry.</param>
    /// <returns>False when the pivot/image/math is unavailable.</returns>
    public static bool TryCreate(CameraProjection projection, ViewportImageBounds image, Vector3 origin, out TranslationGeometry? geometry)
    {
        ArgumentNullException.ThrowIfNull(projection); geometry = null;
        if (!image.TryMapPixel(image.Left, image.Top, projection.Width, projection.Height, out _, out _) ||
            !projection.TryProject(origin, out var ndc) || ndc.Z is < 0 or > 1 ||
            !projection.TryUnproject(ndc with { X = (float)(ndc.X + 144 / image.Width) }, out var side)) return false;
        var pivot = Screen(ndc, image);
        if (!image.TryMapPixel(pivot.X, pivot.Y, projection.Width, projection.Height, out _, out _)) return false;
        var length = Vector3.Distance(origin, side);
        if (!float.IsFinite(length) || length <= 0) return false;
        var handles = ImmutableArray.CreateBuilder<TranslationHandleGeometry>();
        foreach (var kind in new[] { TranslationHandle.X, TranslationHandle.Y, TranslationHandle.Z })
        {
            if (!Project(origin + Axis(kind) * length, projection, image, out var end)) continue;
            var distance = Distance(pivot, end);
            if (distance < 18) continue;
            handles.Add(new(kind, [Lerp(pivot, end, .18), end]));
        }
        foreach (var kind in new[] { TranslationHandle.XY, TranslationHandle.XZ, TranslationHandle.YZ })
        {
            var (a, b) = PlaneAxes(kind); var corners = ImmutableArray.CreateBuilder<TranslationPoint>();
            foreach (var (u, v) in new[] { (.18f, .18f), (.38f, .18f), (.38f, .38f), (.18f, .38f) })
                if (Project(origin + length * (a * u + b * v), projection, image, out var corner)) corners.Add(corner);
            if (corners.Count != 4 || Math.Abs(Area(corners)) < 16) continue;
            handles.Add(new(kind, corners.ToImmutable()));
        }
        geometry = new(projection, image, origin, length, handles.ToImmutable()); return true;
    }

    /// <summary>Finds the nearest axis within6logical pixels, then an interior plane; never clamps outside input.</summary>
    /// <param name="x">Logical pointer X.</param><param name="y">Logical pointer Y.</param>
    /// <returns>A nondegenerate constraint, or None. The caller additionally checks clip, chrome and context.</returns>
    public TranslationHandle Hit(double x, double y)
    {
        if (!Image.TryMapPixel(x, y, Projection.Width, Projection.Height, out _, out _)) return TranslationHandle.None;
        var best = TranslationHandle.None; var nearest = 6d;
        foreach (var handle in Handles.Where(h => h.Points.Length == 2))
        {
            var a = handle.Points[0]; var b = handle.Points[1]; var dx = b.X - a.X; var dy = b.Y - a.Y;
            var t = Math.Clamp(((x - a.X) * dx + (y - a.Y) * dy) / (dx * dx + dy * dy), 0, 1);
            var distance = Distance(new(x, y), Lerp(a, b, t));
            if (distance <= nearest && (best == TranslationHandle.None || distance < nearest)) { nearest = distance; best = handle.Kind; }
        }
        if (best != TranslationHandle.None) return best;
        foreach (var handle in Handles.Where(h => h.Points.Length == 4))
        {
            bool? positive = null; var inside = true;
            for (var i = 0; i < 4; i++)
            {
                var a = handle.Points[i]; var b = handle.Points[(i + 1) % 4];
                var cross = (b.X - a.X) * (y - a.Y) - (b.Y - a.Y) * (x - a.X);
                if (cross == 0) continue;
                if (positive.HasValue && positive.Value != (cross > 0)) { inside = false; break; }
                positive = cross > 0;
            }
            if (inside) return handle.Kind;
        }
        return TranslationHandle.None;
    }

    /// <summary>Projects a world point in the depth range to logical coordinates, allowing off-image X/Y.</summary>
    /// <param name="point">World point.</param><param name="projection">Frozen projection.</param>
    /// <param name="image">Actual logical rectangle.</param><param name="screen">Projected point.</param><returns>Whether math/depth are usable.</returns>
    public static bool Project(Vector3 point, CameraProjection projection, ViewportImageBounds image, out TranslationPoint screen)
    {
        screen = default;
        if (!projection.TryProject(point, out var ndc) || ndc.Z is < 0 or > 1) return false;
        screen = Screen(ndc, image); return double.IsFinite(screen.X) && double.IsFinite(screen.Y);
    }
    private static TranslationPoint Screen(Vector3 ndc, ViewportImageBounds image) => new(image.Left + (ndc.X + 1d) * image.Width / 2, image.Top + (ndc.Y + 1d) * image.Height / 2);
    private static TranslationPoint Lerp(TranslationPoint a, TranslationPoint b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    private static double Distance(TranslationPoint a, TranslationPoint b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    private static double Area(IReadOnlyList<TranslationPoint> points)
    { double area = 0; for (var i = 0; i < points.Count; i++) { var a = points[i]; var b = points[(i + 1) % points.Count]; area += a.X * b.Y - b.X * a.Y; } return area / 2; }
    internal static Vector3 Axis(TranslationHandle kind) => kind switch { TranslationHandle.X => Vector3.UnitX, TranslationHandle.Y => Vector3.UnitY, TranslationHandle.Z => Vector3.UnitZ, _ => default };
    internal static (Vector3 A, Vector3 B) PlaneAxes(TranslationHandle kind) => kind switch
    { TranslationHandle.XY => (Vector3.UnitX, Vector3.UnitY), TranslationHandle.XZ => (Vector3.UnitX, Vector3.UnitZ), TranslationHandle.YZ => (Vector3.UnitY, Vector3.UnitZ), _ => default };
}
