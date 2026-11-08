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
        new PerspectiveProjection(FieldOfView, Near, Far).Validate();
        ContentValidation.Require(ContentValidation.UnitRange(ClearColorSrgb), RenderContentError.InvalidCamera, "Invalid clear color.");
    }
    /// <summary>Creates a finite RH row-vector Vulkan projection from an explicitly supplied rigid camera pose.</summary>
    /// <param name="cameraWorld">Finite proper rigid affine world matrix, without scale/shear/reflection.</param>
    /// <param name="width">Physical width,16..1920.</param><param name="height">Physical height,16..1080.</param><returns>View times Y-flipped projection, depth[0,1].</returns>
    public Matrix4x4 CreateViewProjection(Matrix4x4 cameraWorld, int width, int height)
    {
        Validate();
        return new PerspectiveProjection(FieldOfView, Near, Far).CreateViewProjection(cameraWorld, width, height);
    }
}
