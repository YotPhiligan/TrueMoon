using System.Numerics;
using TrueMoon.Aether.Rendering;

namespace TrueMoon.Aether.Ecs.Rendering;

/// <summary>Orthographic view settings. The camera pose comes from the current WorldTransform.</summary>
/// <param name="VerticalSize">Full visible height in world units, independent of the physical extent.</param>
/// <param name="Near">Positive near distance.</param>
/// <param name="Far">Far distance greater than near.</param>
/// <param name="Layers">Visibility mask.</param>
/// <param name="ClearColorSrgb">Stored-sRGB background.</param>
public readonly record struct OrthographicCamera(float VerticalSize, float Near, float Far, uint Layers, Vector3 ClearColorSrgb)
{
    /// <summary>Checks projection and color before matrix creation.</summary>
    public void Validate()
    {
        ContentValidation.Require(float.IsFinite(VerticalSize) && VerticalSize > 0 &&
            float.IsFinite(Near) && float.IsFinite(Far) && Near > 0 && Far > Near,
            RenderContentError.InvalidCamera, "Invalid orthographic settings.");
        ContentValidation.Require(ContentValidation.UnitRange(ClearColorSrgb), RenderContentError.InvalidCamera, "Invalid clear color.");
    }

    /// <summary>Creates a right-handed row-vector projection with Vulkan depth [0,1] and one Y flip.</summary>
    /// <param name="cameraWorld">Proper rigid camera pose.</param>
    /// <param name="width">Physical width, 16 through 1920.</param>
    /// <param name="height">Physical height, 16 through 1080.</param>
    /// <returns>View times projection for this extent.</returns>
    public Matrix4x4 CreateViewProjection(Matrix4x4 cameraWorld, int width, int height)
    {
        Validate();
        ContentValidation.Require(width is >= 16 and <= 1920 && height is >= 16 and <= 1080,
            RenderContentError.InvalidCamera, "A suspended/unsupported extent has no camera projection.");
        PerspectiveProjection.ValidatePose(cameraWorld);
        ContentValidation.Require(Matrix4x4.Invert(cameraWorld, out var view),
            RenderContentError.InvalidCamera, "Camera view is not invertible.");

        var horizontalSize = (float)((double)VerticalSize * width / height);
        ContentValidation.Require(float.IsFinite(horizontalSize) && horizontalSize > 0,
            RenderContentError.InvalidCamera, "Orthographic width is not representable.");
        var projection = Matrix4x4.CreateOrthographic(horizontalSize, VerticalSize, Near, Far);
        projection.M22 = -projection.M22;
        var result = view * projection;
        ContentValidation.Require(projection.M11 > 0 && projection.M22 < 0 && projection.M33 < 0 &&
            ContentValidation.Finite(result), RenderContentError.InvalidCamera,
            "Camera view/projection is not representable.");
        return result;
    }
}
