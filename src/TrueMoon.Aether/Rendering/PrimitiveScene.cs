using System.Numerics;
using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Gpu.Surfaces;
using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Rendering;

/// <summary>Optional observational rendering capability, captured at a serialized simulation boundary.</summary>
public interface IRenderGameSession : IGameSession
{
    /// <summary>Captures the current cube, camera and opaque stored-sRGB material without mutating the game.</summary>
    PrimitiveScene CaptureRenderScene();
}

/// <summary>First renderer contract: unit cube centered at (0,1,0), right-handed world, +Y up, camera forward -Z.</summary>
/// <param name="Camera">Position and pitch/yaw/roll radians, compatible with the checkpoint pose.</param>
/// <param name="Rotation">Cube rotation around world Y in radians.</param>
/// <param name="Color">Opaque material RGB in stored-sRGB space, each component in [0,1].</param>
public sealed record PrimitiveScene(CameraTransform Camera, float Rotation, Vector3 Color)
{
    /// <summary>Vertical field of view in radians.</summary>
    public float FieldOfView { get; init; } = MathF.PI / 3;
    /// <summary>Positive near plane distance.</summary>
    public float Near { get; init; } = 0.1f;
    /// <summary>Far plane distance, greater than the near plane.</summary>
    public float Far { get; init; } = 100;

    /// <summary>Rejects invalid state before recording any native command.</summary>
    public void Validate()
    {
        Camera.Validate();
        if (!float.IsFinite(Rotation) || !float.IsFinite(FieldOfView) || FieldOfView <= 0 || FieldOfView >= MathF.PI ||
            !float.IsFinite(Near) || !float.IsFinite(Far) || Near <= 0 || Far <= Near ||
            !ValidColor(Color.X) || !ValidColor(Color.Y) || !ValidColor(Color.Z))
            throw new InvalidDataException("Invalid primitive scene camera/material.");
    }

    /// <summary>Row-vector model/view/projection, Vulkan depth [0,1], top-left image origin.</summary>
    /// <param name="width">Physical image width.</param><param name="height">Physical image height.</param>
    /// <returns>Matrix uploaded as the equivalent column-major GLSL transform.</returns>
    public Matrix4x4 CreateTransform(int width, int height)
    {
        Validate(); GpuSurfaceOffer.ValidateExtent(width, height);
        if (width == 0) throw new ArgumentOutOfRangeException(nameof(width));
        var position = new Vector3(Camera.X, Camera.Y, Camera.Z);
        var orientation = Quaternion.CreateFromYawPitchRoll(Camera.Yaw, Camera.Pitch, Camera.Roll);
        var view = Matrix4x4.CreateLookAt(position, position + Vector3.Transform(-Vector3.UnitZ, orientation), Vector3.Transform(Vector3.UnitY, orientation));
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(FieldOfView, (float)width / height, Near, Far);
        projection.M22 = -projection.M22;
        var result = Matrix4x4.CreateRotationY(Rotation) * Matrix4x4.CreateTranslation(0, 1, 0) * view * projection;
        // Finite input alone does not exclude overflow/catastrophic cancellation at extreme camera positions.
        ReadOnlySpan<float> values = stackalloc float[] { result.M11, result.M12, result.M13, result.M14, result.M21, result.M22, result.M23, result.M24,
                     result.M31, result.M32, result.M33, result.M34, result.M41, result.M42, result.M43, result.M44 };
        foreach (var value in values)
            if (!float.IsFinite(value)) throw new InvalidDataException("Camera transform is not representable.");
        return result;
    }
    private static bool ValidColor(float value) => float.IsFinite(value) && value is >= 0 and <= 1;
}
