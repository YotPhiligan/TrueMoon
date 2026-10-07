using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class PropertyNotificationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SetOrClear_ThrowingInvalidationAndObserver_StillNotifiesLaterObserver(bool clear)
    {
        using var element = new ProbeElement { Width = 20 };
        var later = 0;
        element.Invalidated += _ => throw new InvalidationException();
        element.PropertyChanged += (_, _) => throw new ObserverException();
        element.PropertyChanged += (_, property) =>
        {
            Assert.Same(UiProperties.Width, property);
            Assert.Equal(clear ? float.NaN : 50, element.Width);
            Assert.Equal(2, element.Hooks);
            later++;
        };
        var error = Assert.Throws<AggregateException>(() =>
        {
            if (clear) element.Clear(UiProperties.Width);
            else element.Set(UiProperties.Width, 50);
        });
        Assert.Collection(error.InnerExceptions, item => Assert.IsType<InvalidationException>(item), item => Assert.IsType<ObserverException>(item));
        Assert.Equal(1, later);
        Assert.Equal(2, element.Hooks);
    }

    [Fact]
    public void InvariantHookFailure_StillInvalidatesAndNotifiesAfterCommit()
    {
        using var element = new ProbeElement { ThrowInHook = true };
        var invalidations = 0;
        var notifications = 0;
        element.Invalidated += _ => invalidations++;
        element.PropertyChanged += (_, _) => notifications++;
        Assert.Throws<HookException>(() => element.Width = 60);
        Assert.Equal(60, element.Width);
        Assert.Equal(1, invalidations);
        Assert.Equal(1, notifications);
        Assert.Equal(1, element.Hooks);
    }

    private sealed class InvalidationException : Exception;
    private sealed class ObserverException : Exception;
    private sealed class HookException : Exception;
    private sealed class ProbeElement : Element
    {
        public int Hooks { get; private set; }
        public bool ThrowInHook { get; init; }
        protected override void OnPropertyChanged(object property)
        {
            Hooks++;
            if (ThrowInHook) throw new HookException();
        }
    }
}
