namespace TrueMoon.Aether.Gpu;

/// <summary>Deterministic asymmetric sRGB-encoded premultiplied RGBA bytes for the import proof, row zero is top.</summary>
public static class GpuReferencePattern
{
    /// <summary>Creates a bounded whole-image reference with distinct channels, alpha and orientation markers.</summary>
    public static byte[] Create(int width, int height)
        => CreateCore(width, height, 0);
    /// <summary>Creates frame-dependent pixels so slot/surface reuse cannot pass with stale content.</summary>
    public static byte[] Create(int width, int height, GpuFrameStamp stamp)
    {
        Surfaces.GpuSlotLedger.ValidateStamp(stamp, stamp.Identity);
        var marker = 1 + (int)((stamp.FrameId - 1) % 251) * 17 + stamp.Slot * 29 + (int)(stamp.SurfaceGeneration % 251) * 11;
        return CreateCore(width, height, marker);
    }
    private static byte[] CreateCore(int width, int height, int marker)
    {
        if (width is < 16 or > 1920 || height is < 16 or > 1080) throw new ArgumentOutOfRangeException(nameof(width));
        var pixels = new byte[checked(width * height * 4)];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var offset = (y * width + x) * 4; var alpha = (x / 8 + y / 8) % 2 == 0 ? 255 : 128;
            pixels[offset] = (byte)((((x * 7 + y * 3 + 17 + marker) & 255) * alpha + 127) / 255);
            pixels[offset + 1] = (byte)((((x + y * 11 + 31 + marker * 3) & 255) * alpha + 127) / 255);
            pixels[offset + 2] = (byte)((((x * 13 + y + 73 + marker * 7) & 255) * alpha + 127) / 255);
            pixels[offset + 3] = (byte)alpha;
        }
        return pixels;
    }
}
