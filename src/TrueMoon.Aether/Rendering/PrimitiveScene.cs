using System.Numerics;
using System.Collections.Immutable;
using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Gpu.Surfaces;
using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Rendering;

/// <summary>Optional observational rendering capability, captured at a serialized simulation boundary.</summary>
public interface IRenderGameSession : IGameSession
{
    /// <summary>Captures the current primitives and camera without mutating the game.</summary>
    PrimitiveScene CaptureRenderScene();
}

/// <summary>First renderer contract: unit cube centered at (0,1,0), right-handed world, +Y up, camera forward -Z.</summary>
/// <param name="Camera">Position and pitch/yaw/roll radians, compatible with the checkpoint pose.</param>
/// <param name="Rotation">Cube rotation around world Y in radians.</param>
/// <param name="Color">Opaque material RGB in stored-sRGB space, each component in [0,1].</param>
public sealed record PrimitiveScene(CameraTransform Camera, float Rotation, Vector3 Color)
{
    /// <summary>Maximum cubes per frame, matching the bounded runtime scene.</summary>
    public const int MaximumPrimitives = 64;
    private readonly ImmutableArray<RenderPrimitive> _primitives;
    /// <summary>Creates an immutable frame of explicit world-space cubes; an empty list draws only the background.</summary>
    /// <param name="camera">Observational camera pose.</param><param name="primitives">At most 64 validated cubes, copied before publication.</param>
    public PrimitiveScene(CameraTransform camera, IEnumerable<RenderPrimitive> primitives) : this(camera, 0, Vector3.Zero)
    {
        ArgumentNullException.ThrowIfNull(primitives);
        _primitives = primitives.Take(MaximumPrimitives + 1).ToImmutableArray(); Validate();
    }
    /// <summary>Gets explicit primitives, or the original compatibility cube for the three-argument constructor.</summary>
    public ImmutableArray<RenderPrimitive> Primitives => _primitives.IsDefault
        ? [new(Matrix4x4.CreateRotationY(Rotation) * Matrix4x4.CreateTranslation(0, 1, 0), Color)] : _primitives;
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
        if (!_primitives.IsDefault)
        {
            if (_primitives.Length > MaximumPrimitives) throw new InvalidDataException("Too many primitives.");
            foreach (var primitive in _primitives) primitive.Validate();
        }
    }

    /// <summary>Row-vector model/view/projection, Vulkan depth [0,1], top-left image origin.</summary>
    /// <param name="width">Physical image width.</param><param name="height">Physical image height.</param>
    /// <returns>Matrix uploaded as the equivalent column-major GLSL transform.</returns>
    public Matrix4x4 CreateTransform(int width, int height)
    {
        var result = Matrix4x4.CreateRotationY(Rotation) * Matrix4x4.CreateTranslation(0, 1, 0) * CreateViewProjection(width, height);
        RenderPrimitive.ValidateMatrix(result); return result;
    }

    /// <summary>Creates the shared Vulkan view/projection for explicit world-space primitives.</summary>
    /// <param name="width">Physical width.</param><param name="height">Physical height.</param>
    public Matrix4x4 CreateViewProjection(int width, int height)
    {
        Validate(); GpuSurfaceOffer.ValidateExtent(width, height);
        if (width == 0) throw new ArgumentOutOfRangeException(nameof(width));
        var position = new Vector3(Camera.X, Camera.Y, Camera.Z);
        var orientation = Quaternion.CreateFromYawPitchRoll(Camera.Yaw, Camera.Pitch, Camera.Roll);
        var view = Matrix4x4.CreateLookAt(position, position + Vector3.Transform(-Vector3.UnitZ, orientation), Vector3.Transform(Vector3.UnitY, orientation));
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(FieldOfView, (float)width / height, Near, Far);
        projection.M22 = -projection.M22;
        var result = view * projection;
        // Finite input alone does not exclude overflow/catastrophic cancellation at extreme camera positions.
        RenderPrimitive.ValidateMatrix(result);
        return result;
    }
    private static bool ValidColor(float value) => float.IsFinite(value) && value is >= 0 and <= 1;
}
