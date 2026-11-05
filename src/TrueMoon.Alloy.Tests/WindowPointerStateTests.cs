using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class WindowPointerStateTests
{
    [Fact]
    public void PressSnapshotRetainsOrderedPositionAndModifiersAcrossLaterMovement()
    {
        var pointer = new WindowPointerState(14, 19);
        var press = pointer.Create(InputKind.PointerDown, button: 1, shift: true);
        pointer.Move(150, 220);
        var release = pointer.Create(InputKind.PointerUp, button: 1, control: true);
        Assert.Equal(new UiInput(InputKind.PointerDown, 14, 19, Button: 1, Shift: true), press);
        Assert.Equal(new UiInput(InputKind.PointerUp, 150, 220, Button: 1, Control: true), release);
    }

    [Fact]
    public void WheelUsesLastOrderedCursorEventAndPreservesItsDelta()
    {
        var pointer = new WindowPointerState(500, 600); pointer.Move(3.5f, 7.25f);
        var wheel = pointer.Create(InputKind.Wheel, wheel: -2.5f, shift: true, control: true);
        pointer.Move(-10, -20);
        Assert.Equal(new UiInput(InputKind.Wheel, 3.5f, 7.25f, WheelDelta: -2.5f, Shift: true, Control: true), wheel);
    }

    [Theory]
    [InlineData(false, false, "1.7")]
    [InlineData(true, false, "3.5")]
    [InlineData(false, true, "1.52")]
    [InlineData(true, true, "1.7")]
    public void BufferedDragStartsOnPrefixAndRetainsPressModifiers(bool shift, bool control, string expected)
    {
        var box = new NumberBox("X") { Width = 180, Height = 30, AdjustmentEnabled = true };
        box.Editor.Value = "001.50";
        using var ui = CompactControlHarness.Ui(box, 240, 60); ui.Update();
        var bounds = box.DragBounds;
        var pointer = new WindowPointerState(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
        // Buffer all events before routing; the press must not read the final cursor position.
        var press = pointer.Create(InputKind.PointerDown, shift: shift, control: control);
        pointer.Move(press.X + 8, press.Y);
        var move = pointer.Create(InputKind.PointerMove);
        var release = pointer.Create(InputKind.PointerUp);
        Assert.True(ui.HandleInput(press).PointerCaptured);
        Assert.True(box.IsDragging); Assert.True(ui.HandleInput(move).PointerCaptured);
        Assert.Equal(expected, box.Editor.Value);
        Assert.False(ui.HandleInput(release).PointerCaptured); Assert.False(box.IsDragging);
        Assert.Equal(expected, box.Editor.Value);
    }
}
