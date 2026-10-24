using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Argentis;
using TrueMoon.Alloy.Runtime;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Inspector;

public sealed class InspectorIconActionsTests
{
    [Fact]
    public void IconActions_UseCompactSemanticSymbolsWhileSchemaChoicesAndRemovedComponentNamesRemainVisible()
    {
        using var test = new IconsFixture();
        var view = test.View;
        Assert.Equal(new[] { view.Apply, view.Cancel, view.Undo, view.Redo, view.AddComponent }, view.Actions.Children);
        AssertIcon(view.Apply, UiSymbol.Check); AssertIcon(view.Cancel, UiSymbol.Close);
        AssertIcon(view.Undo, UiSymbol.Undo); AssertIcon(view.Redo, UiSymbol.Redo); AssertIcon(view.AddComponent, UiSymbol.Add);
        foreach (var section in view.Components.Values)
        {
            AssertIcon(section.Remove, UiSymbol.Delete);
            Assert.Same(section.Header.Parent, section.Remove.Parent);
            Assert.True(section.Header.Bounds.X + section.Header.Bounds.Width <= section.Remove.Bounds.X + .01f);
        }
        foreach (var property in view.Properties.Values) AssertIcon(property.Reset, UiSymbol.Undo, 24);
        Assert.All(view.AddButtons.Values, button => { Assert.IsNotType<IconButton>(button); Assert.NotEmpty(button.Value); });

        test.Owner.Model.Refresh(InspectorFixture.Parent); test.Drain();
        Assert.Equal(2, view.RestoreButtons.Count);
        foreach (var removed in test.Owner.Model.RemovedComponents)
        {
            var restore = view.RestoreButtons[removed.Id]; AssertIcon(restore, UiSymbol.Redo);
            Assert.Contains(restore.Parent!.Children.OfType<Text>(), label => label.Value == removed.Title && label.IsVisible);
        }
        var apply = view.Apply; var tree = DocumentUiFixture.Tree(view).ToArray();
        test.Ui.SetTheme(Theme.Light); view.WithViewStyle(new EditorInspectorViewStyle()); test.Drain();
        Assert.Same(apply, view.Apply); Assert.Equal(tree, DocumentUiFixture.Tree(view));
        AssertIcon(view.Properties[(DocumentData.Custom, "text")].Reset, UiSymbol.Undo, 24);
    }

    [Fact]
    public void Tooltip_DisabledIconAppearsAtExact500MillisecondsWithoutStealingFocusOrKeepingIdleFrames()
    {
        using var test = new IconsFixture(); var view = test.View; var input = view.Properties[(DocumentData.Transform, "x")].Input;
        test.Ui.Focus(input); test.Hover(view.Apply);
        Assert.False(view.Apply.IsEnabled); Assert.True(view.NeedsFrame);
        test.Clock.Advance(499); test.Drain();
        Assert.Null(test.Host.Popup); Assert.True(input.IsFocused); Assert.True(view.NeedsFrame);
        test.Clock.Advance(1); test.Drain();
        var popup = Assert.IsAssignableFrom<Element>(test.Host.Popup);
        Assert.Equal(view.Apply.AccessibleName + " · сейчас недоступно", Assert.Single(DocumentUiFixture.Tree(popup).OfType<Text>()).Value);
        Assert.Same(view, test.Host.PopupOwner); Assert.True(input.IsFocused);
        Assert.False(view.NeedsFrame); Assert.False(test.Ui.Update());
        Assert.Equal(0, test.Owner.Owner.Document.Read().Content!.UndoCount); Assert.False(test.Owner.Model.HasDraft);
    }

