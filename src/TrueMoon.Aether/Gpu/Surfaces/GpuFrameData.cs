using System.Diagnostics;
using TrueMoon.Aether.Frames;

namespace TrueMoon.Aether.Gpu.Surfaces;

/// <summary>Runtime-produced RGBA8 upload or immutable GPU scene, with matching simulation metadata.</summary>
public sealed record GpuFrameData(byte[] Pixels, FrameMetadata Metadata)
{
    /// <summary>Optional GPU draw workload. Its pixel array must be empty; reference uploads omit this property.</summary>
    public Rendering.PrimitiveScene? Scene { get; init; }

    /// <summary>Captures a GPU-only scene and matching metadata; allocates no CPU pixel buffer.</summary>
    public static GpuFrameData FromScene(GpuFrameStamp stamp, ulong tick, Rendering.PrimitiveScene scene)
    {
        ArgumentNullException.ThrowIfNull(stamp);
        ArgumentNullException.ThrowIfNull(scene); scene.Validate();
        return new([], new(stamp.Slot, FrameNumber(stamp), tick, 0, Stopwatch.GetTimestamp())) { Scene = scene };
    }
    /// <summary>Monotonic frame number when the three persistent slots are produced in order.</summary>
    public static ulong FrameNumber(GpuFrameStamp stamp)
    { ArgumentNullException.ThrowIfNull(stamp); GpuSlotLedger.ValidateStamp(stamp, stamp.Identity); return checked((stamp.FrameId - 1) * 3 + (ulong)stamp.Slot + 1); }
    /// <summary>Validates shape and exact slot/use correspondence before native upload.</summary>
    public void Validate(GpuFrameStamp stamp, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(stamp); GpuSlotLedger.ValidateStamp(stamp, stamp.Identity); GpuSurfaceOffer.ValidateExtent(width, height);
        Scene?.CreateTransform(width, height);
        if (width == 0 || Pixels == null || Pixels.Length != (Scene == null ? checked(width * height * 4) : 0) || Metadata == null ||
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
