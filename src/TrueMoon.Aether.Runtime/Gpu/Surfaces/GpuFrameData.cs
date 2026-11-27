using System.Collections.Immutable;
using System.Diagnostics;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Aether.Runtime.Rendering.Snapshots;

namespace TrueMoon.Aether.Runtime.Gpu.Surfaces;

/// <summary>Runtime-produced RGBA8 upload or immutable GPU scene, with matching simulation metadata.</summary>
public sealed record GpuFrameData(byte[] Pixels, FrameMetadata Metadata)
{
    /// <summary>Optional GPU draw workload. Its pixel array must be empty; reference uploads omit this property.</summary>
    public PrimitiveScene? Scene { get; init; }
    /// <summary>Owned f.3 reservation, exclusive with Scene/CPU pixels; transfers to the producer until submitted or discarded.</summary>
    public RenderSnapshotRecording? Snapshot { get; init; }
    internal ImmutableArray<RenderPrimitive> PreviewOverlay { get; private init; }
    private PreviewFrameStamp? ExpectedPreview { get; init; }
    private GpuFrameStamp? PreviewTarget { get; init; }
    private RenderSnapshotRecording? PreviewRecording { get; init; }

    internal static GpuFrameData FromPreviewSnapshot(GpuFrameStamp stamp, RenderSnapshotRecording recording,
        PreviewViewState view, ImmutableArray<RenderPrimitive> overlay)
    {
        ArgumentNullException.ThrowIfNull(recording);
        ArgumentNullException.ThrowIfNull(view);
        var data = recording.Snapshot;
        if (data.CameraWorld != view.Camera.World || data.Configuration.CameraRevision != view.Camera.Revision ||
            data.SceneId.Value != view.Context.SceneId || data.SourceRevision != view.Context.SceneRevision || data.Tick != 0)
            throw new InvalidDataException("Preview reservation does not describe the captured scene and camera.");
        if (!overlay.IsDefaultOrEmpty && overlay.Length > PrimitiveScene.MaximumPrimitives)
            throw new InvalidDataException("Too many preview primitives.");
        var preview = view.Stamp;
        return new([], new(stamp.Slot, FrameNumber(stamp), 0, 0, Stopwatch.GetTimestamp()) { PreviewView = preview })
        {
            Snapshot = recording, PreviewOverlay = overlay, ExpectedPreview = preview, PreviewTarget = stamp,
            PreviewRecording = recording
        };
    }
    /// <summary>Transfers a live mesh reservation into the explicit GPU-only frame profile.</summary>
    public static GpuFrameData FromSnapshot(GpuFrameStamp stamp, RenderSnapshotRecording recording)
    {
        ArgumentNullException.ThrowIfNull(stamp); ArgumentNullException.ThrowIfNull(recording);
        var data = recording.Snapshot;
        return new([], new(stamp.Slot, FrameNumber(stamp), data.Tick, 0, Stopwatch.GetTimestamp())
            { RenderView = RenderSnapshotStamp.FromSnapshot(data) }) { Snapshot = recording };
    }

