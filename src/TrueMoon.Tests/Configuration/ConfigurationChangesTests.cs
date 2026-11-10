using System.Collections.Concurrent;
using System.Text.Json;
using TrueMoon.Configuration;
using Xunit;

namespace TrueMoon.Tests.Configuration;

public sealed class ConfigurationChangesTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnlyPublishedCandidatesProduceNotifications(bool microsoft)
    {
        await using var fixture = new ConfigurationReloadFixture(microsoft);
        var observed = new ConcurrentQueue<ConfigurationUpdate>();
        var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = fixture.Changes.Subscribe(update => { observed.Enqueue(update); if (update.Generation == 2) delivered.TrySetResult(); });
        Assert.False((await fixture.Reload.ReloadAsync()).Published);
        fixture.Source.Replace(ConfigurationReloadFixture.Entry("Feature:Count", 20), ConfigurationReloadFixture.Entry("Feature:Startup", "changed"));
        Assert.True((await fixture.Reload.ReloadAsync()).RestartRequired);
        fixture.Source.Replace(ConfigurationReloadFixture.Entry("Feature:Count", "invalid"));
        await Assert.ThrowsAsync<ConfigurationException>(() => fixture.Reload.ReloadAsync().AsTask());
        fixture.Source.Replace(ConfigurationReloadFixture.Entry("Feature:Count", 21));
        await fixture.Reload.ReloadAsync();
        await delivered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.App.StopAsync();
        var published = Assert.Single(observed);
        Assert.True(published.Published);
        Assert.False(published.RestartRequired);
        Assert.Equal(2, published.Generation);
        Assert.Equal(new[] { "Feature:Count" }, published.ChangedKeys);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SubscriptionCapacityIs64AndDisposalReleasesSlotWithoutOwningRuntime(bool microsoft)
    {
        await using var fixture = new ConfigurationReloadFixture(microsoft);
        var tokens = new List<IDisposable>();
        try
        {
            for (var index = 0; index < 64; index++) tokens.Add(fixture.Changes.Subscribe(_ => { }));
            Assert.Throws<InvalidOperationException>(() => fixture.Changes.Subscribe(_ => { }));
            tokens[0].Dispose();
            tokens[0].Dispose();
            tokens.Add(fixture.Changes.Subscribe(_ => { }));
            Assert.Equal(20, fixture.Reader.Current.GetOptions(fixture.Schema).Count);
            Assert.Throws<InvalidOperationException>(() => fixture.Changes.Subscribe(_ => { }));
        }
        finally { foreach (var token in tokens) token.Dispose(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SlowObserverGetsLatestPendingGenerationWithUnionOfCanonicalChangedKeys(bool microsoft)
    {
        await using var fixture = new ConfigurationReloadFixture(microsoft);
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<ConfigurationUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
        var observed = new ConcurrentQueue<ConfigurationUpdate>();
        using var subscription = fixture.Changes.Subscribe(update =>
        {
            observed.Enqueue(update);
            if (update.Generation == 2) { entered.TrySetResult(); release.Wait(); }
            else second.TrySetResult(update);
        });
        try
        {
            fixture.Source.Replace(ConfigurationReloadFixture.Entry("Feature:Count", 21));
            await fixture.Reload.ReloadAsync();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Source.Replace(ConfigurationReloadFixture.Entry("Feature:Count", 22), ConfigurationReloadFixture.Entry("feature:title", "title"));
            await fixture.Reload.ReloadAsync();
            fixture.Source.Replace(ConfigurationReloadFixture.Entry("Feature:Count", 23), ConfigurationReloadFixture.Entry("Feature:Title", "title"),
                ConfigurationReloadFixture.Entry("feature:note", "note"));
            await fixture.Reload.ReloadAsync();
            Assert.Single(observed);
            release.Set();
            var latest = await second.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await fixture.App.StopAsync();
            Assert.Equal(new long[] { 2, 4 }, observed.Select(update => update.Generation));
            Assert.Equal(4, latest.Generation);
            Assert.Equal(new[] { "Feature:Count", "Feature:Note", "Feature:Title" }, latest.ChangedKeys);
            Assert.True(latest.Published);
        }
        finally { release.Set(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ObserverExceptionsDoNotRollbackPublicationOrBlockOtherObservers(bool microsoft)
    {
        await using var fixture = new ConfigurationReloadFixture(microsoft);
        var attempts = 0;
        var brokenEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var healthy = new TaskCompletionSource<ConfigurationUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var broken = fixture.Changes.Subscribe(_ => { Interlocked.Increment(ref attempts); brokenEntered.TrySetResult(); throw new FormatException("observer-secret"); });
        using var good = fixture.Changes.Subscribe(update => healthy.TrySetResult(update));
        fixture.Source.Replace(ConfigurationReloadFixture.Entry("Feature:Count", 32));
        var published = await fixture.Reload.ReloadAsync();
        Assert.True(published.Published);
        Assert.Equal(2, (await healthy.Task.WaitAsync(TimeSpan.FromSeconds(5))).Generation);
        await brokenEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.App.StopAsync();
        Assert.Equal(1, attempts);
        Assert.Equal(32, fixture.Reader.Current.GetOptions(fixture.Schema).Count);
        Assert.Equal(2, fixture.Reader.Current.Generation);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallbackCanUnsubscribeItselfWithoutDisposingTheRuntime(bool microsoft)
    {
        await using var fixture = new ConfigurationReloadFixture(microsoft);
        var called = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        IDisposable? subscription = null;
        subscription = fixture.Changes.Subscribe(_ =>
        {
            Interlocked.Increment(ref calls);
            subscription!.Dispose();
            called.TrySetResult();
        });
        try
        {
            fixture.Source.Replace(ConfigurationReloadFixture.Entry("Feature:Count", 23));
            await fixture.Reload.ReloadAsync();
            await called.Task.WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Source.Replace(ConfigurationReloadFixture.Entry("Feature:Count", 24));
            await fixture.Reload.ReloadAsync();
            await fixture.App.StopAsync();
            Assert.Equal(1, calls);
            Assert.Equal(24, fixture.Reader.Current.GetOptions(fixture.Schema).Count);
        }
        finally { subscription.Dispose(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallbackCanSynchronouslyRequestReloadBecauseDeliveryIsOutsideGate(bool microsoft)
    {
        await using var fixture = new ConfigurationReloadFixture(microsoft);
        var nested = new TaskCompletionSource<ConfigurationUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = fixture.Changes.Subscribe(update =>
        {
            if (update.Generation != 2) return;
            fixture.Source.Replace(ConfigurationReloadFixture.Entry("Feature:Count", 42));
            RequestNestedReload(fixture.Reload, nested);
        });
        fixture.Source.Replace(ConfigurationReloadFixture.Entry("Feature:Count", 41));
        await fixture.Reload.ReloadAsync();
        var inner = await nested.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(inner.Published);
        Assert.Equal(3, inner.Generation);
        Assert.Equal(42, fixture.Reader.Current.GetOptions(fixture.Schema).Count);
        Assert.Equal(1, fixture.Registrations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StopWaitsForRunningCallbackAndClosesAllAdmissionWhileCurrentRemainsReadable(bool microsoft)
    {
        await using var fixture = new ConfigurationReloadFixture(microsoft);
        await fixture.App.StartAsync();
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = fixture.Changes.Subscribe(_ => { entered.TrySetResult(); release.Wait(); });
        try
        {
            fixture.Source.Replace(ConfigurationReloadFixture.Entry("Feature:Count", 26));
            await fixture.Reload.ReloadAsync();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var stop = fixture.App.StopAsync();
            Assert.False(stop.IsCompleted);
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Reload.ReloadAsync().AsTask());
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Writer.WriteAsync("mutable", []).AsTask());
            Assert.Throws<InvalidOperationException>(() => fixture.Changes.Subscribe(_ => { }));
            Assert.Equal(26, fixture.Reader.Current.GetOptions(fixture.Schema).Count);
            release.Set();
            await stop.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(2, fixture.Reader.Current.Generation);
        }
        finally { release.Set(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedStartupClosesReloadWriteAndSubscriptionsBeforeAppDisposal(bool microsoft)
    {
        var failure = new InvalidOperationException("test-startup-failure");
        await using var fixture = new ConfigurationReloadFixture(microsoft, setup: context =>
            context.Services(services => services.Singleton<IStartable>(_ => new FailingReloadStart(failure))));
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.App.StartAsync()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Reload.ReloadAsync().AsTask());
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Writer.WriteAsync("mutable", []).AsTask());
        Assert.Throws<InvalidOperationException>(() => fixture.Changes.Subscribe(_ => { }));
        Assert.Equal(20, fixture.Reader.Current.GetOptions(fixture.Schema).Count);
        Assert.Equal(1, fixture.Reader.Current.Generation);
    }

    private static void RequestNestedReload(IConfigurationReloader reload, TaskCompletionSource<ConfigurationUpdate> completion)
    {
        var pending = reload.ReloadAsync().AsTask();
        try
        {
            var finished = Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(5))).GetAwaiter().GetResult();
            if (!ReferenceEquals(finished, pending)) throw new TimeoutException("Nested reload could not enter the gate from a callback.");
            completion.TrySetResult(pending.GetAwaiter().GetResult());
        }
        catch (Exception error) { completion.TrySetException(error); }
    }
    private sealed class FailingReloadStart(Exception failure) : IStartable
    {
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.FromException(failure);
    }
}
