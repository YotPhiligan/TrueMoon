using System.Numerics;

namespace TrueMoon.Aether.Rendering;

/// <summary>Perspective geometry shared by World and external cameras; RH, row vectors, Vulkan depth [0,1].</summary>
/// <param name="FieldOfView">Vertical radians in (0,pi).</param>
/// <param name="Near">Positive near distance.</param><param name="Far">Finite far distance greater than near.</param>
public readonly record struct PerspectiveProjection(float FieldOfView, float Near, float Far)
{
    /// <summary>Gets the primitive preview's original perspective.</summary>
    public static PerspectiveProjection Default => new(MathF.PI / 3, .1f, 100);

    /// <summary>Rejects invalid settings before creating matrices.</summary>
    public void Validate() => ContentValidation.Require(float.IsFinite(FieldOfView) && FieldOfView > 0 && FieldOfView < MathF.PI &&
        float.IsFinite(Near) && float.IsFinite(Far) && Near > 0 && Far > Near,
        RenderContentError.InvalidCamera, "Invalid perspective settings.");

    /// <summary>Creates View*Projection, applying the Vulkan Y flip exactly once.</summary>
    /// <param name="cameraWorld">Finite proper rigid affine pose.</param>
    /// <param name="width">Physical width,16..1920.</param><param name="height">Physical height,16..1080.</param>
    /// <returns>Finite view/projection. A finite inverse is additionally required by <see cref="CameraProjection"/>.</returns>
    public Matrix4x4 CreateViewProjection(Matrix4x4 cameraWorld, int width, int height)
    {
        Validate();
        ContentValidation.Require(width is >= 16 and <= 1920 && height is >= 16 and <= 1080,
            RenderContentError.InvalidCamera, "A suspended/unsupported extent has no camera projection.");
        ValidatePose(cameraWorld);
        ContentValidation.Require(Matrix4x4.Invert(cameraWorld, out var view), RenderContentError.InvalidCamera, "Camera view is not invertible.");
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(FieldOfView, (float)width / height, Near, Far);
        projection.M22 = -projection.M22;
        var result = view * projection;
        ContentValidation.Require(ContentValidation.Finite(result), RenderContentError.InvalidCamera, "Camera view/projection is not representable.");
        return result;
    }

    internal static void ValidatePose(Matrix4x4 cameraWorld)
    {
        ContentValidation.Require(ContentValidation.Affine(cameraWorld), RenderContentError.InvalidCamera, "Invalid affine camera pose.");
        var x = new Vector3(cameraWorld.M11, cameraWorld.M12, cameraWorld.M13);
        var y = new Vector3(cameraWorld.M21, cameraWorld.M22, cameraWorld.M23);
        var z = new Vector3(cameraWorld.M31, cameraWorld.M32, cameraWorld.M33);
        const float tolerance = .0001f;
        ContentValidation.Require(Math.Abs(Dot(x, x) - 1) <= tolerance && Math.Abs(Dot(y, y) - 1) <= tolerance && Math.Abs(Dot(z, z) - 1) <= tolerance &&
            Math.Abs(Dot(x, y)) <= tolerance && Math.Abs(Dot(x, z)) <= tolerance && Math.Abs(Dot(y, z)) <= tolerance &&
            Math.Abs((double)cameraWorld.GetDeterminant() - 1) <= tolerance,
            RenderContentError.InvalidCamera, "Camera pose must be proper rigid without scale or shear.");
    }

    private static double Dot(Vector3 a, Vector3 b) => (double)a.X * b.X + (double)a.Y * b.Y + (double)a.Z * b.Z;
}
