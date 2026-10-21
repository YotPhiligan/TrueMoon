using Game.Logic;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Transport;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Sessions;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Tests;

public sealed class AppIntegrationTests
{
    private const string Hash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private static SceneSnapshot Scene() => new("room", 7, []);
    private static IAppBuilder Builder(bool di) => App.Builder(c => { if (di) c.UseDI(); });
    private static void Configure(AetherOptions options, Factory factory)
    {
        options.HostContractId = Hash; options.GameBuildId = Hash; options.ReportDirectory = "aether-app-tests";
        options.Deadlines = new() { HeartbeatInterval = TimeSpan.FromSeconds(30) };
        options.RuntimeFactory = _ => factory;
    }
    private static IApp Create(bool di, Factory factory, Action<IAppConfigurationContext>? after = null)
        => Builder(di).Setup(c => { c.UseAether(o => Configure(o, factory)); after?.Invoke(c); }).Build();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Registration_SnapshotsOptions_AndAppStopOwnsExplicitSession(bool di)
    {
        var factory = new Factory(); var unused = new Factory(); var calls = 0; var observations = 0; var replacedObserver = 0;
        var app = Builder(di).Setup(c =>
        {
            AetherOptions configured = null!;
            c.UseAether(o => { Configure(o, factory); configured = o; o.RuntimeFactory = _ => { calls++; return factory; }; o.StatusChanged = _ => observations++; });
            configured.GameBuildId = new('B', 64); configured.ReportDirectory = "changed";
            configured.Deadlines = new() { Startup = TimeSpan.Zero }; configured.RuntimeFactory = _ => unused;
            configured.StatusChanged = _ => replacedObserver++;
        }).Build();
        await app.StartAsync();
        var host = Assert.IsType<AetherHost>(app.Services.Resolve<AetherHost>());
        Assert.Same(host, app.Services.Resolve<AetherHost>()); Assert.Equal(1, calls); Assert.Empty(factory.Leases);
        await host.Supervisor.StartAsync(Scene());
        var lease = Assert.Single(factory.Leases);
        Assert.Equal(Hash, lease.Launch.Identity.GameBuildId);
        Assert.Equal(Path.GetFullPath("aether-app-tests"), lease.Launch.ReportDirectory);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await app.StopAsync(canceled.Token);
        Assert.Equal(SessionState.Stopped, host.Supervisor.Status.State);
        Assert.True(lease.ResourcesReleased); Assert.Equal(1, lease.Disposals); Assert.Equal(0, factory.Disposals);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => host.Supervisor.StartAsync(Scene()));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => host.Frames.StartAsync(Scene(), 32, 16));
        await app.DisposeAsync(); await host.DisposeAsync(); host.Dispose();
        Assert.Equal(1, lease.Disposals); Assert.Equal(1, factory.Disposals); Assert.Equal(0, unused.Disposals);
        Assert.True(observations > 0); Assert.Equal(0, replacedObserver);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task DisposeWithoutAppStart_ReapsResolvedHost_ExactlyOnce(bool di, bool asynchronous)
    {
        var factory = new Factory(); var app = Create(di, factory);
        var host = Assert.IsType<AetherHost>(app.Services.Resolve<AetherHost>()); await host.Supervisor.StartAsync(Scene());
        if (asynchronous) await app.DisposeAsync(); else app.Dispose();
        var lease = Assert.Single(factory.Leases);
        Assert.True(lease.ResourcesReleased); Assert.Equal(1, lease.Disposals); Assert.Equal(1, factory.Disposals);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => host.Supervisor.StartAsync(Scene()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedStartup_RollsBackPreviouslyStartedAetherLifetime(bool di)
    {
        var factory = new Factory(); var expected = new IOException("fixture startup failure");
        var app = Create(di, factory, c => c.Services(r => r.Singleton<IStartable>(resolver =>
            new FailingStartup(Assert.IsType<AetherHost>(resolver.Resolve<AetherHost>()), expected))));
        var error = await Assert.ThrowsAsync<IOException>(() => app.StartAsync());
        Assert.Same(expected, error);
        var lease = Assert.Single(factory.Leases); Assert.True(lease.ResourcesReleased); Assert.Equal(1, lease.Disposals);
        await app.DisposeAsync(); Assert.Equal(1, factory.Disposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IndependentApps_HaveIndependentHostsAndShutdown(bool di)
    {
        var firstFactory = new Factory(); var secondFactory = new Factory();
        await using var first = Create(di, firstFactory); await using var second = Create(di, secondFactory);
        await first.StartAsync(); await second.StartAsync();
        var a = Assert.IsType<AetherHost>(first.Services.Resolve<AetherHost>());
        var b = Assert.IsType<AetherHost>(second.Services.Resolve<AetherHost>());
        Assert.NotSame(a, b); await a.Supervisor.StartAsync(Scene()); await b.Supervisor.StartAsync(Scene());
        Assert.NotEqual(a.Supervisor.Status.Identity!.SessionId, b.Supervisor.Status.Identity!.SessionId);
        await first.StopAsync(); Assert.True(firstFactory.Leases[0].ResourcesReleased);
        Assert.False(secondFactory.Leases[0].ResourcesReleased); Assert.Equal(SessionState.Paused, b.Supervisor.Status.State);
        await second.StopAsync(); Assert.True(secondFactory.Leases[0].ResourcesReleased);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void DuplicateRegistrationOrFactoryConflict_FailsBeforeFactoryCreation(bool di, bool factoryConflict)
    {
        var calls = 0;
        var builder = Builder(di).Setup(c =>
        {
            if (factoryConflict) c.Services(r => r.Singleton<IRuntimeFactory>(_ => { calls++; return new Factory(); }));
            c.UseAether(o => { Configure(o, new Factory()); o.RuntimeFactory = _ => { calls++; return new Factory(); }; });
            if (!factoryConflict) c.UseAether(o => Configure(o, new Factory()));
        });
        Assert.Throws<InvalidOperationException>(() => builder.Build()); Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentShutdown_JoinsCleanup_AndRejectsNewGeneration(bool di)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var factory = new Factory { BeforeStop = async () => { entered.TrySetResult(); await release.Task; } };
        await using var app = Create(di, factory); await app.StartAsync();
        var host = Assert.IsType<AetherHost>(app.Services.Resolve<AetherHost>());
        await host.Supervisor.StartAsync(Scene());
        var first = host.DisposeAsync().AsTask(); await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var second = host.DisposeAsync().AsTask(); Assert.Same(first, second);
        var lateStart = host.Supervisor.StartAsync(Scene()); release.SetResult();
        await first; await Assert.ThrowsAsync<ObjectDisposedException>(() => lateStart);
        Assert.Single(factory.Leases); Assert.Equal(1, factory.Leases[0].Stops); Assert.Equal(1, factory.Leases[0].Disposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedCleanup_IsObservable_AndCanBeRetriedBeforeProviderDisposal(bool di)
    {
        var factory = new Factory { StopFailures = 1 }; var app = Create(di, factory); await app.StartAsync();
        var host = Assert.IsType<AetherHost>(app.Services.Resolve<AetherHost>()); await host.Supervisor.StartAsync(Scene());
        await Assert.ThrowsAsync<IOException>(() => app.StopAsync());
        await host.DisposeAsync();
        var lease = Assert.Single(factory.Leases); Assert.True(lease.ResourcesReleased);
        Assert.Equal(2, lease.Stops); Assert.Equal(1, lease.Disposals); Assert.Equal(0, host.Frames.MappingCount);
        await app.DisposeAsync(); Assert.Equal(1, factory.Disposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AppStop_DrainsFrameWorkersAndMappings(bool di)
    {
        var factory = new Factory { Produce = true }; var app = Create(di, factory); await app.StartAsync();
        var host = Assert.IsType<AetherHost>(app.Services.Resolve<AetherHost>());
        var starting = host.Frames.StartAsync(Scene(), 32, 16);
        await TestWait.UntilAsync(() => host.Frames.Display?.Receiver.FirstFrame.IsCompletedSuccessfully == true, TimeSpan.FromSeconds(3));
        var display = host.Frames.Display!;
        using (var frame = Assert.IsType<FrameLease>(display.Receiver.TakeLatest())) { host.Frames.Presented(display, frame.Metadata); }
        await starting; Assert.Equal(1, host.Frames.MappingCount);
        await app.StopAsync();
        Assert.Equal(0, host.Frames.MappingCount); Assert.Null(host.Frames.Display);
        Assert.True(display.Receiver.Completion.IsCompleted); Assert.True(factory.Leases[0].Worker.IsCompleted);
        Assert.True(factory.Leases[0].ResourcesReleased); await app.DisposeAsync(); Assert.Equal(1, factory.Disposals);
    }

    [Theory]
    [InlineData(0, typeof(InvalidDataException))]
    [InlineData(1, typeof(ArgumentException))]
    [InlineData(2, typeof(ArgumentOutOfRangeException))]
    [InlineData(3, typeof(ArgumentNullException))]
    [InlineData(4, typeof(ArgumentNullException))]
    public void InvalidOptions_AreRejectedDuringConfiguration(int invalid, Type expected)
    {
        var factory = new Factory();
        Assert.Throws(expected, () => App.Build(c => c.UseAether(o =>
        {
            Configure(o, factory);
            switch (invalid)
            {
                case 0: o.HostContractId = "invalid"; break;
                case 1: o.ReportDirectory = ""; break;
                case 2: o.Deadlines = new() { Startup = TimeSpan.Zero }; break;
                case 3: o.RuntimeFactory = null!; break;
                case 4: o.Deadlines = null!; break;
            }
        })));
        Assert.Empty(factory.Leases); Assert.Equal(0, factory.Disposals);
    }

    [Fact]
    public void NullArguments_AreRejectedBeforeConfiguration()
    {
        var calls = 0;
        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() =>
            AppConfigurationExtensions.UseAether(null!, _ => calls++)).ParamName);
        Assert.Equal("configure", Assert.Throws<ArgumentNullException>(() =>
            App.Build(c => c.UseAether(null!))).ParamName);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NullFactoryResult_IsAnObservableStartupFailure(bool di)
    {
        var app = Builder(di).Setup(c => c.UseAether(o => { Configure(o, new Factory()); o.RuntimeFactory = _ => null!; })).Build();
        var error = await Record.ExceptionAsync(() => app.StartAsync());
        Assert.NotNull(error); Assert.Contains("Aether runtime factory returned null", error.ToString());
        await app.DisposeAsync();
    }

    private sealed class FailingStartup(AetherHost host, Exception error) : IStartable
    {
        public async Task StartAsync(CancellationToken cancellationToken = default)
        { await host.Supervisor.StartAsync(Scene(), token: cancellationToken); throw error; }
    }
    private sealed class Factory : IRuntimeFactory, IDisposable
    {
        public bool Produce;
        public int StopFailures, Disposals;
        public Func<Task>? BeforeStop;
        public List<Lease> Leases { get; } = [];
        public IRuntimeLease Create(SessionLaunch launch)
        { var lease = new Lease(this, launch); Leases.Add(lease); return lease; }
        public void Dispose() { Disposals++; Assert.All(Leases, lease => Assert.True(lease.ResourcesReleased)); }
    }
    private sealed class Lease(Factory factory, SessionLaunch launch) : IRuntimeLease
    {
        private readonly ProbeGame _game = new(); private readonly FrameProducer _producer = new();
        private readonly CancellationTokenSource _end = new();
        private readonly TaskCompletionSource _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private RuntimeProtocol _protocol = null!;
        public SessionLaunch Launch => launch;
        public int Stops, Disposals;
        public Task Worker { get; private set; } = Task.CompletedTask;
        public int ProcessId => 123;
        public Task Exited => _exit.Task;
        public bool ResourcesReleased { get; private set; }
        public Task LaunchAsync(CancellationToken token)
        {
            _protocol = new(launch.Identity, _game, ProcessId, _producer.Configure);
            if (factory.Produce) Worker = _producer.RunAsync(_protocol, _end.Token);
            return Task.CompletedTask;
        }
        public Task<ControlReply> SendAsync(ControlRequest request, CancellationToken token) => _protocol.SendAsync(request, token);
        public async Task StopAsync(CancellationToken gracefulDeadline = default)
        {
            Stops++;
            if (factory.BeforeStop != null) await factory.BeforeStop();
            if (factory.StopFailures-- > 0) throw new IOException("fixture reap failure");
            _end.Cancel(); try { await Worker; } catch (OperationCanceledException) { }
            _producer.Dispose(); _exit.TrySetResult();
        }
        public ValueTask DisposeAsync()
        { Disposals++; _game.Dispose(); _end.Dispose(); ResourcesReleased = true; return ValueTask.CompletedTask; }
    }
}
