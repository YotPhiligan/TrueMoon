using TrueMoon.Diagnostics;
using Xunit;

namespace TrueMoon.Tests;

public class AppLifetimeTests
{
    [ThreadStatic] private static bool _insideCancel;

    [Fact]
    public async Task Cancel_IsConcurrentIdempotentAndReleasesWaiters()
    {
        using var source = new EventsSource<IAppLifetime>();
        using var lifetime = new DefaultAppLifetime(source);
        var cancellations = 0;
        using var registration = lifetime.AppCancellationToken.Register(() => Interlocked.Increment(ref cancellations));
        var wait = lifetime.WaitAsync();
        await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(lifetime.Cancel)));
        await wait.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(lifetime.AppCancellationToken.IsCancellationRequested);
        Assert.Equal(1, cancellations);
    }

    [Fact]
    public async Task Cancel_DoesNotRunWaitContinuationsInline()
    {
        using var source = new EventsSource<IAppLifetime>();
        using var lifetime = new DefaultAppLifetime(source);
        var continuation = lifetime.WaitAsync().ContinueWith(_ => _insideCancel, CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        _insideCancel = true;
        try { lifetime.Cancel(); }
        finally { _insideCancel = false; }
        Assert.False(await continuation.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void CancellationCallback_CanRegisterNotificationsFromAnotherThread()
    {
        using var source = new EventsSource<IAppLifetime>();
        using var lifetime = new DefaultAppLifetime(source);
        var notificationCount = 0;
        using var registration = lifetime.AppCancellationToken.Register(() =>
        {
            var registrationTask = Task.Run(() => lifetime.OnStopping(() => notificationCount++));
            Assert.True(registrationTask.Wait(TimeSpan.FromSeconds(5)), "Cancellation callback ran under the lifetime lock.");
        });
        lifetime.Cancel();
        lifetime.Stopping();
        Assert.Equal(1, notificationCount);
    }

    [Fact]
    public async Task CancellationCallbackFailure_IsVisibleAndWaitStillCompletes()
    {
        using var source = new EventsSource<IAppLifetime>();
        using var lifetime = new DefaultAppLifetime(source);
        var failure = new InvalidOperationException("callback");
        using var registration = lifetime.AppCancellationToken.Register(() => throw failure);
        var error = Assert.Throws<AggregateException>(lifetime.Cancel);
        Assert.Same(failure, Assert.Single(error.InnerExceptions));
        await lifetime.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(lifetime.AppCancellationToken.IsCancellationRequested);
        lifetime.Cancel();
    }

    [Fact]
    public void ShutdownNotifications_RunOnceContinueAfterFailureAndInvokeLateListeners()
    {
        using var source = new EventsSource<IAppLifetime>();
        using var lifetime = new DefaultAppLifetime(source);
        var trace = new List<string>();
        var failure = new InvalidOperationException("stopping");
        lifetime.OnStopping(() => { trace.Add("first"); throw failure; });
        lifetime.OnStopping(() => trace.Add("second"));
        lifetime.OnStopped(() => trace.Add("stopped"));
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(lifetime.Stopping));
        lifetime.Stopping();
        lifetime.Stopped();
        lifetime.Stopped();
        lifetime.OnStopping(() => trace.Add("late-stopping"));
        lifetime.OnStopped(() => trace.Add("late-stopped"));
        Assert.Equal(new[] { "first", "second", "stopped", "late-stopping", "late-stopped" }, trace);
    }

    [Fact]
    public void DirectStopped_NotifiesStoppingBeforeStopped()
    {
        using var source = new EventsSource<IAppLifetime>();
        using var lifetime = new DefaultAppLifetime(source);
        var trace = new List<string>();
        lifetime.OnStopping(() => trace.Add("stopping"));
        lifetime.OnStopped(() => trace.Add("stopped"));
        lifetime.Stopped();
        Assert.Equal(new[] { "stopping", "stopped" }, trace);
        Assert.True(lifetime.AppCancellationToken.IsCancellationRequested);
    }

    [Fact]
    public async Task Dispose_ReleasesOwnedSourceAndWaitersAndRejectsNewCallbacks()
    {
        using var source = new EventsSource<IAppLifetime>();
        var lifetime = new DefaultAppLifetime(source);
        var token = lifetime.AppCancellationToken;
        var wait = lifetime.WaitAsync();
        lifetime.Dispose();
        lifetime.Dispose();
        await wait.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Throws<ObjectDisposedException>(() => token.WaitHandle);
        Assert.Throws<ObjectDisposedException>(() => lifetime.OnStopped(() => { }));
        Assert.Throws<ObjectDisposedException>(() => lifetime.Cancel());
    }

    [Fact]
    public void Dispose_PreservesBorrowedCancellationSource()
    {
        using var source = new EventsSource<IAppLifetime>();
        using var borrowed = new CancellationTokenSource();
        var lifetime = new DefaultAppLifetime(source, new AppCancellationTokenSourceHandle(borrowed));
        lifetime.Dispose();
        var cancellations = 0;
        using var registration = borrowed.Token.Register(() => cancellations++);
        Assert.False(borrowed.IsCancellationRequested);
        borrowed.Cancel();
        Assert.Equal(1, cancellations);
    }

    [Fact]
    public async Task WaitCancellation_DoesNotCancelApplicationLifetime()
    {
        using var source = new EventsSource<IAppLifetime>();
        using var lifetime = new DefaultAppLifetime(source);
        using var waiting = new CancellationTokenSource();
        waiting.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => lifetime.WaitAsync(waiting.Token));
        Assert.False(lifetime.AppCancellationToken.IsCancellationRequested);
        lifetime.Cancel();
        await lifetime.WaitAsync();
    }

    [Fact]
    public async Task BorrowedSourceCancellation_ReleasesLifetimeWaiters()
    {
        using var source = new EventsSource<IAppLifetime>();
        using var borrowed = new CancellationTokenSource();
        using var lifetime = new DefaultAppLifetime(source, new AppCancellationTokenSourceHandle(borrowed));
        var wait = lifetime.WaitAsync();
        Assert.False(wait.IsCompleted);
        borrowed.Cancel();
        await wait.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(lifetime.AppCancellationToken.IsCancellationRequested);
    }

    [Fact]
    public void CancellationCallback_CanDisposeLifetimeWithoutDisposingActiveSource()
    {
        using var source = new EventsSource<IAppLifetime>();
        var lifetime = new DefaultAppLifetime(source);
        var token = lifetime.AppCancellationToken;
        var callbackCount = 0;
        using var registration = token.Register(() =>
        {
            lifetime.Dispose();
            Assert.True(token.WaitHandle.WaitOne(0));
            callbackCount++;
        });
        lifetime.Cancel();
        Assert.Equal(1, callbackCount);
        Assert.Throws<ObjectDisposedException>(() => token.WaitHandle);
        Assert.True(lifetime.WaitAsync().IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ConcurrentDispose_DefersSourceDisposalUntilCancellationCallbacksFinish()
    {
        using var source = new EventsSource<IAppLifetime>();
        var lifetime = new DefaultAppLifetime(source);
        var token = lifetime.AppCancellationToken;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        using var registration = token.Register(() =>
        {
            entered.SetResult();
            Assert.True(release.Wait(TimeSpan.FromSeconds(5)));
            Assert.True(token.WaitHandle.WaitOne(0));
        });
        var cancel = Task.Run(lifetime.Cancel);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            lifetime.Dispose();
            Assert.True(token.WaitHandle.WaitOne(0));
        }
        finally { release.Set(); }
        await cancel.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Throws<ObjectDisposedException>(() => token.WaitHandle);
    }
}