    /// <summary>Captures a GPU-only scene and matching metadata; allocates no CPU pixel buffer.</summary>
    public static GpuFrameData FromScene(GpuFrameStamp stamp, ulong tick, PrimitiveScene scene)
    {
        ArgumentNullException.ThrowIfNull(stamp);
        ArgumentNullException.ThrowIfNull(scene); scene.Validate();
        return new([], new(stamp.Slot, FrameNumber(stamp), tick, 0, Stopwatch.GetTimestamp()) { PreviewView = scene.PreviewView?.Stamp }) { Scene = scene };
    }
    /// <summary>Monotonic frame number when the three persistent slots are produced in order.</summary>
    public static ulong FrameNumber(GpuFrameStamp stamp)
    { ArgumentNullException.ThrowIfNull(stamp); GpuSlotLedger.ValidateStamp(stamp, stamp.Identity); return checked((stamp.FrameId - 1) * 3 + (ulong)stamp.Slot + 1); }
    /// <summary>Validates shape and exact slot/use correspondence before native upload.</summary>
    public void Validate(GpuFrameStamp stamp, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(stamp); GpuSlotLedger.ValidateStamp(stamp, stamp.Identity); GpuSurfaceOffer.ValidateExtent(width, height);
        Scene?.CreateViewProjection(width, height);
        Metadata?.RenderView?.Validate();
        if (Snapshot != null && (Scene != null || Snapshot.IsReleased || Snapshot.Snapshot.Configuration.Width != width ||
            Snapshot.Snapshot.Configuration.Height != height || Snapshot.Snapshot.Identity.SessionId != stamp.Identity.SessionId ||
            Snapshot.Snapshot.Configuration.SurfaceGeneration != stamp.SurfaceGeneration ||
            (ExpectedPreview == null && Metadata?.RenderView != RenderSnapshotStamp.FromSnapshot(Snapshot.Snapshot)) ||
            (ExpectedPreview != null && (Metadata?.RenderView != null || PreviewTarget != stamp || Metadata?.Tick != 0 ||
                !ReferenceEquals(Snapshot, PreviewRecording)))) ||
            Snapshot == null && (Metadata?.RenderView != null || ExpectedPreview != null))
            throw new InvalidDataException("Mesh reservation/metadata/target mismatch.");
        Metadata?.PreviewView?.Validate();
        if (Metadata?.PreviewView != (ExpectedPreview ?? Scene?.PreviewView?.Stamp) ||
            ExpectedPreview != null && Snapshot is { } recording &&
            (recording.Snapshot.SceneId.Value != ExpectedPreview.SceneId ||
                recording.Snapshot.SourceRevision != ExpectedPreview.SceneRevision ||
                recording.Snapshot.Configuration.CameraRevision != ExpectedPreview.CameraRevision))
            throw new InvalidDataException("Preview metadata must match the captured render scene.");
        if (width == 0 || Pixels == null || Pixels.Length != (Scene == null && Snapshot == null ? checked(width * height * 4) : 0) || Metadata == null ||
            Metadata.Slot != stamp.Slot || Metadata.FrameId != FrameNumber(stamp) || Metadata.ProducedTimestamp <= 0)
            throw new InvalidDataException("GPU frame pixels/metadata do not match the produced slot.");
    }
    /// <summary>Creates the reference game pattern using an atomically captured tick and selected game's color.</summary>
    /// <remarks>This uploads a CPU-generated reference pattern through Vulkan staging; a 3D renderer is a later stage.</remarks>
    public static GpuFrameData GamePattern(GpuFrameStamp stamp, int width, int height, ulong tick, uint color)
    {
        ArgumentNullException.ThrowIfNull(stamp); GpuSlotLedger.ValidateStamp(stamp, stamp.Identity); GpuSurfaceOffer.ValidateExtent(width, height);
        if (width == 0) throw new ArgumentOutOfRangeException(nameof(width));
        var measured = FrameTrace.Log.Begin();
        var frame = FrameNumber(stamp); var pixels = new byte[checked(width * height * 4)];
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
        {
            var pixel = FramePattern.Pixel(x, y, width, height, frame, tick, color); var offset = (y * width + x) * 4;
            pixels[offset] = (byte)(pixel >> 16); pixels[offset + 1] = (byte)(pixel >> 8); pixels[offset + 2] = (byte)pixel; pixels[offset + 3] = 255;
        }
        var result = new GpuFrameData(pixels, new(stamp.Slot, frame, tick, 0, Stopwatch.GetTimestamp()));
        FrameTrace.Log.Complete("pattern", stamp.Identity, stamp.SurfaceGeneration, frame, measured);
        return result;
    }
}

/// <summary>CPU-observed ready fence and optional runtime metadata; diagnostic reference producers omit metadata.</summary>
public sealed record GpuProducedFrame(GpuFrameCompletion Completion, FrameMetadata? Metadata);
