using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class StatusBarTests
{
    [Fact]
    public void SecondaryOverflow_PreservesDeclarationOrderAndRetainsLaterItemsFirst()
    {
        using var fixture = new Fixture();
        Assert.Empty(fixture.Bar.OverflowedItems);
        fixture.Resize(450);
        Assert.Equal(new Element[] { fixture.First }, fixture.Bar.OverflowedItems);
        Assert.False(fixture.First.Parent!.IsVisible);
        Assert.True(fixture.Second.Parent!.IsVisible);
        Assert.True(fixture.Last.Parent!.IsVisible);
        Assert.True(fixture.Second.Bounds.X < fixture.Last.Bounds.X);

        fixture.Resize(360);
        Assert.Equal(new Element[] { fixture.First, fixture.Second }, fixture.Bar.OverflowedItems);
        Assert.True(fixture.Last.Parent.IsVisible);
        fixture.Resize(216);
        Assert.Equal(new Element[] { fixture.First, fixture.Second, fixture.Last }, fixture.Bar.OverflowedItems);
        Assert.Equal(120, fixture.Primary.Bounds.Width);
    }

    [Fact]
    public void SecondaryFit_AtExactWidthKeepsAllAndOnePixelLessCollapsesLowestPriority()
    {
        using var fixture = new Fixture();
        fixture.Resize(510);
        Assert.Empty(fixture.Bar.OverflowedItems);
        Assert.Equal(120, fixture.Primary.Bounds.Width);
        Assert.Equal(80, fixture.First.Bounds.Width);
        Assert.Equal(90, fixture.Second.Bounds.Width);
        Assert.Equal(100, fixture.Last.Bounds.Width);
        fixture.Resize(509);
        Assert.Same(fixture.First, Assert.Single(fixture.Bar.OverflowedItems));
        Assert.True(fixture.Second.Parent!.IsVisible);
        Assert.True(fixture.Last.Parent!.IsVisible);
        Assert.Equal(56, fixture.Action.Bounds.Width);
        Assert.Equal(24, fixture.Details.Bounds.Width);
    }

    [Fact]
    public void NarrowWidth_ReservesDetailsAndActionBeforePrimaryAndSecondary()
    {
        using var fixture = new Fixture();
        fixture.Resize(100);
        Assert.Equal(new Rect(76, 2, 24, 24), fixture.Details.Bounds);
        Assert.Equal(new Rect(12, 2, 56, 24), fixture.Action.Bounds);
        Assert.Equal(new Rect(0, 2, 4, 24), fixture.Primary.Bounds);
        Assert.Equal(3, fixture.Bar.OverflowedItems.Count);
        fixture.Resize(70);
        Assert.Equal(24, fixture.Details.Bounds.Width);
        Assert.Equal(38, fixture.Action.Bounds.Width);
        Assert.Equal(0, fixture.Primary.Bounds.Width);
        Assert.Equal(fixture.Details.Bounds.X, fixture.Action.Bounds.X + fixture.Action.Bounds.Width);
    }

    [Fact]
    public void HiddenSecondary_DoesNotConsumeBudgetOrAppearInOverflowAndKeepsItsVisibility()
    {
        using var fixture = new Fixture();
        fixture.First.IsVisible = false;
        fixture.Resize(422);
        Assert.Empty(fixture.Bar.OverflowedItems);
        Assert.False(fixture.First.IsVisible);
        Assert.False(fixture.First.Parent!.IsVisible);
        Assert.True(fixture.Second.Parent!.IsVisible);
        Assert.True(fixture.Last.Parent!.IsVisible);
        fixture.Resize(216);
        Assert.Equal(new Element[] { fixture.Second, fixture.Last }, fixture.Bar.OverflowedItems);
        fixture.Resize(600);
        Assert.Empty(fixture.Bar.OverflowedItems);
        Assert.False(fixture.First.IsVisible);
        Assert.False(fixture.First.Parent.IsVisible);
    }

    [Fact]
    public void HiddenAction_ReleasesItsWidthAndSpacingForSecondaryItems()
    {
        using var fixture = new Fixture();
        fixture.Resize(446);
        Assert.Same(fixture.First, Assert.Single(fixture.Bar.OverflowedItems));
        fixture.Action.IsVisible = false;
        fixture.Ui.Update();
        Assert.Empty(fixture.Bar.OverflowedItems);
        Assert.Equal(default, fixture.Action.Bounds);
        Assert.Equal(120, fixture.Primary.Bounds.Width);
        Assert.True(fixture.First.Parent!.IsVisible);
        fixture.Action.IsVisible = true;
        fixture.Ui.Update();
        Assert.Same(fixture.First, Assert.Single(fixture.Bar.OverflowedItems));
        Assert.Equal(56, fixture.Action.Bounds.Width);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(15, 12)]
    [InlineData(24, 28)]
    [InlineData(70, 28)]
    [InlineData(100, 28)]
    [InlineData(510, 28)]
    public void Arrange_ExtremelyNarrowOrShortBoundsStayFiniteAndVisiblePartsNeverOverlap(float width, float height)
    {
        using var primary = new Part(500);
        using var action = new Part(56);
        using var details = new Part(24);
        using var secondary = new Part(100);
        using var bar = new StatusBar(primary, action, [secondary], details) { Padding = default };
        bar.Measure(new(width, height), new FixedTextLayout());
        bar.Arrange(new(13, 17, width, height));
        var visible = bar.Children.Where(element => element.IsVisible).ToArray();
        Assert.All(visible.Append(bar), element =>
        {
            var bounds = element.Bounds;
            Assert.True(float.IsFinite(bounds.X)); Assert.True(float.IsFinite(bounds.Y));
            Assert.True(float.IsFinite(bounds.Width)); Assert.True(float.IsFinite(bounds.Height));
            Assert.InRange(bounds.Width, 0, width); Assert.InRange(bounds.Height, 0, height);
            if (bounds.Width > 0 && bounds.Height > 0)
            {
                Assert.InRange(bounds.X, bar.Bounds.X, bar.Bounds.X + bar.Bounds.Width - bounds.Width);
                Assert.InRange(bounds.Y, bar.Bounds.Y, bar.Bounds.Y + bar.Bounds.Height - bounds.Height);
            }
        });
        var occupied = visible.Where(element => element.Bounds.Width > 0 && element.Bounds.Height > 0)
            .OrderBy(element => element.Bounds.X).ToArray();
        for (var i = 1; i < occupied.Length; i++)
            Assert.True(occupied[i - 1].Bounds.X + occupied[i - 1].Bounds.Width <= occupied[i].Bounds.X);
        Assert.Equal(Math.Min(24, width), details.Bounds.Width);
    }

    [Fact]
    public void OverflowCollapse_PreservesChildVisibilityAndExcludesItFromTabUntilExpanded()
    {
        using var fixture = new Fixture();
        fixture.Resize(450);
        Assert.True(fixture.First.IsVisible);
        Assert.True(fixture.First.IsAttached);
        Assert.False(fixture.First.IsDisposed);
        fixture.Ui.Focus(fixture.Action);
        fixture.Tab(); Assert.True(fixture.Second.IsFocused);
        fixture.Tab(); Assert.True(fixture.Last.IsFocused);
        fixture.Tab(); Assert.True(fixture.Details.IsFocused);
        Assert.False(fixture.First.IsFocused);

        fixture.Resize(600);
        Assert.True(fixture.First.Parent!.IsVisible);
        Assert.Equal(80, fixture.First.Bounds.Width);
        Assert.Empty(fixture.Bar.OverflowedItems);
        fixture.Ui.Focus(fixture.Action);
        fixture.Tab(); Assert.True(fixture.First.IsFocused);
    }

    [Fact]
    public void FocusedOverflow_ReturnsFocusToDetailsOnNextFrameAndStopsRequestingFrames()
    {
        using var fixture = new Fixture();
        fixture.Ui.Focus(fixture.First);
        fixture.Resize(450);
        Assert.True(fixture.Bar.NeedsFrame);
        Assert.False(fixture.Details.IsFocused);
        fixture.Ui.Update();
        Assert.True(fixture.Details.IsFocused);
        Assert.False(fixture.First.IsFocused);
        Assert.False(fixture.Bar.NeedsFrame);
        fixture.Ui.Update();
        Assert.False(fixture.Ui.NeedsUpdate);
    }

    [Fact]
    public void FocusedDescendant_WhenItsSecondaryOverflowsMovesFocusToDetails()
    {
        var leaf = new Part(80, true);
        var group = new ContentControl(); group.SetContent(leaf);
        var details = new Part(24, true);
        var bar = new StatusBar(new Part(500), new Part(56), [group], details) { Padding = default };
        using var ui = new UiSession(bar, new Surface(), new UiViewport(600, 28));
        ui.Update(); ui.Focus(leaf);
        ui.Resize(new(216, 28)); ui.Update();
        Assert.Same(group, Assert.Single(bar.OverflowedItems));
        Assert.True(bar.NeedsFrame);
        ui.Update();
        Assert.True(details.IsFocused); Assert.False(leaf.IsFocused);
        Assert.False(bar.NeedsFrame);
        Assert.True(leaf.IsVisible); Assert.True(leaf.IsAttached); Assert.False(leaf.IsDisposed);
    }

    [Fact]
    public void DisabledDetails_WhenFocusedSecondaryOverflowsSettlesWithoutInvalidFocus()
    {
        using var fixture = new Fixture();
        fixture.Ui.Focus(fixture.First);
        fixture.Details.IsEnabled = false;
        fixture.Resize(450);
        Assert.True(fixture.Bar.NeedsFrame);
        fixture.Ui.Update();
        Assert.False(fixture.Bar.NeedsFrame);
        Assert.False(fixture.Details.IsFocused);
        Assert.False(fixture.First.IsFocused);
    }

    [Fact]
    public void Expansion_RestoresSameOwnedElementsAndLeavesPreviousOverflowSnapshotStable()
    {
        using var fixture = new Fixture();
        var firstOwner = fixture.First.Parent;
        fixture.Resize(360);
        var snapshot = fixture.Bar.OverflowedItems;
        Assert.Equal(new Element[] { fixture.First, fixture.Second }, snapshot);
        fixture.Resize(600);
        Assert.Empty(fixture.Bar.OverflowedItems);
        Assert.Equal(new Element[] { fixture.First, fixture.Second }, snapshot);
        Assert.Same(firstOwner, fixture.First.Parent);
        Assert.True(fixture.First.Parent!.IsVisible);
        Assert.Equal(0, fixture.First.DisposeCalls);
        Assert.True(fixture.First.IsAttached);
    }

    [Fact]
    public void Detach_CancelsPendingFocusReturnWithoutDisposingAndReattachKeepsParts()
    {
        using var fixture = new Fixture();
        fixture.Ui.Focus(fixture.First);
        fixture.Resize(450);
        Assert.True(fixture.Bar.NeedsFrame);
        fixture.Root.Items.Remove(fixture.Bar);
        Assert.False(fixture.Bar.NeedsFrame);
        Assert.False(fixture.Bar.IsAttached);
        Assert.False(fixture.First.IsAttached);
        Assert.False(fixture.Bar.IsDisposed);
        fixture.Ui.Update();
        fixture.Root.Add(fixture.Bar);
        fixture.Resize(600);
        Assert.True(fixture.First.IsAttached);
        Assert.True(fixture.First.Parent!.IsVisible);
        Assert.Empty(fixture.Bar.OverflowedItems);
        Assert.False(fixture.Details.IsFocused);
        Assert.False(fixture.Bar.NeedsFrame);
    }

    [Fact]
    public void Disposal_ReleasesEveryOwnedPartOnceAndClearsOverflowAndPendingFrame()
    {
        using var fixture = new Fixture();
        fixture.Ui.Focus(fixture.First);
        fixture.Resize(360);
        Assert.True(fixture.Bar.NeedsFrame);
        fixture.Root.Items.Remove(fixture.Bar);
        fixture.Bar.Dispose(); fixture.Bar.Dispose();
        Assert.True(fixture.Bar.IsDisposed);
        Assert.Empty(fixture.Bar.OverflowedItems);
        Assert.False(fixture.Bar.NeedsFrame);
        Assert.All(fixture.Parts, part =>
        {
            Assert.True(part.IsDisposed);
            Assert.False(part.IsAttached);
            Assert.Equal(1, part.DisposeCalls);
        });
    }

    [Fact]
    public void Constructor_RejectsNullDuplicateAndAlreadyOwnedPartsWithoutTakingOwnership()
    {
        using var primary = new Part(100);
        using var action = new Part(56);
        using var details = new Part(24);
        using var secondary = new Part(80);
        Assert.Throws<ArgumentNullException>(() => new StatusBar(null!, action, [], details));
        Assert.Throws<ArgumentNullException>(() => new StatusBar(primary, null!, [], details));
        Assert.Throws<ArgumentNullException>(() => new StatusBar(primary, action, null!, details));
        Assert.Throws<ArgumentNullException>(() => new StatusBar(primary, action, [], null!));
        Assert.Throws<ArgumentException>(() => new StatusBar(primary, action, [primary], details));
        Assert.Throws<ArgumentException>(() => new StatusBar(primary, action, [secondary, secondary], details));
        Assert.Throws<ArgumentException>(() => new StatusBar(primary, action, [null!], details));
        Assert.Null(primary.Parent); Assert.Null(action.Parent); Assert.Null(details.Parent); Assert.Null(secondary.Parent);
        using var owner = new Panel(); owner.Add(secondary);
        Assert.Throws<ArgumentException>(() => new StatusBar(primary, action, [secondary], details));
        Assert.Same(owner, secondary.Parent);
        Assert.Null(primary.Parent); Assert.Null(action.Parent); Assert.Null(details.Parent);
        Assert.False(primary.IsDisposed); Assert.False(action.IsDisposed); Assert.False(details.IsDisposed);
    }

    private sealed class Fixture : IDisposable
    {
        public Part Primary { get; } = new(500);
        public Part Action { get; } = new(56, true);
        public Part First { get; } = new(80, true);
        public Part Second { get; } = new(90, true);
        public Part Last { get; } = new(100, true);
        public Part Details { get; } = new(24, true);
        public Part[] Parts => [Primary, Action, First, Second, Last, Details];
        public Panel Root { get; } = new();
        public StatusBar Bar { get; }
        public UiSession Ui { get; }
        public Fixture()
        {
            Bar = new(Primary, Action, [First, Second, Last], Details) { Padding = default };
            Root.Add(Bar);
            Ui = new(Root, new Surface(), new UiViewport(600, 28)); Ui.Update();
        }
        public void Resize(int width) { Ui.Resize(new(width, 28)); Ui.Update(); }
        public void Tab() => Ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Tab));
        public void Dispose() => Ui.Dispose();
    }

    private sealed class Part(float width, bool focusable = false) : Element
    {
        public int DisposeCalls { get; private set; }
        public override bool Focusable => focusable;
        protected override Size MeasureCore(Size available, ITextLayoutService text) => new(width, 24);
        public override void Dispose() { if (!IsDisposed) DisposeCalls++; base.Dispose(); }
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
