using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class DefaultStyleTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SquareDefaults_ApplyToConstructorsFactoriesAndEditors(bool light)
    {
        var theme = light ? Theme.Light : Theme.Dark;
        Element[] controls = [new Button(), Button.Simple(), new TextBox(), TextBox.Create(), new RichTextEditor(),
            new ProgressBar(), new Slider(), new CheckBox(), new Border(), new IconButton(UiSymbol.Close, "Close")];
        foreach (var control in controls)
        {
            using (control) { control.ApplyTheme(theme); Assert.Equal(0, control.CornerRadius); }
        }
        using var editor = new TextBox(); editor.ApplyTheme(theme);
        Assert.Equal(theme.Input, editor.Background); Assert.Equal(1, editor.BorderWidth);
        editor.IsReadOnly = true; Assert.Equal(theme.ReadOnly, editor.Background);
        editor.IsEnabled = false; Assert.Equal(theme.ControlDisabled, editor.Background);
    }

    [Fact]
    public void Appearance_LocalStyleThemePriorityAndClearAreStableAcrossThemeChanges()
    {
        using var button = new Button { Style = new Style().With(UiProperties.CornerRadius, 3f).With(UiProperties.Background, new Color(1, 2, 3)) };
        button.CornerRadius = 7; button.Background = new(4, 5, 6); button.ApplyTheme(Theme.Light);
        Assert.Equal(7, button.CornerRadius); Assert.Equal(new Color(4, 5, 6), button.Background);
        button.Clear(UiProperties.CornerRadius); button.Clear(UiProperties.Background);
        Assert.Equal(3, button.CornerRadius); Assert.Equal(new Color(1, 2, 3), button.Background);
        button.Style = null; Assert.Equal(0, button.CornerRadius); Assert.Equal(new Color(255, 255, 255), button.Background);
        button.Tone = ControlTone.Danger; Assert.Equal(new Color(190, 43, 61), button.Background); Assert.Equal(new Color(255, 255, 255), button.Foreground);
    }

    [Theory]
    [InlineData(-1f)] [InlineData(float.NaN)] [InlineData(float.PositiveInfinity)] [InlineData(65f)]
    public void InvalidAppearance_RejectsBeforeMutation(float value)
    {
        using var button = new Button(); var changes = 0; button.Invalidated += _ => changes++;
        Assert.Throws<ArgumentOutOfRangeException>(() => button.CornerRadius = value);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Style().With(UiProperties.CornerRadius, value));
        Assert.Throws<ArgumentOutOfRangeException>(() => Theme.Dark with { CornerRadius = value });
        Assert.Equal(0, button.CornerRadius); Assert.Equal(0, changes);
    }

    [Theory]
    [InlineData(false, ControlTone.Neutral)] [InlineData(true, ControlTone.Neutral)]
    [InlineData(false, ControlTone.Accent)] [InlineData(true, ControlTone.Accent)]
    [InlineData(false, ControlTone.Positive)] [InlineData(true, ControlTone.Positive)]
    [InlineData(false, ControlTone.Danger)] [InlineData(true, ControlTone.Danger)]
    public void EnabledText_ContrastRemainsAtLeastFourPointFiveAcrossActionStates(bool light, ControlTone tone)
    {
        var button = new Button("Action") { Tone = tone }; using var ui = Create(button); ui.SetTheme(light ? Theme.Light : Theme.Dark); ui.Update();
        Check(); button.IsHovered = true; Check();
        ui.Focus(button); ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Space)); Check();
        void Check() => Assert.True(Contrast(button.Foreground, button.Background) >= 4.5, $"{light}/{tone}/{button.IsHovered}/{button.IsPressed}");
    }

    [Fact]
    public void FocusAndError_UseInsideSquareOutlineAndStructuralErrorMarker()
    {
        var editor = new TextBox { HasError = true }; using var ui = Create(editor); ui.Update(); ui.Focus(editor);
        var drawing = new Drawing(); editor.Draw(drawing);
        Assert.Contains(drawing.Strokes, x => x.Bounds == new Rect(.5f, .5f, 239, 139) && x.Color == new Color(255, 112, 128) && x.Width == 1 && x.Radius == 0);
        Assert.Contains(drawing.Strokes, x => x.Bounds == new Rect(2, 2, 236, 136) && x.Color == new Color(255, 201, 92) && x.Width == 2 && x.Radius == 0);
        Assert.Contains(drawing.Fills, x => x.Bounds == new Rect(0, 0, 3, 140) && x.Color == new Color(255, 112, 128));
        Assert.All(drawing.Fills, x => Assert.Equal(0, x.Radius));
        Assert.Equal(drawing.Saves, drawing.Restores);
    }

    [Theory]
    [InlineData(UiKey.Backspace, false)] [InlineData(UiKey.Delete, false)] [InlineData(UiKey.X, true)] [InlineData(UiKey.V, true)]
    public void ReadOnly_RejectsMutationWithoutTouchingSelectionOrClipboard(UiKey key, bool control)
    {
        var editor = new TextBox { Value = "Aб👩‍💻", IsReadOnly = true }; var clipboard = new Clipboard();
        using var ui = new UiSession(editor, new Surface(), new UiViewport(240, 140), clipboard); ui.Update(); ui.Focus(editor);
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.A, Control: true)); var length = editor.Value.Length;
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: key, Control: control)); ui.HandleInput(new UiInput(InputKind.Text, Text: "replace"));
        Assert.Equal("Aб👩‍💻", editor.Value); Assert.Equal(length, editor.SelectionLength); Assert.Equal(0, clipboard.Reads); Assert.Equal(0, clipboard.Writes);
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.C, Control: true)); Assert.Equal("Aб👩‍💻", clipboard.Value); Assert.Equal(1, clipboard.Writes);
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Left)); Assert.Equal(0, editor.SelectionLength); Assert.Equal(0, editor.CaretIndex);
    }

    [Fact]
    public void ThemeSwitch_PreservesFocusSelectionValuesAndScrollAndReturnsToIdle()
    {
        var editor = new TextBox { Value = "Original" }; var check = new CheckBox { IsChecked = true }; var slider = new Slider { Value = .37f };
        var root = new ScrollViewer().Content(new VStack().WithChildren(editor, check, slider, new SizedElement(100, 500)));
        using var ui = Create(root); ui.Update(); ui.Focus(editor); ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.A, Control: true));
        root.Offset = 80; ui.Update(); ui.SetTheme(Theme.Light); ui.Update();
        Assert.True(editor.IsFocused); Assert.Equal("Original", editor.Value); Assert.Equal(8, editor.SelectionLength);
        Assert.True(check.IsChecked); Assert.Equal(.37f, slider.Value); Assert.Equal(80, root.Offset); Assert.False(ui.Update());
    }

    [Fact]
    public void Scrollbar_DragClampsIgnoresSecondaryButtonAndCancellationStopsGesture()
    {
        var root = new ScrollViewer().Content(new SizedElement(100, 500)); using var ui = Create(root); ui.Update();
        Assert.Equal(new Rect(0, 0, 230, 140), root.ChildClipBounds);
        Assert.True(ui.HandleInput(new UiInput(InputKind.PointerDown, 235, 10)).PointerCaptured);
        Assert.False(ui.HandleInput(new UiInput(InputKind.PointerUp, 235, 130, Button: 1)).Handled); Assert.Equal(0, root.Offset);
        ui.HandleInput(new UiInput(InputKind.PointerMove, 235, 500)); ui.Update(); Assert.Equal(360, root.Offset);
        ui.HandleInput(new UiInput(InputKind.FocusLost)); ui.HandleInput(new UiInput(InputKind.PointerMove, 235, 0)); Assert.Equal(360, root.Offset);
        root.ShowScrollBar = false; ui.Update(); Assert.Equal(new Rect(0, 0, 240, 140), root.ChildClipBounds);
        Assert.False(ui.HandleInput(new UiInput(InputKind.PointerDown, 235, 10)).PointerCaptured);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DefaultFrame_CommandsCapabilitiesRegionsAndIdleFollowObservedWindow(bool resizable)
    {
        var content = new TextBox { Value = "Content" }; var root = WindowFrame.CreateDefault("Window", content);
        var commands = new Commands(resizable); var closes = 0; var binding = new DefaultWindowFrameBinding(root, commands, () => closes++);
        using var ui = Create(root); binding.Synchronize(true); ui.Update(); var title = root.DefaultTitleBar!;
        Assert.Equal(resizable, root.ResizeGrip!.IsVisible); Assert.True(title.MinimizeButton.IsEnabled); Assert.Equal(resizable, title.MaximizeButton.IsEnabled);
        Assert.Equal(WindowRegionRole.Caption, WindowRegionMap.Create(root).HitTest(20, 15));
        Assert.Equal(WindowRegionRole.Client, WindowRegionMap.Create(root).HitTest(title.CloseButton.Bounds.X + 10, 15));
        Assert.Equal(resizable ? WindowRegionRole.ResizeBottomRight : WindowRegionRole.Client, WindowRegionMap.Create(root).HitTest(232, 131));
        Assert.Equal(new Rect(1, 37, 238, 102), content.Bounds);
        Click(title.MinimizeButton); Assert.Equal(UiWindowState.Minimized, commands.State); commands.Restore();
        if (resizable)
        {
            Click(title.MaximizeButton); binding.Synchronize(true); ui.Update(); Assert.Equal(UiSymbol.Restore, title.MaximizeButton.Symbol); Assert.False(root.ResizeGrip.IsVisible);
            Assert.Equal(new Rect(1, 37, 238, 102), content.Bounds);
            Assert.Equal(WindowRegionRole.Client, WindowRegionMap.Create(root).HitTest(232, 131));
            Click(title.MaximizeButton); binding.Synchronize(true); ui.Update(); Assert.Equal(UiWindowState.Normal, commands.State); Assert.True(root.ResizeGrip.IsVisible);
            Assert.Equal(new Rect(1, 37, 238, 102), content.Bounds);
        }
        Click(title.CloseButton); Assert.Equal(1, closes); Assert.Equal(0, commands.Closes);
        binding.Synchronize(false); ui.Update(); Assert.Equal(Theme.Dark.Surface, title.Background); Assert.Equal("Content", content.Value);
        binding.Synchronize(false); Assert.False(ui.Update());
        void Click(Button button) { var b = button.Bounds; ui.HandleInput(new UiInput(InputKind.PointerDown, b.X + 8, b.Y + 8)); ui.HandleInput(new UiInput(InputKind.PointerUp, b.X + 8, b.Y + 8)); }
    }

    [Fact]
    public void DefaultFrame_ResizeGripOverlaysContentWithoutAddingMeasuredHeight()
    {
        using var frame = WindowFrame.CreateDefault("Window", new SizedElement(100, 80));
        frame.ApplyWindowState(new(true, false, true, true, true));
        frame.Measure(new(400, 240), new FixedTextLayout());
        Assert.Equal(118, frame.DesiredSize.Height);
        frame.Arrange(new(10, 20, 400, 240));
        Assert.Equal(new Rect(11, 57, 398, 202), frame.Children[1].Bounds);
        Assert.Equal(new Rect(393, 243, 16, 16), frame.ResizeGrip!.Bounds);
        frame.ApplyWindowState(new(true, true, true, true, true));
        frame.Measure(new(400, 240), new FixedTextLayout()); frame.Arrange(new(10, 20, 400, 240));
        Assert.Equal(118, frame.DesiredSize.Height);
        Assert.Equal(new Rect(11, 57, 398, 202), frame.Children[1].Bounds);
        Assert.Equal(default, frame.ResizeGrip.Bounds);
    }

    [Fact]
    public void HiddenScrollbar_CancelsCapturedDragBeforeChangingOffset()
    {
        var root = new ScrollViewer().Content(new SizedElement(100, 500)); using var ui = Create(root); ui.Update();
        Assert.True(ui.HandleInput(new UiInput(InputKind.PointerDown, 235, 10)).PointerCaptured);
        root.ShowScrollBar = false; ui.Update();
        var move = ui.HandleInput(new UiInput(InputKind.PointerMove, 235, 130));
        Assert.False(move.PointerCaptured); Assert.False(move.Handled); Assert.Equal(0, root.Offset);
        Assert.False(ui.HandleInput(new UiInput(InputKind.PointerUp, 235, 130)).PointerCaptured);
    }

    private static UiSession Create(Element root) => new(root, new Surface(), new UiViewport(240, 140));
    private static double Contrast(Color a, Color b)
    {
        static double L(Color c) { static double S(byte x) { var s = x / 255d; return s <= .04045 ? s / 12.92 : Math.Pow((s + .055) / 1.055, 2.4); } return .2126 * S(c.R) + .7152 * S(c.G) + .0722 * S(c.B); }
        var x = L(a); var y = L(b); return (Math.Max(x, y) + .05) / (Math.Min(x, y) + .05);
    }
    private sealed class Surface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new FixedTextLayout(); public void VerifyAvailable() { } public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport) => root.Draw(new RecordingDrawingContext()); public void Dispose() { }
    }
    private sealed class Clipboard : IUiClipboard
    {
        public int Reads, Writes; public string? Value;
        public string? GetText() { Reads++; return Value; } public void SetText(string text) { Writes++; Value = text; }
    }
    private sealed class Commands(bool resizable) : IWindowChromeHost
    {
        public WindowChromeOptions? Chrome { get; } = new() { Resizable = resizable };
        public WindowChromeCapabilities ChromeCapabilities => new(true, true, true);
        public UiWindowState State { get; private set; } public UiViewport Viewport => new(240, 140); public int Closes;
        public event Action? RenderRequested { add { } remove { } } public event Action<UiInput>? Input { add { } remove { } }
        public void SetWindowRegions(WindowRegionMap regions) { } public void Run() { } public void Close() => Closes++;
        public void Minimize() => State = UiWindowState.Minimized; public void Maximize() => State = UiWindowState.Maximized; public void Restore() => State = UiWindowState.Normal; public void Dispose() { }
    }
    private sealed class Drawing : IDrawingContext
    {
        public int Saves, Restores;
        public List<(Rect Bounds, Color Color, float Radius)> Fills { get; } = [];
        public List<(Rect Bounds, Color Color, float Width, float Radius)> Strokes { get; } = [];
        public void Save() => Saves++; public void Restore() => Restores++; public void Clip(Rect rectangle) { } public void Translate(float x, float y) { }
        public void Fill(Rect rectangle, Color color, float radius = 0) => Fills.Add((rectangle, color, radius));
        public void Stroke(Rect rectangle, Color color, float width = 1, float radius = 0) => Strokes.Add((rectangle, color, width, radius));
        public void Text(string text, float x, float y, Color color, float fontSize, string fontFamily) { } public void Image(IImageSource source, Rect destination) { }
    }
}
