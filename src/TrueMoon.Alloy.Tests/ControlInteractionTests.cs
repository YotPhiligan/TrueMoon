using SkiaSharp;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;
using Xunit;
using Image = TrueMoon.Argentis.Image;

namespace TrueMoon.Alloy.Tests;

public sealed class ControlInteractionTests
{
    private static UiSession Create(Element root) => new(root, new Surface(), new UiViewport(240, 140));

    [Theory]
    [InlineData(UiKey.Enter)]
    [InlineData(UiKey.Space)]
    public void Button_KeyboardRepeatActivatesOnceAndFocusLossCancelsRelease(UiKey key)
    {
        var calls = 0; var button = new Button("action").OnClick(() => calls++); using var ui = Create(button); ui.Update(); ui.Focus(button);
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: key)); ui.HandleInput(new UiInput(InputKind.KeyDown, Key: key));
        Assert.True(button.IsPressed); Assert.Equal(0, calls);
        ui.HandleInput(new UiInput(InputKind.KeyUp, Key: key)); Assert.False(button.IsPressed); Assert.Equal(1, calls);
        Assert.False(ui.HandleInput(new UiInput(InputKind.KeyUp, Key: key)).Handled); Assert.Equal(1, calls);
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: key)); ui.HandleInput(new UiInput(InputKind.FocusLost));
        Assert.False(button.IsPressed); Assert.False(button.IsFocused);
        ui.HandleInput(new UiInput(InputKind.KeyUp, Key: key)); Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ButtonAndCheckBox_DisablingDuringPressCancelsInputWithoutActivation(bool checkBox)
    {
        var calls = 0; Button button = checkBox ? new CheckBox("toggle") : new Button("action"); button.Click += () => calls++;
        var root = new Panel().WithChildren(button); using var ui = Create(root); ui.Update();
        Assert.True(ui.HandleInput(new UiInput(InputKind.PointerDown, 20, 20)).PointerCaptured); Assert.True(button.IsPressed);
        root.IsEnabled = false; ui.Update(); Assert.False(button.IsPressed); Assert.False(button.IsFocused);
        var released = ui.HandleInput(new UiInput(InputKind.PointerUp, 20, 20)); Assert.False(released.PointerCaptured); Assert.False(released.KeyboardFocused); Assert.Equal(0, calls);
        if (button is CheckBox check) Assert.False(check.IsChecked);
        root.IsEnabled = true; ui.Update(); ui.HandleInput(new UiInput(InputKind.PointerUp, 20, 20)); Assert.Equal(0, calls);
    }

    [Fact]
    public void Button_NonprimaryPointerAndHalfOpenReleaseEdgeDoNotActivate()
    {
        var clicks = 0; var button = new Button("action").Width(100).Height(40).OnClick(() => clicks++);
        using var ui = Create(button); ui.Update();
        Assert.False(ui.HandleInput(new UiInput(InputKind.PointerDown, 20, 20, Button: 1)).PointerCaptured); Assert.False(button.IsPressed);
        ui.HandleInput(new UiInput(InputKind.PointerDown, 20, 20)); ui.HandleInput(new UiInput(InputKind.PointerUp, 100, 20));
        Assert.Equal(0, clicks); Assert.False(button.IsPressed);
        ui.HandleInput(new UiInput(InputKind.PointerDown, 20, 20)); ui.HandleInput(new UiInput(InputKind.PointerUp, 99.99f, 20)); Assert.Equal(1, clicks);
    }

    [Fact]
    public void Slider_DragOutsideBoundsClampsAndNonprimaryButtonsCannotChangeOrEndGesture()
    {
        var slider = new Slider().Width(100).Height(28); using var ui = Create(slider); ui.Update();
        Assert.False(ui.HandleInput(new UiInput(InputKind.PointerDown, 30, 10, Button: 1)).PointerCaptured); Assert.Equal(0, slider.Value);
        Assert.True(ui.HandleInput(new UiInput(InputKind.PointerDown, 30, 10)).PointerCaptured); Assert.Equal(.3f, slider.Value, 4);
        var secondary = ui.HandleInput(new UiInput(InputKind.PointerDown, 90, 10, Button: 1)); Assert.False(secondary.Handled); Assert.True(secondary.PointerCaptured); Assert.Equal(.3f, slider.Value, 4);
        secondary = ui.HandleInput(new UiInput(InputKind.PointerUp, 90, 10, Button: 1)); Assert.False(secondary.Handled); Assert.True(secondary.PointerCaptured); Assert.Equal(.3f, slider.Value, 4);
        ui.HandleInput(new UiInput(InputKind.PointerMove, -50, 10)); Assert.Equal(0, slider.Value);
        ui.HandleInput(new UiInput(InputKind.PointerMove, 500, 10)); Assert.Equal(1, slider.Value);
        Assert.False(ui.HandleInput(new UiInput(InputKind.PointerUp, 500, 10)).PointerCaptured); Assert.Equal(1, slider.Value);
        ui.HandleInput(new UiInput(InputKind.PointerMove, 0, 10)); Assert.Equal(1, slider.Value);
    }

    [Theory]
    [InlineData(UiKey.Left, 0f)]
    [InlineData(UiKey.Right, 1f)]
    public void Slider_ArrowAtEndpointDoesNotChangeValueOrRedrawAndCancelledDragStaysStopped(UiKey key, float endpoint)
    {
        var slider = new Slider { Value = endpoint }; using var ui = Create(slider); ui.Update(); ui.Focus(slider); ui.Update();
        Assert.True(ui.HandleInput(new UiInput(InputKind.KeyDown, Key: key)).Handled); Assert.Equal(endpoint, slider.Value); Assert.False(ui.Update());
        ui.HandleInput(new UiInput(InputKind.PointerDown, 60, 20)); var beforeCancel = slider.Value; ui.HandleInput(new UiInput(InputKind.FocusLost));
        var moved = ui.HandleInput(new UiInput(InputKind.PointerMove, 230, 20)); Assert.False(moved.PointerCaptured); Assert.False(moved.KeyboardFocused); Assert.Equal(beforeCancel, slider.Value);
    }

    [Fact]
    public void Slider_DisabledAncestorCancelsCapturedDragBeforeFurtherValueChanges()
    {
        var slider = new Slider(); var root = new Panel().WithChildren(slider); using var ui = Create(root); ui.Update();
        ui.HandleInput(new UiInput(InputKind.PointerDown, 60, 20)); var value = slider.Value; root.IsEnabled = false; ui.Update();
        var result = ui.HandleInput(new UiInput(InputKind.PointerMove, 220, 20)); Assert.False(result.PointerCaptured); Assert.False(result.KeyboardFocused); Assert.Equal(value, slider.Value);
        root.IsEnabled = true; ui.Update(); ui.HandleInput(new UiInput(InputKind.PointerMove, 220, 20)); Assert.Equal(value, slider.Value);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(.5f, 100f)]
    [InlineData(1f, 200f)]
    public void ProgressBar_DrawsExactNormalizedExtentAndDoesNotTakeKeyboardFocus(float value, float width)
    {
        using var progress = new ProgressBar { Value = value, Width = 200, Height = 20 };
        progress.Measure(new Size(240, 140), new FixedTextLayout()); progress.Arrange(new Rect(10, 15, 200, 20)); var drawing = new Drawing(); progress.Draw(drawing);
        Assert.False(progress.Focusable); Assert.Equal(2, drawing.Fills.Count); Assert.Equal(new Rect(10, 15, 200, 20), drawing.Fills[0].Bounds);
        Assert.Equal(new Rect(10, 15, width, 20), drawing.Fills[1].Bounds); Assert.Equal(Theme.Dark.Control, drawing.Fills[0].Color); Assert.Equal(Theme.Dark.Accent, drawing.Fills[1].Color);
        Assert.Equal(drawing.Saves, drawing.Restores);
    }

    [Theory]
    [InlineData(-.001f)]
    [InlineData(1.001f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void ProgressBarAndSlider_InvalidValuesPreservePriorValueAndDoNotInvalidate(float invalid)
    {
        using var progress = new ProgressBar { Value = .5f }; using var slider = new Slider { Value = .5f }; var changes = 0;
        progress.Invalidated += _ => changes++; slider.Invalidated += _ => changes++;
        Assert.Throws<ArgumentOutOfRangeException>(() => progress.Value = invalid); Assert.Throws<ArgumentOutOfRangeException>(() => slider.Value = invalid);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Style().With(ProgressBar.ValueProperty, invalid));
        Assert.Equal(.5f, progress.Value); Assert.Equal(.5f, slider.Value); Assert.Equal(0, changes);
    }

    [Fact]
    public void DisabledControls_DrawDisabledTextCheckedMarkAndProgressFill()
    {
        var check = new CheckBox("checked") { IsChecked = true }; var progress = new ProgressBar { Value = .5f, Height = 20 }; var slider = new Slider { Value = .5f, Height = 28 };
        var root = new VStack { IsEnabled = false }.WithChildren(check, progress, slider, new Text("label")); using var ui = Create(root); ui.Update();
        var drawing = new Drawing(); root.Draw(drawing);
        Assert.All(drawing.Texts, text => Assert.Equal(Theme.Dark.Disabled, text.Color));
        Assert.Contains(drawing.Fills, fill => fill.Bounds.Width == 8 && fill.Bounds.Height == 8 && fill.Color == Theme.Dark.Disabled);
        Assert.Contains(drawing.Strokes, stroke => stroke.Bounds.Width == 16 && stroke.Color == Theme.Dark.Disabled);
        Assert.Contains(drawing.Fills, fill => fill.Bounds == progress.Bounds with { Width = progress.Bounds.Width / 2 } && fill.Color == Theme.Dark.Disabled);
        Assert.Contains(drawing.Fills, fill => fill.Bounds == slider.Bounds with { Width = slider.Bounds.Width / 2 } && fill.Color == Theme.Dark.Disabled);
    }

    [Fact]
    public void TextBox_SelectionAndCaretUseDistinctDrawingGeometryAndDisabledInputCannotEdit()
    {
        var editor = new TextBox { Value = "AB", Width = 200, Height = 32 }; using var ui = Create(editor); ui.Update(); ui.Focus(editor);
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.A, Control: true)); var drawing = new Drawing(); editor.Draw(drawing);
        Assert.Contains(drawing.Fills, fill => fill.Bounds == new Rect(8, 8, 16, 16) && fill.Color == Theme.Dark.Accent with { A = 100 });
        Assert.Contains(drawing.Fills, fill => fill.Bounds == new Rect(24, 8, 1, 16) && fill.Color == Theme.Dark.Foreground);
        Assert.Equal(2, editor.SelectionLength); Assert.Equal(2, editor.CaretIndex);
        editor.IsEnabled = false; ui.Update(); Assert.False(ui.HandleInput(new UiInput(InputKind.Text, Text: "replacement")).Handled); Assert.Equal("AB", editor.Value);
        drawing = new Drawing(); editor.Draw(drawing); Assert.DoesNotContain(drawing.Fills, fill => fill.Color == Theme.Dark.Accent with { A = 100 });
        Assert.Equal(Theme.Dark.Disabled, Assert.Single(drawing.Texts).Color); Assert.False(editor.IsFocused);
    }

    [Fact]
    public void TabAndShiftTab_WrapEligibleInteractiveNodesAndSkipHiddenDisabledAndPassiveNodes()
    {
        var first = new Button("first"); var check = new CheckBox("check"); var slider = new Slider(); var editor = new TextBox();
        var disabled = new Button("disabled") { IsEnabled = false }; var hidden = new Panel { IsVisible = false }.WithChildren(new Button("hidden"));
        var root = new VStack().WithChildren(first, new Text("passive"), disabled, hidden, new ProgressBar(), Image.Create(), check, slider, editor);
        using var ui = Create(root); ui.Update();
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Tab, Shift: true)); Assert.True(editor.IsFocused);
        foreach (var expected in new Element[] { first, check, slider, editor, first })
        {
            Assert.True(ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Tab)).Handled); Assert.True(expected.IsFocused);
            Assert.Equal(1, new Element[] { first, check, slider, editor }.Count(control => control.IsFocused));
        }
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Tab, Shift: true)); Assert.True(editor.IsFocused); Assert.False(disabled.IsFocused); Assert.False(hidden.Items[0].IsFocused);
    }

    [Fact]
    public void NestedScroll_WheelBubblesFromChildAndThenFromInnerBoundaryToOuter()
    {
        var inner = new ScrollViewer().Height(80).Content(new VStack().WithChildren(new Button("wheel through").Height(40), new SizedElement(100, 300)));
        var outer = new ScrollViewer().Content(new VStack().WithChildren(inner, new SizedElement(100, 300))); using var ui = Create(outer); ui.Update();
        Assert.True(ui.HandleInput(new UiInput(InputKind.Wheel, 20, 20, WheelDelta: -1)).Handled); ui.Update(); Assert.Equal(36, inner.Offset); Assert.Equal(0, outer.Offset);
        inner.Offset = 1000; ui.Update(); Assert.Equal(260, inner.Offset);
        Assert.True(ui.HandleInput(new UiInput(InputKind.Wheel, 20, 20, WheelDelta: -1)).Handled); ui.Update(); Assert.Equal(260, inner.Offset); Assert.Equal(36, outer.Offset);
        Assert.True(ui.HandleInput(new UiInput(InputKind.Wheel, 20, 100, WheelDelta: -1)).Handled); ui.Update(); Assert.Equal(260, inner.Offset); Assert.Equal(72, outer.Offset);
        outer.Offset = 1000; ui.Update(); Assert.Equal(240, outer.Offset);
        Assert.False(ui.HandleInput(new UiInput(InputKind.Wheel, 20, 100, WheelDelta: -1)).Handled); Assert.Equal(240, outer.Offset);
    }

    [Fact]
    public void ScrollClipping_PreventsOverflowingButtonFromStealingSiblingHits()
    {
        var innerClicks = 0; var siblingClicks = 0;
        var overflow = new Button("tall").Height(200).OnClick(() => innerClicks++);
        var scroll = new ScrollViewer().Height(80).Content(overflow);
        var sibling = new Button("below").Width(100).Height(30).Configure(x => { x.Margin = new Thickness(0, 90, 0, 0); x.HorizontalAlignment = LayoutAlignment.Start; x.VerticalAlignment = LayoutAlignment.Start; }).OnClick(() => siblingClicks++);
        var root = new Panel().WithChildren(sibling, scroll); using var ui = Create(root); ui.Update();
        Click(ui, 20, 20); Assert.Equal(1, innerClicks); Assert.Equal(0, siblingClicks);
        Click(ui, 20, 100); Assert.Equal(1, innerClicks); Assert.Equal(1, siblingClicks); Assert.True(sibling.IsFocused); Assert.False(overflow.IsFocused);
        Assert.Equal(200, scroll.Extent); Assert.Equal(80, scroll.Bounds.Height);
    }

    [Fact]
    public void ScrollExtentShrinkAndHiddenContent_ClampCurrentOffsetAtEveryRemainingRange()
    {
        var first = new SizedElement(100, 200); var second = new SizedElement(100, 200); var removed = new SizedElement(100, 200);
        var content = new VStack().WithChildren(first, second, removed); var scroll = new ScrollViewer().Content(content); using var ui = Create(scroll); ui.Update();
        scroll.Offset = 450; ui.Update(); Assert.Equal(450, scroll.Offset); Assert.Equal(600, scroll.Extent);
        content.Items.Remove(removed); removed.Dispose(); ui.Update(); Assert.Equal(260, scroll.Offset); Assert.Equal(400, scroll.Extent);
        second.IsVisible = false; ui.Update(); Assert.Equal(60, scroll.Offset); Assert.Equal(200, scroll.Extent);
        first.IsVisible = false; ui.Update(); Assert.Equal(0, scroll.Offset); Assert.Equal(0, scroll.Extent); Assert.Same(first, content.Items[0]);
    }

    [Fact]
    public void Image_SourceReplacementAndControlDisposalKeepBorrowedImagesAlive()
    {
        var first = new BorrowedImage(new Size(16, 12)); var second = new BorrowedImage(new Size(8, 4));
        var image = new Image { Source = first, Padding = new Thickness(2) }; using var ui = Create(image); ui.Update();
        Assert.Equal(new Size(20, 16), image.DesiredSize); var drawing = new Drawing(); image.Draw(drawing);
        Assert.Same(first, Assert.Single(drawing.Images).Source); Assert.Equal(new Rect(2, 2, 236, 136), drawing.Images[0].Bounds);
        image.Source = second; ui.Update(); Assert.Equal(new Size(12, 8), image.DesiredSize); Assert.Equal(0, first.Disposals);
        image.Source = null; ui.Update(); drawing = new Drawing(); image.Draw(drawing); Assert.Empty(drawing.Images); Assert.Equal(new Size(4, 4), image.DesiredSize);
        ui.Dispose(); Assert.Equal(0, first.Disposals); Assert.Equal(0, second.Disposals);
        first.Dispose(); second.Dispose(); Assert.Equal(1, first.Disposals); Assert.Equal(1, second.Disposals);
    }

    [Fact]
    public void Image_RealSkiaDecodeDrawAndBorrowedReuseProduceExactPixels()
    {
        SkiaNativeLibrary.Initialize(); var path = Path.Combine(Path.GetTempPath(), $"alloy-image-test-{Guid.NewGuid():N}.png");
        try
        {
            using (var bitmap = new SKBitmap(2, 2))
            {
                bitmap.Erase(new SKColor(20, 200, 60)); using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100); using var stream = File.Create(path); encoded.SaveTo(stream);
            }
            using var source = new SkiaImageSource(path); var image = new Image { Source = source, Width = 20, Height = 20, Padding = new Thickness(2), Margin = new Thickness(10), HorizontalAlignment = LayoutAlignment.Start, VerticalAlignment = LayoutAlignment.Start };
            using var ui = UiSession.Create(image, new SkiaRasterRenderBackend(), new RasterUiTarget(), new UiViewport(50, 50)); ui.Update();
            using var snapshot = ui.SnapshotRasterImage(); using var pixels = SKBitmap.FromImage(snapshot);
            Assert.Equal(new SKColor(20, 200, 60), pixels.GetPixel(20, 20)); Assert.Equal(new SKColor(0, 0, 0, 0), pixels.GetPixel(10, 10)); Assert.Equal(new SKColor(0, 0, 0, 0), pixels.GetPixel(35, 20));
            ui.Dispose(); Assert.Equal(new Size(2, 2), source.Size);
            using var reuse = UiSession.Create(new Image { Source = source }, new SkiaRasterRenderBackend(), new RasterUiTarget(), new UiViewport(20, 20)); reuse.Update();
            using var again = reuse.SnapshotRasterImage(); using var againPixels = SKBitmap.FromImage(again); Assert.Equal(new SKColor(20, 200, 60), againPixels.GetPixel(10, 10));
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(5f, 20f, 95f)]
    [InlineData(10f, 20f, 100f)]
    [InlineData(50f, 20f, 100f)]
    [InlineData(120f, 20f, 110f)]
    [InlineData(30f, 200f, 120f)]
    [InlineData(-1000f, 20f, 0f)]
    [InlineData(1000f, 20f, 380f)]
    [InlineData(10f, 0f, 100f)]
    public void BringIntoView_UsesPaddedViewportMinimalDeltaAndExtentClamp(float y, float height, float expectedOffset)
    {
        var scroll = new ScrollViewer { Padding = new Thickness(10) }.Content(new SizedElement(100, 500)); using var ui = Create(scroll); ui.Update(); scroll.Offset = 100; ui.Update();
        Assert.Equal(new Rect(10, 10, 220, 120), scroll.ChildClipBounds);
        scroll.BringIntoView(new Rect(10, y, 20, height)); Assert.Equal(expectedOffset, scroll.Offset); ui.Update();
        Assert.Equal(expectedOffset, scroll.Offset); Assert.Equal(500, scroll.Extent);
    }

    [Theory]
    [InlineData(float.NaN, 10f, 20f, 20f)]
    [InlineData(10f, float.PositiveInfinity, 20f, 20f)]
    [InlineData(10f, 10f, float.NegativeInfinity, 20f)]
    [InlineData(10f, 10f, 20f, float.NaN)]
    [InlineData(10f, 10f, -1f, 20f)]
    [InlineData(10f, 10f, 20f, -1f)]
    public void BringIntoView_InvalidRectanglePreservesOffsetAndDoesNotRedraw(float x, float y, float width, float height)
    {
        var scroll = new ScrollViewer().Content(new SizedElement(100, 500)); using var ui = Create(scroll); ui.Update(); scroll.Offset = 80; ui.Update();
        Assert.Throws<ArgumentOutOfRangeException>(() => scroll.BringIntoView(new Rect(x, y, width, height)));
        Assert.Equal(80, scroll.Offset); Assert.False(ui.Update());
    }

    [Fact]
    public void FocusNestedOffscreenControl_RevealsItFromInnerToOuterUsingActualScrollDelta()
    {
        var first = new Button("first").Height(40); var last = new Button("target").Height(40);
        var inner = new ScrollViewer().Height(80).Content(new VStack().WithChildren(first, new SizedElement(100, 110), last, new SizedElement(100, 110)));
        var outer = new ScrollViewer().Content(new VStack().WithChildren(new SizedElement(100, 150), inner, new SizedElement(100, 150)));
        using var ui = Create(outer); ui.Update(); Assert.Equal(300, last.Bounds.Y);
        ui.Focus(last); Assert.Equal(110, inner.Offset); Assert.Equal(90, outer.Offset); ui.Update();
        Assert.True(last.IsFocused); Assert.Equal(new Rect(0, 100, 240, 40), last.Bounds);
        Assert.True(inner.ChildClipBounds.Contains(last.Bounds.X, last.Bounds.Y)); Assert.True(outer.ChildClipBounds.Contains(last.Bounds.X, last.Bounds.Y));
        ui.Focus(first); ui.Update(); Assert.True(first.IsFocused); Assert.False(last.IsFocused); Assert.Equal(0, inner.Offset); Assert.Equal(90, outer.Offset); Assert.Equal(60, first.Bounds.Y);
    }

    [Fact]
    public void PaddedScroll_ClipsDrawingAndHitTestingToSameInnerViewport()
    {
        var clicks = 0; var button = new Button("") { Height = 200, Background = new Color(255, 0, 0) }.OnClick(() => clicks++);
        var scroll = new ScrollViewer { Padding = new Thickness(10) }.Content(button); using var ui = Create(scroll); ui.Update();
        Click(ui, 5, 20); Assert.Equal(0, clicks); Click(ui, 20, 135); Assert.Equal(0, clicks); Click(ui, 20, 20); Assert.Equal(1, clicks);
        var fill = new SizedElement(100, 300) { Background = new Color(255, 0, 0) }; var rasterScroll = new ScrollViewer { Padding = new Thickness(10) }.Content(fill);
        using var raster = UiSession.Create(rasterScroll, new SkiaRasterRenderBackend(), new RasterUiTarget(), new UiViewport(240, 140)); raster.Update();
        using var image = raster.SnapshotRasterImage(); using var bitmap = SKBitmap.FromImage(image);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(20, 20)); Assert.Equal(new SKColor(0, 0, 0, 0), bitmap.GetPixel(5, 20));
        Assert.Equal(new SKColor(0, 0, 0, 0), bitmap.GetPixel(20, 5)); Assert.Equal(new SKColor(0, 0, 0, 0), bitmap.GetPixel(20, 135));
    }

    private static void Click(UiSession ui, float x, float y) { ui.HandleInput(new UiInput(InputKind.PointerDown, x, y)); ui.HandleInput(new UiInput(InputKind.PointerUp, x, y)); }
    private sealed class BorrowedImage(Size size) : IImageSource { public int Disposals { get; private set; } public Size Size => size; public void Dispose() => Disposals++; }
    private sealed class Surface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new FixedTextLayout();
        public void VerifyAvailable() { }
        public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport) => root.Draw(new RecordingDrawingContext());
        public void Dispose() { }
    }
    private sealed class Drawing : IDrawingContext
    {
        public int Saves { get; private set; } public int Restores { get; private set; }
        public List<(Rect Bounds, Color Color)> Fills { get; } = []; public List<(Rect Bounds, Color Color)> Strokes { get; } = [];
        public List<(string Value, Color Color)> Texts { get; } = []; public List<(IImageSource Source, Rect Bounds)> Images { get; } = [];
        public void Save() => Saves++; public void Restore() => Restores++; public void Clip(Rect rectangle) { } public void Translate(float x, float y) { }
        public void Fill(Rect rectangle, Color color, float radius = 0) => Fills.Add((rectangle, color));
        public void Stroke(Rect rectangle, Color color, float width = 1, float radius = 0) => Strokes.Add((rectangle, color));
        public void Text(string text, float x, float y, Color color, float fontSize, string fontFamily) => Texts.Add((text, color));
        public void Image(IImageSource source, Rect destination) => Images.Add((source, destination));
    }
}
