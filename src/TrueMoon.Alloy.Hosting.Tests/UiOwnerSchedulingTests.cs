using Xunit;

namespace TrueMoon.Alloy.Hosting.Tests;

public sealed class UiOwnerSchedulingTests
{
    [Fact]
    public async Task Wake_InterruptsParticipantWaitAndStopDrainsOnTheSameOwner()
    {
        var loop = new UiOwnerLoop(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var close = 0; var owner = 0; var cleanupOwner = 0;
        try
        {
            loop.Schedule(() => owner = Environment.CurrentManagedThreadId, () => { entered.TrySetResult(); return Volatile.Read(ref close) != 0; },
                () => cleanupOwner = Environment.CurrentManagedThreadId, () => false, ready, done, waitMilliseconds: () => 60000);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Volatile.Write(ref close, 1); loop.Wake(); await done.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(owner, cleanupOwner); Assert.NotEqual(0, owner);
        }
        finally { await loop.StopAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
    }

    [Fact]
    public async Task DelayFailure_FaultsOnlyItsParticipantAndPreservesCleanup()
    {
        var loop = new UiOwnerLoop(); var cause = new Exception("delay"); var cleanups = 0;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            loop.Schedule(() => { }, () => false, () => cleanups++, () => false, ready, done, waitMilliseconds: () => throw cause);
            Assert.Same(cause, await Record.ExceptionAsync(() => done.Task.WaitAsync(TimeSpan.FromSeconds(5))));
            var sibling = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            loop.Schedule(() => { }, () => true, () => cleanups++, () => false, new TaskCompletionSource(), sibling);
            await sibling.Task.WaitAsync(TimeSpan.FromSeconds(5)); Assert.Equal(2, cleanups);
        }
        finally { await loop.StopAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
    }
}
