using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class SplitViewTests
{
    [Theory]
    [InlineData(false, SplitOrientation.Horizontal)] [InlineData(true, SplitOrientation.Horizontal)]
    [InlineData(false, SplitOrientation.Vertical)] [InlineData(true, SplitOrientation.Vertical)]
    public void Divider_UsesThemeHoverFocusDisabledAndLocalStyleWithBalancedClipping(bool light, SplitOrientation orientation)
    {
        using var split = new SplitView { Orientation = orientation }; split.ApplyTheme(light ? Theme.Light : Theme.Dark); Arrange(split, 400);
        var theme = split.Theme; var bounds = split.Divider.Bounds;
        var line = orientation == SplitOrientation.Horizontal ? new Rect(bounds.X + 3, bounds.Y, 2, bounds.Height)
            : new Rect(bounds.X, bounds.Y + 3, bounds.Width, 2);
        Check(theme.Border); split.Divider.IsHovered = true; Check(theme.BorderHover);
        split.Divider.IsFocused = true; Check(theme.Focus); split.IsEnabled = false; Check(theme.Disabled);
        split.Divider.Style = new Style().With(UiProperties.Foreground, theme.Accent); Check(theme.Accent);
        void Check(Color color)
        {
            var drawing = new OutlineDrawing(); split.Divider.Draw(drawing);
            Assert.Contains(drawing.Fills, fill => fill == (line, color)); Assert.Equal(drawing.Saves, drawing.Restores);
        }
    }

    [Fact]
    public void AutoPreference_ClickWithoutMovementRemainsAutomaticAndUnrelatedInputDoesNotResize()
    {
        var split = new SplitView(); using var ui = Ui(split); ui.Update();
        Assert.Equal(196, split.ActualFirstPaneSize);
        Assert.False(ui.HandleInput(new(InputKind.PointerDown, X: 198, Y: 20, Button: 1)).PointerCaptured);
        Down(ui, split); Assert.True(ui.HandleInput(new(InputKind.PointerUp, X: 198, Y: 2, Button: 1)).PointerCaptured);
        Assert.False(ui.HandleInput(new(InputKind.PointerUp, X: 198, Y: 2)).PointerCaptured);
        Assert.True(float.IsNaN(split.FirstPaneSize));
        Assert.False(ui.HandleInput(new(InputKind.Wheel, X: 198, Y: 20, WheelDelta: 1)).Handled);
        ui.Resize(new(200, 400)); ui.Update(); Assert.Equal(96, split.ActualFirstPaneSize);
    }

    [Theory]
    [InlineData(1f)] [InlineData(1.5f)] [InlineData(2f)]
    public void Input_LogicalCoordinatesWorkAtViewportScaleAndNestedDividerDoesNotResizeParent(float scale)
    {
        var child = new SplitView { Orientation = SplitOrientation.Vertical, FirstPaneSize = 100 };
        var root = new SplitView(new Text("left"), child) { FirstPaneSize = 120 };
        using var ui = CompactControlHarness.Ui(root, (int)(400 * scale), (int)(400 * scale));
        ui.Resize(new((int)(400 * scale), (int)(400 * scale), scale)); ui.Update();
        Down(ui, child); ui.HandleInput(new(InputKind.PointerMove, X: 140, Y: 142)); ui.Update();
        Assert.Equal(140, child.ActualFirstPaneSize); Assert.Equal(120, root.ActualFirstPaneSize);
        Assert.True(child.IsResizing); Assert.False(root.IsResizing);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape)); ui.Update();
        Assert.Equal(100, child.FirstPaneSize); Assert.Equal(120, root.FirstPaneSize);
    }

    [Fact]
    public void TypedStyle_LocalOverrideAndClearRelayoutWithoutReplacingContent()
    {
        var first = new TextBox { Value = "retained" }; var split = new SplitView(first, new TextBox())
        { Style = new Style().With(SplitView.FirstPaneSizeProperty, 180f).With(SplitView.DividerThicknessProperty, 12f) };
        using var ui = Ui(split); ui.Update(); Assert.Equal(180, split.ActualFirstPaneSize); Assert.Equal(12, split.Divider.Bounds.Width);
        split.FirstPaneSize = 210; ui.SetTheme(Theme.Light); ui.Update(); Assert.Equal(210, split.ActualFirstPaneSize);
        split.Clear(SplitView.FirstPaneSizeProperty); ui.Update(); Assert.Equal(180, split.ActualFirstPaneSize);
        Assert.Same(first, split.FirstPane.Child); Assert.Equal("retained", first.Value); Assert.False(ui.Update());
    }

    [Theory]
    [InlineData(SplitOrientation.Horizontal)] [InlineData(SplitOrientation.Vertical)]
    public void Layout_UsesAxisConstraintsAndKeepsPreferredSizeAcrossHostResize(SplitOrientation orientation)
    {
        using var split = new SplitView(new Text("first"), new Text("second")) { Orientation = orientation, FirstPaneSize = 280 };
        Constraints(split, 100, 300, 80, 240);
        Arrange(split, 400);
        Assert.Equal(280, split.ActualFirstPaneSize);
        Assert.Equal(Slot(orientation, 0, 280), split.FirstPane.Bounds);
        Assert.Equal(Slot(orientation, 280, 8), split.Divider.Bounds);
        Assert.Equal(Slot(orientation, 288, 112), split.SecondPane.Bounds);
        Arrange(split, 220);
        Assert.Equal(132, split.ActualFirstPaneSize); Assert.Equal(280, split.FirstPaneSize);
        Arrange(split, 400); Assert.Equal(280, split.ActualFirstPaneSize);
    }

    [Theory]
    [InlineData(0, 0, 0, 0)] [InlineData(4, 0, 4, 0)] [InlineData(8, 0, 8, 0)]
    [InlineData(108, 40, 8, 60)] [InlineData(258, 100, 8, 150)]
    public void Layout_ShortageProportionallyCompressesMinimaWithoutOverflow(float axis, float first, float divider, float second)
    {
        using var split = new SplitView { FirstPaneSize = 180 };
        Constraints(split, 100, 300, 150, 300); Arrange(split, axis);
        Assert.Equal(first, split.FirstPane.Bounds.Width, 4); Assert.Equal(second, split.SecondPane.Bounds.Width, 4);
        Assert.Equal(divider, split.Divider.Bounds.Width); Assert.Equal(10 + axis, split.SecondPane.Bounds.X + split.SecondPane.Bounds.Width, 4);
        Assert.Equal(180, split.FirstPaneSize);
    }

    [Fact]
    public void Layout_MaximaLeaveTrailingSpaceAndUnboundedMeasureStaysFinite()
    {
        using var split = new SplitView(new Text("a"), new Text("b"));
        Constraints(split, 20, 100, 30, 140); Arrange(split, 500);
        Assert.Equal(100, split.ActualFirstPaneSize); Assert.Equal(140, split.SecondPane.Bounds.Width);
        Assert.Equal(258, split.SecondPane.Bounds.X + split.SecondPane.Bounds.Width);
        split.Measure(new(float.PositiveInfinity, 100), new FixedTextLayout());
        Assert.True(float.IsFinite(split.DesiredSize.Width)); Assert.True(float.IsNaN(split.FirstPaneSize));
    }

    [Theory]
    [InlineData(SplitOrientation.Horizontal)] [InlineData(SplitOrientation.Vertical)]
    public void Pointer_DragClampsAndReleaseOutsideCompletesWithoutRetainingCapture(SplitOrientation orientation)
    {
        var split = new SplitView { Orientation = orientation, FirstPaneSize = 120 };
        Constraints(split, 80, 220, 60, 300);
        using var ui = Ui(split); ui.Update();
        Assert.True(Down(ui, split).PointerCaptured); Assert.True(split.Divider.IsFocused);
        Move(ui, orientation, 900); ui.Update(); Assert.Equal(220, split.ActualFirstPaneSize);
        var release = ui.HandleInput(Input(orientation, InputKind.PointerUp, -40)); ui.Update();
        Assert.False(release.PointerCaptured); Assert.False(split.IsResizing); Assert.Equal(92, split.ActualFirstPaneSize);
        Assert.False(ui.HandleInput(Input(orientation, InputKind.PointerMove, 200)).PointerCaptured);
        Assert.Equal(92, split.FirstPaneSize);
    }

    [Theory]
    [InlineData("escape")] [InlineData("focus")] [InlineData("window")] [InlineData("capture")]
    [InlineData("hide")] [InlineData("disable")] [InlineData("detach")] [InlineData("resize")]
    [InlineData("orientation")] [InlineData("constraints")] [InlineData("dispose")]
    public void Cancellation_RestoresPreferenceAndReleasesGesture(string terminal)
    {
        var editor = new TextBox { Value = "draft" }; var split = new SplitView(editor, new TextBox()) { FirstPaneSize = 120 };
        var root = new ContentControl(); root.SetContent(split);
        using var ui = CompactControlHarness.Ui(root, 400, 400); ui.Update();
        Down(ui, split); Move(ui, split.Orientation, 200); ui.Update(); Assert.True(split.IsResizing);
        switch (terminal)
        {
            case "escape": ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape)); break;
            case "focus": ui.Focus(editor); break;
            case "window": ui.HandleInput(new(InputKind.FocusLost)); break;
            case "capture": ui.Capture(editor); ui.ReleaseCapture(editor); break;
            case "hide": root.IsVisible = false; ui.Update(); break;
            case "disable": root.IsEnabled = false; ui.Update(); break;
            case "detach": root.SetContent(null); break;
            case "resize": ui.Resize(new(300, 400)); ui.Update(); break;
            case "orientation": split.Orientation = SplitOrientation.Vertical; break;
            case "constraints": split.FirstPane.MinWidth = 130; ui.Update(); break;
            case "dispose": root.SetContent(null); split.Dispose(); break;
        }
        Assert.False(split.IsResizing);
        if (!split.IsDisposed) { Assert.Equal(120, split.FirstPaneSize); Assert.Equal("draft", editor.Value); }
        Assert.False(ui.HandleInput(new(InputKind.PointerMove, X: 350, Y: 350)).PointerCaptured);
        if (split.Parent == null && !split.IsDisposed) split.Dispose();
    }

    [Fact]
    public void ExternalPreference_InterruptsDragAndDoesNotRollBackNewValue()
    {
        var split = new SplitView { FirstPaneSize = 120 }; using var ui = Ui(split); ui.Update();
        Down(ui, split); Move(ui, split.Orientation, 180); split.FirstPaneSize = 250; ui.Update();
        Assert.False(split.IsResizing); Assert.Equal(250, split.ActualFirstPaneSize);
        Assert.False(ui.HandleInput(new(InputKind.PointerMove, X: 300)).PointerCaptured);
    }

    [Fact]
    public void ReentrantPreferenceObserver_InterruptsBeforeNextPointerMoveCanOverwriteConsumerValue()
    {
        var split = new SplitView { FirstPaneSize = 120 }; using var ui = Ui(split); ui.Update();
        split.PropertyChanged += (_, property) =>
        {
            if (ReferenceEquals(property, SplitView.FirstPaneSizeProperty) && split.FirstPaneSize is > 120 and < 250)
                split.FirstPaneSize = 250;
        };
        Down(ui, split); Move(ui, split.Orientation, 180); ui.Update();
        Assert.False(split.IsResizing); Assert.Equal(250, split.ActualFirstPaneSize);
        Assert.False(ui.HandleInput(new(InputKind.PointerMove, X: 200)).PointerCaptured);
        Assert.Equal(250, split.FirstPaneSize);
    }

    [Theory]
    [InlineData(SplitOrientation.Horizontal)] [InlineData(SplitOrientation.Vertical)]
    public void Keyboard_TabAxisArrowsShiftHomeEndRespectBoundsAndIgnoreOtherKeys(SplitOrientation orientation)
    {
        var split = new SplitView { Orientation = orientation, FirstPaneSize = 120 };
        Constraints(split, 80, 240, 80, 300); using var ui = Ui(split); ui.Update();
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Tab)); Assert.True(split.Divider.IsFocused);
        var positive = orientation == SplitOrientation.Horizontal ? UiKey.Right : UiKey.Down;
        ui.HandleInput(new(InputKind.KeyDown, Key: positive)); ui.HandleInput(new(InputKind.KeyDown, Key: positive));
        Assert.Equal(136, split.FirstPaneSize);
        ui.HandleInput(new(InputKind.KeyDown, Key: positive, Shift: true)); Assert.Equal(216, split.FirstPaneSize);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Home)); Assert.Equal(92, split.FirstPaneSize);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.End)); Assert.Equal(240, split.FirstPaneSize);
        Assert.False(ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Space)).Handled);
        Assert.False(ui.HandleInput(new(InputKind.KeyDown, Key: positive, Control: true)).Handled);
        Assert.Equal(240, split.FirstPaneSize); Assert.Equal("Resize panels", split.Divider.AccessibleName);
    }

    [Fact]
    public void ThemeAndResize_RetainChildrenTextSelectionScrollFocusAndBecomeIdle()
    {
        var editor = new TextBox { Value = "unchanged draft" }; var scroll = new ScrollViewer();
        scroll.SetContent(new Text("content") { Height = 1000 });
        var split = new SplitView(editor, scroll) { FirstPaneSize = 140 }; using var ui = Ui(split); ui.Update();
        ui.Focus(editor); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true)); scroll.Offset = 40;
        split.FirstPaneSize = 200; ui.SetTheme(Theme.Light); ui.Update(); ui.Resize(new(360, 400)); ui.Update();
        Assert.Same(editor, split.FirstPane.Child); Assert.Same(scroll, split.SecondPane.Child);
        Assert.Equal("unchanged draft", editor.Value); Assert.Equal(editor.Value.Length, editor.SelectionLength);
        Assert.True(editor.IsFocused); Assert.Equal(40, scroll.Offset); Assert.Equal(Theme.Light, split.Divider.Theme);
        Assert.False(ui.Update());
    }

    [Fact]
    public void CaptureFailureAndPropertyObserverFailure_ReleaseGestureAndPreserveOriginalError()
    {
        var split = new SplitView { FirstPaneSize = 120 }; using var ui = Ui(split); ui.Update();
        var context = new FailingCapture(ui);
        var error = Assert.Throws<IOException>(() => split.Divider.HandleInput(new(InputKind.PointerDown, X: 124, Y: 20), context));
        Assert.Equal("capture", error.Message); Assert.False(split.IsResizing); Assert.Equal(1, context.Releases);
        Down(ui, split);
        split.PropertyChanged += (_, property) => { if (ReferenceEquals(property, SplitView.FirstPaneSizeProperty)) throw new IOException("observer"); };
        Assert.Throws<IOException>(() => Move(ui, split.Orientation, 180));
        Assert.False(split.IsResizing); Assert.False(ui.HandleInput(new(InputKind.PointerMove)).PointerCaptured);
    }

    [Fact]
    public void ContentOwnership_RejectsAliasesAndInvalidChildrenBeforeAdoptionAndDisposesOnce()
    {
        using var content = new TextBox(); Assert.Throws<ArgumentException>(() => new SplitView(content, content)); Assert.Null(content.Parent);
        using var parent = new Panel(); var owned = new TextBox(); parent.Add(owned);
        Assert.Throws<InvalidOperationException>(() => new SplitView(content, owned)); Assert.Null(content.Parent);
        var split = new SplitView(content, new TextBox()); var old = split.FirstPane.Child;
        split.FirstPane.SetContent(new Text("replacement")); Assert.Null(old!.Parent); Assert.False(old.IsDisposed);
        split.Dispose(); split.Dispose(); Assert.True(split.Divider.IsDisposed); Assert.True(split.SecondPane.Child!.IsDisposed);
        Assert.False(content.IsDisposed);
    }

    [Theory]
    [InlineData(-1)] [InlineData(float.PositiveInfinity)] [InlineData(float.NegativeInfinity)]
    public void InvalidDimensions_AreRejectedWithoutChangingLayout(float value)
    {
        using var split = new SplitView { FirstPaneSize = 120 };
        Assert.Throws<ArgumentOutOfRangeException>(() => split.FirstPaneSize = value);
        Assert.Throws<ArgumentOutOfRangeException>(() => split.DividerThickness = value);
        Assert.Equal(120, split.FirstPaneSize);
    }

    private static UiSession Ui(SplitView split) => CompactControlHarness.Ui(split, 400, 400);
    private static UiInputResult Down(UiSession ui, SplitView split) => ui.HandleInput(new(InputKind.PointerDown, X: split.Divider.Bounds.X + 2, Y: split.Divider.Bounds.Y + 2));
    private static void Move(UiSession ui, SplitOrientation orientation, float coordinate) => ui.HandleInput(Input(orientation, InputKind.PointerMove, coordinate));
    private static UiInput Input(SplitOrientation orientation, InputKind kind, float coordinate) => orientation == SplitOrientation.Horizontal ? new(kind, X: coordinate, Y: 20) : new(kind, X: 20, Y: coordinate);
    private static Rect Slot(SplitOrientation orientation, float offset, float size) => orientation == SplitOrientation.Horizontal ? new(10 + offset, 20, size, 120) : new(10, 20 + offset, 120, size);
    private static void Arrange(SplitView split, float axis)
    {
        var horizontal = split.Orientation == SplitOrientation.Horizontal;
        split.Measure(horizontal ? new(axis, 120) : new(120, axis), new FixedTextLayout());
        split.Arrange(horizontal ? new(10, 20, axis, 120) : new(10, 20, 120, axis));
    }
    private static void Constraints(SplitView split, float min1, float max1, float min2, float max2)
    {
        if (split.Orientation == SplitOrientation.Horizontal)
        { split.FirstPane.MinWidth = min1; split.FirstPane.MaxWidth = max1; split.SecondPane.MinWidth = min2; split.SecondPane.MaxWidth = max2; }
        else { split.FirstPane.MinHeight = min1; split.FirstPane.MaxHeight = max1; split.SecondPane.MinHeight = min2; split.SecondPane.MaxHeight = max2; }
    }
    private sealed class FailingCapture(UiSession ui) : IInputContext
    {
        public int Releases;
        public void Focus(Element element) => ui.Focus(element);
        public void Capture(Element element) => throw new IOException("capture");
        public void ReleaseCapture(Element element) => Releases++;
        public string? GetClipboardText() => null;
        public void SetClipboardText(string text) { }
    }
}
