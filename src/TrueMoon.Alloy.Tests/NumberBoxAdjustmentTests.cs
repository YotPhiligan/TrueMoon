using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class NumberBoxAdjustmentTests
{
    [Theory]
    [InlineData(false, false, UiKey.Up, "1.6")] [InlineData(false, false, UiKey.Down, "1.4")]
    [InlineData(true, false, UiKey.Up, "2.5")] [InlineData(false, true, UiKey.Up, "1.51")]
    [InlineData(true, true, UiKey.Up, "1.6")]
    public void FocusedTextBox_ArrowBubblesToExactDecimalStepWithModifiers(bool shift, bool control, UiKey key, string expected)
    {
        var box = new NumberBox { AdjustmentEnabled = true }; box.Editor.Value = "1.5";
        using var ui = CompactControlHarness.Ui(box); ui.Update(); ui.Focus(box.Editor);
        var changes = 0; box.Editor.PropertyChanged += (_, property) => { if (ReferenceEquals(property, TextBox.ValueProperty)) changes++; };
        var result = ui.HandleInput(new(InputKind.KeyDown, Key: key, Shift: shift, Control: control));
        Assert.True(result.Handled); Assert.False(result.PointerCaptured); Assert.Equal(expected, box.Editor.Value);
        Assert.True(box.Editor.IsFocused); Assert.Equal(1, changes); Assert.False(box.IsDragging);
    }

    [Theory]
    [InlineData("9007199254740993", 1, true, "9007199254740994")]
    [InlineData("9223372036854775806", 1, true, "9223372036854775807")]
    [InlineData("9223372036854775807", 1, false, "9223372036854775807")]
    [InlineData("-9223372036854775807", -1, true, "-9223372036854775808")]
    [InlineData("-9223372036854775808", -1, false, "-9223372036854775808")]
    [InlineData("9223372036854775808", -1, false, "9223372036854775808")]
    public void IntegerArithmetic_PreservesInt64PrecisionAndRejectsOverflowWithoutClamping(string value, int direction, bool accepted, string expected)
    {
        using var box = new NumberBox { AdjustmentEnabled = true, NumericKind = NumericValueKind.Integer }; box.Editor.Value = value;
        Assert.Equal(accepted, box.TryStep(direction)); Assert.Equal(expected, box.Editor.Value); Assert.False(box.IsDragging);
    }

    [Fact]
    public void IntegerModifiersAndFiniteBounds_DoNotRoundCandidatesThroughDouble()
    {
        using var box = new NumberBox { AdjustmentEnabled = true, NumericKind = NumericValueKind.Integer }; box.Editor.Value = "10";
        Assert.True(box.TryStep(1, shift: true)); Assert.Equal("20", box.Editor.Value);
        Assert.True(box.TryStep(-1, control: true)); Assert.Equal("19", box.Editor.Value);
        Assert.True(box.TryStep(1, shift: true, control: true)); Assert.Equal("20", box.Editor.Value);
        box.Editor.Value = "9007199254740992"; box.Maximum = 9007199254740992d;
        Assert.False(box.TryStep(1)); Assert.Equal("9007199254740992", box.Editor.Value);
        box.Maximum = double.PositiveInfinity; box.Minimum = -9007199254740992d; box.Editor.Value = "-9007199254740992";
        Assert.False(box.TryStep(-1)); Assert.Equal("-9007199254740992", box.Editor.Value);
        box.Minimum = 1.5; box.Maximum = 3.5; box.Editor.Value = "1";
        Assert.True(box.TryStep(1)); Assert.Equal("2", box.Editor.Value); Assert.False(box.TryStep(-1)); Assert.Equal("2", box.Editor.Value);
        box.Editor.Value = "4"; Assert.True(box.TryStep(-1)); Assert.Equal("3", box.Editor.Value);
    }

    [Theory]
    [InlineData("")] [InlineData("-")] [InlineData("1e-")] [InlineData("NaN")] [InlineData("Infinity")] [InlineData("1,5")]
    public void InvalidRealDraft_IsNeverCoercedOrPublished(string value)
    {
        var box = new NumberBox("X") { AdjustmentEnabled = true }; box.Editor.Value = value; var changes = 0; var starts = 0;
        box.Editor.PropertyChanged += (_, _) => changes++;
        Assert.False(box.TryStep(1)); Assert.False(box.TryStep(-1)); Assert.Equal(value, box.Editor.Value); Assert.Equal(0, changes);
        box.GestureStarted += () => starts++; using var ui = CompactControlHarness.Ui(box, 200, 40); ui.Update();
        var bounds = box.DragBounds;
        Assert.False(ui.HandleInput(new(InputKind.PointerDown, bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2)).PointerCaptured);
        Assert.False(box.IsDragging); Assert.Equal(value, box.Editor.Value); Assert.Equal(0, starts); Assert.Equal(0, changes);
    }

    [Fact]
    public void RealBoundsAndOverflow_RejectWholeCandidateAndKeepLastAcceptedValue()
    {
        using var box = new NumberBox { AdjustmentEnabled = true, Step = 1, Minimum = 0, Maximum = 2 }; box.Editor.Value = "1";
        Assert.True(box.TryStep(1)); Assert.Equal("2", box.Editor.Value); Assert.False(box.TryStep(1)); Assert.Equal("2", box.Editor.Value);
        box.Editor.Value = "1.5"; Assert.False(box.TryStep(1)); Assert.Equal("1.5", box.Editor.Value);
        Assert.True(box.TryStep(-1)); Assert.Equal("0.5", box.Editor.Value);
        box.Minimum = double.NegativeInfinity; box.Maximum = double.PositiveInfinity; box.Step = double.MaxValue; box.Editor.Value = "0";
        Assert.True(box.TryStep(1)); var maximum = box.Editor.Value; Assert.Equal(double.MaxValue, double.Parse(maximum, System.Globalization.CultureInfo.InvariantCulture));
        Assert.False(box.TryStep(1)); Assert.Equal(maximum, box.Editor.Value);
        box.Minimum = 5; box.Maximum = 4; Assert.False(box.TryStep(-1)); Assert.Equal(maximum, box.Editor.Value);
    }

    [Fact]
    public void Adjustment_IsOptInAndRespectsReadOnlyDisabledAndConsumerAdmissionWithVisibleMarker()
    {
        var box = new NumberBox(); box.Editor.Value = "001.5"; using var ui = CompactControlHarness.Ui(box, 200, 40); ui.Update();
        Assert.False(box.TryStep(1)); Assert.Equal(0, box.DragBounds.Width); Assert.Equal(200, box.Editor.Bounds.Width);
        box.AdjustmentEnabled = true; ui.Update(); Assert.Equal(14, box.DragBounds.Width); Assert.Equal(186, box.Editor.Bounds.Width);
        var drawing = new OutlineDrawing(); box.Draw(drawing); Assert.Contains(drawing.Fills, item => item.Bounds == new Rect(3, 20, 8, 1));
        box.Editor.IsReadOnly = true; Assert.False(box.TryStep(1)); box.Editor.IsReadOnly = false;
        box.IsEnabled = false; Assert.False(box.TryStep(1)); box.IsEnabled = true;
        box.Editor.IsEnabled = false; Assert.False(box.TryStep(1)); box.Editor.IsEnabled = true;
        box.CanAdjust = () => false; Assert.False(box.TryStep(1)); Assert.Equal("001.5", box.Editor.Value);
        box.CanAdjust = () => true; Assert.True(box.TryStep(1)); Assert.Equal("1.6", box.Editor.Value);
    }

    [Fact]
    public void InvalidSettingsAndDirection_RejectBeforeStateOrInvalidationChanges()
    {
        using var box = new NumberBox(); var changes = 0; box.Invalidated += _ => changes++;
        foreach (var step in new[] { 0d, -1, double.NaN, double.PositiveInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => box.Step = step);
            Assert.Throws<ArgumentOutOfRangeException>(() => new Style().With(NumberBox.StepProperty, step));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => box.Minimum = double.NaN);
        Assert.Throws<ArgumentOutOfRangeException>(() => box.Maximum = double.NaN);
        Assert.Throws<ArgumentOutOfRangeException>(() => box.NumericKind = (NumericValueKind)99);
        Assert.Throws<ArgumentOutOfRangeException>(() => box.TryStep(0)); Assert.Throws<ArgumentOutOfRangeException>(() => box.TryStep(2));
        Assert.Equal(.1, box.Step); Assert.Equal(double.NegativeInfinity, box.Minimum); Assert.Equal(double.PositiveInfinity, box.Maximum);
        Assert.Equal(0, changes); Assert.Equal("", box.Editor.Value);
    }
}
