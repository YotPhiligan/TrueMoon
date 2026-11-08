using System.Numerics;

namespace TrueMoon.Aether.Rendering;

/// <summary>Frozen finite projection/inverse and physical extent for one camera capture.</summary>
public sealed class CameraProjection
{
    /// <summary>Creates a projection with a usable Single inverse; ray endpoints are validated independently.</summary>
    /// <param name="perspective">Shared perspective settings.</param><param name="cameraWorld">Proper rigid pose.</param>
    /// <param name="width">Physical width,16..1920.</param><param name="height">Physical height,16..1080.</param>
    public CameraProjection(PerspectiveProjection perspective, Matrix4x4 cameraWorld, int width, int height)
    {
        ViewProjection = perspective.CreateViewProjection(cameraWorld, width, height);
        ContentValidation.Require(ContentValidation.TryInvertFinite(ViewProjection, out var inverse),
            RenderContentError.InvalidCamera, "Camera view/projection has no representable inverse.");
        InverseViewProjection = inverse; Width = width; Height = height;
    }

    /// <summary>Gets the exact row-vector view/projection.</summary>
    public Matrix4x4 ViewProjection { get; }
    /// <summary>Gets the validated inverse.</summary>
    public Matrix4x4 InverseViewProjection { get; }
    /// <summary>Gets physical width.</summary>
    public int Width { get; }
    /// <summary>Gets physical height.</summary>
    public int Height { get; }

    /// <summary>Projects a finite point in front of the camera to NDC; coordinates may lie outside the clip volume.</summary>
    /// <param name="world">World point.</param><param name="ndc">Finite NDC, or default on failure.</param><returns>False at/behind the camera or on numeric failure.</returns>
    public bool TryProject(Vector3 world, out Vector3 ndc) => Transform(world, ViewProjection, true, out ndc);

    /// <summary>Unprojects finite NDC with depth [0,1]; X/Y may lie outside the image.</summary>
    /// <param name="ndc">NDC point with Vulkan depth.</param><param name="world">Finite world point, or default.</param>
    /// <returns>False on invalid depth, zero homogeneous W or unrepresentable result.</returns>
    public bool TryUnproject(Vector3 ndc, out Vector3 world)
    {
        world = default;
        return ndc.Z is >= 0 and <= 1 && Transform(ndc, InverseViewProjection, false, out world);
    }

    /// <summary>Creates a normalized near-to-far world ray through the center of a physical pixel.</summary>
    /// <param name="pixelX">Column in [0,Width).</param><param name="pixelY">Row in [0,Height).</param>
    /// <param name="ray">Bounded world-distance ray, or default on failure.</param><returns>False for outside pixels or invalid unprojection.</returns>
    public bool TryCreateRay(int pixelX, int pixelY, out Ray3 ray)
    {
        ray = default;
        if (pixelX < 0 || pixelX >= Width || pixelY < 0 || pixelY >= Height) return false;
        var x = (float)(2 * (pixelX + .5d) / Width - 1);
        var y = (float)(2 * (pixelY + .5d) / Height - 1);
        if (!TryUnproject(new(x, y, 0), out var near) || !TryUnproject(new(x, y, 1), out var far)) return false;
        return Ray3.TryFromSegment(near, far, out ray);
    }

    private static bool Transform(Vector3 point, Matrix4x4 matrix, bool positiveW, out Vector3 result)
    {
        result = default;
        if (!ContentValidation.Finite(point)) return false;
        var w = (double)point.X * matrix.M14 + (double)point.Y * matrix.M24 + (double)point.Z * matrix.M34 + matrix.M44;
        if (!double.IsFinite(w) || (positiveW ? w <= 0 : w == 0)) return false;
        var candidate = new Vector3(
            (float)(((double)point.X * matrix.M11 + (double)point.Y * matrix.M21 + (double)point.Z * matrix.M31 + matrix.M41) / w),
            (float)(((double)point.X * matrix.M12 + (double)point.Y * matrix.M22 + (double)point.Z * matrix.M32 + matrix.M42) / w),
            (float)(((double)point.X * matrix.M13 + (double)point.Y * matrix.M23 + (double)point.Z * matrix.M33 + matrix.M43) / w));
        if (!ContentValidation.Finite(candidate)) return false;
        result = candidate; return true;
    }
}
