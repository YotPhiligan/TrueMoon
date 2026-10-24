using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class MenuInteractionTests
{
    [Fact]
    public void MenuBackdrop_FollowsThemeWithoutReplacingPopupOrFocusAndCanBeDisabled()
    {
        using var fixture = new Fixture();
        fixture.Menu.OpenGroup("file", fixture.Ui); fixture.Ui.Update();
        var popup = fixture.Host.Popup!; var focused = fixture.Rows[1];
        Assert.Same(Theme.Dark.MenuBackdrop, popup.Backdrop);
        Assert.Equal(Color.Transparent, popup.Background);
        Assert.All(fixture.Rows, row => Assert.Equal(Color.Transparent, row.Background));
        fixture.Ui.SetTheme(Theme.Light); fixture.Ui.Update();
        Assert.Same(popup, fixture.Host.Popup); Assert.True(focused.IsFocused);
        Assert.Same(Theme.Light.MenuBackdrop, popup.Backdrop);
        fixture.Ui.SetTheme(Theme.Light with { MenuBackdrop = null }); fixture.Ui.Update();
        Assert.Same(popup, fixture.Host.Popup); Assert.True(focused.IsFocused);
        Assert.Null(popup.Backdrop); Assert.Equal(Theme.Light.Surface, popup.Background);
        Assert.Equal(0, fixture.Open.Executions);
    }

    [Fact]
    public void DisclosureClick_RevealsThenOpensFirstMenuWithoutExecutingAction()
    {
        using var fixture = new Fixture();
        var bounds = fixture.Menu.MoreButton.Bounds;
        fixture.Click(fixture.Menu.MoreButton);
        fixture.Tick(179);
        Assert.True(fixture.Menu.IsExpanded); Assert.False(fixture.Menu.IsMenuOpen);
        Assert.True(fixture.Menu.MoreButton.IsFocused); Assert.Null(fixture.Host.Popup);
        fixture.Tick(1);
        Assert.True(fixture.Menu.IsExpanded); Assert.Equal(1, fixture.Menu.RevealProgress);
        Assert.True(fixture.Menu.IsMenuOpen); Assert.True(fixture.Rows[1].IsFocused);
        Assert.Equal(0, fixture.Open.Executions); Assert.Equal(0, fixture.Save.Executions);
        Assert.Equal(bounds, fixture.Menu.MoreButton.Bounds);
        var popup = fixture.Host.Popup;
        fixture.Click(fixture.Menu.MoreButton); fixture.Tick(180);
        Assert.True(popup!.IsDisposed); Assert.Null(fixture.Host.Popup);
        Assert.False(fixture.Menu.IsExpanded); Assert.Equal(0, fixture.Menu.RevealProgress);
        Assert.True(fixture.Menu.MoreButton.IsFocused);
    }

    [Theory]
    [InlineData("click")]
    [InlineData("escape")]
    [InlineData("focus-lost")]
    [InlineData("tab")]
    [InlineData("outside")]
    public void DismissDuringDisclosure_CancelsPendingFirstMenu(string dismissal)
    {
        using var fixture = new Fixture();
        fixture.Click(fixture.Menu.MoreButton); fixture.Tick(60);
        Assert.True(fixture.Menu.IsExpanded); Assert.Null(fixture.Host.Popup);
        switch (dismissal)
        {
            case "click": fixture.Click(fixture.Menu.MoreButton); break;
            case "escape": fixture.Key(UiKey.Escape); break;
            case "focus-lost": fixture.Ui.HandleInput(new(InputKind.FocusLost)); break;
            case "tab": fixture.Key(UiKey.Tab); Assert.True(fixture.After.IsFocused); break;
            case "outside": fixture.Click(fixture.After); break;
        }
        fixture.Tick(500);
        Assert.False(fixture.Menu.IsExpanded); Assert.Equal(0, fixture.Menu.RevealProgress);
        Assert.False(fixture.Menu.NeedsFrame); Assert.Null(fixture.Host.Popup);
        Assert.Equal(0, fixture.Open.Executions); Assert.Equal(0, fixture.Save.Executions);
    }

    [Fact]
    public void DisclosureDots_RotateWithRevealAndReverseWithoutMovingTheButton()
    {
        using var fixture = new Fixture();
        var bounds = fixture.Menu.MoreButton.Bounds;
        var collapsed = Dots();
        Assert.Equal(collapsed[0].X, collapsed[2].X, 4);
        Assert.Equal(8, collapsed[2].Y - collapsed[0].Y, 4);
        Assert.Equal(0, fixture.Menu.MoreButton.BorderWidth);
        fixture.Move(fixture.Menu.MoreButton); fixture.Tick(90);
        var halfway = Dots();
        Assert.Equal(4 * MathF.Sqrt(2), halfway[2].X - halfway[0].X, 4);
        Assert.Equal(4 * MathF.Sqrt(2), halfway[2].Y - halfway[0].Y, 4);
        Assert.Equal(collapsed[1], halfway[1]); Assert.Equal(bounds, fixture.Menu.MoreButton.Bounds);
        fixture.Tick(90); var expanded = Dots();
        Assert.Equal(expanded[0].Y, expanded[2].Y, 4);
        Assert.Equal(8, expanded[2].X - expanded[0].X, 4);
        Assert.False(fixture.Menu.IsMenuOpen);
        fixture.Key(UiKey.Escape); fixture.Tick(90);
        Assert.Equal(halfway, Dots());
        fixture.Tick(90); Assert.Equal(collapsed, Dots());
        Assert.Equal(bounds, fixture.Menu.MoreButton.Bounds); Assert.False(fixture.Menu.NeedsFrame);

        (float X, float Y)[] Dots()
        {
            var drawing = new DotDrawing(); fixture.Menu.MoreButton.Draw(drawing);
            Assert.Equal(3, drawing.Dots.Count); Assert.Equal(0, drawing.TextCalls);
            return drawing.Dots.Select(dot => (dot.X + dot.Width / 2, dot.Y + dot.Height / 2)).ToArray();
        }
    }

    [Fact]
    public void DisclosureClick_ZeroMotionOpensFirstWithoutExtraGesture()
    {
        using var fixture = new Fixture(); fixture.Menu.AnimationDuration = TimeSpan.Zero;
        fixture.Click(fixture.Menu.MoreButton); fixture.Ui.Update();
        Assert.True(fixture.Menu.IsMenuOpen); Assert.True(fixture.Rows[1].IsFocused);
        Assert.Equal(1, fixture.Menu.RevealProgress); Assert.False(fixture.Menu.NeedsFrame);
        fixture.Menu.CloseMenu(); fixture.Ui.Update();
        Assert.Equal(0, fixture.Menu.RevealProgress); Assert.Null(fixture.Host.Popup);
        Assert.False(fixture.Ui.NeedsUpdate); Assert.Equal(0, fixture.Open.Executions);
    }

    [Theory]
    [InlineData(UiKey.Enter)]
    [InlineData(UiKey.Space)]
    public void DisclosureKeyboardActivation_OpensFirstWithoutExecutingItsFocusedItem(UiKey key)
    {
        using var fixture = new Fixture(); fixture.Ui.Focus(fixture.Menu.MoreButton);
        fixture.Key(key); fixture.Key(key, up: true); fixture.Tick(180);
        Assert.True(fixture.Menu.IsMenuOpen); Assert.True(fixture.Rows[1].IsFocused);
        Assert.Equal(0, fixture.Open.Executions); Assert.Equal(0, fixture.Save.Executions);
        fixture.Key(UiKey.Escape); fixture.Tick(180);
        Assert.Null(fixture.Host.Popup); Assert.True(fixture.Menu.MoreButton.IsFocused);
        Assert.False(fixture.Menu.IsExpanded);
    }

    [Fact]
    public void DetachingDuringDisclosure_CancelsFirstMenuOnReattach()
    {
        using var fixture = new Fixture(); fixture.Click(fixture.Menu.MoreButton); fixture.Tick(60);
        fixture.Content.Items.Remove(fixture.Menu); fixture.Tick(500);
        Assert.False(fixture.Menu.IsExpanded); Assert.False(fixture.Menu.NeedsFrame);
        fixture.Content.Items.Insert(0, fixture.Menu); fixture.Tick(500);
        Assert.Null(fixture.Host.Popup); Assert.Equal(0, fixture.Menu.RevealProgress);
        Assert.False(fixture.Menu.NeedsFrame); Assert.False(fixture.Menu.IsDisposed);
        Assert.Equal(0, fixture.Open.Executions);
    }

    [Fact]
    public void HoverDisclosure_HasLeaveGraceAndReversesFromCurrentPresentation()
    {
        using var fixture = new Fixture();
        fixture.Move(fixture.Menu.MoreButton); fixture.Tick(90);
        Assert.Equal(.5f, fixture.Menu.RevealProgress, 4);
        fixture.Move(700, 180); fixture.Tick(239);
        Assert.True(fixture.Menu.IsExpanded);
        fixture.Tick(1); Assert.False(fixture.Menu.IsExpanded);
        fixture.Tick(89); var shrinking = fixture.Menu.RevealProgress;
        Assert.Equal(.5f, shrinking, 4);
        fixture.Move(fixture.Menu.MoreButton);
        Assert.Equal(shrinking, fixture.Menu.RevealProgress);
        fixture.Tick(90); Assert.Equal(.75f, fixture.Menu.RevealProgress, 4);
        fixture.Tick(90); Assert.Equal(1, fixture.Menu.RevealProgress);
    }

    [Fact]
    public void Keyboard_F10ArrowsSkipDisabledRowsAndEnterExecutesExactlyOnce()
    {
        using var fixture = new Fixture();
        fixture.Key(UiKey.F10); Assert.True(fixture.Menu.Headings["file"].IsFocused);
        fixture.Key(UiKey.Down); fixture.Ui.Update();
        var rows = fixture.Rows;
        Assert.Equal(new[] { "disabled", "open", "save" }, rows.Select(row => row.ActionId));
        Assert.False(rows[0].IsEnabled); Assert.True(rows[1].IsFocused);
        fixture.Key(UiKey.Down); Assert.True(rows[2].IsFocused);
        fixture.Key(UiKey.Down); Assert.True(rows[1].IsFocused);
        fixture.Key(UiKey.End); Assert.True(rows[2].IsFocused);
        fixture.Key(UiKey.Home); Assert.True(rows[1].IsFocused);
        fixture.Key(UiKey.Enter); fixture.Key(UiKey.Enter, up: true);
        Assert.Equal(1, fixture.Open.Executions); Assert.Equal(0, fixture.Save.Executions);
        Assert.Equal(0, fixture.Disabled.Executions); Assert.Null(fixture.Host.Popup);
        Assert.All(rows, row => Assert.True(row.IsDisposed));
    }

    [Fact]
    public void HeadingHoverAndHorizontalKeys_ReplaceThePopupWithoutRunningCommands()
    {
        using var fixture = new Fixture();
        fixture.Menu.OpenGroup("file", fixture.Ui); fixture.Ui.Update(); var old = fixture.Host.Popup;
        fixture.Move(fixture.Menu.Headings["edit"]); fixture.Ui.Update();
        Assert.True(old!.IsDisposed); Assert.Equal("undo", Assert.Single(fixture.Rows).ActionId);
        fixture.Key(UiKey.Right); fixture.Ui.Update();
        Assert.Equal("disabled", fixture.Rows[0].ActionId);
        fixture.Key(UiKey.Left); fixture.Ui.Update();
        Assert.Equal("undo", Assert.Single(fixture.Rows).ActionId);
        Assert.Equal(0, fixture.Open.Executions + fixture.Undo.Executions);
    }

    [Fact]
    public void HeadingClick_OpensAndTogglesItsPopupWithoutExecutingAnAction()
    {
        using var fixture = new Fixture();
        fixture.Move(fixture.Menu.MoreButton); fixture.Tick(180);
        fixture.Click(fixture.Menu.Headings["file"]); fixture.Ui.Update();
        Assert.True(fixture.Menu.IsMenuOpen); Assert.Equal("open", fixture.Rows[1].ActionId);
        var popup = fixture.Host.Popup;
        fixture.Click(fixture.Menu.Headings["file"]); fixture.Tick(180);
        Assert.True(popup!.IsDisposed); Assert.Null(fixture.Host.Popup);
        Assert.Equal(0, fixture.Open.Executions); Assert.False(fixture.Menu.IsExpanded);
    }

    [Fact]
    public void Escape_ClosesPopupAndDisclosureAndRestoresDisclosureFocus()
    {
        using var fixture = new Fixture();
        fixture.Menu.OpenGroup("file", fixture.Ui); fixture.Ui.Update();
        fixture.Key(UiKey.Escape); fixture.Tick(180);
        Assert.Null(fixture.Host.Popup); Assert.False(fixture.Menu.IsMenuOpen);
        Assert.False(fixture.Menu.IsExpanded); Assert.Equal(0, fixture.Menu.RevealProgress);
        Assert.True(fixture.Menu.MoreButton.IsFocused);
    }

    [Fact]
    public void CollapsedHeadings_DoNotAppearInTheTabOrder()
    {
        using var fixture = new Fixture();
        fixture.Ui.Focus(fixture.Menu.MoreButton); fixture.Key(UiKey.Tab);
        Assert.True(fixture.After.IsFocused);
        Assert.All(fixture.Menu.Headings.Values, heading => Assert.False(heading.IsFocused));
    }

    [Fact]
    public void OutsideClick_ClosesPopupAndDisclosureWithoutActivatingUnderlyingContent()
    {
        using var fixture = new Fixture(); var clicks = 0; fixture.After.Click += () => clicks++;
        fixture.Menu.OpenGroup("file", fixture.Ui); fixture.Ui.Update();
        fixture.Move(700, 70);
        fixture.Ui.HandleInput(new(InputKind.PointerDown, 700, 70));
        fixture.Ui.HandleInput(new(InputKind.PointerUp, 700, 70)); fixture.Tick(180);
        Assert.Null(fixture.Host.Popup); Assert.False(fixture.Menu.IsExpanded);
        Assert.Equal(0, clicks); Assert.Equal(0, fixture.Menu.RevealProgress);
    }

    [Fact]
    public void Tab_FromPopupDismissesAndMovesToOrdinaryContent()
    {
        using var fixture = new Fixture();
        fixture.Menu.OpenGroup("file", fixture.Ui); fixture.Ui.Update();
        fixture.Key(UiKey.Tab); fixture.Tick(180);
        Assert.Null(fixture.Host.Popup); Assert.False(fixture.Menu.IsExpanded);
        Assert.True(fixture.After.IsFocused);
    }

    [Fact]
    public void PointerActivation_ExecutesSharedCommandOnceAndDisconnectsPopupRows()
    {
        using var fixture = new Fixture();
        fixture.Menu.OpenGroup("file", fixture.Ui); fixture.Ui.Update();
        var rows = fixture.Rows; Assert.Equal(1, fixture.Open.Subscribers);
        fixture.Click(rows[1]);
        Assert.Equal(1, fixture.Open.Executions); Assert.Null(fixture.Host.Popup);
        Assert.Equal(0, fixture.Open.Subscribers); Assert.True(rows[1].IsDisposed);
        fixture.Open.SetAvailable(false); Assert.Equal(1, fixture.Open.Executions);
    }

    [Fact]
    public void AvailabilityChange_UpdatesExistingRowAndPreventsAnAlreadyPressedAction()
    {
        using var fixture = new Fixture();
        fixture.Menu.OpenGroup("file", fixture.Ui); fixture.Ui.Update(); var row = fixture.Rows[1];
        fixture.Pointer(InputKind.PointerDown, row);
        fixture.Open.SetAvailable(false); fixture.Ui.Update();
        Assert.Same(row, fixture.Rows[1]); Assert.False(row.IsEnabled);
        fixture.Pointer(InputKind.PointerUp, row);
        Assert.Equal(0, fixture.Open.Executions); Assert.NotNull(fixture.Host.Popup);
        fixture.Open.SetAvailable(true); fixture.Ui.Update(); Assert.True(row.IsEnabled);
        fixture.Click(row); Assert.Equal(1, fixture.Open.Executions);
    }

    [Fact]
    public void DisabledRowClick_DoesNotExecuteOrDismissThePopup()
    {
        using var fixture = new Fixture();
        fixture.Menu.OpenGroup("file", fixture.Ui); fixture.Ui.Update(); var popup = fixture.Host.Popup;
        fixture.Click(fixture.Rows[0]);
        Assert.Equal(0, fixture.Disabled.Executions); Assert.Same(popup, fixture.Host.Popup);
        Assert.True(fixture.Menu.IsMenuOpen); Assert.False(popup!.IsDisposed);
    }

    [Fact]
    public void ThemeChange_RetainsHeadingsPopupFocusAndCommandAvailability()
    {
        using var fixture = new Fixture();
        fixture.Menu.OpenGroup("file", fixture.Ui); fixture.Ui.Update();
        var heading = fixture.Menu.Headings["file"]; var popup = fixture.Host.Popup; var selected = fixture.Rows[1];
        fixture.Ui.SetTheme(Theme.Light); fixture.Ui.Update();
        Assert.Same(heading, fixture.Menu.Headings["file"]); Assert.Same(popup, fixture.Host.Popup);
        Assert.True(selected.IsFocused); Assert.Same(Theme.Light, selected.Theme);
        Assert.False(fixture.Rows[0].IsEnabled); Assert.Equal(1, fixture.Open.Subscribers);
    }

    [Fact]
    public void FocusLoss_SettlesDisclosureAndDisposesPopupWithoutExtraFrames()
    {
        using var fixture = new Fixture();
        fixture.Menu.OpenGroup("file", fixture.Ui); fixture.Ui.Update(); var popup = fixture.Host.Popup;
        fixture.Ui.HandleInput(new(InputKind.FocusLost)); fixture.Ui.Update();
        Assert.True(popup!.IsDisposed); Assert.Null(fixture.Host.Popup);
        Assert.Equal(0, fixture.Menu.RevealProgress); Assert.False(fixture.Menu.IsExpanded);
        Assert.False(fixture.Menu.NeedsFrame); Assert.False(fixture.Ui.NeedsUpdate);
    }

    [Fact]
    public void DetachingMenu_ReleasesPopupCommandsAndEndsTransitions()
    {
        using var fixture = new Fixture();
        fixture.Menu.OpenGroup("file", fixture.Ui); fixture.Ui.Update(); var popup = fixture.Host.Popup;
        fixture.Content.Items.Remove(fixture.Menu); fixture.Ui.Update();
        Assert.True(popup!.IsDisposed); Assert.Null(fixture.Host.Popup);
        Assert.Equal(0, fixture.Open.Subscribers); Assert.False(fixture.Menu.NeedsFrame);
        Assert.False(fixture.Menu.IsExpanded); Assert.False(fixture.Menu.IsDisposed); fixture.Menu.Dispose();
    }

    [Fact]
    public void GroupDeclarations_FreezeInputsAndRejectEmptyOrDuplicateActions()
    {
        var command = new Command(); var actions = new List<MenuAction> { new("open", "Open", command) };
        var group = new MenuGroup("file", "File", actions); actions.Clear();
        Assert.Single(group.Actions);
        Assert.Throws<ArgumentException>(() => new MenuGroup("file", "File", []));
        Assert.Throws<ArgumentException>(() => new MenuGroup("file", "File", [new("a", "A", command), new("a", "B", command)]));
        using var host = new OverlayHost(new Panel());
        Assert.Throws<ArgumentException>(() => new MenuBar(host, []));
        Assert.Throws<ArgumentException>(() => new MenuBar(host, [group, group]));
    }

    [Fact]
    public void DelegateCommand_RechecksAdmissionAndRaisesAvailabilityOnlyWhenRequested()
    {
        var enabled = false; var count = 0; var changes = 0;
        var command = new DelegateUiCommand(() => count++, () => enabled);
        command.CanExecuteChanged += () => changes++;
        command.Execute(); Assert.Equal(0, count); Assert.False(command.CanExecute);
        enabled = true; Assert.Equal(0, changes); command.Refresh(); Assert.Equal(1, changes);
        command.Execute(); Assert.Equal(1, count); enabled = false; command.Execute(); Assert.Equal(1, count);
    }

    [Fact]
    public void ZeroMotionAndZeroGrace_SettleWithoutPendingAnimationAndRejectNegativeTime()
    {
        using var fixture = new Fixture();
        Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Menu.AnimationDuration = TimeSpan.FromTicks(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Menu.CloseDelay = TimeSpan.FromTicks(-1));
        fixture.Menu.AnimationDuration = TimeSpan.Zero; fixture.Menu.CloseDelay = TimeSpan.Zero;
        fixture.Move(fixture.Menu.MoreButton); fixture.Ui.Update();
        Assert.Equal(1, fixture.Menu.RevealProgress); Assert.False(fixture.Menu.NeedsFrame);
        fixture.Move(700, 180); fixture.Ui.Update();
        Assert.False(fixture.Menu.IsExpanded); Assert.Equal(0, fixture.Menu.RevealProgress);
        Assert.False(fixture.Menu.NeedsFrame);
    }

    private sealed class DotDrawing : IDrawingContext
    {
        public List<Rect> Dots { get; } = [];
        public int TextCalls { get; private set; }
        public void Save() { } public void Restore() { } public void Clip(Rect rectangle) { } public void Translate(float x, float y) { }
        public void Fill(Rect rectangle, Color color, float radius = 0) { if (rectangle.Width <= 2.1f && rectangle.Height <= 2.1f) Dots.Add(rectangle); }
        public void Stroke(Rect rectangle, Color color, float width = 1, float radius = 0) { }
        public void Text(string text, float x, float y, Color color, float fontSize, string fontFamily) => TextCalls++;
        public void Image(IImageSource source, Rect destination) { }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ManualTime _time = new();
        public Command Disabled { get; } = new(false);
        public Command Open { get; } = new();
        public Command Save { get; } = new();
        public Command Undo { get; } = new();
        public VStack Content { get; } = new() { Spacing = 0 };
        public Button After { get; } = new("body") { Height = 80 };
        public OverlayHost Host { get; }
        public MenuBar Menu { get; }
        public UiSession Ui { get; }
        public MenuItem[] Rows => Host.Popup!.Children.Cast<MenuItem>().ToArray();
        public Fixture()
        {
            Host = new(Content);
            Menu = new(Host, [new("file", "File", [new("disabled", "Unavailable", Disabled), new("open", "Open", Open, UiSymbol.Folder), new("save", "Save", Save)]),
                new("edit", "Edit", [new("undo", "Undo", Undo, UiSymbol.Undo)])]) { HorizontalAlignment = LayoutAlignment.Start, Height = 32 };
            Content.Add(Menu); Content.Add(After);
            Ui = new(Host, new Surface(), new UiViewport(800, 240), null, new UiDispatchOptions(), _time); Ui.Update();
        }
        public void Tick(int milliseconds) { _time.Advance(TimeSpan.FromMilliseconds(milliseconds)); Ui.Update(); }
        public void Move(Element element) => Move(element.Bounds.X + element.Bounds.Width / 2, element.Bounds.Y + element.Bounds.Height / 2);
        public void Move(float x, float y) => Ui.HandleInput(new(InputKind.PointerMove, x, y));
        public void Pointer(InputKind kind, Element element) => Ui.HandleInput(new(kind,
            element.Bounds.X + element.Bounds.Width / 2, element.Bounds.Y + element.Bounds.Height / 2));
        public void Click(Element element) { Pointer(InputKind.PointerDown, element); Pointer(InputKind.PointerUp, element); }
        public void Key(UiKey key, bool up = false) => Ui.HandleInput(new(up ? InputKind.KeyUp : InputKind.KeyDown, Key: key));
        public void Dispose() => Ui.Dispose();
    }

    private sealed class Command(bool available = true) : IUiCommand
    {
        private Action? _changed;
        public bool CanExecute { get; private set; } = available;
        public int Executions { get; private set; }
        public int Subscribers => _changed?.GetInvocationList().Length ?? 0;
        public event Action? CanExecuteChanged { add => _changed += value; remove => _changed -= value; }
        public void Execute() { if (CanExecute) Executions++; }
        public void SetAvailable(bool available) { CanExecute = available; _changed?.Invoke(); }
    }

    private sealed class ManualTime : TimeProvider
    {
        private long _timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _timestamp;
        public void Advance(TimeSpan elapsed) => _timestamp += elapsed.Ticks;
    }

    private sealed class Surface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new FixedTextLayout();
        public void VerifyAvailable() { }
        public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport) { }
        public void Dispose() { }
    }
}
