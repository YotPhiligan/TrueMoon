using System.Runtime.InteropServices;
using SkiaSharp;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class SkiaDrawingResourceTests
{
    private static SKSurface Surface()
    {
        SkiaNativeLibrary.Initialize();
        return SKSurface.Create(new SKImageInfo(240, 120, SKColorType.Rgba8888, SKAlphaType.Premul))!;
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void ReusedPaintAndFonts_AlternatingCommandsMatchUncachedPixels(float scale)
    {
        using var actual = Surface(); using var expected = Surface();
        using var resources = new SkiaDrawingResources();
        var drawing = new SkiaDrawingContext(actual.Canvas, resources);
        // Exercise reuse across frames as well as stroke -> fill -> text state changes.
        for (var frame = 0; frame < 3; frame++)
        {
            var color = frame == 1 ? new SKColor(210, 70, 40, 128) : new SKColor(40, 80, 230);
            actual.Canvas.Clear(SKColors.Transparent); expected.Canvas.Clear(SKColors.Transparent);
            actual.Canvas.Save(); expected.Canvas.Save();
            actual.Canvas.Scale(scale); expected.Canvas.Scale(scale);
            drawing.Fill(new Rect(2, 2, 80, 45), new Color(color.Red, color.Green, color.Blue, color.Alpha), 3);
            drawing.Stroke(new Rect(5, 5, 70, 35), new Color(240, 40, 100), 5, 4);
            drawing.Fill(new Rect(85, 5, 20, 20), new Color(20, 220, 80, 128));
            drawing.Text("Aa Привет", 4, 52, new Color(250, 230, 30), 16, "Segoe UI");
            drawing.Text("Mm 123", 86, 28, new Color(255, 255, 255), 12, "Consolas");
            using (var paint = new SKPaint { IsAntialias = true, Color = color })
                expected.Canvas.DrawRoundRect(new SKRect(2, 2, 82, 47), 3, 3, paint);
            using (var paint = new SKPaint { IsAntialias = true, Color = new SKColor(240, 40, 100), Style = SKPaintStyle.Stroke, StrokeWidth = 5 })
                expected.Canvas.DrawRoundRect(new SKRect(5, 5, 75, 40), 4, 4, paint);
            using (var paint = new SKPaint { IsAntialias = true, Color = new SKColor(20, 220, 80, 128) })
                expected.Canvas.DrawRoundRect(new SKRect(85, 5, 105, 25), 0, 0, paint);
            FreshText(expected.Canvas, "Aa Привет", 4, 52, new SKColor(250, 230, 30), 16, "Segoe UI");
            FreshText(expected.Canvas, "Mm 123", 86, 28, SKColors.White, 12, "Consolas");
            actual.Canvas.Restore(); expected.Canvas.Restore();
            Assert.Equal(Pixels(expected), Pixels(actual));
            Assert.Contains(Pixels(actual), value => value != 0);
        }
    }

    [Fact]
    public void FontCache_EvictsOldFontsAndPreservesMeasurementAcrossFamilyAndSizeChanges()
    {
        using var surface = Surface(); using var resources = new SkiaDrawingResources();
        var drawing = new SkiaDrawingContext(surface.Canvas, resources);
        var first = resources.Font("Segoe UI", 12);
        Assert.Same(first.Font, resources.Font("Segoe UI", 12).Font);
        var retained = new List<SKFont> { first.Font };
        for (var i = 0; i < 40; i++)
        {
            var family = i % 2 == 0 ? "Segoe UI" : "Consolas"; var size = 13 + i;
            retained.Add(resources.Font(family, size).Font);
            using var typeface = SKTypeface.FromFamilyName(family); using var fresh = new SKFont(typeface, size);
            Assert.Equal(new Size(fresh.MeasureText("Aa Привет"), fresh.Metrics.Descent - fresh.Metrics.Ascent),
                drawing.Measure("Aa Привет", size, family));
        }
        Assert.Equal(IntPtr.Zero, first.Font.Handle);
        Assert.InRange(retained.Count(font => font.Handle != IntPtr.Zero), 1, 16);
        var replacement = resources.Font("Segoe UI", 12);
        Assert.NotSame(first.Font, replacement.Font);
        Assert.NotEqual(IntPtr.Zero, replacement.Font.Handle);
        using var originalTypeface = SKTypeface.FromFamilyName("Segoe UI"); using var originalFont = new SKFont(originalTypeface, 12);
        Assert.Equal(originalFont.MeasureText("Aa Привет"), replacement.Font.MeasureText("Aa Привет"));
        resources.Dispose();
        Assert.All(retained, font => Assert.Equal(IntPtr.Zero, font.Handle));
        Assert.Equal(IntPtr.Zero, replacement.Font.Handle);
    }

    [Fact]
    public void DrawingResources_ForeignAccessCannotMutateOrDisposeOwnerResources()
    {
        using var surface = Surface(); using var resources = new SkiaDrawingResources();
        var font = resources.Font("Segoe UI", 16).Font;
        var paint = resources.Paint(SKColors.Blue, SKPaintStyle.Fill);
        Exception? fontError = null, paintError = null, disposeError = null;
        var worker = new Thread(() =>
        {
            fontError = Record.Exception(() => resources.Font("Consolas", 20));
            paintError = Record.Exception(() => resources.Paint(SKColors.Red, SKPaintStyle.Stroke, 7));
            disposeError = Record.Exception(resources.Dispose);
        });
        worker.Start(); worker.Join();
        Assert.IsType<InvalidOperationException>(fontError);
        Assert.IsType<InvalidOperationException>(paintError);
        Assert.IsType<InvalidOperationException>(disposeError);
        Assert.Equal(SKColors.Blue, paint.Color); Assert.Equal(SKPaintStyle.Fill, paint.Style);
        Assert.Same(font, resources.Font("Segoe UI", 16).Font);
        resources.Dispose(); resources.Dispose();
        Assert.Equal(IntPtr.Zero, font.Handle); Assert.Equal(IntPtr.Zero, paint.Handle);
        Assert.Throws<ObjectDisposedException>(() => resources.Font("Segoe UI", 16));
        Assert.Throws<ObjectDisposedException>(() => resources.Paint(SKColors.Blue, SKPaintStyle.Fill));
    }

    [Fact]
    public void SurfaceTextService_ResizeSuspensionAndDisposeRespectResourceLifetime()
    {
        using var surface = (SkiaRasterSurface)new SkiaRasterRenderBackend().CreateSurface(new RasterUiTarget(), new UiViewport(100, 60));
        var text = surface.TextLayout;
        var expected = text.Measure("Привет", 16, "Segoe UI");
        surface.Resize(new UiViewport(0, 60));
        Assert.Equal(expected, text.Measure("Привет", 16, "Segoe UI"));
        surface.Resize(new UiViewport(200, 120, 2));
        Assert.Same(text, surface.TextLayout);
        Assert.Equal(expected, text.Measure("Привет", 16, "Segoe UI"));
        surface.Dispose();
        Assert.Throws<ObjectDisposedException>(() => text.Measure("Привет", 16, "Segoe UI"));
    }

    private static void FreshText(SKCanvas canvas, string text, float x, float y, SKColor color, float size, string family)
    {
        using var typeface = SKTypeface.FromFamilyName(family); using var font = new SKFont(typeface, size);
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        canvas.DrawText(text, x, y - font.Metrics.Ascent, SKTextAlign.Left, font, paint);
    }

    private static byte[] Pixels(SKSurface surface)
    {
        using var image = surface.Snapshot();
        using var bitmap = new SKBitmap(new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        Assert.True(image.ReadPixels(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0));
        var bytes = new byte[bitmap.RowBytes * bitmap.Height];
        Marshal.Copy(bitmap.GetPixels(), bytes, 0, bytes.Length);
        return bytes;
    }
}
