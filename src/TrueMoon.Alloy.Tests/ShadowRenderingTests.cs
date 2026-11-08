using SkiaSharp;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class ShadowRenderingTests
{
    [Theory] [InlineData(1f)] [InlineData(1.5f)] [InlineData(2f)]
    public void OuterShadow_BlursOutsideBoundsWithoutDarkeningTransparentInteriorOrChangingLayout(float scale)
    {
        var root = new Panel { Background = new(255, 255, 255) }; var panel = Surface(); root.Add(panel);
        panel.Shadow = new(3, 0, 4, new(0, 0, 0, 160));
        using var ui = Create(root, scale); ui.Update(); var bounds = panel.Bounds; var size = panel.DesiredSize;
        using var first = ui.SnapshotRasterImage();
        Assert.Equal(SKColors.White, Pixel(first, 55, 35, scale)); // Transparent middle is never shadowed.
        var near = Pixel(first, 55, 62, scale).Red; var far = Pixel(first, 55, 68, scale).Red;
        Assert.InRange(near, (byte)90, (byte)235); Assert.True(far > near); Assert.InRange(far, (byte)240, (byte)255);
        Assert.Equal(SKColors.White, Pixel(first, 10, 10, scale));
        panel.Shadow = null; ui.Update(); using var second = ui.SnapshotRasterImage();
        Assert.Equal(SKColors.White, Pixel(second, 55, 62, scale)); Assert.Equal(bounds, panel.Bounds); Assert.Equal(size, panel.DesiredSize);
        Assert.False(ui.Update()); Assert.False(ui.NeedsUpdate);
    }

    [Theory] [InlineData(0.5f)] [InlineData(1f)]
    public void Shadow_RespectsAncestorClipAndSubtreeOpacity(float opacity)
    {
        var root = new Panel { Background = new(255, 255, 255) };
        var clip = new Panel { Width = 90, Height = 64, HorizontalAlignment = LayoutAlignment.Start, VerticalAlignment = LayoutAlignment.Start };
        var panel = Surface(); panel.Opacity = opacity; panel.Shadow = new(0, 8, 8, new(0, 0, 0));
        clip.Add(panel); root.Add(clip); using var ui = Create(root); ui.Update(); using var image = ui.SnapshotRasterImage();
        Assert.InRange(Pixel(image, 60, 62).Red, opacity == 1 ? 0 : 126, opacity == 1 ? 0 : 129);
        Assert.Equal(SKColors.White, Pixel(image, 60, 66)); // Beyond clipping parent.
        Assert.Equal(SKColors.White, Pixel(image, 55, 35));
        Assert.Equal(new Rect(30, 20, 50, 40), panel.Bounds);
    }

    [Fact]
    public void Shadow_HasNoPointerHitAreaAndRoundedCornersRemainTransparent()
    {
        var root = new Panel { Background = new(255, 255, 255) };
        var background = new Button("") { Background = new(255, 255, 255), BorderWidth = 0 }; var clicks = 0;
        background.Click += () => clicks++; root.Add(background); var panel = Surface(); root.Add(panel);
        panel.CornerRadius = 12; panel.Background = new(255, 0, 0); panel.Shadow = new(0, 0, 5, new(0, 0, 0));
        using var ui = Create(root); ui.Update(); using var image = ui.SnapshotRasterImage();
        Assert.Equal(SKColors.White, Pixel(image, 30, 20)); Assert.Equal(SKColors.Red, Pixel(image, 55, 35));
        Assert.False(panel.Bounds.Contains(55, 64)); Assert.True(Pixel(image, 55, 63).Red < 10);
        ui.HandleInput(new(InputKind.PointerDown, 55, 64)); ui.HandleInput(new(InputKind.PointerUp, 55, 64));
        Assert.Equal(1, clicks); Assert.True(background.IsFocused);
    }

    [Fact]
    public void MenuShadow_UsesThemeAndHonorsStyleLocalNullAndRemoval()
    {
        var action = new MenuAction("one", "One", new DelegateUiCommand(() => { }));
        var menu = new MenuPopup([action], () => { }); using var ui = Create(menu); ui.Update();
        Assert.Same(Theme.Dark.MenuShadow, menu.Shadow);
        ui.SetTheme(Theme.Light); ui.Update(); Assert.Same(Theme.Light.MenuShadow, menu.Shadow);
        var styled = new BoxShadow(1, -2, 3, new(1, 2, 3)); menu.Style = new Style().With(UiProperties.Shadow, styled);
        Assert.Same(styled, menu.Shadow); menu.Shadow = null; Assert.Null(menu.Shadow);
        menu.Clear(UiProperties.Shadow); Assert.Same(styled, menu.Shadow);
        menu.Style = null; Assert.Same(Theme.Light.MenuShadow, menu.Shadow);
        ui.SetTheme(Theme.Light with { MenuShadow = null }); ui.Update(); Assert.Null(menu.Shadow);
    }

    [Theory]
    [InlineData(-1, 0, 0)] [InlineData(33, 0, 0)] [InlineData(float.NaN, 0, 0)]
    [InlineData(1, float.PositiveInfinity, 0)] [InlineData(1, 65, 0)] [InlineData(1, 0, -65)]
    public void Shadow_RejectsNonFiniteOrUnboundedValues(float sigma, float x, float y)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new BoxShadow(sigma, x, y, default));

    [Fact]
    public void LegacyContext_OmitsShadowAndExceptionRestoresShadowOpacityScope()
    {
        using var panel = new Throwing { Shadow = new(2, 3, 4, new(0, 0, 0)), Opacity = .5f };
        panel.Measure(new(100, 80), new FixedTextLayout()); panel.Arrange(new(0, 0, 100, 80));
        var legacy = new Drawing(); Assert.Throws<InvalidOperationException>(() => panel.Draw(legacy)); Assert.Equal(0, legacy.Depth);
        var drawing = new ShadowDrawing(); Assert.Throws<InvalidOperationException>(() => panel.Draw(drawing));
        Assert.Equal(0, drawing.Depth); Assert.Equal(new[] { "opacity", "shadow", "clip", "content" }, drawing.Events);
        Assert.Equal(new Rect(-3, -2, 112, 92), drawing.LayerBounds);
    }

    private static Border Surface() => new() { Width = 50, Height = 40, BorderWidth = 0, Margin = new(30, 20, 0, 0), HorizontalAlignment = LayoutAlignment.Start, VerticalAlignment = LayoutAlignment.Start };
    private static UiSession Create(Element root, float scale = 1) => UiSession.Create(root, new SkiaRasterRenderBackend(), new RasterUiTarget(), new((int)(160 * scale), (int)(100 * scale), scale));
    private static SKColor Pixel(SKImage image, int x, int y, float scale = 1) { using var bitmap = SKBitmap.FromImage(image); return bitmap.GetPixel((int)(x * scale), (int)(y * scale)); }
    private sealed class Throwing : Element { protected override void DrawCore(IDrawingContext context) { ((Drawing)context).Events.Add("content"); throw new InvalidOperationException("draw"); } }
    private class Drawing : IDrawingContext
    {
        internal int Depth; internal List<string> Events = [];
        public void Save() => Depth++;
        public void Restore() { Assert.True(Depth > 0); Depth--; }
        public void Clip(Rect rectangle) => Events.Add("clip");
        public void Translate(float x, float y) { }
        public void Fill(Rect rectangle, Color color, float radius = 0) { }
        public void Stroke(Rect rectangle, Color color, float width = 1, float radius = 0) { }
        public void Text(string text, float x, float y, Color color, float fontSize, string fontFamily) { }
        public void Image(IImageSource source, Rect destination) { }
    }
    private sealed class ShadowDrawing : Drawing, IShadowDrawingContext, IOpacityDrawingContext
    {
        internal Rect LayerBounds;
        public void DrawShadow(Rect rectangle, BoxShadow shadow, float cornerRadius) => Events.Add("shadow");
        public void SaveOpacity(Rect bounds, float opacity) { Depth++; LayerBounds = bounds; Events.Add("opacity"); }
    }
}
