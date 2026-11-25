using TrueMoon.Aether.Input;
using TrueMoon.Argentis.Input;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Player.Tests;

public sealed class PlayerInputTests
{
    [Fact]
    public void Capture_HoldsMovement_ConsumesLookAndAction_RejectsRepeatUntilKeyUp()
    {
        var input = new PlayerInput(); Assert.False(input.Key(UiKey.W, true));
        input.Capture(10, 20); input.Key(UiKey.W, true); input.Key(UiKey.D, true);
        input.Move(110, -80); input.Key(UiKey.E, true); input.Key(UiKey.E, true);
        var first = input.Take(); first.Validate();
        Assert.True(first.Captured); Assert.Equal(GameButtons.Forward | GameButtons.Right, first.Buttons);
        Assert.Equal(.3f, first.Yaw, 5); Assert.Equal(-.3f, first.Pitch, 5); Assert.True(first.InteractPressed);
        var next = input.Take(); Assert.Equal(first.Buttons, next.Buttons); Assert.Equal(0, next.Yaw); Assert.Equal(0, next.Pitch); Assert.False(next.InteractPressed);
        input.Key(UiKey.E, true); Assert.False(input.Take().InteractPressed);
        input.Key(UiKey.E, false); input.Key(UiKey.E, true); Assert.True(input.Take().InteractPressed);
        input.Key(UiKey.W, false); Assert.Equal(GameButtons.Right, input.Take().Buttons);
    }
    [Fact]
    public void ReleaseAndFreshCapture_ClearAllHeldAndTransientState_AndLookIsBounded()
    {
        var input = new PlayerInput(); input.Capture(0, 0); input.Key(UiKey.S, true); input.Key(UiKey.E, true);
        input.Move(float.MaxValue, -float.MaxValue); var bounded = input.Take();
        Assert.Equal(.5f, bounded.Yaw); Assert.Equal(-.5f, bounded.Pitch);
        input.Release(); Assert.Equal(default, input.Take()); Assert.False(input.IsCaptured);
        input.Capture(1, 2); Assert.Equal(new GameInputState(true), input.Take());
        input.Key(UiKey.A, true); input.Capture(5, 6); Assert.Equal(GameButtons.None, input.Take().Buttons);
        Assert.False(input.Key(UiKey.Escape, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => input.Capture(float.NaN, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => input.Move(0, float.PositiveInfinity));
    }
    [Theory]
    [InlineData(0,0,0,0)][InlineData(15,100,0,0)][InlineData(16,16,16,16)]
    [InlineData(1920,1080,1920,1080)][InlineData(3840,2160,1920,1080)]
    [InlineData(1920,2160,960,1080)][InlineData(int.MaxValue,16,0,0)]
    public void PhysicalExtent_PreservesAspect_AndSuspendsOutsideProfile(int width,int height,int expectedWidth,int expectedHeight)
        => Assert.Equal((expectedWidth,expectedHeight),PlayerWindow.FitExtent(width,height));
    [Fact]
    public void NegativeExtent_IsRejected() => Assert.Throws<ArgumentOutOfRangeException>(()=>PlayerWindow.FitExtent(1,-1));
    [Fact]
    public void ViewStyleAndFocusLoss_PreserveTypedData_AndReleaseInput()
    {
        var input=new PlayerInput(); var data=new PlayerViewModel("captured");
        using var view=new PlayerView(data,input,new PlayerViewStyle());
        Assert.Same(data, view.DataContext);
        Assert.Same(view.Canvas, view.Child);
        Assert.Same(view, view.Canvas.Parent);
        input.Capture(0,0); input.Key(UiKey.W,true); view.Canvas.OnInputCancelled();
        Assert.False(input.IsCaptured); Assert.Equal(GameButtons.None,input.Take().Buttons);
        Assert.True(view.Canvas.Focusable); Assert.False(view.Canvas.IsDisposed);
    }
}
