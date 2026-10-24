using SkiaSharp;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class BackdropRenderingTests
{
    [Theory]
    [InlineData(1f)] [InlineData(1.5f)] [InlineData(2f)]
    public void FrostedPanel_BlursEarlierContentAndKeepsForegroundSharp(float scale)
    {
        var root = new SplitBackground();
        var panel = new ForegroundPatch
        {
            Width = 60, Height = 40, Margin = new(60, 20, 0, 0),
            BorderWidth = 0,
            HorizontalAlignment = LayoutAlignment.Start, VerticalAlignment = LayoutAlignment.Start,
            Backdrop = new(4, new(0, 0, 255, 64), new(30, 40, 50))
        };
        root.Add(panel);
        using var ui = Create(root, scale); ui.Update();
        using var image = ui.SnapshotRasterImage();
        // The kernel must sample the black half outside the panel's own clip.
        var edge = Pixel(image, 61, 40, scale);
        Assert.Equal((byte)255, edge.Alpha);
        Assert.InRange(edge.Red, (byte)80, (byte)175);
        Assert.InRange(edge.Blue, (byte)(edge.Red + 60), (byte)(edge.Red + 66));
        Assert.Equal(SKColors.White, Pixel(image, 80, 10, scale)); // outside panel
        Assert.Equal(SKColors.Red, Pixel(image, 83, 38, scale)); // later foreground
        Assert.Equal(new SKColor(191, 191, 255), Pixel(image, 110, 45, scale));
        Assert.False(ui.Update()); Assert.False(ui.NeedsRender);
    }

    [Fact]
    public void FrostedPanel_ReplacesSampledAlphaInsteadOfBlendingTheBackdropTwice()
    {
        var root = new Panel { Background = new(200, 100, 50, 128) };
        root.Add(new Border { Backdrop = new(4, Color.Transparent, new(1, 2, 3)) });
        using var ui = Create(root); ui.Update();
        using var image = ui.SnapshotRasterImage();
        var color = Pixel(image, 60, 40);
        Assert.InRange(color.Red, (byte)198, (byte)201);
        Assert.InRange(color.Green, (byte)98, (byte)101);
        Assert.InRange(color.Blue, (byte)48, (byte)51);
        Assert.Equal((byte)128, color.Alpha);
    }

    [Fact]
    public void RoundedBackdrop_ClipsBlurAndTintAndDoesNotReuseThePreviousFrame()
    {
        var root = new Panel { Background = new(220, 40, 20) };
        var panel = new Border
        {
            Width = 60, Height = 40, Margin = new(30, 20, 0, 0),
            HorizontalAlignment = LayoutAlignment.Start, VerticalAlignment = LayoutAlignment.Start,
            CornerRadius = 12, Backdrop = new(4, new(0, 0, 200, 128), new(1, 2, 3))
        };
        root.Add(panel);
        using var ui = Create(root); ui.Update();
        using var first = ui.SnapshotRasterImage();
        Assert.Equal(new SKColor(220, 40, 20), Pixel(first, 30, 20));
        Assert.InRange(Pixel(first, 60, 40).Blue, (byte)109, (byte)111);
        root.Background = new(20, 220, 40); ui.Update();
        using var second = ui.SnapshotRasterImage();
        Assert.Equal(new SKColor(20, 220, 40), Pixel(second, 30, 20));
        var center = Pixel(second, 60, 40);
        Assert.InRange(center.Red, (byte)9, (byte)11);
        Assert.InRange(center.Green, (byte)109, (byte)111);
        Assert.InRange(center.Blue, (byte)119, (byte)121);
        Assert.Same(panel, Assert.Single(root.Children));
        Assert.InRange(Pixel(first, 60, 40).Red, (byte)109, (byte)111);
    }

    [Fact]
    public void NestedBackdrops_SampleEarlierMaterialsInPaintOrder()
    {
        var root = new Panel { Background = new(200, 0, 0) };
        var outer = new Panel { Backdrop = new(3, new(0, 200, 0, 128), new(1, 2, 3)) };
        outer.Add(new Border { Backdrop = new(3, new(0, 0, 200, 128), new(1, 2, 3)) });
        root.Add(outer);
        using var ui = Create(root); ui.Update();
        using var image = ui.SnapshotRasterImage();
        var color = Pixel(image, 60, 40);
        Assert.InRange(color.Red, (byte)48, (byte)51);
        Assert.InRange(color.Green, (byte)49, (byte)51);
        Assert.InRange(color.Blue, (byte)99, (byte)101);
        Assert.Equal((byte)255, color.Alpha);
    }

    [Theory]
    [InlineData(0f, 128)]
    [InlineData(12f, 255)]
    public void TintOnlyOrOpaqueMaterial_DoesNotRequireASamplingSurface(float sigma, int alpha)
    {
        SkiaNativeLibrary.Initialize();
        using var surface = SKSurface.Create(new SKImageInfo(100, 80));
        using var resources = new SkiaDrawingResources();
        var drawing = new SkiaDrawingContext(surface.Canvas, resources);
        surface.Canvas.Clear(SKColors.Red);
        Assert.True(drawing.TryDrawBackdrop(new(10, 10, 40, 30), new(sigma, new(0, 0, 200, (byte)alpha), new(1, 2, 3)), 0));
        using var image = surface.Snapshot();
        var color = Pixel(image, 20, 20);
        Assert.InRange(color.Blue, (byte)(alpha == 255 ? 200 : 99), (byte)(alpha == 255 ? 200 : 101));
        Assert.Equal(SKColors.Red, Pixel(image, 5, 5));
        Assert.Equal(alpha == 255 ? (byte)0 : (byte)127, color.Red);
    }

    [Fact]
    public void UnsupportedSampling_PaintsOpaqueFallbackBeforeForeground()
    {
        var material = new BackdropMaterial(8, new(20, 30, 40, 128), new(50, 60, 70));
        using var panel = new ForegroundPatch().WithBackdrop(material);
        panel.Measure(new(120, 80), new FixedTextLayout()); panel.Arrange(new(0, 0, 120, 80));
        var drawing = new LegacyDrawing(); panel.Draw(drawing);
        Assert.Equal(new[] { material.Fallback, new Color(255, 0, 0) }, drawing.Fills);
        panel.Backdrop = null; drawing.Fills.Clear(); panel.Draw(drawing);
        Assert.Equal(new[] { new Color(255, 0, 0) }, drawing.Fills);
    }

    [Fact]
    public void SkiaWithoutSurfaceOrUnsupportedTransform_ReturnsFalseWithoutPainting()
    {
        SkiaNativeLibrary.Initialize();
        using var surface = SKSurface.Create(new SKImageInfo(100, 80));
        using var resources = new SkiaDrawingResources();
        surface.Canvas.Clear(SKColors.Red);
        var material = new BackdropMaterial(8, new(0, 0, 255, 128), new(1, 2, 3));
        Assert.False(new SkiaDrawingContext(surface.Canvas, resources).TryDrawBackdrop(new(10, 10, 40, 30), material, 0));
        surface.Canvas.Skew(.1f, 0);
        Assert.False(new SkiaDrawingContext(surface.Canvas, resources, surface).TryDrawBackdrop(new(10, 10, 40, 30), material, 0));
        using var image = surface.Snapshot();
        Assert.Equal(SKColors.Red, Pixel(image, 20, 20));
    }

    [Theory]
    [InlineData(-1f)] [InlineData(float.NaN)] [InlineData(float.PositiveInfinity)] [InlineData(64.01f)]
    public void Material_RejectsUnboundedOrInvalidBlur(float sigma)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new BackdropMaterial(sigma, default, new(1, 2, 3)));

    [Fact]
    public void Material_RequiresOpaqueFallbackAndSupportsStyleOverrideRemoval()
    {
        Assert.Throws<ArgumentException>(() => new BackdropMaterial(1, default, Color.Transparent));
        using var panel = new Border();
        var styled = new BackdropMaterial(0, new(1, 2, 3, 128), new(4, 5, 6));
        var local = new BackdropMaterial(64, default, new(7, 8, 9));
        panel.Style = new Style().With(UiProperties.Backdrop, styled);
        Assert.Same(styled, panel.Backdrop);
        panel.Backdrop = local; Assert.Same(local, panel.Backdrop);
        panel.Backdrop = null; Assert.Null(panel.Backdrop);
        panel.Clear(UiProperties.Backdrop); Assert.Same(styled, panel.Backdrop);
    }

    private static UiSession Create(Element root, float scale = 1) => UiSession.Create(root,
        new SkiaRasterRenderBackend(), new RasterUiTarget(), new((int)(160 * scale), (int)(100 * scale), scale));

    private static SKColor Pixel(SKImage image, int x, int y, float scale = 1)
    {
        using var bitmap = SKBitmap.FromImage(image);
        return bitmap.GetPixel((int)(x * scale), (int)(y * scale));
    }

    private sealed class SplitBackground : Panel
    {
        protected override void DrawCore(IDrawingContext context)
        {
            context.Fill(new(0, 0, 60, 100), new(0, 0, 0));
            context.Fill(new(60, 0, 100, 100), new(255, 255, 255));
        }
    }

    private sealed class ForegroundPatch : Border
    {
        protected override void DrawCore(IDrawingContext context)
            => context.Fill(new(Bounds.X + 20, Bounds.Y + 15, 8, 10), new(255, 0, 0));
    }

    private sealed class LegacyDrawing : IDrawingContext
    {
        internal readonly List<Color> Fills = [];
        public void Save() { }
        public void Restore() { }
        public void Clip(Rect rectangle) { }
        public void Translate(float x, float y) { }
        public void Fill(Rect rectangle, Color color, float radius = 0) => Fills.Add(color);
        public void Stroke(Rect rectangle, Color color, float width = 1, float radius = 0) { }
        public void Text(string text, float x, float y, Color color, float fontSize, string fontFamily) { }
        public void Image(IImageSource source, Rect destination) { }
    }
}
