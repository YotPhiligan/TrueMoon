using SkiaSharp;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class OpacityRenderingTests
{
    [Theory]
    [InlineData(-.001f)] [InlineData(1.001f)] [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)] [InlineData(float.NegativeInfinity)]
    public void InvalidOpacity_IsRejectedBeforeNotificationOrStyleMutation(float invalid)
    {
        using var panel = new Panel { Opacity = .5f };
        var invalidations = new List<Invalidation>(); var notifications = 0;
        panel.Invalidated += invalidations.Add;
        panel.PropertyChanged += (_, _) => notifications++;

        Assert.Throws<ArgumentOutOfRangeException>(() => panel.Opacity = invalid);
        Assert.Throws<ArgumentOutOfRangeException>(() => panel.Set(UiProperties.Opacity, invalid));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Style().With(UiProperties.Opacity, invalid));

        Assert.Equal(.5f, panel.Opacity); Assert.Empty(invalidations); Assert.Equal(0, notifications);
    }

    [Fact]
    public void Opacity_LocalOverrideAndClearRestoreStyledGroupWithoutReplacingChildren()
    {
        using var root = new Panel { Style = new Style().With(UiProperties.Opacity, .25f) };
        var child = new Panel { Background = new(0, 0, 255) }; root.Add(child); Arrange(root);
        var bounds = child.Bounds;
        var reasons = new List<Invalidation>(); root.Invalidated += reasons.Add;
        root.Opacity = .5f; root.Opacity = .5f;
        var drawing = new OpacityDrawing(); root.Draw(drawing);
        Assert.Equal(.5f, Assert.Single(drawing.Layers).Opacity);

        root.Clear(UiProperties.Opacity);
        drawing = new OpacityDrawing(); root.Draw(drawing);

        Assert.Equal(.25f, Assert.Single(drawing.Layers).Opacity);
        Assert.Equal(new[] { Invalidation.Render, Invalidation.Render }, reasons);
        Assert.Same(child, Assert.Single(root.Children)); Assert.Equal(bounds, child.Bounds);
        root.Style = null; Assert.Equal(1f, root.Opacity);
    }

    [Fact]
    public void Opacity_ChangesOnlyRenderingAndRetainsLayoutAndChildren()
    {
        var root = new CountingPanel();
        var child = new Panel { Background = new(0, 0, 255) }; root.Add(child);
        using var ui = Create(root); ui.Update();
        var measures = root.Measures; var arranges = root.Arranges; var bounds = child.Bounds;
        using var before = ui.SnapshotRasterImage(); Assert.Equal(SKColors.Blue, Pixel(before, 30, 30));

        root.Opacity = .5f;
        Assert.True(ui.NeedsRender); Assert.True(ui.Update());
        using var after = ui.SnapshotRasterImage();

        Assert.Equal(new SKColor(0, 0, 255, 128), Pixel(after, 30, 30));
        Assert.Equal(measures, root.Measures); Assert.Equal(arranges, root.Arranges);
        Assert.Same(child, Assert.Single(root.Children)); Assert.Equal(bounds, child.Bounds);
        Assert.False(ui.Update()); Assert.False(ui.NeedsUpdate);
    }

    [Fact]
    public void ZeroOpacity_DoesNotChangeInputAdmissionOrKeyboardFocus()
    {
        var clicks = 0; var button = new Button("") { Opacity = 0 };
        button.Click += () => clicks++;
        using var ui = Create(button); ui.Update();
        var down = ui.HandleInput(new(InputKind.PointerDown, 20, 20));
        var up = ui.HandleInput(new(InputKind.PointerUp, 20, 20));

        Assert.True(down.Handled); Assert.True(down.PointerCaptured); Assert.True(down.KeyboardFocused);
        Assert.True(up.Handled); Assert.False(up.PointerCaptured); Assert.Equal(1, clicks);
        Assert.True(button.IsFocused); Assert.True(button.IsEnabled); Assert.True(button.IsVisible);
    }

    [Theory]
    [InlineData(0f, 0)] [InlineData(.5f, 1)] [InlineData(1f, 0)]
    public void Draw_OpacityBoundaryUsesOptionalCapabilityAndSkipsZeroSubtree(float opacity, int layers)
    {
        using var root = new Panel { Opacity = opacity, Background = new(255, 0, 0) };
        root.Add(new Panel { Background = new(0, 0, 255) }); Arrange(root);
        var drawing = new OpacityDrawing(); root.Draw(drawing);

        Assert.Equal(layers, drawing.Layers.Count);
        Assert.Equal(opacity == 0 ? Array.Empty<Color>() : new[] { new Color(255, 0, 0), new Color(0, 0, 255) }, drawing.Fills);
        Assert.Equal(0, drawing.Depth);
        Assert.Equal(opacity == 0 ? 0 : layers + 2, drawing.Restores);
        if (opacity == 0) Assert.Empty(drawing.Events);
        if (layers == 1) Assert.Equal((root.Bounds, .5f), Assert.Single(drawing.Layers));
    }

    [Theory]
    [InlineData(0f)] [InlineData(.5f)] [InlineData(1f)]
    public void Draw_LegacyContextFallsBackToOpaqueFractionalSubtree(float opacity)
    {
        using var root = new Panel { Opacity = opacity, Background = new(255, 0, 0) };
        root.Add(new Panel { Background = new(0, 0, 255) }); Arrange(root);
        var drawing = new LegacyDrawing(); root.Draw(drawing);

        Assert.Equal(opacity == 0 ? Array.Empty<Color>() : new[] { new Color(255, 0, 0), new Color(0, 0, 255) }, drawing.Fills);
        Assert.Equal(opacity == 0 ? 0 : 2, drawing.Restores); Assert.Equal(0, drawing.Depth);
    }

    [Fact]
    public void Draw_NestedOpacityLayersSurroundCompleteSubtreesInOrder()
    {
        using var root = new Panel { Opacity = .5f, Background = new(255, 0, 0) };
        root.Add(new Panel { Opacity = .25f, Background = new(0, 0, 255) }); Arrange(root);
        var drawing = new OpacityDrawing(); root.Draw(drawing);

        Assert.Equal(new[] { "save", "clip", "opacity", "fill", "save", "clip", "opacity", "fill",
            "restore", "restore", "restore", "restore" }, drawing.Events);
        Assert.Equal(new[] { (root.Bounds, .5f), (root.Children[0].Bounds, .25f) }, drawing.Layers);
        Assert.Equal(0, drawing.Depth); Assert.Equal(4, drawing.Restores);
    }

    [Fact]
    public void Draw_ThrowingChildRestoresEveryOpacityAndClipState()
    {
        using var root = new Panel { Opacity = .5f };
        var error = new InvalidOperationException("draw failed");
        root.Add(new ThrowingElement(error) { Opacity = .25f }); Arrange(root);
        var drawing = new OpacityDrawing();

        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => root.Draw(drawing)));

        Assert.Equal(new[] { "save", "clip", "opacity", "save", "clip", "opacity",
            "restore", "restore", "restore", "restore" }, drawing.Events);
        Assert.Equal(0, drawing.Depth); Assert.Equal(4, drawing.Restores);
    }

    [Theory]
    [InlineData(float.NaN, 0f, 20f, 20f)]
    [InlineData(0f, float.PositiveInfinity, 20f, 20f)]
    [InlineData(0f, 0f, float.NaN, 20f)]
    [InlineData(0f, 0f, 20f, float.PositiveInfinity)]
    [InlineData(0f, 0f, -1f, 20f)]
    [InlineData(0f, 0f, 20f, -1f)]
    public void SkiaOpacity_InvalidLayerBoundsLeaveCanvasStateAndPixelsUntouched(float x, float y, float width, float height)
    {
        SkiaNativeLibrary.Initialize();
        using var surface = SKSurface.Create(new SKImageInfo(40, 40));
        using var resources = new SkiaDrawingResources();
        var drawing = new SkiaDrawingContext(surface.Canvas, resources, surface);
        surface.Canvas.Clear(SKColors.Red); var saves = surface.Canvas.SaveCount;

        Assert.Throws<ArgumentOutOfRangeException>(() => drawing.SaveOpacity(new(x, y, width, height), .5f));

        Assert.Equal(saves, surface.Canvas.SaveCount);
        using var image = surface.Snapshot(); Assert.Equal(SKColors.Red, Pixel(image, 10, 10));
    }

    [Theory]
    [InlineData(-.001f)] [InlineData(1.001f)] [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)] [InlineData(float.NegativeInfinity)]
    public void SkiaOpacity_InvalidAlphaLeavesCanvasStateAndPixelsUntouched(float invalid)
    {
        SkiaNativeLibrary.Initialize();
        using var surface = SKSurface.Create(new SKImageInfo(40, 40));
        using var resources = new SkiaDrawingResources();
        var drawing = new SkiaDrawingContext(surface.Canvas, resources, surface);
        surface.Canvas.Clear(SKColors.Red); var saves = surface.Canvas.SaveCount;

        Assert.Throws<ArgumentOutOfRangeException>(() => drawing.SaveOpacity(new(0, 0, 20, 20), invalid));

        Assert.Equal(saves, surface.Canvas.SaveCount);
        using var image = surface.Snapshot(); Assert.Equal(SKColors.Red, Pixel(image, 10, 10));
    }

    [Theory]
    [InlineData(0f, 255, 0)] [InlineData(.5f, 127, 1)] [InlineData(1f, 0, 0)]
    public void SkiaOpacity_InclusiveBoundariesCompositeOnOrdinaryRestore(float opacity, byte remaining, byte tolerance)
    {
        SkiaNativeLibrary.Initialize();
        using var surface = SKSurface.Create(new SKImageInfo(40, 40));
        using var resources = new SkiaDrawingResources();
        var drawing = new SkiaDrawingContext(surface.Canvas, resources, surface);
        surface.Canvas.Clear(SKColors.White); var saves = surface.Canvas.SaveCount;

        drawing.SaveOpacity(new(0, 0, 40, 40), opacity);
        Assert.Equal(saves + 1, surface.Canvas.SaveCount);
        drawing.Fill(new(0, 0, 40, 40), new(0, 0, 255)); drawing.Restore();

        Assert.Equal(saves, surface.Canvas.SaveCount);
        using var image = surface.Snapshot(); var pixel = Pixel(image, 10, 10);
        // SaveLayer's CPU fixed-point blend can quantize the ideal half-alpha remainder by one byte.
        Assert.InRange(pixel.Red, remaining - tolerance, remaining + tolerance);
        Assert.Equal(pixel.Red, pixel.Green); Assert.Equal((byte)255, pixel.Blue); Assert.Equal((byte)255, pixel.Alpha);
    }

    [Theory]
    [InlineData(1f)] [InlineData(1.5f)] [InlineData(2f)]
    public void GroupOpacity_CompositesOverlappingChildrenAndForegroundImageOnce(float scale)
    {
        using var source = SkiaImageSource.CopyBgraPixels(1, 1, new byte[] { 0, 255, 0, 255 });
        var root = new Panel { Background = new(255, 255, 255) };
        var group = new ImagePatch(source) { Opacity = .5f, Background = new(255, 0, 0) };
        group.Add(Patch(new(0, 0, 255), 60, 20)); root.Add(group);
        using var ui = Create(root, scale); ui.Update();
        using var image = ui.SnapshotRasterImage();

        Assert.Equal(new SKColor(255, 127, 127), Pixel(image, 10, 10, scale));
        Assert.Equal(new SKColor(127, 127, 255), Pixel(image, 65, 25, scale));
        Assert.Equal(new SKColor(127, 255, 127), Pixel(image, 25, 25, scale));
        Assert.False(ui.Update());
    }

    [Theory]
    [InlineData(1f)] [InlineData(1.5f)] [InlineData(2f)]
    public void NestedGroupOpacity_MultipliesLayersWithoutFadingEarlierSiblingsTwice(float scale)
    {
        var root = new Panel { Background = new(255, 255, 255) };
        var outer = new Panel { Opacity = .5f, Background = new(255, 0, 0) };
        var inner = Patch(new(0, 0, 255), 20, 20); inner.Opacity = .5f;
        outer.Add(inner); root.Add(outer);
        using var ui = Create(root, scale); ui.Update();
        using var image = ui.SnapshotRasterImage();

        Assert.Equal(new SKColor(255, 127, 127), Pixel(image, 10, 10, scale));
        Assert.Equal(new SKColor(191, 127, 191), Pixel(image, 25, 25, scale));
        Assert.Equal(new SKColor(255, 127, 127), Pixel(image, 50, 25, scale));
    }

    [Theory]
    [InlineData(1f)] [InlineData(1.5f)] [InlineData(2f)]
    public void FrostedGroup_SamplesEarlierRootAndKeepsLaterForegroundSharp(float scale)
    {
        var root = new SplitBackground();
        var group = new ForegroundPatch
        {
            Opacity = .5f, Width = 60, Height = 40, Margin = new(60, 20, 0, 0),
            HorizontalAlignment = LayoutAlignment.Start, VerticalAlignment = LayoutAlignment.Start,
            Backdrop = new(4, new(0, 0, 255, 64), new(30, 40, 50))
        };
        root.Add(group);
        using var ui = Create(root, scale); ui.Update();
        using var image = ui.SnapshotRasterImage();
        var edge = Pixel(image, 61, 40, scale);

        Assert.Equal((byte)255, edge.Alpha);
        Assert.InRange(edge.Red, (byte)167, (byte)215);
        Assert.InRange(edge.Blue - edge.Red, 31, 33);
        Assert.Equal(new SKColor(223, 223, 255), Pixel(image, 110, 45, scale));
        Assert.Equal(new SKColor(255, 127, 127), Pixel(image, 83, 38, scale));
        Assert.Equal(SKColors.White, Pixel(image, 80, 10, scale));
        Assert.Equal(SKColors.Black, Pixel(image, 20, 40, scale));

        root.Left = new(0, 200, 0); root.Right = new(200, 0, 0); root.Invalidate(); ui.Update();
        using var next = ui.SnapshotRasterImage();
        var changed = Pixel(next, 110, 45, scale);
        Assert.InRange(changed.Red, (byte)174, (byte)176);
        Assert.Equal((byte)0, changed.Green);
        Assert.InRange(changed.Blue, (byte)31, (byte)33);
        Assert.Equal(new SKColor(223, 223, 255), Pixel(image, 110, 45, scale));
    }

    [Theory]
    [InlineData(false, 18, 22, 30, 208)]
    [InlineData(true, 242, 245, 250, 216)]
    public void Theme_PanelMaterialUsesDedicatedDarkAndLightSettings(bool light, byte r, byte g, byte b, byte alpha)
    {
        var theme = light ? Theme.Light : Theme.Dark;
        var material = Assert.IsType<BackdropMaterial>(theme.PanelBackdrop);

        Assert.Equal(8, material.BlurSigma); Assert.Equal(new Color(r, g, b, alpha), material.Tint);
        Assert.Equal(new Color(r, g, b), material.Fallback);
        Assert.NotSame(theme.MenuBackdrop, material); Assert.Equal(12, theme.MenuBackdrop!.BlurSigma);
        Assert.Same(material, (theme with { MenuBackdrop = null }).PanelBackdrop);
        Assert.Same(theme.MenuBackdrop, (theme with { PanelBackdrop = null }).MenuBackdrop);
    }

    private static Panel Patch(Color color, float x, float y) => new()
    {
        Background = color, Width = 20, Height = 20, Margin = new(x, y, 0, 0),
        HorizontalAlignment = LayoutAlignment.Start, VerticalAlignment = LayoutAlignment.Start
    };
    private static UiSession Create(Element root, float scale = 1) => UiSession.Create(root,
        new SkiaRasterRenderBackend(), new RasterUiTarget(), new((int)(160 * scale), (int)(100 * scale), scale));
    private static SKColor Pixel(SKImage image, int x, int y, float scale = 1)
    {
        using var bitmap = SKBitmap.FromImage(image);
        return bitmap.GetPixel((int)(x * scale), (int)(y * scale));
    }
    private static void Arrange(Element element)
    {
        element.Measure(new(160, 100), new FixedTextLayout()); element.Arrange(new(0, 0, 160, 100));
    }

    private sealed class CountingPanel : Panel
    {
        public int Measures, Arranges;
        protected override Size MeasureCore(Size available, ITextLayoutService text) { Measures++; return base.MeasureCore(available, text); }
        protected override void ArrangeCore(Rect area) { Arranges++; base.ArrangeCore(area); }
    }
    private sealed class ThrowingElement(Exception error) : Element
    {
        protected override Size MeasureCore(Size available, ITextLayoutService text) => new(20, 20);
        protected override void DrawCore(IDrawingContext context) => throw error;
    }
    private sealed class ImagePatch(IImageSource image) : Panel
    {
        protected override void DrawCore(IDrawingContext context) => context.Image(image, new(20, 20, 20, 20));
    }
    private sealed class SplitBackground : Panel
    {
        public Color Left = new(0, 0, 0), Right = new(255, 255, 255);
        protected override void DrawCore(IDrawingContext context)
        {
            context.Fill(new(0, 0, 60, 100), Left); context.Fill(new(60, 0, 100, 100), Right);
        }
    }
    private sealed class ForegroundPatch : Panel
    {
        protected override void DrawCore(IDrawingContext context) => context.Fill(new(Bounds.X + 20, Bounds.Y + 15, 8, 10), new(255, 0, 0));
    }
    private class LegacyDrawing : IDrawingContext
    {
        public List<Color> Fills { get; } = [];
        public List<string> Events { get; } = [];
        public int Depth { get; protected set; }
        public int Restores { get; private set; }
        public void Save() { Depth++; Events.Add("save"); }
        public void Restore() { Assert.True(Depth > 0); Depth--; Restores++; Events.Add("restore"); }
        public void Clip(Rect rectangle) => Events.Add("clip");
        public void Translate(float x, float y) { }
        public void Fill(Rect rectangle, Color color, float radius = 0) { Fills.Add(color); Events.Add("fill"); }
        public void Stroke(Rect rectangle, Color color, float width = 1, float radius = 0) { }
        public void Text(string text, float x, float y, Color color, float fontSize, string fontFamily) { }
        public void Image(IImageSource source, Rect destination) { }
    }
    private sealed class OpacityDrawing : LegacyDrawing, IOpacityDrawingContext
    {
        public List<(Rect Bounds, float Opacity)> Layers { get; } = [];
        public void SaveOpacity(Rect bounds, float opacity) { Depth++; Layers.Add((bounds, opacity)); Events.Add("opacity"); }
    }
}
