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
        Assert.Equal(new Rect(10, -150, 100, 250), child.Bounds);
        Assert.Equal(new Size(40, 80), scroll.DesiredSize);

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

        Assert.Throws<InvalidOperationException>(() => panel.Draw(context));
        Assert.Equal(2, context.Saves);
        Assert.Equal(2, context.Restores);
        Assert.Equal(new[] { panel.Bounds, child.Bounds }, context.Clips);
    }
}
