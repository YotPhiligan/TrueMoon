using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class MenuPopupTests
{
    [Fact]
    public void Navigation_SkipsUnavailableCommandsTrapsTabAndDismissesBeforeExecution()
    {
        var trigger = Button.Simple("Open"); var host = new OverlayHost(trigger);
        using var ui = Create(host); ui.Update(); var order = new List<string>();
        var first = new DelegateUiCommand(() => order.Add("first"));
        var last = new DelegateUiCommand(() => { Assert.Null(host.Popup); Assert.True(trigger.IsFocused); order.Add("last"); });
        var popup = new MenuPopup([new("a", "First", first), new("b", "Disabled", new DelegateUiCommand(() => throw new Exception(), () => false)), new("c", "Last", last)], () => host.Close());
        host.Open(trigger, popup, trigger.Bounds, ui, trigger); popup.FocusFirst(ui); ui.Update();
        Assert.True(popup.Children[0].IsFocused);
        Key(UiKey.Down); Assert.True(popup.Children[2].IsFocused);
        Key(UiKey.Tab); Assert.True(popup.Children[0].IsFocused);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Tab, Shift: true)); Assert.True(popup.Children[2].IsFocused);
        Key(UiKey.Home); Assert.True(popup.Children[0].IsFocused); Key(UiKey.End); Assert.True(popup.Children[2].IsFocused);
        Key(UiKey.Enter); ui.HandleInput(new(InputKind.KeyUp, Key: UiKey.Enter));
        Assert.Equal(new[] { "last" }, order); Assert.True(popup.IsDisposed); Assert.Null(host.Popup);
        void Key(UiKey key) => ui.HandleInput(new(InputKind.KeyDown, Key: key));
    }

    [Fact]
    public void AllDisabled_FocusesPopupAndConsumesNavigationWithoutRunningActions()
    {
        var trigger = Button.Simple("Open"); var host = new OverlayHost(trigger); using var ui = Create(host); ui.Update(); var calls = 0;
        var popup = new MenuPopup([new("a", "Disabled", new DelegateUiCommand(() => calls++, () => false))], () => host.Close());
        host.Open(trigger, popup, trigger.Bounds, ui, trigger); popup.FocusFirst(ui); ui.Update(); Assert.True(popup.IsFocused);
        foreach (var key in new[] { UiKey.Up, UiKey.Down, UiKey.Home, UiKey.End, UiKey.Tab }) Assert.True(ui.HandleInput(new(InputKind.KeyDown, Key: key)).Handled);
        Assert.Equal(0, calls); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape)); Assert.Null(host.Popup); Assert.True(trigger.IsFocused);
    }

    [Theory]
    [InlineData("empty")] [InlineData("duplicate")] [InlineData("overflow")] [InlineData("null")]
    public void InvalidActionLists_AreRejectedBeforeCommandSubscriptions(string kind)
    {
        var command = new Command(); IEnumerable<MenuAction> actions = kind switch
        {
            "empty" => [], "duplicate" => [new("a", "A", command), new("a", "B", command)], "null" => [null!],
            _ => Enumerable.Range(0, 65).Select(index => new MenuAction(index.ToString(), "Action", command))
        };
        Assert.ThrowsAny<ArgumentException>(() => new MenuPopup(actions, () => { })); Assert.Equal(0, command.Subscribers);
    }

    [Fact]
    public void ExactCapacityAndDispose_DisconnectAllRowsAndPreserveBorrowedCommand()
    {
        var command = new Command(); var popup = new MenuPopup(Enumerable.Range(0, 64).Select(index => new MenuAction(index.ToString(), "Action", command)), () => { });
        Assert.Equal(64, popup.Children.Count); Assert.Equal(64, command.Subscribers);
        popup.Dispose(); popup.Dispose(); Assert.Equal(0, command.Subscribers); command.Execute(); Assert.Equal(1, command.Calls);
    }

    [Fact]
    public void ShiftF10_RemainsAvailableForContextConsumersWhilePlainF10OpensMenuBar()
    {
        var content = new VStack(); var host = new OverlayHost(content);
        var bar = new MenuBar(host, [new("file", "File", [new("new", "New", new DelegateUiCommand(() => { }))])]); content.Add(bar);
        using var ui = Create(host); ui.Update(); ui.Focus(bar.MoreButton);
        Assert.False(ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.F10, Shift: true)).Handled);
        Assert.False(bar.IsExpanded); Assert.True(bar.MoreButton.IsFocused);
        Assert.True(ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.F10)).Handled); Assert.True(bar.IsExpanded); Assert.True(bar.Headings["file"].IsFocused);
    }

    [Fact]
    public void Escape_DismissesFocusedContextBeforeAnExpandedMenuBar()
    {
        var content = new VStack(); var host = new OverlayHost(content); var trigger = Button.Simple("Context");
        var bar = new MenuBar(host, [new("file", "File", [new("new", "New", new DelegateUiCommand(() => { }))])]);
        content.WithChildren(bar, trigger); using var ui = Create(host); ui.Update(); ui.Focus(bar.MoreButton);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.F10)); Assert.True(bar.IsExpanded);
        ui.Focus(trigger);
        var popup = new MenuPopup([new("context", "Context action", new DelegateUiCommand(() => { }))], () => host.Close());
        host.Open(trigger, popup, trigger.Bounds, ui, trigger); popup.FocusFirst(ui); ui.Update();
        Assert.True(bar.IsExpanded); Assert.True(popup.Children[0].IsFocused);
        Assert.True(ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape)).Handled);
        Assert.Null(host.Popup); Assert.True(popup.IsDisposed); Assert.True(trigger.IsFocused);
        Assert.False(bar.MoreButton.IsFocused); Assert.False(bar.IsExpanded);
    }

    private static UiSession Create(Element root) => new(root, new Surface(), new(640, 480));
    private sealed class Surface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new FixedTextLayout();
        public void VerifyAvailable() { }
        public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport) { }
        public void Dispose() { }
    }
    private sealed class Command : IUiCommand
    {
        public int Subscribers, Calls;
        public bool CanExecute => true;
        public event Action? CanExecuteChanged { add { Subscribers++; } remove { Subscribers--; } }
        public void Execute() => Calls++;
    }
}
