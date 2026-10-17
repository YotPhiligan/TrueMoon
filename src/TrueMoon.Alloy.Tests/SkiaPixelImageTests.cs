using SkiaSharp;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class SkiaPixelImageTests
{
    [Fact]
    public void CopyBgraPixels_OwnsPixelsPreservesChannelsOriginAndPremultipliedAlpha()
    {
        byte[] pixels = [0, 0, 255, 255, 0, 255, 0, 255, 255, 0, 0, 255, 0, 0, 128, 128];
        using var image = SkiaImageSource.CopyBgraPixels(2, 2, pixels); Array.Clear(pixels);
        Assert.Equal(new Size(2, 2), image.Size);
        using var surface = SKSurface.Create(new SKImageInfo(2, 2, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var resources = new SkiaDrawingResources();
        new SkiaDrawingContext(surface.Canvas, resources).Image(image, new(0, 0, 2, 2));
        using var snapshot = surface.Snapshot(); using var bitmap = SKBitmap.FromImage(snapshot);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(0, 0)); Assert.Equal(SKColors.Lime, bitmap.GetPixel(1, 0));
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(0, 1)); Assert.Equal(new SKColor(255, 0, 0, 128), bitmap.GetPixel(1, 1));
    }
    [Theory]
    [InlineData(0, 1, 0)] [InlineData(1, 0, 0)] [InlineData(-1, 1, 0)]
    [InlineData(1, 1, 3)] [InlineData(1, 1, 5)] [InlineData(int.MaxValue, 1, 0)]
    public void CopyBgraPixels_RejectsInvalidShapeBeforeNativeAllocation(int width, int height, int length)
    {
        var error = Record.Exception(() => SkiaImageSource.CopyBgraPixels(width, height, new byte[length]));
        if (width <= 0 || height <= 0) Assert.IsType<ArgumentOutOfRangeException>(error);
        else if (width == int.MaxValue) Assert.IsType<OverflowException>(error);
        else Assert.IsType<ArgumentException>(error);
    }
}
