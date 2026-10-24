using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests;

public sealed class EditorIslandTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InitialContext_CompletesBeforeAttachmentAndDoesNotScheduleAnEntrance(bool open)
    {
        var child = new Probe();
        var island = new EditorIsland(child) { IsOpen = open };
        var clock = new HeaderClock();
        using var ui = Create(island, clock);
        ui.Update();
        Assert.Equal(open ? 1f : 0f, island.Reveal);
        Assert.Equal(open ? 1f : 0f, island.Opacity);
        Assert.Equal(open, island.IsVisible);
        Assert.Equal(open, child.IsEnabled);
        Assert.False(island.NeedsFrame);
        Assert.False(ui.NeedsUpdate);
        clock.Advance(1000);
        Assert.False(ui.Update());
    }

    [Theory]
    [InlineData(false, -4f)]
    [InlineData(true, 4f)]
    public void Closing_DisablesContentImmediatelyAndKeepsHalfOpacitySlideUntil160Milliseconds(bool fromRight, float halfSlide)
    {
        var child = new Probe();
        var island = new EditorIsland(child, fromRight) { Padding = new(8) };
        var clock = new HeaderClock();
        using var ui = Create(island, clock);
        ui.Update();
        var original = child.Bounds;
        island.IsOpen = false;
        Assert.False(child.IsEffectivelyEnabled);
        Assert.True(island.IsEnabled);
        Assert.True(island.IsVisible);
        Assert.True(island.NeedsFrame);
        ui.Update();
        Assert.Equal(1f, island.Reveal);
        clock.Advance(80); ui.Update();
        Assert.Equal(.5f, island.Reveal, 5);
        Assert.Equal(.5f, island.Opacity, 5);
        Assert.Equal(original.X + halfSlide, child.Bounds.X, 3);
        Assert.Equal(original.Y, child.Bounds.Y);
        Assert.Equal(original.Width, child.Bounds.Width);
        Assert.True(island.IsVisible);
        clock.Advance(79); ui.Update();
        Assert.True(island.IsVisible); Assert.InRange(island.Reveal, 0.000001f, .001f);
        clock.Advance(1); ui.Update();
        Assert.Equal(0f, island.Reveal);
        Assert.Equal(0f, island.Opacity);
        Assert.False(island.IsVisible);
        Assert.False(island.NeedsFrame);
        Assert.False(ui.NeedsUpdate);
        Assert.False(child.IsDisposed);
    }

    [Fact]
    public void OpeningAndRapidReversal_ContinueFromCurrentProgressAndSettleWithoutIdleFrames()
    {
        var child = new Probe();
        var island = new EditorIsland(child) { IsOpen = false, Padding = new(8) };
        var clock = new HeaderClock();
        using var ui = Create(island, clock);
        ui.Update();
        island.IsOpen = true; ui.Update();
        Assert.True(child.IsEffectivelyEnabled); Assert.True(island.IsVisible);
        Assert.Equal(0f, island.Reveal); Assert.True(island.NeedsFrame);
        clock.Advance(80); ui.Update();
        Assert.Equal(.5f, island.Reveal, 5);
        var half = child.Bounds;
        island.IsOpen = false;
        Assert.Equal(.5f, island.Reveal, 5);
        ui.Update(); Assert.Equal(half, child.Bounds);
        clock.Advance(80); ui.Update(); Assert.Equal(.25f, island.Reveal, 5);
        var reversed = child.Bounds;
        island.IsOpen = true;
        Assert.Equal(.25f, island.Reveal, 5);
        ui.Update(); Assert.Equal(reversed, child.Bounds);
        clock.Advance(160); ui.Update();
        Assert.Equal(1f, island.Reveal); Assert.Equal(1f, island.Opacity);
        Assert.True(child.IsEffectivelyEnabled); Assert.False(island.NeedsFrame); Assert.False(ui.NeedsUpdate);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Detach_CompletesCurrentTargetWithoutSchedulingOrDisposingRetainedContent(bool open)
    {
        var child = new Probe(); var island = new EditorIsland(child) { IsOpen = !open };
        var root = new Panel(); root.Add(island); var clock = new HeaderClock();
        using var ui = Create(root, clock);
        ui.Update(); island.IsOpen = open; ui.Update(); clock.Advance(40); ui.Update();
        Assert.True(island.NeedsFrame);
        root.Items.Remove(island);
        Assert.False(island.IsAttached); Assert.False(child.IsAttached);
        Assert.False(island.NeedsFrame); Assert.Equal(open ? 1f : 0f, island.Reveal);
        Assert.Equal(open, island.IsVisible); Assert.False(island.IsDisposed); Assert.False(child.IsDisposed);
        ui.Update(); clock.Advance(1000); Assert.False(ui.Update());
        root.Add(island); ui.Update();
        Assert.Same(child, island.Child); Assert.Equal(open ? 1f : 0f, island.Reveal); Assert.False(island.NeedsFrame);
    }

    [Fact]
    public void Reopening_PreservesTheBorrowedContentsDisabledState()
    {
        var child = new Probe { IsEnabled = false };
        var island = new EditorIsland(child);
        using var ui = Create(island, new HeaderClock()); ui.Update();
        island.IsOpen = false; island.IsOpen = true; ui.Update();
        Assert.False(child.IsEnabled); Assert.False(child.IsEffectivelyEnabled); Assert.True(island.IsEnabled);
        Assert.Same(child, island.Child);
    }

    [Fact]
    public void Ownership_RejectsInvalidContentAndDisposesAdoptedContentExactlyOnce()
    {
        Assert.Throws<ArgumentNullException>(() => new EditorIsland(null!));
        using var original = new Panel(); var child = new Probe(); original.Add(child);
        Assert.Throws<InvalidOperationException>(() => new EditorIsland(child));
        Assert.Same(original, child.Parent); Assert.False(child.IsDisposed);
        original.Items.Remove(child);
        var island = new EditorIsland(child); island.Dispose(); island.Dispose();
        Assert.True(child.IsDisposed); Assert.Equal(1, child.Disposals);
        Assert.Throws<ObjectDisposedException>(() => new EditorIsland(child));
    }

    [Fact]
    public void ClosingFocusedContent_ClearsFocusAndRejectsInputWhileViewportReceivesClicksAfterFade()
    {
        var viewport = new Probe(); var content = new Probe();
        var island = new EditorIsland(content) { Padding = new(8) };
        using var root = new EditorLayout(new Probe(), new Probe(), viewport, island, new Probe());
        var clock = new HeaderClock(); using var ui = Create(root, clock); ui.Update();
        var contentBounds = content.Bounds;
        Click(ui, contentBounds); Assert.Equal(1, content.PointerDowns); Assert.True(content.IsFocused);
        var fixedViewport = viewport.Bounds;
        island.IsOpen = false; ui.Update();
        Assert.False(content.IsFocused); Assert.False(content.IsEffectivelyEnabled);
        Click(ui, content.Bounds); Assert.Equal(1, content.PointerDowns);
        clock.Advance(160); ui.Update();
        Assert.False(island.IsVisible); Assert.Equal(fixedViewport, viewport.Bounds);
        Click(ui, contentBounds);
        Assert.Equal(1, viewport.PointerDowns); Assert.True(viewport.IsFocused);
        Assert.Equal(0, content.Keys);
    }

    [Theory]
    [InlineData(false, 8)]
    [InlineData(true, 5)]
    public void Shadow_StaysInsideEightUnitGutterWithoutChangingContentBounds(bool light, int alpha)
    {
        var child = new Probe { CornerRadius = 8 };
        using var island = new EditorIsland(child) { Padding = new(8) };
        island.ApplyTheme(light ? Theme.Light : Theme.Dark);
        island.Measure(new(100, 70), new TextLayout()); island.Arrange(new(0, 0, 100, 70));
        var before = child.Bounds; var drawing = new ShadowDrawing(); island.Draw(drawing);
        Assert.Equal(new Rect(8, 8, 84, 54), before); Assert.Equal(before, child.Bounds);
        Assert.Equal(6, drawing.Fills.Count); Assert.Equal(0, drawing.Depth);
        Assert.All(drawing.Fills, fill =>
        {
            Assert.Equal(new Color(0, 0, 0, (byte)alpha), fill.Color);
            Assert.InRange(fill.Area.X, 0, 100); Assert.InRange(fill.Area.X + fill.Area.Width, 0, 100);
            Assert.InRange(fill.Area.Y, 0, 70); Assert.InRange(fill.Area.Y + fill.Area.Height, 0, 70);
            Assert.InRange(fill.Radius, 9, 14);
        });
        Assert.Equal(new Rect(2, 4, 96, 66), drawing.Fills[0].Area);
        Assert.Equal(new Rect(7, 9, 86, 56), drawing.Fills[^1].Area);
    }

    private static UiSession Create(Element root, HeaderClock clock) =>
        new(root, new Surface(), new UiViewport(400, 300), null, new UiDispatchOptions(), clock);
    private static void Click(UiSession ui, Rect bounds)
    {
        var x = bounds.X + bounds.Width / 2; var y = bounds.Y + bounds.Height / 2;
        ui.HandleInput(new(InputKind.PointerDown, x, y)); ui.HandleInput(new(InputKind.PointerUp, x, y));
    }

    private sealed class Probe : Element
    {
        public int Disposals { get; private set; }
        public int PointerDowns { get; private set; }
        public int Keys { get; private set; }
        public override bool Focusable => true;
        protected override Size MeasureCore(Size available, ITextLayoutService text) =>
            new(Math.Min(200, available.Width), Math.Min(40, available.Height));
        public override bool HandleInput(UiInput input, IInputContext context)
        {
            if (input.Kind == InputKind.PointerDown) { PointerDowns++; context.Focus(this); return true; }
            if (input.Kind == InputKind.KeyDown) { Keys++; return true; }
            return false;
        }
        public override void Dispose() { if (!IsDisposed) Disposals++; base.Dispose(); }
    }

    private sealed class TextLayout : ITextLayoutService
    { public Size Measure(string text, float fontSize, string fontFamily) => new(text.Length * fontSize / 2, fontSize); }
    private sealed class Surface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new TextLayout();
        public void VerifyAvailable() { }
        public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport) { }
        public void Dispose() { }
    }
    private sealed class ShadowDrawing : IDrawingContext
    {
        public List<(Rect Area, Color Color, float Radius)> Fills { get; } = [];
        public int Depth { get; private set; }
        public void Save() => Depth++; public void Restore() => Depth--;
        public void Clip(Rect rectangle) { } public void Translate(float x, float y) { }
        public void Fill(Rect rectangle, Color color, float radius = 0) => Fills.Add((rectangle, color, radius));
        public void Stroke(Rect rectangle, Color color, float width = 1, float radius = 0) { }
        public void Text(string text, float x, float y, Color color, float fontSize, string fontFamily) { }
        public void Image(IImageSource source, Rect destination) { }
    }
}
