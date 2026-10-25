using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests;

public sealed class EditorOverlayLayoutTests
{
    [Fact]
    public void OverlayChrome_ExtendsSceneUnderCaptionAndFooterWhileIslandsKeepTheirVisibleMargins()
    {
        var hierarchyContent = new Box(100, 80); var inspectorContent = new Box(100, 1000);
        var hierarchy = new EditorIsland(hierarchyContent) { Padding = new(8) };
        var inspector = new EditorIsland(inspectorContent, true) { Padding = new(8) };
        using var layout = new EditorLayout(new Box(600, 180) { IsVisible = false }, hierarchy,
            new Box(2000, 2000), inspector, new Box(10, 10), overlayChrome: true);
        Arrange(layout, new(10, 20, 1280, 720));
        Assert.Equal(new Rect(10, 20, 1280, 720), layout.Viewport.Bounds);
        Assert.Equal(new Rect(10, 712, 1280, 28), layout.Status.Bounds);
        Assert.Equal(new Rect(26, 72, 248, 80), hierarchyContent.Bounds);
        Assert.Equal(new Rect(954, 72, 320, 624), inspectorContent.Bounds);
        Assert.Equal(16f, hierarchyContent.Bounds.X - layout.Viewport.Bounds.X);
        Assert.Equal(16f, layout.Status.Bounds.Y - inspectorContent.Bounds.Y - inspectorContent.Bounds.Height);
        var viewport = layout.Viewport.Bounds; inspector.IsOpen = false;
        Arrange(layout, new(10, 20, 1280, 720));
        Assert.Equal(viewport, layout.Viewport.Bounds); Assert.Equal(default, inspector.Bounds);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(3f, 3f)]
    [InlineData(12f, 35f)]
    [InlineData(80f, 100f)]
    [InlineData(320f, 240f)]
    [InlineData(1280f, 720f)]
    public void OverlayChrome_TinyAndNarrowWindowsKeepSceneFullAndEveryOverlayClipped(float width, float height)
    {
        using var layout = new EditorLayout(new Box(1000, 1000), new Box(1000, 1000),
            new Box(1000, 1000), new Box(1000, 1000), new Box(1000, 1000), overlayChrome: true);
        Arrange(layout, new(0, 0, width, height));
        Assert.Equal(new Rect(0, 0, width, height), layout.Viewport.Bounds);
        foreach (var child in layout.Children)
        {
            var b = child.Bounds;
            Assert.True(float.IsFinite(b.X + b.Y + b.Width + b.Height));
            Assert.InRange(b.Width, 0, width); Assert.InRange(b.Height, 0, height);
            Assert.InRange(b.X, 0, width); Assert.InRange(b.X + b.Width, 0, width);
            Assert.InRange(b.Y, 0, height); Assert.InRange(b.Y + b.Height, 0, height);
        }
        Assert.True(layout.Hierarchy.Bounds.X + layout.Hierarchy.Bounds.Width <= layout.Inspector.Bounds.X);
        Assert.True(layout.Hierarchy.Bounds.Y >= Math.Min(36, height));
    }

