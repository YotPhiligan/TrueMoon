using Xunit;

namespace TrueMoon.Alloy.Hosting.Tests;

public sealed class UiCloseCoordinatorTests
{
    private static TaskCompletionSource<bool> Gate() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task WorkerRequests_ShareAttemptWithoutInvokingHandlersOnWorkers()
    {
        var close = new UiCloseCoordinator(() => { }); var requests = new Task<bool>[16];
        await Task.WhenAll(Enumerable.Range(0, requests.Length).Select(index => Task.Run(() => { requests[index] = close.Request(UiCloseReason.Application); })));
        Assert.All(requests, request => { Assert.Same(requests[0], request); Assert.False(request.IsCompleted); });
        Assert.True(close.Poll(() => [])); Assert.All(requests, request => Assert.True(request.Result));
    }

    [Fact]
    public async Task NullHandlerTask_FaultsAttemptAndKeepsOpen()
    {
        var close = new UiCloseCoordinator(() => { }); var request = close.Request(UiCloseReason.Application);
        Assert.False(close.Poll(() => [_ => null!]));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => request);
        Assert.Contains("returned null", error.Message); Assert.Same(error, close.LastError); Assert.False(close.IsRequested);
    }

    [Fact]
    public async Task ConcurrentRequests_JoinAndDrainSequentialOwnerCallbacks()
    {
        var wakes = 0; var close = new UiCloseCoordinator(() => Interlocked.Increment(ref wakes));
        var save = Gate(); var drain = Gate(); var calls = new List<string>(); var owner = Environment.CurrentManagedThreadId;
        Func<UiCloseReason, Task<bool>>[] handlers =
        [reason => { Assert.Equal(owner, Environment.CurrentManagedThreadId); Assert.Equal(UiCloseReason.Native, reason); calls.Add("save"); return save.Task; },
         _ => { Assert.Equal(owner, Environment.CurrentManagedThreadId); calls.Add("drain"); return drain.Task; }];
        var first = close.Request(UiCloseReason.Native);
        Assert.Same(first, close.Request(UiCloseReason.Application));
        Assert.False(close.Poll(() => handlers)); Assert.False(first.IsCompleted); Assert.Equal(["save"], calls);
        Assert.False(close.Poll(() => throw new Exception("Snapshot repeated"))); Assert.Single(calls);
        save.SetResult(true);
        Assert.False(close.Poll(() => handlers)); Assert.Equal(["save", "drain"], calls); Assert.False(first.IsCompleted);
        drain.SetResult(true); Assert.True(close.Poll(() => handlers)); Assert.True(await first); Assert.True(wakes >= 2);
        close.Finish(); Assert.False(close.IsRequested); Assert.True(await close.Request(UiCloseReason.Application));
    }

    [Fact]
    public async Task Veto_KeepsOpenAndFreshAttemptCanApprove()
    {
        var close = new UiCloseCoordinator(() => { }); var later = 0;
        var first = close.Request(UiCloseReason.Application);
        Assert.False(close.Poll(() => [_ => Task.FromResult(false), _ => { later++; return Task.FromResult(true); }]));
        Assert.False(await first); Assert.False(close.IsRequested); Assert.Null(close.LastError); Assert.Equal(0, later);
        var retry = close.Request(UiCloseReason.Native); Assert.NotSame(first, retry);
        Assert.True(close.Poll(() => [])); Assert.True(await retry);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveFailure_IsObservableAndRetryable(bool synchronous)
    {
        var cause = new IOException("save"); var close = new UiCloseCoordinator(() => { });
        var first = close.Request(UiCloseReason.Native);
        Assert.False(close.Poll(() => [_ => synchronous ? throw cause : Task.FromException<bool>(cause)]));
        Assert.Same(cause, await Record.ExceptionAsync(() => first)); Assert.Same(cause, close.LastError); Assert.False(close.IsRequested);
        var retry = close.Request(UiCloseReason.Application); Assert.Null(close.LastError);
        Assert.True(close.Poll(() => [])); Assert.True(await retry);
    }

    [Fact]
    public async Task ForcedUpgrade_DrainsPendingHandlerAndIgnoresVeto()
    {
        var close = new UiCloseCoordinator(() => { }); var save = Gate(); var reasons = new List<UiCloseReason>();
        Func<UiCloseReason, Task<bool>>[] handlers =
        [reason => { reasons.Add(reason); return save.Task; }, reason => { reasons.Add(reason); return Task.FromResult(false); }];
        var first = close.Request(UiCloseReason.Application); Assert.False(close.Poll(() => handlers));
        Assert.Same(first, close.Request(UiCloseReason.Shutdown)); Assert.False(close.Poll(() => handlers)); Assert.False(first.IsCompleted);
        save.SetResult(false); Assert.True(close.Poll(() => handlers)); Assert.True(await first);
        Assert.Equal([UiCloseReason.Application, UiCloseReason.Shutdown], reasons);
    }

    [Fact]
    public async Task ForcedFailures_PreserveEveryErrorAfterRemainingDrain()
    {
        var close = new UiCloseCoordinator(() => { }); var drain = Gate(); var first = new Exception("save"); var second = new Exception("drain");
        var called = 0; var request = close.Request(UiCloseReason.Shutdown);
        Func<UiCloseReason, Task<bool>>[] handlers =
        [_ => throw first, _ => { called++; return drain.Task; }, _ => Task.FromException<bool>(second)];
        Assert.False(close.Poll(() => handlers)); Assert.Equal(1, called); Assert.False(request.IsCompleted);
        drain.SetResult(true);
        var failure = Assert.Throws<AggregateException>(() => close.Poll(() => handlers));
        Assert.Equal([first, second], failure.InnerExceptions); Assert.Same(failure, close.LastError);
        Assert.True(await request); // This internal approval still requires the owner's faulted Completion publicly.
    }

    [Fact]
    public async Task CanceledHandler_CancelsUserAttemptButForcedShutdownContinues()
    {
        var close = new UiCloseCoordinator(() => { }); var canceled = Task.FromCanceled<bool>(new CancellationToken(true));
        var request = close.Request(UiCloseReason.Application);
        Assert.False(close.Poll(() => [_ => canceled]));
        Assert.IsAssignableFrom<OperationCanceledException>(await Record.ExceptionAsync(() => request)); Assert.False(close.IsRequested);
        _ = close.Request(UiCloseReason.Shutdown);
        Assert.ThrowsAny<OperationCanceledException>(() => close.Poll(() => [_ => canceled])); Assert.NotNull(close.LastError);
    }

    [Fact]
    public async Task WaitCancellation_DoesNotCancelSharedAttempt()
    {
        var close = new UiCloseCoordinator(() => { }); var request = close.Request(UiCloseReason.Application);
        using var cancellation = new CancellationTokenSource(); var wait = request.WaitAsync(cancellation.Token); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait); Assert.False(request.IsCompleted); Assert.True(close.IsRequested);
        Assert.True(close.Poll(() => [])); Assert.True(await request);
    }

    [Fact]
    public async Task HandlerBatch_IsBoundedAndSnapshotIsStable()
    {
        var close = new UiCloseCoordinator(() => { }); var calls = 0;
        var handlers = Enumerable.Range(0, 80).Select<int, Func<UiCloseReason, Task<bool>>>(_ => _ => { calls++; return Task.FromResult(true); }).ToArray();
        var request = close.Request(UiCloseReason.Application);
        Assert.False(close.Poll(() => handlers)); Assert.InRange(calls, 1, 64); Assert.False(request.IsCompleted);
        Assert.True(close.Poll(() => throw new Exception("Snapshot repeated"))); Assert.Equal(80, calls); Assert.True(await request);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnerLoop_StaysResponsiveAndCompletesOnlyAfterCleanup(bool cleanupFails)
    {
        var loop = new UiOwnerLoop(); var close = new UiCloseCoordinator(loop.Wake); var drain = Gate();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sibling = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = 0; var cleanupOwner = 0; var cause = new Exception("cleanup");
        var request = close.Request(UiCloseReason.Native);
        try
        {
            loop.Schedule(() => owner = Environment.CurrentManagedThreadId,
                () => close.Poll(() => [_ => { Assert.Equal(owner, Environment.CurrentManagedThreadId); entered.TrySetResult(); return drain.Task; }]),
                () => { cleanupOwner = Environment.CurrentManagedThreadId; close.Finish(); if (cleanupFails) throw cause; },
                () => false, new TaskCompletionSource(), done, waitMilliseconds: () => 60000);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            loop.Schedule(() => { }, () => true, () => sibling.TrySetResult(), () => false, new TaskCompletionSource(), new TaskCompletionSource());
            await sibling.Task.WaitAsync(TimeSpan.FromSeconds(5)); Assert.False(done.Task.IsCompleted); Assert.False(request.IsCompleted); Assert.Equal(0, cleanupOwner);
            drain.SetResult(true);
            var failure = await Record.ExceptionAsync(() => done.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(owner, cleanupOwner);
            if (cleanupFails) Assert.Same(cause, failure); else Assert.Null(failure);
        }
        finally { drain.TrySetResult(true); await loop.StopAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
    }
}
