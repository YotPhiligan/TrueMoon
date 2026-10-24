using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class ScalarTransitionTests
{
    [Fact]
    public void Reversal_StartsAtTheCurrentValueAndEndsWithoutOvershoot()
    {
        var transition = new ScalarTransition();
        transition.MoveTo(1, TimeSpan.FromMilliseconds(180));
        Assert.True(transition.Advance(TimeSpan.FromMilliseconds(90)));
        Assert.Equal(.5f, transition.Value, 5);
        transition.MoveTo(0, TimeSpan.FromMilliseconds(180));
        Assert.Equal(.5f, transition.Value, 5);
        transition.Advance(TimeSpan.FromMilliseconds(90));
        Assert.Equal(.25f, transition.Value, 5);
        transition.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(0, transition.Value);
        Assert.False(transition.IsRunning);
        Assert.False(transition.Advance(TimeSpan.FromMilliseconds(1)));
    }

    [Fact]
    public void RepeatedTarget_DoesNotRestartAnActiveTransition()
    {
        var transition = new ScalarTransition();
        transition.MoveTo(10, TimeSpan.FromSeconds(1));
        transition.Advance(TimeSpan.FromMilliseconds(500));
        transition.MoveTo(10, TimeSpan.FromSeconds(1));
        transition.Advance(TimeSpan.FromMilliseconds(500));
        Assert.Equal(10, transition.Value);
        Assert.False(transition.IsRunning);
    }

    [Fact]
    public void ZeroDurationAndComplete_SettleExactlyAndDoNotNeedFrames()
    {
        var transition = new ScalarTransition();
        Assert.Equal(0, transition.Value);
        Assert.False(transition.IsRunning);
        transition.MoveTo(-2, TimeSpan.Zero);
        Assert.Equal(-2, transition.Value);
        Assert.False(transition.IsRunning);
        transition.MoveTo(3, TimeSpan.FromSeconds(1));
        Assert.False(transition.Advance(TimeSpan.Zero));
        Assert.Equal(-2, transition.Value);
        transition.Complete();
        Assert.Equal(3, transition.Value);
        Assert.False(transition.IsRunning);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void NonFiniteTargets_DoNotChangeAnExistingTransition(float value)
    {
        var transition = new ScalarTransition();
        transition.MoveTo(1, TimeSpan.FromSeconds(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => transition.MoveTo(value, TimeSpan.Zero));
        transition.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(1, transition.Value);
    }

    [Fact]
    public void NegativeTime_IsRejectedWithoutAdvancing()
    {
        var transition = new ScalarTransition();
        Assert.Throws<ArgumentOutOfRangeException>(() => transition.MoveTo(1, TimeSpan.FromTicks(-1)));
        transition.MoveTo(1, TimeSpan.FromSeconds(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => transition.Advance(TimeSpan.FromTicks(-1)));
        Assert.Equal(0, transition.Value);
        Assert.True(transition.IsRunning);
    }
}
