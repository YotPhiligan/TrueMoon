using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class Vector3EditorTests
{
    [Theory]
    [InlineData(291f, true)] [InlineData(292f, false)] [InlineData(293f, false)] [InlineData(600f, false)]
    public void Layout_ExactBreakpointChangesLabelPlacementAndKeepsThreeEqualColumns(float width, bool stacked)
    {
        using var row = new Vector3Editor("Position") { Spacing = 4 };
        row.FirstCell.Add(new Text("diagnostic") { Height = 20 });
        row.Measure(new(width, 200), new FixedTextLayout()); row.Arrange(new(10, 20, width, row.DesiredSize.Height));
        var cells = new[] { row.FirstCell, row.SecondCell, row.ThirdCell };
        var firstX = stacked ? 10 : 102; var firstY = stacked ? 40 : 20;
        var expectedWidth = (width - (stacked ? 8 : 100)) / 3;
        Assert.Equal(stacked ? 72 : 52, row.DesiredSize.Height);
        Assert.Equal(new Rect(10, stacked ? 20 : 28, stacked ? width : 88, 16), row.Label.Bounds);
        for (var index = 0; index < cells.Length; index++)
        {
            Assert.Equal(firstX + (expectedWidth + 4) * index, cells[index].Bounds.X, 3);
            Assert.Equal(firstY, cells[index].Bounds.Y); Assert.Equal(expectedWidth, cells[index].Bounds.Width, 3);
        }
        Assert.Equal(10 + width, row.ThirdCell.Bounds.X + row.ThirdCell.Bounds.Width, 3);
        Assert.Equal(new[] { EditorColorRole.AxisX, EditorColorRole.AxisY, EditorColorRole.AxisZ }, new[] { row.First.ColorRole, row.Second.ColorRole, row.Third.ColorRole });
        Assert.Equal(new[] { "X", "Y", "Z" }, new[] { row.First.Prefix.Value, row.Second.Prefix.Value, row.Third.Prefix.Value });
    }

    [Theory]
    [InlineData(0f)] [InlineData(1f)] [InlineData(7f)]
    public void Layout_TinyWidthNeverPlacesCellsOutsideAvailableSlot(float width)
    {
        using var row = new Vector3Editor("Position") { Spacing = 4 };
        row.Measure(new(width, 90), new FixedTextLayout()); row.Arrange(new(5, 9, width, 90));
        Assert.All(CompactControlHarness.Tree(row), child =>
        {
            Assert.True(float.IsFinite(child.Bounds.Width)); Assert.InRange(child.Bounds.Width, 0, width);
            Assert.InRange(child.Bounds.X, 5, 5 + width); Assert.True(child.Bounds.X + child.Bounds.Width <= 5 + width + .001f);
        });
        Assert.Equal("Position", row.Label.Value);
    }

    [Fact]
    public void ResizeAndTheme_PreserveCellsDraftFocusSelectionAndConsumerDiagnosticsUntilOwnedDisposal()
    {
        var row = new Vector3Editor("Position") { Spacing = 4 }; var hint = new Text("consumer diagnostic"); row.SecondCell.Add(hint);
        using var ui = CompactControlHarness.Ui(row, 600); ui.Update(); ui.Focus(row.Second.Editor);
        ui.HandleInput(new(InputKind.Text, Text: "1e-")); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Left, Shift: true));
        var tree = CompactControlHarness.Tree(row).ToArray();
        ui.Resize(new(200, 120)); ui.SetTheme(Theme.Light); ui.Update();
        Assert.Equal(tree, CompactControlHarness.Tree(row)); Assert.Equal("1e-", row.Second.Editor.Value);
        Assert.True(row.Second.Editor.IsFocused); Assert.Equal(2, row.Second.Editor.SelectionStart); Assert.Equal(1, row.Second.Editor.SelectionLength);
        Assert.Same(hint, row.SecondCell.Children[1]); Assert.Same(row.Second, row.SecondCell.Children[0]);
        Assert.True(row.Label.Bounds.Y + row.Label.Bounds.Height < row.FirstCell.Bounds.Y);
        Assert.Equal(Theme.Light.AxisY, row.Second.Prefix.Foreground);
        ui.Resize(new(600, 120)); ui.Update();
        Assert.Equal(row.First.Bounds.Y + row.First.Bounds.Height / 2, row.Label.Bounds.Y + row.Label.Bounds.Height / 2);
        ui.Dispose(); Assert.All(tree, child => Assert.True(child.IsDisposed));
    }

    [Theory]
    [InlineData(-1f)] [InlineData(float.NaN)] [InlineData(float.PositiveInfinity)] [InlineData(float.NegativeInfinity)]
    public void InvalidMetrics_RejectLocalAndStyleBeforeInvalidation(float invalid)
    {
        using var row = new Vector3Editor(); var invalidations = 0; row.Invalidated += _ => invalidations++;
        Assert.Throws<ArgumentOutOfRangeException>(() => row.LabelWidth = invalid);
        Assert.Throws<ArgumentOutOfRangeException>(() => row.ColumnWidth = invalid);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Style().With(Vector3Editor.LabelWidthProperty, invalid));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Style().With(Vector3Editor.ColumnWidthProperty, invalid));
        Assert.Equal(88, row.LabelWidth); Assert.Equal(64, row.ColumnWidth); Assert.Equal(0, invalidations);
    }

    [Fact]
    public void MetricZeroPartitions_LabelMayCollapseButColumnsRemainPositiveAndNullLabelsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new Vector3Editor(null!));
        using var row = new Vector3Editor("Label") { LabelWidth = 0, ColumnWidth = .5f, Spacing = 1 };
        row.Measure(new(10, 80), new FixedTextLayout()); row.Arrange(new(0, 0, 10, 80));
        Assert.Equal(0, row.Label.Bounds.Width); Assert.Equal(1, row.FirstCell.Bounds.X); Assert.Equal(7f / 3, row.FirstCell.Bounds.Width, 4);
        Assert.Throws<ArgumentOutOfRangeException>(() => row.ColumnWidth = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Style().With(Vector3Editor.ColumnWidthProperty, 0f));
        Assert.Equal(.5f, row.ColumnWidth);
    }
}
