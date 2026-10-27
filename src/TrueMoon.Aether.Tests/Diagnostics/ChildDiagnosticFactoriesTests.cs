using TrueMoon.Aether.Diagnostics;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Enerit.IO.Pipes;
using TrueMoon.Generated.TrueMoon.Aether;
using Xunit;
using static TrueMoon.Aether.Tests.Diagnostics.ChildDiagnosticFixture;

namespace TrueMoon.Aether.Tests.Diagnostics;

public class ChildDiagnosticFactoriesTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AppFactories_ExchangeUsingOptionalCoreAccessAndCopiedOptionsWithCallerOwnedProducts(bool di)
    {
        var options = Options(); var launch = Launch(options); var originalTimeout = options.RequestTimeout;
        var host = Build(di, options, true); var child = Build(di, options, true);
        try
        {
            options.Enabled = false; options.MaxBatchRecords = 0; options.RequestTimeout = TimeSpan.Zero;
            var copied = host.Services.Resolve<ChildDiagnosticConfiguration>()!.Copy();
            Assert.True(copied.Enabled); Assert.Equal(2, copied.MaxBatchRecords); Assert.Equal(originalTimeout, copied.RequestTimeout);
            launch = launch with { DiagnosticDelivery = copied };
            var receivers = host.Services.Resolve<IFactory<ChildDiagnosticReceiver, ChildDiagnosticReceiverArguments>>()!;
            var senders = child.Services.Resolve<IFactory<ChildDiagnosticForwarder, SessionLaunch>>()!;
            await using var receiver = receivers.Create(new(launch, Environment.ProcessId));
            await using var sender = senders.Create(launch);
            var source = child.Services.Resolve<IEventsSourceFactory>()!.Create("TrueMoon.DeliveryApp." + Guid.NewGuid().ToString("N"));
            source.Write(DiagnosticLevel.Warning, 719, "Delivery.App", "app delivery");
            await UntilAsync(() => sender.Read().Accepted > 0);
            var childPipeline = child.Services.Resolve<DiagnosticPipeline>()!; var hostPipeline = host.Services.Resolve<DiagnosticPipeline>()!;
            var imported = Assert.Single(hostPipeline.Read(), x => x.EventName == "Delivery.App" && x.Origin != null);
            Assert.Equal(childPipeline.ApplicationId, imported.Origin!.ApplicationId); Assert.Equal(hostPipeline.ApplicationId, imported.ApplicationId);
            Assert.Equal(launch.Identity.SessionId.ToString("D"), imported.Properties["SessionId"]);
            Assert.True(receiver.Read().Enabled); Assert.True(sender.Read().Enabled);
            await sender.StopAsync(); await receiver.StopAsync();
            Assert.True(sender.Read().Released); Assert.True(receiver.Read().Released);
            Assert.False(hostPipeline.EmergencyStatus?.Failures > 0); Assert.False(childPipeline.EmergencyStatus?.Failures > 0);
        }
        finally { await child.DisposeAsync(); await host.DisposeAsync(); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AppWithoutPipeline_ProvidesLocalOnlyProductsAndNativeFactoryWithoutReceiver(bool di)
    {
        await using var app = Build(di, Options(), false); var launch = Launch();
        Assert.Null(app.Services.GetService(typeof(IDiagnosticRecordReader))); Assert.Null(app.Services.GetService(typeof(IDiagnosticRecordImporter)));
        await using var receiver = app.Services.Resolve<IFactory<ChildDiagnosticReceiver, ChildDiagnosticReceiverArguments>>()!.Create(new(launch, Environment.ProcessId));
        await using var sender = app.Services.Resolve<IFactory<ChildDiagnosticForwarder, SessionLaunch>>()!.Create(launch);
        Assert.False(receiver.Read().Enabled); Assert.False(sender.Read().Enabled); Assert.True(receiver.Read().Released); Assert.True(sender.Read().Released);
        var native = Assert.IsType<NativeRuntimeFactory>(app.Services.Resolve<IRuntimeFactory>());
        await using var lease = Assert.IsType<NativeRuntimeLease>(native.Create(launch));
        Assert.Null(lease.ChildDiagnosticsStatus); Assert.Equal(0, lease.ProcessId); Assert.Same(lease, Assert.Single(native.Leases));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RuntimeHost_OwnsSenderAndDrainsItAfterPreviewGameCleanup(bool di)
    {
        var launch = PreviewLaunch(); var reader = new DeliveryReader(); var clients = new DeliveryClients();
        PreviewGameSession? game = null; ChildDiagnosticForwarder? sender = null; var checkedAfterGame = 0;
        var app = App.Builder(c => { if (di) c.UseDI(); }).Setup(c =>
        {
            c.Services(s =>
            {
                s.Singleton<IFactory<PreviewGameSession>>(_ => new Factory<PreviewGameSession>(() => game = new()));
                s.Singleton<IFactory<ChildDiagnosticForwarder, SessionLaunch>>(_ => new Factory<ChildDiagnosticForwarder, SessionLaunch>(value => sender = new(value, reader, clients)));
            });
            c.UseAether(Configure); c.UseAetherPreviewRuntime(launch, int.MaxValue);
        }).Build();
        try
        {
            var runtime = app.Services.Resolve<AetherRuntimeHost>()!;
            Assert.NotNull(sender); Assert.NotNull(game);
            reader.OnLatest = () => { Assert.Throws<ObjectDisposedException>(() => game!.CaptureRenderScene()); Interlocked.Increment(ref checkedAfterGame); return 0; };
            await runtime.DisposeAsync(); await runtime.DisposeAsync();
            Assert.Equal(1, checkedAfterGame); Assert.True(sender!.Read().Closed); Assert.True(sender.Read().Released); Assert.True(sender.Read().DrainComplete);
            Assert.True(clients.Product!.Completion.IsCompletedSuccessfully); Assert.Equal(0, reader.Disposals); Assert.Equal(0, clients.Disposals);
            Assert.Equal(0, app.Services.Resolve<AetherHost>()!.Gpu.ResourceCount);
        }
        finally { await app.DisposeAsync(); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RuntimeHost_DeliveryFactoryFailureRollsBackGameAndControlWithoutChangingCause(bool di)
    {
        PreviewGameSession? game = null; var cause = new IOException("sender activation");
        await using var app = App.Builder(c => { if (di) c.UseDI(); }).Setup(c =>
        {
            c.Services(s =>
            {
                s.Singleton<IFactory<PreviewGameSession>>(_ => new Factory<PreviewGameSession>(() => game = new()));
                s.Singleton<IFactory<ChildDiagnosticForwarder, SessionLaunch>>(_ => new Factory<ChildDiagnosticForwarder, SessionLaunch>(_ => throw cause));
            });
            c.UseAether(Configure); c.UseAetherPreviewRuntime(PreviewLaunch(), int.MaxValue);
        }).Build();
        Assert.Same(cause, Assert.Throws<IOException>(() => app.Services.Resolve<AetherRuntimeHost>()));
        Assert.NotNull(game); Assert.Throws<ObjectDisposedException>(() => game.CaptureRenderScene());
        Assert.Equal(0, app.Services.Resolve<AetherHost>()!.Gpu.ResourceCount);
    }

    private static IApp Build(bool di, ChildDiagnosticDeliveryOptions options, bool pipeline) => App.Builder(c => { if (di) c.UseDI(); }).Setup(c =>
    {
        if (pipeline) c.UseDiagnostics(new DiagnosticPipelineOptions { MinimumLevel = DiagnosticLevel.Warning, Emergency = new() { Enabled = false } });
        else c.UseDiagnostics();
        c.UseAether(o => { Configure(o); o.ChildDiagnostics = options; });
    }).Build();

    [Fact]
    public async Task BlockedDiagnosticImporter_DoesNotBlockIndependentControlConnection()
    {
        var options = Options(); options.RequestTimeout = TimeSpan.FromSeconds(2); options.ShutdownTimeout = TimeSpan.FromSeconds(2);
        var launch = Launch(options); var entered = Signal(); using var release = new ManualResetEventSlim();
        var importer = new DeliveryImporter((_, _) => { entered.TrySetResult(); Assert.True(release.Wait(Deadline)); return true; });
        await using var receiver = new ChildDiagnosticReceiver(launch, Environment.ProcessId, importer, new DeliveryServers(options));
        using var game = new PreviewGameSession(); var protocol = new RuntimeProtocol(launch.Identity, game, Environment.ProcessId);
        await using var server = new PipesInvocationServer<IEngineControl>(new DeliveryEvents<PipesInvocationServer<IEngineControl>>(),
            new EngineControlInvocationServerHandler(protocol, new DeliveryEvents<EngineControlInvocationServerHandler>()), launch.Endpoint, new() { MetricsEnabled = false });
        await using var client = new PipesInvocationClient<IEngineControl>(new DeliveryEvents<PipesInvocationClient<IEngineControl>>(), launch.Endpoint,
            new() { RequestTimeout = TimeSpan.FromSeconds(2), MetricsEnabled = false });
        var control = new EngineControlGeneratedImplementation(client); var reader = new DeliveryReader(); reader.Add(Record());
        await using var sender = new ChildDiagnosticForwarder(launch, reader, new DeliveryClients(options));
        try
        {
            await entered.Task.WaitAsync(Deadline);
            Assert.Equal("HelloAccepted", (await control.SendAsync(ControlRequest.Create(launch.Identity, 1, CommandKind.Hello))).Phase);
            var load = ControlRequest.Create(launch.Identity, 2, CommandKind.LoadScene); load.SceneId = "isolated-control"; load.Revision = 91; load.Entities = [];
            Assert.Equal(91, (await control.SendAsync(load)).Revision);
            var status = await control.SendAsync(ControlRequest.Create(launch.Identity, 3, CommandKind.GetStatus));
            Assert.Equal("Paused", status.Phase); Assert.Equal(91, status.Revision); Assert.Equal(0, status.Tick); Assert.Equal(3, status.Stamp.Sequence);
            Assert.Single(importer.Calls); Assert.Equal(0, receiver.Read().Accepted); Assert.False(receiver.Read().Closed);
        }
        finally { release.Set(); }
        await UntilAsync(() => sender.Read().Accepted == 1); await sender.StopAsync(); await receiver.StopAsync();
        Assert.Equal(1, receiver.Read().Accepted); Assert.True(receiver.Read().Released); Assert.True(sender.Read().Released);
    }
    private static void Configure(AetherOptions o)
    { o.HostContractId = new('A', 64); o.GameBuildId = BuildIdentity.PreviewId; o.ReportDirectory = Path.Combine(Path.GetTempPath(), "TrueMoon-delivery-factory-" + Guid.NewGuid().ToString("N")); }
    private static SessionLaunch PreviewLaunch() => Launch() with { Role = RuntimeRole.EditPreview, Identity = Launch().Identity with { GameBuildId = BuildIdentity.PreviewId } };
}
