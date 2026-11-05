using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class PropertyEditorRowTests
{
    [Theory]
    [InlineData(false, 171f, true)] [InlineData(false, 172f, false)] [InlineData(false, 173f, false)]
    [InlineData(true, 205f, true)] [InlineData(true, 206f, false)] [InlineData(true, 207f, false)]
    public void Layout_ExactResponsiveThresholdAccountsForVisibleAccessory(bool accessory, float width, bool stacked)
    {
        var input = new SizedElement(50, 26); var action = accessory ? new SizedElement(30, 26) : null;
        using var row = new PropertyEditorRow(input, action, "Value") { Spacing = 4 };
        row.Measure(new(width, 100), new FixedTextLayout()); row.Arrange(new(10, 20, width, row.DesiredSize.Height));
        var x = stacked ? 10 : 102; var y = stacked ? 40 : 20;
        Assert.Equal(stacked ? 46 : 26, row.DesiredSize.Height);
        Assert.Equal(new Rect(10, stacked ? 20 : 25, stacked ? width : 88, 16), row.Label.Bounds);
        Assert.Equal(new Rect(x, y, width - (stacked ? 0 : 92) - (accessory ? 34 : 0), 26), input.Bounds);
        if (action != null) Assert.Equal(new Rect(10 + width - 30, y, 30, 26), action.Bounds);
        Assert.Same(input, row.Editor); Assert.Same(action, row.Accessory); Assert.False(row.Empty.IsVisible);
    }

    [Theory]
    [InlineData(120f, 120f)] [InlineData(96f, 96f)] [InlineData(float.NaN, 374f)] [InlineData(0f, 0f)]
    public void ValueWidth_CapsOrFillsValueWithoutMovingSharedLabelAndKeepsAccessoryNextToValue(float cap, float expected)
    {
        var input = new SizedElement(50, 26); var action = new SizedElement(30, 26);
        using var row = new PropertyEditorRow(input, action, "Value") { Spacing = 4, ValueWidth = cap };
        row.Measure(new(500, 100), new FixedTextLayout()); row.Arrange(new(0, 0, 500, 26));
        Assert.Equal(new Rect(92, 0, expected, 26), input.Bounds);
        Assert.Equal(new Rect(92 + expected + 4, 0, 30, 26), action.Bounds); Assert.Equal(88, row.Label.Bounds.Width);
        action.IsVisible = false; row.Measure(new(500, 100), new FixedTextLayout()); row.Arrange(new(0, 0, 500, 26));
        Assert.Equal(float.IsNaN(cap) ? 408 : expected, input.Bounds.Width); Assert.Equal(default, action.Bounds);
    }

    [Theory]
    [InlineData(0f)] [InlineData(1f)] [InlineData(7f)] [InlineData(29f)]
    public void TinyWidth_ClampsEditorAccessoryAndLabelInsideSlot(float width)
    {
        using var row = new PropertyEditorRow(new SizedElement(100, 26), new SizedElement(30, 26), "Value") { Spacing = 4 };
        row.Measure(new(width, 90), new FixedTextLayout()); row.Arrange(new(5, 9, width, 90));
        Assert.All(row.Children.Where(child => child.IsVisible), child =>
        {
            Assert.InRange(child.Bounds.Width, 0, width); Assert.InRange(child.Bounds.X, 5, 5 + width);
            Assert.True(float.IsFinite(child.Bounds.Width)); Assert.True(child.Bounds.X + child.Bounds.Width <= 5 + width);
        });
        Assert.Equal(0, row.Editor.Bounds.Width); Assert.Equal(width, row.Accessory!.Bounds.Width);
    }

    [Fact]
    public void ShowEmpty_CompactsWithoutReplacingDraftAndDisposesOnlyOwnedPartsOnce()
    {
        var input = new TextBox { Value = "stored draft", Height = 80, MinWidth = 0 }; var action = new SizedElement(30, 26);
        var row = new PropertyEditorRow(input, action, "Value") { Spacing = 4 };
        var resources = new[] { new DisposalCounter(), new DisposalCounter() }; input.Own(resources[0]); action.Own(resources[1]);
        row.Measure(new(500, 200), new FixedTextLayout()); Assert.Equal(80, row.DesiredSize.Height);
        var tree = CompactControlHarness.Tree(row).ToArray(); row.ShowEmpty(true);
        row.Measure(new(500, 200), new FixedTextLayout()); row.Arrange(new(0, 0, 500, row.DesiredSize.Height));
        Assert.Equal(26, row.DesiredSize.Height); Assert.Equal(default, input.Bounds); Assert.True(row.Empty.IsVisible); Assert.False(input.IsVisible);
        Assert.Equal("Не задано", row.Empty.Value); Assert.Equal("stored draft", input.Value); Assert.Equal(tree, CompactControlHarness.Tree(row));
        row.ShowEmpty(false); row.Measure(new(500, 200), new FixedTextLayout()); Assert.Equal(80, row.DesiredSize.Height);
        Assert.True(input.IsVisible); Assert.False(row.Empty.IsVisible); row.Dispose(); row.Dispose();
        Assert.All(tree, child => Assert.True(child.IsDisposed)); Assert.All(resources, resource => Assert.Equal(1, resource.Count));
    }

    [Fact]
    public void Constructor_ValidatesEveryIncomingPartBeforeTransferringOwnership()
    {
        using var input = new SizedElement(); using var foreignParent = new Panel(); var foreign = new SizedElement(); foreignParent.Add(foreign);
        Assert.Throws<ArgumentNullException>(() => new PropertyEditorRow(null!));
        Assert.Throws<ArgumentNullException>(() => new PropertyEditorRow(input, label: null!));
        Assert.Throws<ArgumentException>(() => new PropertyEditorRow(input, input));
        Assert.Throws<InvalidOperationException>(() => new PropertyEditorRow(input, foreign));
        Assert.Null(input.Parent); Assert.False(input.IsDisposed); Assert.Same(foreignParent, foreign.Parent); Assert.False(foreign.IsDisposed);
        var disposed = new SizedElement(); disposed.Dispose();
        Assert.Throws<ObjectDisposedException>(() => new PropertyEditorRow(input, disposed)); Assert.Null(input.Parent);
        var attached = new SizedElement(); using var ui = CompactControlHarness.Ui(attached);
        Assert.Throws<InvalidOperationException>(() => new PropertyEditorRow(input, attached)); Assert.Null(input.Parent); Assert.Same(attached, ui.Root);
    }

    [Theory]
    [InlineData(-1f)] [InlineData(-2f)] [InlineData(float.PositiveInfinity)] [InlineData(float.NegativeInfinity)]
    public void InvalidDimensions_RejectLocalAndStyledValuesBeforeInvalidating(float invalid)
    {
        using var row = new PropertyEditorRow(new SizedElement()); var changes = 0; row.Invalidated += _ => changes++;
        Assert.Throws<ArgumentOutOfRangeException>(() => row.LabelWidth = invalid);
        Assert.Throws<ArgumentOutOfRangeException>(() => row.MinimumValueWidth = invalid);
        Assert.Throws<ArgumentOutOfRangeException>(() => row.ValueWidth = invalid);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Style().With(PropertyEditorRow.ValueWidthProperty, invalid));
        Assert.Equal(88, row.LabelWidth); Assert.Equal(80, row.MinimumValueWidth); Assert.True(float.IsNaN(row.ValueWidth)); Assert.Equal(0, changes);
    }

    [Fact]
    public void ZeroAndNaNPartitions_AllowCollapsedLabelAndFillButKeepMinimumValuePositive()
    {
        using var row = new PropertyEditorRow(new SizedElement()) { LabelWidth = 0, ValueWidth = float.NaN };
        Assert.Throws<ArgumentOutOfRangeException>(() => row.MinimumValueWidth = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => row.MinimumValueWidth = float.NaN);
        Assert.Throws<ArgumentOutOfRangeException>(() => row.LabelWidth = float.NaN);
        row.Measure(new(200, 40), new FixedTextLayout()); row.Arrange(new(0, 0, 200, 40));
        Assert.Equal(0, row.Label.Bounds.Width); Assert.Equal(200, row.Editor.Bounds.Width); Assert.Equal(80, row.MinimumValueWidth);
    }
}