    [Fact]
    public void ContextualTools_PaintAndReceiveInputAboveIslandsWithoutResizingTheScene()
    {
        var painted = new List<string>();
        var tools = new Box(600, 120, "tools", painted) { IsVisible = false };
        var hierarchy = new Box(300, 200, "hierarchy", painted); var inspector = new Box(300, 200, "inspector", painted);
        var viewport = new Box(2000, 2000, "viewport", painted); var status = new Box(30, 20, "status", painted);
        using var layout = new EditorLayout(tools, hierarchy, viewport, inspector, status, overlayChrome: true);
        using var ui = new UiSession(layout, new Surface(), new(800, 500)); ui.Update();
        var scene = viewport.Bounds; tools.IsVisible = true; ui.Update(); layout.Draw(new Drawing());
        Assert.Equal(new[] { "viewport", "hierarchy", "inspector", "status", "tools" }, painted);
        Assert.Equal(scene, viewport.Bounds); Assert.Equal(new Rect(80, 44, 640, 120), tools.Bounds);
        Click(ui, 100, 60); Click(ui, 30, 200); Click(ui, 400, 300); Click(ui, 400, 490);
        Assert.Equal(1, tools.Clicks); Assert.Equal(1, hierarchy.Clicks); Assert.Equal(1, viewport.Clicks); Assert.Equal(1, status.Clicks);
        Assert.Equal(0, inspector.Clicks);
        tools.IsVisible = false; ui.Update(); Click(ui, 100, 60);
        Assert.Equal(2, hierarchy.Clicks); Assert.Equal(scene, viewport.Bounds);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void CompactEditor_DefaultFrameOverlayUsesLogicalGeometryWithoutAnIdleDocumentBand(float scale)
    {
        using var model = new EditorViewModel(new(EditorScene.Demo()), new((_, _) => Task.CompletedTask, CancellationToken.None));
        var view = new EditorView(model, new EditorViewStyle(), compactHeader: true);
        var frame = WindowFrame.CreateDefault("Editor", view); frame.TitleBarOverContent = true;
        using var ui = new UiSession(frame, new Surface(), new((int)(1280 * scale), (int)(800 * scale), scale)); ui.Update();
        var layout = DocumentTree(view).OfType<EditorLayout>().Single();
        Assert.True(layout.OverlayChrome); Assert.Equal(new Rect(1, 1, 1278, 798), view.Bounds);
        Assert.Equal(view.Bounds, view.ViewportFrame.Bounds); Assert.False(view.Toolbar.IsVisible);
        Assert.Equal(new Rect(2, 2, 1276, 796), view.Canvas.Bounds);
        Assert.Equal(53f, view.HierarchyIsland.Child!.Bounds.Y); Assert.False(view.ViewportTitle.IsVisible);
        Assert.False(ui.NeedsUpdate); Assert.False(ui.Update());
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(-1f)]
    public void InvalidChromeInset_RejectsBeforeAdoptingAnyRegion(float inset)
    {
        using var tools = new Box(1, 1); using var hierarchy = new Box(1, 1); using var viewport = new Box(1, 1);
        using var inspector = new Box(1, 1); using var status = new Box(1, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => new EditorLayout(tools, hierarchy, viewport, inspector, status, true, inset));
        Assert.All(new Element[] { tools, hierarchy, viewport, inspector, status }, element => { Assert.Null(element.Parent); Assert.False(element.IsDisposed); });
    }

    private static IEnumerable<Element> DocumentTree(Element root)
    { yield return root; foreach (var child in root.Children) foreach (var nested in DocumentTree(child)) yield return nested; }
    private static void Arrange(Element root, Rect bounds) { root.Measure(new(bounds.Width, bounds.Height), new TextLayout()); root.Arrange(bounds); }
    private static void Click(UiSession ui, float x, float y)
    { ui.HandleInput(new(InputKind.PointerDown, x, y)); ui.HandleInput(new(InputKind.PointerUp, x, y)); }
    private sealed class Box(float width, float height, string? name = null, List<string>? painted = null) : Element
    {
        public int Clicks { get; private set; }
        protected override Size MeasureCore(Size available, ITextLayoutService text) => new(Math.Min(width, available.Width), Math.Min(height, available.Height));
        protected override void DrawCore(IDrawingContext context) { if (name != null) painted!.Add(name); }
        public override bool HandleInput(UiInput input, IInputContext context)
        { if (input.Kind != InputKind.PointerDown) return false; Clicks++; return true; }
    }
    private sealed class TextLayout : ITextLayoutService
    { public Size Measure(string text, float fontSize, string fontFamily) => new(text.Length * fontSize / 2, fontSize); }
    private sealed class Surface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new TextLayout();
        public void VerifyAvailable() { } public void Resize(UiViewport viewport) { } public void Render(Element root, UiViewport viewport) { } public void Dispose() { }
    }
    private sealed class Drawing : IDrawingContext
    {
        public void Save() { } public void Restore() { } public void Clip(Rect rectangle) { } public void Translate(float x, float y) { }
        public void Fill(Rect rectangle, Color color, float radius = 0) { } public void Stroke(Rect rectangle, Color color, float width = 1, float radius = 0) { }
        public void Text(string text, float x, float y, Color color, float fontSize, string fontFamily) { } public void Image(IImageSource source, Rect destination) { }
    }
}
