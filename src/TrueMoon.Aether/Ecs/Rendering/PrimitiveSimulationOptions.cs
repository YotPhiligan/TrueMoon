using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Rendering;

namespace TrueMoon.Aether.Ecs.Rendering;

/// <summary>Composition-time limits, fixed step and fixed render-view camera. Values are copied before use.</summary>
public sealed class PrimitiveSimulationOptions
{
    /// <summary>Maximum transform nodes,1..1048576. Default65536; admission count, not a byte quota.</summary>
    public int MaximumTransformEntities { get; set; } = 65536;
    /// <summary>Maximum enabled drawables,0..64, independent of logical/group entity count.</summary>
    public int MaximumPrimitives { get; set; } = PrimitiveScene.MaximumPrimitives;
    /// <summary>Caller-driven positive fixed duration, at most one second.</summary>
    public TimeSpan FixedStep { get; set; } = TimeSpan.FromSeconds(1d / 60);
    /// <summary>World-space render-view camera; no camera component or automatic selection is implied.</summary>
    public CameraTransform Camera { get; set; } = new(0, 1, 6, 0, 0, 0);
    /// <summary>Vertical field of view in radians.</summary>
    public float FieldOfView { get; set; } = MathF.PI / 3;
    /// <summary>Positive near plane.</summary>
    public float Near { get; set; } = 0.1f;
    /// <summary>Finite far plane greater than Near.</summary>
    public float Far { get; set; } = 100;

    internal PrimitiveSimulationOptions Snapshot()
    {
        if (MaximumTransformEntities is < 1 or > 1048576) throw new ArgumentOutOfRangeException(nameof(MaximumTransformEntities));
        if (MaximumPrimitives is < 0 or > PrimitiveScene.MaximumPrimitives) throw new ArgumentOutOfRangeException(nameof(MaximumPrimitives));
        if (FixedStep <= TimeSpan.Zero || FixedStep > TimeSpan.FromSeconds(1)) throw new ArgumentOutOfRangeException(nameof(FixedStep));
        var copy = new PrimitiveSimulationOptions { MaximumTransformEntities = MaximumTransformEntities, MaximumPrimitives = MaximumPrimitives,
            FixedStep = FixedStep, Camera = Camera, FieldOfView = FieldOfView, Near = Near, Far = Far };
        copy.Scene([]).CreateViewProjection(16, 16); return copy;
    }
    internal PrimitiveScene Scene(IEnumerable<RenderPrimitive> values) => new(Camera, values) { FieldOfView = FieldOfView, Near = Near, Far = Far };
}
