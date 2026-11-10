using System.Numerics;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Rendering.Snapshots;

/// <summary>Explicit camera choice. Exactly one live entity or external rigid pose is selected.</summary>
/// <param name="Entity">Same-World camera, or default for an external camera.</param>
/// <param name="ExternalWorld">External proper rigid camera pose.</param><param name="ExternalSettings">External perspective settings.</param>
public readonly record struct RenderCameraSource(EntityHandle Entity, Matrix4x4 ExternalWorld, PerspectiveCamera ExternalSettings)
{
    /// <summary>Selects a live entity with PerspectiveCamera and current-tick WorldTransform.</summary>
    /// <param name="entity">Nondefault entity handle.</param><returns>Explicit entity source.</returns>
    public static RenderCameraSource FromEntity(EntityHandle entity)
    {
        if (entity == default) throw new ArgumentException("A camera entity is required.", nameof(entity));
        return new(entity, default, default);
    }
    /// <summary>Selects an external rigid camera, validated at capture.</summary>
    /// <param name="world">Camera world pose.</param><param name="settings">Perspective settings.</param><returns>External camera source.</returns>
    public static RenderCameraSource External(Matrix4x4 world, PerspectiveCamera settings) => new(default, world, settings);
}

/// <summary>Caller-versioned single-view settings. Zero by zero suspends publication; partial zero is invalid.</summary>
/// <param name="Camera">Explicit camera source.</param><param name="Width">Physical width.</param><param name="Height">Physical height.</param>
/// <param name="CameraRevision">Nonzero monotonic selection/external camera revision.</param>
/// <param name="SurfaceGeneration">Nonzero monotonic extent/surface generation; changed extent requires a new value.</param>
public sealed record RenderViewConfiguration(RenderCameraSource Camera, int Width, int Height, ulong CameraRevision = 1, ulong SurfaceGeneration = 1)
{
    internal bool Suspended => Width == 0 && Height == 0;
    internal void Validate()
    {
        if (CameraRevision == 0 || SurfaceGeneration == 0 || !(Suspended || Width is >= 16 and <= 1920 && Height is >= 16 and <= 1080))
            throw new ArgumentOutOfRangeException(nameof(RenderViewConfiguration), "Invalid view extent or generation.");
    }
}

/// <summary>Copied lower limits for one owned view. Definition buffers and native allocations are budgeted by their resource owners.</summary>
public sealed class RenderSnapshotOptions
{
    /// <summary>Visible draw ranges, after validating every renderer.</summary>
    public int MaximumDrawRanges { get; set; } = 4096;
    /// <summary>Accounted header plus actual draw-value array bytes per snapshot.</summary>
    public long MaximumPayloadBytes { get; set; } = 2 * 1024 * 1024;
    /// <summary>Sum of retained snapshot value buffers, including candidates and failed cleanup.</summary>
    public long MaximumRetainedBytes { get; set; } = 16 * 1024 * 1024;
    /// <summary>Distinct retained envelopes, including candidate/retired owners.</summary>
    public int MaximumSnapshots { get; set; } = 7;
    /// <summary>Accepted backend uses, including completed uses not yet collected.</summary>
    public int MaximumSubmissions { get; set; } = 3;
    internal RenderSnapshotOptions Snapshot()
    {
        if (MaximumDrawRanges is < 0 or > 4096 || MaximumPayloadBytes is < 0 or > 2 * 1024 * 1024 ||
            MaximumRetainedBytes is < 0 or > 16 * 1024 * 1024 || MaximumSnapshots is < 1 or > 7 || MaximumSubmissions is < 1 or > 3)
            throw new ArgumentOutOfRangeException(nameof(RenderSnapshotOptions));
        return new() { MaximumDrawRanges = MaximumDrawRanges, MaximumPayloadBytes = MaximumPayloadBytes,
            MaximumRetainedBytes = MaximumRetainedBytes, MaximumSnapshots = MaximumSnapshots, MaximumSubmissions = MaximumSubmissions };
    }
}

/// <summary>Typed owned-view factory request. Binding/World/device are borrowed; release the view before them.</summary>
/// <param name="Binding">Ready binding owned by the same host.</param><param name="SceneId">Persistent source scene.</param>
/// <param name="SourceRevision">Captured authoring revision, not a live World edit counter.</param>
/// <param name="Options">Optional copied lower limits.</param>
public sealed record RenderSnapshotViewRequest(Resources.RenderWorldBinding Binding, SceneId SceneId, long SourceRevision, RenderSnapshotOptions? Options = null);
