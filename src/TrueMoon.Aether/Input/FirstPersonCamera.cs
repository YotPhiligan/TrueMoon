using System.Numerics;
using TrueMoon.Aether.Checkpoints;

namespace TrueMoon.Aether.Input;

/// <summary>Deterministic first-person camera motion on the XZ plane; +Y up, camera forward -Z.</summary>
public static class FirstPersonCamera
{
    /// <summary>Fixed simulation step in seconds.</summary>
    public const float TickSeconds = 1f / 60;
    /// <summary>Movement speed in world units per second.</summary>
    public const float Speed = 3;
    /// <summary>Pitch limit keeps the camera away from its vertical singularity.</summary>
    public const float MaximumPitch = MathF.PI / 2 - .01f;

    /// <summary>Applies bounded look and normalized WASD movement for exactly one fixed tick.</summary>
    /// <param name="camera">Current checkpoint-compatible pose.</param><param name="input">Input for this tick.</param>
    /// <returns>Validated new pose; never mutates the original value.</returns>
    public static CameraTransform Step(CameraTransform camera, GameInputTick input)
    {
        camera.Validate(); input.Validate();
        var yaw = MathF.IEEERemainder(camera.Yaw + input.Yaw, MathF.Tau);
        var pitch = Math.Clamp(camera.Pitch + input.Pitch, -MaximumPitch, MaximumPitch);
        var forward = ((input.Buttons & GameButtons.Forward) != 0 ? 1 : 0) - ((input.Buttons & GameButtons.Backward) != 0 ? 1 : 0);
        var right = ((input.Buttons & GameButtons.Right) != 0 ? 1 : 0) - ((input.Buttons & GameButtons.Left) != 0 ? 1 : 0);
        var direction = new Vector2(right, forward); if (direction.LengthSquared() > 1) direction = Vector2.Normalize(direction);
        var distance = Speed * TickSeconds;
        var result = camera with { X = camera.X + (MathF.Cos(yaw) * direction.X - MathF.Sin(yaw) * direction.Y) * distance,
            Z = camera.Z + (-MathF.Sin(yaw) * direction.X - MathF.Cos(yaw) * direction.Y) * distance, Yaw = yaw, Pitch = pitch };
        result.Validate(); return result;
    }
}
