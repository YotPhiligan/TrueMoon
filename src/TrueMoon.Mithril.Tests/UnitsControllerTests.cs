using TrueMoon.Mithril.Units;

namespace TrueMoon.Mithril.Tests;

public class UnitsControllerTests
{
    [Fact]
    public async Task ConfigurationSnapshot_IsIndependentAndStartIsIdempotent()
    {
        using var lifetime = new TestLifetime();
        var input = Config(1);
        await using var controller = new FakeController(lifetime, [input]);
        input.Name = "mutated"; input.HostingPolicy = UnitHostingPolicy.None;
        await controller.StartAsync(); await controller.StartAsync();
        Assert.Equal("unit1", Assert.Single(controller.Created).GetConfiguration().Name);
        Assert.Equal(1, controller.Created[0].Starts);
        await controller.StopAsync(); await controller.StopAsync();
        Assert.Equal(1, controller.Created[0].Stops);
        Assert.Equal(1, controller.Created[0].Disposals);
        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.StartAsync());
    }

    [Theory]
    [InlineData("index")]
    [InlineData("duplicate-index")]
    [InlineData("duplicate-name")]
    [InlineData("name")]
    [InlineData("hosting")]
    [InlineData("startup")]
    [InlineData("restart")]
    [InlineData("delay")]
    [InlineData("external-path")]
    public void InvalidConfiguration_IsRejectedBeforeAnyHandle(string shape)
    {
        using var lifetime = new TestLifetime();
        var first = Config(shape == "index" ? 0 : 1);
        var values = new List<IUnitConfiguration> { first };
        switch (shape)
        {
            case "duplicate-index": values.Add(Config(1)); break;
            case "duplicate-name": var second = Config(2); second.Name = first.Name; values.Add(second); break;
            case "name": first.Name = " "; break;
            case "hosting": first.HostingPolicy = (UnitHostingPolicy)99; break;
            case "startup": first.StartupPolicy = (UnitStartupPolicy)99; break;
            case "restart": first.RestartPolicy = (UnitRestartPolicy)99; break;
            case "delay": first.TerminationDelay = -1; break;
            case "external-path": first.HostingPolicy = UnitHostingPolicy.External; break;
        }
        Assert.ThrowsAny<ArgumentException>(() => new FakeController(lifetime, values));
    }

    [Theory]
    [InlineData("request")]
    [InlineData("main-delayed")]
    public void UnsupportedLifetimeOrLocalDelayedHosting_IsExplicit(string shape)
    {
        using var lifetime = new TestLifetime();
        var config = Config(1);
        if (shape == "request") config.LifetimePolicy = UnitLifetimePolicy.Request;
        else { config.HostingPolicy = UnitHostingPolicy.MainProcess; config.StartupPolicy = UnitStartupPolicy.Delayed; }
        Assert.Throws<NotSupportedException>(() => new FakeController(lifetime, [config]));
    }

    [Theory]
    [InlineData("throw")]
    [InlineData("false")]
    [InlineData("cancel")]
    public async Task FailedStartup_RollsBackAllHandlesAndPreservesCause(string failure)
    {
        using var lifetime = new TestLifetime();
        using var cancellation = new CancellationTokenSource();
        var original = new ApplicationException("launch-failed");
        await using var controller = new FakeController(lifetime, [Config(1), Config(2)])
        {
            Configure = handle =>
            {
                if (handle.GetConfiguration().Index != 2) return;
                handle.StartAction = () => failure switch
                {
                    "throw" => throw original,
                    "false" => false,
                    _ => CancelAndSucceed(cancellation)
                };
            }
        };
        var error = await Record.ExceptionAsync(() => controller.StartAsync(cancellation.Token));
        if (failure == "throw") Assert.Same(original, error);
        else if (failure == "false") Assert.IsType<InvalidOperationException>(error);
        else Assert.IsAssignableFrom<OperationCanceledException>(error);
        Assert.Equal(new[] { "stop2", "stop1" }, controller.Log);
        Assert.All(controller.Created, h => { Assert.Equal(1, h.Stops); Assert.Equal(1, h.Disposals); });
        await controller.StopAsync();
        Assert.Equal(2, controller.Log.Count);
    }

    [Fact]
    public async Task Stop_FalseAndThrow_StillCleansOthersAndRetainsFailedHandlesForRetry()
    {
        using var lifetime = new TestLifetime();
        await using var controller = new FakeController(lifetime, [Config(1), Config(2), Config(3)]);
        await controller.StartAsync();
        controller.Created[1].StopAction = () => false;
        var original = new ApplicationException("stop-failed");
        controller.Created[2].StopAction = () => throw original;
        var error = await Assert.ThrowsAsync<AggregateException>(() => controller.StopAsync());
        Assert.Contains(original, error.InnerExceptions);
        Assert.Equal(2, error.InnerExceptions.Count);
        Assert.Equal(new[] { "stop3", "stop2", "stop1" }, controller.Log);
        Assert.Equal(1, controller.Created[0].Disposals);
        Assert.Equal(0, controller.Created[1].Disposals);
        controller.Created[1].StopAction = controller.Created[2].StopAction = () => true;
        await controller.StopAsync();
        Assert.Equal(new[] { "stop3", "stop2", "stop1", "stop3", "stop2" }, controller.Log);
        Assert.All(controller.Created, h => Assert.Equal(1, h.Disposals));
    }

    [Fact]
    public async Task DelayedSpawn_RejectsUnknownDuplicateAndUnsupportedParameters()
    {
        using var lifetime = new TestLifetime();
        var config = Config(1); config.StartupPolicy = UnitStartupPolicy.Delayed;
        await using var controller = new FakeController(lifetime, [config]);
        await controller.StartAsync(); Assert.Empty(controller.Created);
        await Assert.ThrowsAsync<ArgumentException>(() => controller.SpawnUnitAsync(2));
        await Assert.ThrowsAsync<ArgumentException>(() => controller.SpawnUnitAsync("absent"));
        await Assert.ThrowsAsync<NotSupportedException>(() => controller.SpawnUnitAsync(1, [42]));
        await controller.SpawnUnitAsync("unit1");
        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.SpawnUnitAsync(1));
        Assert.Single(controller.Created);
    }

    [Fact]
    public async Task ConcurrentStopDuringSpawn_CleansTheStartedHandleAndBlocksFurtherLaunch()
    {
        using var lifetime = new TestLifetime();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var controller = new FakeController(lifetime, [Config(1)])
        { Configure = h => h.StartAsyncAction = async () => { entered.SetResult(); await release.Task; return true; } };
        var start = controller.StartAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var stop = controller.StopAsync(); release.SetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(() => start.WaitAsync(TimeSpan.FromSeconds(3)));
        await stop.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1, Assert.Single(controller.Created).Stops);
        Assert.Equal(1, controller.Created[0].Disposals);
        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.SpawnUnitAsync(1));
    }

    [Fact]
    public async Task ExitControlsLifetime_ButCallbacksAfterStopDoNotCancelAndConcurrentDisposeRunsOnce()
    {
        using var lifetime = new TestLifetime();
        var config = Config(1); config.IsControlAppLifetime = true;
        var controller = new FakeController(lifetime, [config]);
        await controller.StartAsync();
        var handle = Assert.Single(controller.Created);
        handle.FireExit(); handle.FireExit(); Assert.Equal(1, lifetime.Calls);
        await Task.WhenAll(controller.DisposeAsync().AsTask(), controller.DisposeAsync().AsTask());
        handle.FireExit(); Assert.Equal(1, lifetime.Calls);
        Assert.Equal(1, handle.Stops); Assert.Equal(1, handle.Disposals);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => controller.SpawnUnitAsync(1));
    }

    private static bool CancelAndSucceed(CancellationTokenSource source) { source.Cancel(); return true; }
    [Fact]
    public async Task Dispose_FailureIsSharedButEveryHandleIsAttemptedAndFailedCleanupCanBeRetried()
    {
        using var lifetime = new TestLifetime();
        var controller = new FakeController(lifetime, [Config(1), Config(2)]);
        await controller.StartAsync();
        controller.Created[0].StopAction = () => false;
        var original = new ApplicationException("dispose-failed");
        controller.Created[1].DisposeAction = () => throw original;
        var first = controller.DisposeAsync().AsTask();
        var second = controller.DisposeAsync().AsTask();
        Assert.Same(first, second);
        var error = await Assert.ThrowsAsync<AggregateException>(() => first);
        Assert.Contains(original, error.InnerExceptions);
        Assert.Equal(2, error.InnerExceptions.Count);
        Assert.All(controller.Created, h => { Assert.Equal(1, h.Stops); Assert.Equal(1, h.Disposals); });
        controller.Created[1].DisposeAction = null;
        await controller.StopAsync();
        Assert.Equal(2, controller.Created[1].Stops);
        Assert.Equal(1, controller.Created[0].Stops);
    }

    [Fact]
    public async Task Dispose_CallbackCanDisposeItsControllerWithoutWaitingOnItself()
    {
        using var lifetime = new TestLifetime();
        var controller = new FakeController(lifetime, [Config(1)]);
        await controller.StartAsync();
        controller.Created[0].DisposeAction = controller.Dispose;
        await controller.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1, controller.Created[0].Disposals);
    }
    private static UnitConfiguration Config(int index) => new(index) { RestartPolicy = UnitRestartPolicy.Never };

    private sealed class FakeController(TestLifetime lifetime, IReadOnlyList<IUnitConfiguration> configs)
        : UnitsController(new TestEvents<UnitsController>(), lifetime, configs)
    {
        internal readonly List<FakeHandle> Created = [];
        internal readonly List<string> Log = [];
        internal Action<FakeHandle>? Configure;
        protected override IUnitHandle CreateHandle(IUnitConfiguration config)
        {
            var handle = new FakeHandle(config, Log);
            Configure?.Invoke(handle); Created.Add(handle); return handle;
        }
    }

    private sealed class FakeHandle(IUnitConfiguration config, List<string> log)
        : UnitHandleBase(config, new TestEvents<FakeHandle>()), IAsyncDisposable
    {
        internal int Starts, Stops, Disposals;
        internal Func<bool> StartAction = () => true;
        internal Func<bool> StopAction = () => true;
        internal Func<Task<bool>>? StartAsyncAction;
        internal Action? DisposeAction;
        public override async Task<bool?> StartAsync(CancellationToken token = default)
        { Starts++; return StartAsyncAction is null ? StartAction() : await StartAsyncAction(); }
        public override Task<bool?> StopAsync(CancellationToken token = default)
        { Stops++; log.Add($"stop{Configuration.Index}"); return Task.FromResult<bool?>(StopAction()); }
        public ValueTask DisposeAsync() { Disposals++; DisposeAction?.Invoke(); return ValueTask.CompletedTask; }
        internal void FireExit() => OnExitAction?.Invoke(this);
    }
}
