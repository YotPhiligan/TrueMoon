using System.Text.Json;
using TrueMoon.Aether;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Gpu.Surfaces;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Enerit.IO.Pipes;
using TrueMoon.Generated.TrueMoon.Aether;
using TrueMoon.Mithril;
using TrueMoon.Mithril.Units;

namespace TrueMoon.Editor;

// Process bootstrap only. RuntimeProtocol, game loading, transport, rendering and ownership stay in Aether.
internal static class RuntimeEntryPoint
{
    internal static async Task RunAsync()
    {
        var text = Environment.GetEnvironmentVariable(NativeRuntimeFactory.BootstrapKey) ?? throw new InvalidDataException("Missing runtime bootstrap.");
        if (text.Length > 16384) throw new InvalidDataException("Oversized runtime bootstrap.");
        var launch = JsonSerializer.Deserialize<SessionLaunch>(text) ?? throw new InvalidDataException("Invalid runtime bootstrap.");
        launch.Identity.Validate();
        if (launch.Identity.HostContractId != BuildIdentity.HostId || launch.Build == null || launch.Build.BuildId != launch.Identity.GameBuildId ||
            !launch.Endpoint.StartsWith("engine-", StringComparison.Ordinal) || launch.Endpoint.Length > 100)
            throw new InvalidDataException("Runtime requires a matching selected game build.");
        var parentId = int.Parse(Environment.GetEnvironmentVariable("-p") ?? throw new InvalidDataException("Missing runtime parent."));
        await using var app = App.Build(context => context.UseAether(options =>
        { options.HostContractId = launch.Identity.HostContractId; options.GameBuildId = launch.Identity.GameBuildId; options.ReportDirectory = launch.ReportDirectory; }));
        await app.StartAsync(); var lifetime = app.Services.Resolve<IAppLifetime>()!;
        await using var parent = new UnitParentProcessEventsHandler(app.Services.Resolve<IConfiguration>()!, lifetime, new AetherEvents<UnitParentProcessEventsHandler>());
        parent.StartListen(parentId);
        using var end = CancellationTokenSource.CreateLinkedTokenSource(lifetime.AppCancellationToken);
        using var loaded = GameBuildLoader.Load(launch.Build, launch.Identity.HostContractId);
        using var frames = new FrameProducer();
        var protocol = new RuntimeProtocol(launch.Identity, loaded.Session, Environment.ProcessId, frames.Configure, () => frames.Dropped, launch.RequireRestore);
        var handler = new BoundedControlHandler(new EngineControlInvocationServerHandler(protocol, new AetherEvents<EngineControlInvocationServerHandler>()));
        using var server = new PipesInvocationServer<IEngineControl>(new AetherEvents<PipesInvocationServer<IEngineControl>>(), handler, launch.Endpoint);
        var engine = app.Services.Resolve<AetherHost>()!;
        var gpu = engine.Gpu.RunSurfaceProducerAsync(launch, end.Token,
            validation: Environment.GetEnvironmentVariable("TRUEMOON_GPU_VALIDATION") == "1",
            frameProvider: (stamp, width, height) => { var frame = protocol.CaptureRenderFrame(); return GpuFrameData.FromScene(stamp, frame.Tick, frame.Scene); });
        var workers = new[] { TickAsync(protocol, end.Token), ReadStopAsync(lifetime, end.Token), frames.RunAsync(protocol, end.Token), gpu };
        foreach (var worker in workers) _ = worker.ContinueWith(_ => end.Cancel(), CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        try { await Task.Delay(Timeout.InfiniteTimeSpan, end.Token); }
        catch (OperationCanceledException) when (end.IsCancellationRequested) { }
        finally
        {
            end.Cancel(); server.Dispose();
            try { await Task.WhenAll(workers); } catch (OperationCanceledException) when (end.IsCancellationRequested) { }
            finally
            {
                await parent.StopAsync(); await parent.Completion; await app.StopAsync();
                File.WriteAllText(Path.Combine(launch.ReportDirectory, $"runtime-{launch.Identity.Generation}.json"), JsonSerializer.Serialize(new
                { Pid = Environment.ProcessId, Validation = engine.Gpu.LastSurfaceProducer, Resources = engine.Gpu.ResourceCount, Handles = engine.Gpu.OpenHandleCount }));
            }
        }
    }
    private static async Task TickAsync(RuntimeProtocol protocol, CancellationToken token)
    { using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1d / 60)); while (await timer.WaitForNextTickAsync(token)) protocol.FixedTick(); }
    private static async Task ReadStopAsync(IAppLifetime lifetime, CancellationToken token)
    { while (await Console.In.ReadLineAsync(token) is { } line) if (line == UnitCodes.Exit) { lifetime.Cancel(); return; } lifetime.Cancel(); }
}
