using System.Numerics;
using TrueMoon.Aether.Rendering;

namespace TrueMoon.Aether.Ecs.Rendering;

/// <summary>Unmanaged perspective settings. View selection is explicit; pose comes from current WorldTransform.</summary>
/// <param name="FieldOfView">Vertical radians in (0,pi).</param><param name="Near">Positive near distance.</param>
/// <param name="Far">Far distance greater than near.</param><param name="Layers">Visibility mask.</param>
/// <param name="ClearColorSrgb">Stored-sRGB background, not a linear tint.</param>
public readonly record struct PerspectiveCamera(float FieldOfView, float Near, float Far, uint Layers, Vector3 ClearColorSrgb)
{
    /// <summary>Gets the exact v1 default settings.</summary>
    public static PerspectiveCamera Default => new(MathF.PI / 3, .1f, 100, uint.MaxValue, new(.04f, .06f, .10f));
    /// <summary>Checks projection and color before matrix creation.</summary>
    public void Validate()
    {
        ContentValidation.Require(float.IsFinite(FieldOfView) && FieldOfView > 0 && FieldOfView < MathF.PI && float.IsFinite(Near) &&
            float.IsFinite(Far) && Near > 0 && Far > Near && ContentValidation.UnitRange(ClearColorSrgb),
            RenderContentError.InvalidCamera, "Invalid perspective or clear color.");
    }
    /// <summary>Creates a finite RH row-vector Vulkan projection from an explicitly supplied rigid camera pose.</summary>
    /// <param name="cameraWorld">Finite proper rigid affine world matrix, without scale/shear/reflection.</param>
    /// <param name="width">Physical width,16..1920.</param><param name="height">Physical height,16..1080.</param><returns>View times Y-flipped projection, depth[0,1].</returns>
    public Matrix4x4 CreateViewProjection(Matrix4x4 cameraWorld, int width, int height)
    {
        Validate();
        ContentValidation.Require(width is >= 16 and <= 1920 && height is >= 16 and <= 1080, RenderContentError.InvalidCamera, "A suspended/unsupported extent has no camera projection.");
        ContentValidation.Require(ContentValidation.Affine(cameraWorld), RenderContentError.InvalidCamera, "Invalid affine camera pose.");
        var x = new Vector3(cameraWorld.M11, cameraWorld.M12, cameraWorld.M13);
        var y = new Vector3(cameraWorld.M21, cameraWorld.M22, cameraWorld.M23);
        var z = new Vector3(cameraWorld.M31, cameraWorld.M32, cameraWorld.M33);
        const float tolerance = .0001f;
        ContentValidation.Require(Math.Abs(Dot(x, x) - 1) <= tolerance && Math.Abs(Dot(y, y) - 1) <= tolerance && Math.Abs(Dot(z, z) - 1) <= tolerance &&
            Math.Abs(Dot(x, y)) <= tolerance && Math.Abs(Dot(x, z)) <= tolerance && Math.Abs(Dot(y, z)) <= tolerance &&
            Math.Abs((double)cameraWorld.GetDeterminant() - 1) <= tolerance, RenderContentError.InvalidCamera, "Camera pose must be proper rigid without scale or shear.");
        ContentValidation.Require(Matrix4x4.Invert(cameraWorld, out var view), RenderContentError.InvalidCamera, "Camera view is not invertible.");
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(FieldOfView, (float)width / height, Near, Far);
        projection.M22 = -projection.M22;
        var result = view * projection;
        ContentValidation.Require(ContentValidation.Finite(result), RenderContentError.InvalidCamera, "Camera view/projection is not representable.");
        return result;
    }
    private static double Dot(Vector3 a, Vector3 b) => (double)a.X * b.X + (double)a.Y * b.Y + (double)a.Z * b.Z;
}
