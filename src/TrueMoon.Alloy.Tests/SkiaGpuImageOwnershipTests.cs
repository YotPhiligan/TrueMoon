using SkiaSharp;
using TrueMoon.Alloy.Rendering.Skia;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class SkiaGpuImageOwnershipTests
{
    [Fact]
    public void ImageContextGuard_RejectsRasterOrForeignSurfaceAndAcceptsOrigin()
    {
        SkiaNativeLibrary.Initialize(); var pixels = SKImage.FromPixelCopy(new SKImageInfo(1, 1, SKColorType.Rgba8888, SKAlphaType.Premul), new byte[] { 255, 0, 0, 255 });
        var owner = new object(); using var image = new SkiaImageSource(pixels, owner);
        using var surface = SKSurface.Create(new SKImageInfo(1, 1));
        using var raster = new SkiaDrawingResources(); using var foreign = new SkiaDrawingResources(gpuOwner: new object());
        Assert.Throws<InvalidOperationException>(() => new SkiaDrawingContext(surface.Canvas, raster).Image(image, new(0, 0, 1, 1)));
        Assert.Throws<InvalidOperationException>(() => new SkiaDrawingContext(surface.Canvas, foreign).Image(image, new(0, 0, 1, 1)));
        using var origin = new SkiaDrawingResources(gpuOwner: owner);
        new SkiaDrawingContext(surface.Canvas, origin).Image(image, new(0, 0, 1, 1));
        using var snapshot = surface.Snapshot(); using var bitmap = SKBitmap.FromImage(snapshot); Assert.Equal(SKColors.Red, bitmap.GetPixel(0, 0));
    }
    [Fact]
    public void ImageOwnerFailure_RetainsImageForDisposeRetryAndThenRejectsUse()
    {
        SkiaNativeLibrary.Initialize(); var pixels = SKImage.FromPixelCopy(new SKImageInfo(2, 1), new byte[8]);
        var ownerError = new InvalidOperationException("owner unavailable"); var fail = true; var checks = 0;
        var image = new SkiaImageSource(pixels, new object(), () => { checks++; if (fail) throw ownerError; });
        Assert.Same(ownerError, Assert.Throws<InvalidOperationException>(() => image.Dispose())); Assert.NotEqual(nint.Zero, pixels.Handle);
        fail = false; Assert.Equal(2, image.Size.Width); image.Dispose(); Assert.Equal(nint.Zero, pixels.Handle);
        image.Dispose(); Assert.Equal(3, checks); Assert.Throws<ObjectDisposedException>(() => image.Size);
    }
}
