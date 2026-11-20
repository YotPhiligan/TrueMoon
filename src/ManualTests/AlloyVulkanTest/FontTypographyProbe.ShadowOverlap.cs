using System.Runtime.InteropServices;
using SkiaSharp;

namespace AlloyVulkanTest;

internal static partial class FontTypographyProbe
{
    // Keep the archived first differing pixel centered in every diagnostic crop.
    private const int HistoricalX = 1077;
    private const int HistoricalY = 457;
    private const int ShadowZoom = 8;
    private static readonly SKRectI ShadowCrop = new(HistoricalX - 24, HistoricalY - 24, HistoricalX + 24, HistoricalY + 24);

    private static string ReadPixel(byte[] pixels, int width, int x, int y) =>
        Convert.ToHexString(pixels.AsSpan((y * width + x) * 4, 4));

    private static void WriteShadowCrop(string output, string name, int frameWidth, byte[] pixels) =>
        WriteEnlargedCrop(Path.Combine(output, $"{name}-overlap-{ShadowZoom}x.png"), pixels, frameWidth, ShadowCrop, ShadowZoom);

    private static void WriteEnlargedCrop(string path, byte[] pixels, int frameWidth, SKRectI crop, int zoom)
    {
        var width = crop.Width * zoom;
        var height = crop.Height * zoom;
        var enlarged = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var source = ((crop.Top + y / zoom) * frameWidth + crop.Left + x / zoom) * 4;
            pixels.AsSpan(source, 4).CopyTo(enlarged.AsSpan((y * width + x) * 4, 4));
        }
        WritePng(path, width, height, enlarged);
    }

    private static void WritePng(string path, int width, int height, byte[] pixels)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }
}
