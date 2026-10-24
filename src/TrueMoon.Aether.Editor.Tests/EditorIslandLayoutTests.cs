using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests;

public sealed class EditorIslandLayoutTests
{
    [Fact]
    public void WideShell_AnchorsContentSurfacesAt16UnitsAndCapsTheirActualHeights()
    {
        var leftContent = new Box(200, 80); var rightContent = new Box(400, 1000);
        var left = new EditorIsland(leftContent) { Padding = new(8) };
        var right = new EditorIsland(rightContent, true) { Padding = new(8) };
        using var layout = new EditorLayout(new Box(10, 10), left, new Box(10, 10), right, new Box(10, 10));
        Arrange(layout, 1280, 720);
        Assert.Equal(new Rect(0, 52, 1280, 640), layout.Viewport.Bounds);
        Assert.Equal(new Rect(8, 60, 264, 96), left.Bounds);
        Assert.Equal(new Rect(16, 68, 248, 80), leftContent.Bounds);
        Assert.Equal(new Rect(936, 60, 336, 624), right.Bounds);
        Assert.Equal(new Rect(944, 68, 320, 608), rightContent.Bounds);
        Assert.Equal(16f, leftContent.Bounds.X);
        Assert.Equal(16f, 1280 - rightContent.Bounds.X - rightContent.Bounds.Width);
        Assert.Equal(8f, layout.Status.Bounds.Y - right.Bounds.Y - right.Bounds.Height);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(3f, 20f)]
    [InlineData(12f, 100f)]
    [InlineData(80f, 200f)]
    [InlineData(320f, 240f)]
    [InlineData(640f, 360f)]
    public void NarrowShell_KeepsBothIslandsInsideBodyWithoutOverlapAndViewportInvariant(float width, float height)
    {
        var left = new EditorIsland(new Box(1000, 1000)) { Padding = new(8) };
        var right = new EditorIsland(new Box(1000, 1000), true) { Padding = new(8) };
        using var layout = new EditorLayout(new Box(10, 10), left, new Box(10, 10), right, new Box(10, 10));
        Arrange(layout, width, height);
        var body = layout.Viewport.Bounds;
        Assert.Equal(new Rect(0, Math.Min(52, height), width, Math.Max(0, height - 80)), body);
        Assert.All(new Element[] { left, right }, panel =>
        {
            var b = panel.Bounds;
            Assert.True(float.IsFinite(b.X + b.Y + b.Width + b.Height));
            Assert.InRange(b.Width, 0, width);
            Assert.InRange(b.Height, 0, body.Height);
            Assert.InRange(b.X, body.X, body.X + body.Width);
            Assert.InRange(b.X + b.Width, body.X, body.X + body.Width);
            Assert.InRange(b.Y, body.Y, body.Y + body.Height);
            Assert.InRange(b.Y + b.Height, body.Y, body.Y + body.Height);
        });
        Assert.True(left.Bounds.X + left.Bounds.Width <= right.Bounds.X);
        if (width <= 320) Assert.Equal(left.Bounds.Width, right.Bounds.Width, 3);
        right.IsOpen = false;
        Arrange(layout, width, height);
        Assert.Equal(body, layout.Viewport.Bounds); Assert.Equal(default, right.Bounds);
        right.IsOpen = true;
        Arrange(layout, width, height);
        Assert.Equal(body, layout.Viewport.Bounds);
    }

    [Fact]
    public void PaintAndInputOrder_PlaceBothIslandsAboveFullViewportAndLeaveExposedBodyInteractive()
    {
        var painted = new List<string>();
        var viewport = new Box(1000, 1000, "viewport", painted);
        var left = new Box(20, 40, "hierarchy", painted); var right = new Box(20, 40, "inspector", painted);
        var toolbar = new Box(10, 10, "toolbar", painted); var status = new Box(10, 10, "status", painted);
        using var root = new EditorLayout(toolbar, left, viewport, right, status);
        using var ui = new UiSession(root, new Surface(), new(1280, 720)); ui.Update();
        root.Draw(new Drawing());
        Assert.Equal(new[] { "toolbar", "viewport", "hierarchy", "inspector", "status" }, painted);
        Click(ui, 40, 70); Click(ui, 1000, 70); Click(ui, 640, 70); Click(ui, 40, 200);
        Assert.Equal(1, left.Clicks); Assert.Equal(1, right.Clicks); Assert.Equal(2, viewport.Clicks);
        Assert.Equal(0, toolbar.Clicks); Assert.Equal(0, status.Clicks);
    }

    [Theory]
    [InlineData(true, true, 186f, 186f)]
    [InlineData(true, false, 264f, 0f)]
    [InlineData(false, true, 0f, 336f)]
    [InlineData(false, false, 0f, 0f)]
    public void IndependentPanelVisibility_AllocatesOnlyVisibleIslandsWithoutReservingAFakeNeighbour(bool hierarchyVisible, bool inspectorVisible,
        float leftWidth, float rightWidth)
    {
        var left = new Box(1000, 40) { IsVisible = hierarchyVisible };
        var right = new Box(1000, 40) { IsVisible = inspectorVisible };
        using var layout = new EditorLayout(new Box(10, 10), left, new Box(10, 10), right, new Box(10, 10));
        Arrange(layout, 400, 300);
        Assert.Equal(new Rect(0, 52, 400, 220), layout.Viewport.Bounds);
        Assert.Equal(leftWidth, left.Bounds.Width); Assert.Equal(rightWidth, right.Bounds.Width);
        Assert.Equal(hierarchyVisible ? new Rect(8, 60, leftWidth, 40) : default, left.Bounds);
        Assert.Equal(inspectorVisible ? new Rect(392 - rightWidth, 60, rightWidth, 40) : default, right.Bounds);
    }

    private static void Arrange(Element layout, float width, float height)
    { layout.Measure(new(width, height), new TextLayout()); layout.Arrange(new(0, 0, width, height)); }
    private static void Click(UiSession ui, float x, float y)
    { ui.HandleInput(new(InputKind.PointerDown, x, y)); ui.HandleInput(new(InputKind.PointerUp, x, y)); }

    private sealed class Box(float width, float height, string? name = null, List<string>? painted = null) : Element
    {
        public int Clicks { get; private set; }
        protected override Size MeasureCore(Size available, ITextLayoutService text) =>
            new(Math.Min(width, available.Width), Math.Min(height, available.Height));
        protected override void DrawCore(IDrawingContext context) { if (name != null) painted!.Add(name); }
        public override bool HandleInput(UiInput input, IInputContext context)
        { if (input.Kind != InputKind.PointerDown) return false; Clicks++; return true; }
    }
    private sealed class TextLayout : ITextLayoutService
    { public Size Measure(string text, float fontSize, string fontFamily) => new(text.Length * fontSize / 2, fontSize); }
    private sealed class Surface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new TextLayout();
        public void VerifyAvailable() { } public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport) { } public void Dispose() { }
    }
    private sealed class Drawing : IDrawingContext
    {
        public void Save() { } public void Restore() { } public void Clip(Rect rectangle) { } public void Translate(float x, float y) { }
        public void Fill(Rect rectangle, Color color, float radius = 0) { }
        public void Stroke(Rect rectangle, Color color, float width = 1, float radius = 0) { }
        public void Text(string text, float x, float y, Color color, float fontSize, string fontFamily) { }
        public void Image(IImageSource source, Rect destination) { }
    }
}