    [Fact]
    public void Tooltip_TargetChangeRestartsDelayAndOnlyCurrentIconCanOwnTheHint()
    {
        using var test = new IconsFixture();
        test.Hover(test.View.Apply); test.Clock.Advance(499); test.Drain();
        test.Hover(test.View.Cancel); test.Clock.Advance(1); test.Drain();
        Assert.Null(test.Host.Popup); Assert.True(test.View.NeedsFrame);
        test.Clock.Advance(498); test.Drain(); Assert.Null(test.Host.Popup);
        test.Clock.Advance(1); test.Drain();
        Assert.Equal(test.View.Cancel.AccessibleName + " · сейчас недоступно",
            Assert.Single(DocumentUiFixture.Tree(test.Host.Popup!).OfType<Text>()).Value);
        Assert.Same(test.View, test.Host.PopupOwner);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Tooltip_UsesOpaqueThemeControlSurfaceAndUpdatesExistingPopupOnThemeChange(bool light)
    {
        using var test = new IconsFixture(); var initial = light ? Theme.Light : Theme.Dark;
        test.Ui.SetTheme(initial); test.Drain(); test.ShowHint(test.View.AddComponent);
        var popup = test.Host.Popup!;
        Assert.Equal(initial.Control, popup.Background); Assert.Equal(255, popup.Background.A);
        var next = light ? Theme.Dark : Theme.Light; test.Ui.SetTheme(next); test.Drain();
        Assert.Same(popup, test.Host.Popup); Assert.Equal(next.Control, popup.Background); Assert.Equal(255, popup.Background.A);
        Assert.Equal(next.Foreground, Assert.Single(DocumentUiFixture.Tree(popup).OfType<Text>()).Foreground);
    }

    [Fact]
    public void Tooltip_WithoutAncestorHostDoesNotScheduleFramesOrMutateTheBorrowedDocument()
    {
        using var owner = new InspectorFixture(); var clock = new HeaderClock();
        var view = new EditorInspectorView(owner.Model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view, clock); ui.Update(); ui.Update();
        var bounds = view.Apply.Bounds; var before = owner.Owner.Document.Read().Content;
        ui.HandleInput(new(InputKind.PointerMove, bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2));
        clock.Advance(1000); ui.Update(); ui.Update();
        Assert.False(view.NeedsFrame); Assert.Same(view, ui.Root); Assert.Same(before, owner.Owner.Document.Read().Content);
        Assert.False(owner.Model.HasDraft); Assert.False(ui.Update());
    }

    [Theory]
    [InlineData("pointer-leave")]
    [InlineData("pointer-down")]
    [InlineData("key")]
    [InlineData("focus-lost")]
    [InlineData("wheel")]
    [InlineData("selection")]
    [InlineData("hide")]
    [InlineData("scroll")]
    public void Tooltip_InputOrContextChangeDisposesOwnedHintWithoutPublishingAnEdit(string change)
    {
        using var test = new IconsFixture(); test.ShowHint(test.View.Apply);
        var popup = test.Host.Popup!; var before = test.Owner.Owner.Document.Read().Content;
        switch (change)
        {
            case "pointer-leave": test.Ui.HandleInput(new(InputKind.PointerMove, 1200, 790)); break;
            case "pointer-down": test.Ui.HandleInput(new(InputKind.PointerDown, 1200, 790)); break;
            case "key": test.Ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A)); break;
            case "focus-lost": test.Ui.HandleInput(new(InputKind.FocusLost)); break;
            case "wheel": test.Ui.HandleInput(new(InputKind.Wheel, 10, 10, WheelDelta: -1)); break;
            case "selection": test.Owner.Model.Refresh(InspectorFixture.Parent); break;
            case "hide": test.View.IsVisible = false; break;
            case "scroll": test.View.Scroll.Offset = 50; break;
        }
        test.Drain(); test.Clock.Advance(1000); test.Drain();
        Assert.Null(test.Host.Popup); Assert.True(popup.IsDisposed); Assert.False(test.View.NeedsFrame);
        Assert.Same(before, test.Owner.Owner.Document.Read().Content); Assert.False(test.Owner.Model.HasDraft);
        Assert.False(test.Host.IsDisposed);
        if (change == "hide")
        {
            test.View.IsVisible = true; test.Drain(); test.ShowHint(test.View.Apply);
            Assert.Equal(test.View.Apply.AccessibleName + " · сейчас недоступно", Assert.Single(DocumentUiFixture.Tree(test.Host.Popup!).OfType<Text>()).Value);
        }
    }

    [Fact]
    public void Tooltip_DetachClosesPopupAndReattachmentRequiresFreshHoverWithoutOwningHostOrModel()
    {
        using var test = new IconsFixture(); test.ShowHint(test.View.AddComponent);
        var popup = test.Host.Popup!; test.Content.Items.Remove(test.View); test.Drain();
        Assert.Null(test.Host.Popup); Assert.True(popup.IsDisposed); Assert.False(test.View.IsDisposed); Assert.False(test.Host.IsDisposed);
        test.Content.Add(test.View); test.Drain(); test.Clock.Advance(1000); test.Drain();
        Assert.Null(test.Host.Popup); Assert.False(test.View.NeedsFrame);
        test.ShowHint(test.View.AddComponent);
        Assert.Equal(test.View.AddComponent.AccessibleName, Assert.Single(DocumentUiFixture.Tree(test.Host.Popup!).OfType<Text>()).Value);
        test.Content.Items.Remove(test.View); test.Drain(); test.View.Dispose();
        Assert.False(test.Host.IsDisposed); Assert.False(test.Owner.Owner.Commands.Read().Closed);
        test.Owner.Property(DocumentData.Custom, "text").Value = "model survives"; test.Owner.Model.Apply();
        Assert.Equal(1, test.Owner.Owner.Document.Read().Content!.UndoCount);
    }

    [Fact]
    public void Tooltip_ExistingForeignPopupIsNeverReplacedOrClosedByInspectorHoverOrDisposal()
    {
        using var test = new IconsFixture(); var foreignOwner = test.Content; var popup = new Text("foreign popup");
        test.Host.Open(foreignOwner, popup, new(500, 10, 10, 10), test.Ui, tooltip: true); test.Drain();
        test.Hover(test.View.Apply); test.Clock.Advance(600); test.Drain();
        Assert.Same(popup, test.Host.Popup); Assert.Same(foreignOwner, test.Host.PopupOwner); Assert.False(test.View.NeedsFrame);
        test.Content.Items.Remove(test.View); test.View.Dispose(); test.Drain();
        Assert.Same(popup, test.Host.Popup); Assert.False(popup.IsDisposed); Assert.False(test.Host.IsDisposed);
    }

    private static void AssertIcon(Button button, UiSymbol symbol, float side = 26)
    {
        Assert.Equal(symbol, Assert.IsAssignableFrom<IconButton>(button).Symbol); Assert.Equal("", button.Value);
        Assert.False(string.IsNullOrWhiteSpace(button.AccessibleName));
        Assert.Equal(side, button.Width); Assert.Equal(side, button.Height); Assert.Equal(new Thickness(0), button.Padding);
        Assert.Equal(side, button.Bounds.Width); Assert.Equal(side, button.Bounds.Height);
    }

    private sealed class IconsFixture : IDisposable
    {
        public InspectorFixture Owner { get; } = new();
        public EditorInspectorView View { get; }
        public Panel Content { get; } = new();
        public OverlayHost Host { get; }
        public HeaderClock Clock { get; } = new();
        public UiSession Ui { get; }
        public IconsFixture()
        {
            View = new(Owner.Model, new EditorInspectorViewStyle()); Content.Add(View); Host = new(Content);
            Ui = InspectorFixture.Ui(Host, Clock); Drain();
        }
        public void Drain() { Ui.Update(); Ui.Update(); }
        public void Hover(Button button)
        {
            View.Scroll.BringIntoView(button.Bounds); Drain();
            var bounds = button.Bounds; Ui.HandleInput(new(InputKind.PointerMove, bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2));
        }
        public void ShowHint(Button button) { Hover(button); Clock.Advance(500); Drain(); Assert.NotNull(Host.Popup); }
        public void Dispose() { Ui.Dispose(); if (!View.IsDisposed) View.Dispose(); Owner.Dispose(); }
    }
}
