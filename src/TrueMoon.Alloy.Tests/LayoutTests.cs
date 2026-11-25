using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Views;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class LayoutTests
{
    private static readonly ITextLayoutService TextLayout = new FixedTextLayout();

    [Fact]
    public void MeasureArrange_AsymmetricSpacing_SeparatesMarginPaddingAndContent()
    {
        using var element = new SizedElement { Margin = new Thickness(1, 2, 3, 4), Padding = new Thickness(5, 6, 7, 8) };
        element.Measure(new Size(100, 80), TextLayout);
        element.Arrange(new Rect(10, 20, 100, 80));

        Assert.Equal(new Size(84, 60), element.Available);
        Assert.Equal(new Size(46, 40), element.DesiredSize);
        Assert.Equal(new Rect(11, 22, 96, 74), element.Bounds);
        Assert.Equal(new Rect(16, 28, 84, 60), element.ContentBounds);
    }

    [Theory]
    [InlineData(LayoutAlignment.Start, 10)]
    [InlineData(LayoutAlignment.Center, 45)]
    [InlineData(LayoutAlignment.End, 80)]
    public void Arrange_NonStretchAlignment_PositionsMeasuredContent(LayoutAlignment alignment, float x)
    {
        using var element = new SizedElement { HorizontalAlignment = alignment, VerticalAlignment = LayoutAlignment.Center };
        element.Measure(new Size(100, 80), TextLayout);
        element.Arrange(new Rect(10, 20, 100, 80));

        Assert.Equal(new Rect(x, 50, 30, 20), element.Bounds);
    }

    [Fact]
    public void MeasureArrange_ExplicitSizeHonorsLimits_ClampsToParentSlot()
    {
        using var element = new SizedElement { Width = 100, Height = 5, MinHeight = 15, MaxWidth = 60 };
        element.Measure(new Size(200, 200), TextLayout);
        element.Arrange(new Rect(0, 0, 40, 10));

        Assert.Equal(new Size(60, 15), element.DesiredSize);
        Assert.Equal(new Rect(0, 0, 40, 10), element.Bounds);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Stack_HiddenChild_DoesNotConsumeExtentOrSpacing(bool vertical)
    {
        using StackBase stack = vertical ? new VStack() : new HStack();
        stack.Spacing = 7;
        var first = new SizedElement(30, 20);
        var hidden = new SizedElement(100, 100) { IsVisible = false };
        var last = new SizedElement(40, 10);
        stack.Items.Add(first);
        stack.Items.Add(hidden);
        stack.Items.Add(last);
        stack.Measure(new Size(200, 100), TextLayout);
        stack.Arrange(new Rect(10, 20, 200, 100));

        Assert.Equal(vertical ? new Size(40, 37) : new Size(77, 20), stack.DesiredSize);
        Assert.Equal(default, hidden.Bounds);
        Assert.Equal(vertical ? new Rect(10, 47, 200, 10) : new Rect(47, 20, 40, 100), last.Bounds);
    }

    [Fact]
    public void Stack_EmptyAndSingleton_OnlyAddsSpacingBetweenChildren()
    {
        using var stack = new VStack { Spacing = 7 };
        stack.Measure(new Size(200, 100), TextLayout);
        Assert.Equal(new Size(0, 0), stack.DesiredSize);

        stack.Items.Add(new SizedElement(30, 20));
        stack.Measure(new Size(200, 100), TextLayout);
        Assert.Equal(new Size(30, 20), stack.DesiredSize);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StackFill_SharesFiniteRemainderAndMeasuresNaturallyWhenUnbounded(bool vertical)
    {
        var fixedChild = new SizedElement(30, 30);
        var first = new SizedElement(10, 10).Fill();
        var second = new SizedElement(10, 10).Fill();
        var view = new StackView(vertical, fixedChild, first, second);
        using var ui = Create(view);
        var stack = Assert.IsAssignableFrom<StackBase>(view.Child);
        stack.Spacing = 5;
        Assert.Collection(stack.Children,
            child => Assert.Same(fixedChild, child),
            child => Assert.Same(first, child),
            child => Assert.Same(second, child));
        Assert.All(stack.Children, child => Assert.Same(stack, child.Parent));

        stack.Measure(new Size(100, 100), TextLayout);
        Assert.Equal(vertical ? new Size(100, 30) : new Size(30, 100), first.Available);
        Assert.Equal(first.Available, second.Available);
        stack.Arrange(new Rect(0, 0, 100, 100));
        Assert.Equal(vertical ? new Rect(0, 35, 100, 30) : new Rect(35, 0, 30, 100), first.Bounds);
        Assert.Equal(vertical ? new Rect(0, 70, 100, 30) : new Rect(70, 0, 30, 100), second.Bounds);

        first.IsVisible = false;
        stack.Measure(new Size(100, 100), TextLayout);
        stack.Arrange(new Rect(0, 0, 100, 100));
        Assert.Equal(default, first.Bounds);
        Assert.Equal(vertical ? new Rect(0, 35, 100, 65) : new Rect(35, 0, 65, 100), second.Bounds);

        stack.Measure(vertical ? new Size(100, float.PositiveInfinity) : new Size(float.PositiveInfinity, 100), TextLayout);
        Assert.Equal(vertical ? new Size(30, 45) : new Size(45, 30), stack.DesiredSize);
        Assert.Equal(vertical ? new Size(100, float.PositiveInfinity) : new Size(float.PositiveInfinity, 100), second.Available);

        stack.Measure(new Size(20, 20), TextLayout);
        stack.Arrange(new Rect(0, 0, 20, 20));
        Assert.Equal(new Rect(0, 0, 20, 20), fixedChild.Bounds);
        Assert.Equal(vertical ? new Rect(0, 20, 20, 0) : new Rect(20, 0, 0, 20), second.Bounds);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StackFactory_RejectsAnOwnedChildAndReleasesEarlierChildren(bool vertical)
    {
        using var first = new SizedElement(10, 10);
        var owned = new SizedElement(20, 20);
        using var owner = new Panel().WithChildren(owned);

        using var invalid = new StackView(vertical, first, owned);
        using var surface = new LayoutSurface();
        Assert.Throws<InvalidOperationException>(() => new UiSession(invalid, surface, new UiViewport(100, 100)));

        Assert.Null(first.Parent);
        Assert.False(first.IsDisposed);
        Assert.Same(owner, owned.Parent);
        Assert.Same(owned, Assert.Single(owner.Children));
        var view = new StackView(vertical, first);
        using var ui = Create(view);
        var stack = Assert.IsAssignableFrom<StackBase>(view.Child);
        Assert.Same(first, Assert.Single(stack.Children));
    }

    private sealed class StackView : View
    {
        public StackView(bool vertical, params Element[] children)
            => Content(ui => vertical ? (Element)ui.VStack(children) : ui.HStack(children));
    }

    private static UiSession Create(Element root) => new(root, new LayoutSurface(), new UiViewport(100, 100));

    private sealed class LayoutSurface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout => LayoutTests.TextLayout;
        public void VerifyAvailable() { }
        public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport, UiPresenters? presenters = null) { }
        public void Dispose() { }
    }

    [Fact]
    public void Panel_ChildrenSharePaddedSlot_DesiredSizeUsesMaximum()
    {
        using var panel = new Panel { Padding = new Thickness(5) };
        var first = new SizedElement(30, 40);
        var second = new SizedElement(60, 20);
        panel.Items.Add(first);
        panel.Items.Add(second);
        panel.Measure(new Size(100, 100), TextLayout);
        panel.Arrange(new Rect(10, 20, 100, 100));

        Assert.Equal(new Size(70, 50), panel.DesiredSize);
        Assert.Equal(new Rect(15, 25, 90, 90), first.Bounds);
        Assert.Equal(first.Bounds, second.Bounds);
    }

    [Fact]
    public void ScrollViewer_OffsetBeyondExtent_ClampsAndResetsWhenContentShrinks()
    {
        using var scroll = new ScrollViewer { Offset = 300 };
        var child = new SizedElement(40, 250);
        scroll.SetContent(child);
        scroll.Measure(new Size(100, 80), TextLayout);
        scroll.Arrange(new Rect(10, 20, 100, 80));

        Assert.Equal(250, scroll.Extent);
        Assert.Equal(170, scroll.Offset);
        Assert.Equal(new Rect(10, -150, 90, 250), child.Bounds);
        Assert.Equal(new Size(50, 80), scroll.DesiredSize);

        scroll.SetContent(null);
        scroll.Measure(new Size(100, 80), TextLayout);
        scroll.Arrange(new Rect(10, 20, 100, 80));
        Assert.Equal(0, scroll.Offset);
        Assert.Equal(0, scroll.Extent);
        child.Dispose();
    }

    [Fact]
    public void Draw_ChildFillThrows_RestoresEveryClipBeforePropagating()
    {
        using var panel = new Panel();
        var child = new SizedElement { Background = new Color(1, 2, 3) };
        panel.Items.Add(child);
        panel.Measure(new Size(100, 80), TextLayout);
        panel.Arrange(new Rect(10, 20, 100, 80));
        var context = new RecordingDrawingContext { ThrowOnFill = true };

        Assert.Throws<InvalidOperationException>(() => TrueMoon.Alloy.Runtime.UiTreeRenderer.Draw(panel, context));
        Assert.Equal(2, context.Saves);
        Assert.Equal(2, context.Restores);
        Assert.Equal(new[] { panel.Bounds, child.Bounds }, context.Clips);
    }
}
