using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class OverlayHostTests
{
    [Theory]
    [InlineData(20, 20, 80, 50, 20, 40, 80, 50)]
    [InlineData(230, 160, 80, 50, 160, 110, 80, 50)]
    [InlineData(230, 160, 500, 400, 0, 0, 240, 180)]
    public void Popup_IsPlacedBelowOrAboveAnchorAndClampedToViewport(float x, float y, float width, float height,
        float expectedX, float expectedY, float expectedWidth, float expectedHeight)
    {
        using var fixture = new Fixture(); var popup = new SizedElement(width, height);
        fixture.Host.Open(fixture.Owner, popup, new(x, y, 10, 20), fixture.Ui);
        fixture.Ui.Update();
        Assert.Equal(new Rect(expectedX, expectedY, expectedWidth, expectedHeight), popup.Bounds);
        Assert.Same(fixture.Owner, fixture.Host.PopupOwner);
        Assert.Same(fixture.Host, popup.Parent);
    }

    [Fact]
    public void Popup_EscDisposesItAndRestoresLiveTriggerFocus()
    {
        using var fixture = new Fixture(); var popup = new Button("popup"); var closed = 0;
        fixture.Host.Closed += () => closed++;
        fixture.Host.Open(fixture.Owner, popup, fixture.Owner.Bounds, fixture.Ui, fixture.Owner);
        fixture.Ui.Update(); fixture.Ui.Focus(popup); Assert.True(popup.IsFocused);
        var result = fixture.Ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape));
        Assert.True(result.Handled); Assert.Null(fixture.Host.Popup); Assert.Null(fixture.Host.PopupOwner);
        Assert.True(popup.IsDisposed); Assert.False(popup.IsAttached); Assert.True(fixture.Owner.IsFocused);
        Assert.Equal(1, closed); fixture.Host.Close(); Assert.Equal(1, closed);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public void OutsideClick_LightDismissConsumesMenusButPassesThroughTooltips(bool tooltip, int expectedClicks)
    {
        using var fixture = new Fixture(); var popup = new SizedElement(80, 40);
        fixture.Host.Open(fixture.Owner, popup, fixture.Owner.Bounds, fixture.Ui, fixture.Owner, tooltip);
        fixture.Ui.Update();
        var down = fixture.Ui.HandleInput(new(InputKind.PointerDown, 220, 150));
        fixture.Ui.HandleInput(new(InputKind.PointerUp, 220, 150));
        Assert.True(down.Handled);
        Assert.Null(fixture.Host.Popup); Assert.True(popup.IsDisposed);
        Assert.Equal(expectedClicks, fixture.BackgroundClicks);
    }

    [Fact]
    public void FocusLoss_ClosesWithoutRestoringFocus()
    {
        using var fixture = new Fixture(); var popup = new Button("popup");
        fixture.Host.Open(fixture.Owner, popup, fixture.Owner.Bounds, fixture.Ui, fixture.Owner);
        fixture.Ui.Update(); fixture.Ui.Focus(popup);
        fixture.Ui.HandleInput(new(InputKind.FocusLost));
        Assert.Null(fixture.Host.Popup); Assert.True(popup.IsDisposed);
        Assert.False(fixture.Owner.IsFocused); Assert.False(fixture.Background.IsFocused);
    }

    [Fact]
    public void Replacement_DisposesOnlyTheOldPopupAndRetainsMainContent()
    {
        using var fixture = new Fixture(); var first = new Button("first"); var second = new Button("second");
        fixture.Host.Open(fixture.Owner, first, fixture.Owner.Bounds, fixture.Ui);
        fixture.Host.Open(fixture.Owner, second, fixture.Owner.Bounds, fixture.Ui);
        Assert.True(first.IsDisposed); Assert.False(second.IsDisposed);
        Assert.Same(second, fixture.Host.Popup); Assert.False(fixture.Owner.IsDisposed);
        fixture.Ui.Dispose(); Assert.True(second.IsDisposed); Assert.True(fixture.Owner.IsDisposed);
    }

    [Fact]
    public void InvalidReplacement_DoesNotDismissTheExistingPopupOrTakeOwnership()
    {
        using var fixture = new Fixture(); var first = new SizedElement(); var candidate = new SizedElement();
        fixture.Host.Open(fixture.Owner, first, fixture.Owner.Bounds, fixture.Ui);
        Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Host.Open(fixture.Owner, candidate,
            new(float.NaN, 0, 10, 10), fixture.Ui));
        Assert.Same(first, fixture.Host.Popup); Assert.False(first.IsDisposed);
        Assert.Null(candidate.Parent); Assert.False(candidate.IsDisposed); candidate.Dispose();
    }

    [Fact]
    public void DetachedOwner_DismissesAtNextInputAndDoesNotReceiveFocus()
    {
        using var fixture = new Fixture(); var popup = new Button("popup");
        fixture.Host.Open(fixture.Owner, popup, fixture.Owner.Bounds, fixture.Ui, fixture.Owner);
        fixture.Content.Items.Remove(fixture.Owner);
        fixture.Ui.HandleInput(new(InputKind.PointerMove, 220, 150));
        Assert.Null(fixture.Host.Popup); Assert.True(popup.IsDisposed);
        Assert.False(fixture.Owner.IsFocused); Assert.False(fixture.Owner.IsDisposed); fixture.Owner.Dispose();
    }

    [Fact]
    public void ThemeChange_RetainsPopupIdentityAndFocus()
    {
        using var fixture = new Fixture(); var popup = new Button("popup");
        fixture.Host.Open(fixture.Owner, popup, fixture.Owner.Bounds, fixture.Ui, fixture.Owner);
        fixture.Ui.Update(); fixture.Ui.Focus(popup); fixture.Ui.SetTheme(Theme.Light); fixture.Ui.Update();
        Assert.Same(popup, fixture.Host.Popup); Assert.True(popup.IsFocused);
        Assert.Same(Theme.Light, popup.Theme); Assert.False(popup.IsDisposed);
    }

    [Fact]
    public void OwnerFromAnotherSession_IsRejectedWithoutReplacingOrAdoptingPopup()
    {
        using var first = new Fixture(); using var second = new Fixture();
        var existing = new SizedElement(); var candidate = new SizedElement();
        first.Host.Open(first.Owner, existing, first.Owner.Bounds, first.Ui);
        Assert.Throws<ArgumentException>(() => first.Host.Open(second.Owner, candidate, second.Owner.Bounds, first.Ui));
        Assert.Same(existing, first.Host.Popup); Assert.Null(candidate.Parent); Assert.False(candidate.IsDisposed);
        candidate.Dispose();
    }

    [Fact]
    public void HiddenOwnerAncestor_DismissesWithoutRestoringHiddenFocus()
    {
        using var fixture = new Fixture(); var popup = new Button("popup");
        fixture.Host.Open(fixture.Owner, popup, fixture.Owner.Bounds, fixture.Ui, fixture.Owner);
        fixture.Content.IsVisible = false;
        fixture.Ui.HandleInput(new(InputKind.PointerMove, 220, 150));
        Assert.Null(fixture.Host.Popup); Assert.True(popup.IsDisposed); Assert.False(fixture.Owner.IsFocused);
    }

    [Fact]
    public void HostDetach_HidesPopupAndReattachmentDisposesItsRetainedTree()
    {
        var root = new Panel(); var owner = new Button("owner"); var host = new OverlayHost(owner); root.Add(host);
        using var ui = new UiSession(root, new Surface(), new UiViewport(240, 180)); ui.Update();
        var popup = new Button("popup"); host.Open(owner, popup, owner.Bounds, ui, owner); ui.Update();
        root.Items.Remove(host);
        Assert.Null(host.Popup); Assert.Null(host.PopupOwner); Assert.False(popup.IsVisible);
        Assert.False(popup.IsAttached); Assert.False(host.IsDisposed);
        root.Add(host); ui.Update();
        Assert.True(popup.IsDisposed); Assert.Null(host.Popup); Assert.Same(owner, host.Content);
        Assert.False(owner.IsDisposed); Assert.True(owner.IsAttached);
    }

    private sealed class Fixture : IDisposable
    {
        public VStack Content { get; } = new() { Spacing = 0 };
        public Button Owner { get; } = new("owner") { Height = 30 };
        public Button Background { get; } = new("background") { Height = 150 };
        public OverlayHost Host { get; }
        public UiSession Ui { get; }
        public int BackgroundClicks { get; private set; }
        public Fixture()
        {
            Background.Click += () => BackgroundClicks++;
            Content.Add(Owner); Content.Add(Background); Host = new(Content);
            Ui = new(Host, new Surface(), new UiViewport(240, 180)); Ui.Update();
        }
        public void Dispose() => Ui.Dispose();
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
