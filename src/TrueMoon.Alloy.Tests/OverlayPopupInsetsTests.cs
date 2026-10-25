using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class OverlayPopupInsetsTests
{
    [Theory]
    [InlineData(false, 20, 5, 80, 50, 20, 36, 80, 50)]
    [InlineData(true, 20, 5, 80, 50, 20, 36, 80, 50)]
    [InlineData(false, 230, 160, 80, 50, 152, 110, 80, 50)]
    [InlineData(true, 230, 160, 80, 50, 152, 110, 80, 50)]
    [InlineData(false, 230, 5, 500, 400, 8, 36, 224, 136)]
    [InlineData(true, 230, 5, 500, 400, 8, 36, 224, 136)]
    public void PopupInsets_KeepMenusAndTooltipsBelowCaptionWhileContentRetainsFullViewport(
        bool tooltip, float x, float y, float width, float height,
        float expectedX, float expectedY, float expectedWidth, float expectedHeight)
    {
        using var fixture = new Fixture(new(8, 36, 8, 8));
        var popup = new SizedElement(width, height);
        fixture.Host.Open(fixture.Owner, popup, new(x, y, 10, 20), fixture.Ui, tooltip: tooltip);
        fixture.Ui.Update();
        Assert.Equal(new Rect(expectedX, expectedY, expectedWidth, expectedHeight), popup.Bounds);
        Assert.Equal(new Size(224, 136), popup.Available);
        Assert.Equal(new Rect(0, 0, 240, 180), fixture.Content.Bounds);
        Assert.Equal(new Rect(0, 0, 240, 30), fixture.Owner.Bounds);
        Assert.Same(fixture.Host, popup.Parent);
    }

    [Fact]
    public void PopupInsets_ChangingPlacementRetainsPopupIdentityFocusAndUnmodifiedContentBounds()
    {
        using var fixture = new Fixture(default);
        var popup = new Button("menu") { Width = 100, Height = 60 };
        fixture.Host.Open(fixture.Owner, popup, new(0, 0, 10, 10), fixture.Ui);
        fixture.Ui.Update(); fixture.Ui.Focus(popup);
        Assert.Equal(new Rect(0, 10, 100, 60), popup.Bounds);
        fixture.Host.PopupInsets = new(8, 36, 12, 10);
        fixture.Ui.Update();
        Assert.Equal(new Rect(8, 36, 100, 60), popup.Bounds);
        Assert.True(popup.IsFocused); Assert.Same(popup, fixture.Host.Popup);
        Assert.Equal(new Rect(0, 0, 240, 180), fixture.Content.Bounds);
        Assert.False(popup.IsDisposed);
        fixture.Host.PopupInsets = default; fixture.Ui.Update();
        Assert.Equal(new Rect(0, 10, 100, 60), popup.Bounds);
        Assert.True(popup.IsFocused);
    }

    [Theory]
    [InlineData(240, 180, 300, 300, 240, 180, 0, 0)]
    [InlineData(240, 180, 240, 180, 240, 180, 0, 0)]
    [InlineData(0, 0, 8, 36, 0, 0, 0, 0)]
    [InlineData(20, 10, 8, 36, 8, 10, 12, 0)]
    public void InsetsExceedingViewport_ProduceFiniteClampedPopupWithoutReducingMainContent(
        int width, int height, float left, float top, float expectedX, float expectedY,
        float expectedWidth, float expectedHeight)
    {
        var owner = new Button("owner");
        using var host = new OverlayHost(owner) { PopupInsets = new(left, top, 0, 0) };
        using var ui = new UiSession(host, new Surface(), new UiViewport(width, height));
        ui.Update();
        var popup = new SizedElement(100, 50);
        host.Open(owner, popup, new(0, 0, 10, 10), ui);
        ui.Update();
        if (width == 0 || height == 0)
        {
            // A suspended session does not perform layout; resume to verify the retained insets.
            ui.Resize(new UiViewport(1, 1)); ui.Update();
            Assert.Equal(new Rect(1, 1, 0, 0), popup.Bounds);
            Assert.Equal(new Rect(0, 0, 1, 1), owner.Bounds);
        }
        else
        {
            Assert.Equal(new Rect(expectedX, expectedY, expectedWidth, expectedHeight), popup.Bounds);
            Assert.Equal(new Rect(0, 0, width, height), owner.Bounds);
        }
        Assert.True(float.IsFinite(popup.Bounds.X)); Assert.True(float.IsFinite(popup.Bounds.Y));
    }

    [Theory]
    [InlineData(0, -1)]
    [InlineData(1, -1)]
    [InlineData(2, -1)]
    [InlineData(3, -1)]
    [InlineData(0, float.NaN)]
    [InlineData(1, float.PositiveInfinity)]
    [InlineData(2, float.NegativeInfinity)]
    [InlineData(3, float.NaN)]
    public void InvalidInsetComponent_IsRejectedWithoutMovingOrClosingExistingPopup(int component, float invalid)
    {
        using var fixture = new Fixture(new(8, 36, 8, 8));
        var popup = new SizedElement(80, 50);
        fixture.Host.Open(fixture.Owner, popup, new(20, 0, 10, 10), fixture.Ui); fixture.Ui.Update();
        var before = popup.Bounds;
        var sides = new[] { 8f, 36f, 8f, 8f }; sides[component] = invalid;
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            fixture.Host.PopupInsets = new(sides[0], sides[1], sides[2], sides[3]));
        fixture.Ui.Update();
        Assert.Equal(before, popup.Bounds); Assert.Same(popup, fixture.Host.Popup);
        Assert.False(popup.IsDisposed); Assert.Equal(new Rect(0, 0, 240, 180), fixture.Content.Bounds);
    }

    private sealed class Fixture : IDisposable
    {
        public VStack Content { get; } = new() { Spacing = 0 };
        public Button Owner { get; } = new("owner") { Height = 30 };
        public OverlayHost Host { get; }
        public UiSession Ui { get; }
        public Fixture(Thickness insets)
        {
            Content.Add(Owner); Content.Add(new SizedElement(240, 150) { Height = 150 });
            Host = new(Content) { PopupInsets = insets };
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
