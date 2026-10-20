using TrueMoon.Aether;
using System.Text.Json;
using TrueMoon.Aether.Contracts;
using TrueMoon.Aether.Builds;
using Game.Logic;
using TrueMoon;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
using TrueMoon.Aether.Gpu.Surfaces;
using TrueMoon.Dependencies;
using TrueMoon.Configuration;
using TrueMoon.Enerit.IO.Pipes;
using TrueMoon.Generated.TrueMoon.Aether;
using TrueMoon.Mithril;
using TrueMoon.Mithril.Units;

namespace EngineSessionProbe;

public static class RuntimeHost
{
    public const string BootstrapKey = NativeRuntimeFactory.BootstrapKey;
    public static async Task<int> RunAsync()
    {
        var text = Environment.GetEnvironmentVariable(BootstrapKey) ?? throw new InvalidDataException("Missing bootstrap.");
        if (text.Length > 16384) throw new InvalidDataException("Oversized bootstrap.");
        var launch = JsonSerializer.Deserialize<SessionLaunch>(text) ?? throw new InvalidDataException("Invalid bootstrap.");
        launch.Identity.Validate();
        if (launch.Identity.HostContractId != BuildIdentity.HostId || launch.Build == null && launch.Identity.GameBuildId != BuildIdentity.GameId ||
            launch.Build != null && launch.Build.BuildId != launch.Identity.GameBuildId ||
            !launch.Endpoint.StartsWith("engine-", StringComparison.Ordinal) || launch.Endpoint.Length > 100)
            throw new InvalidDataException("Bootstrap build/endpoint mismatch.");
        var parentId = int.Parse(Environment.GetEnvironmentVariable("-p") ?? throw new InvalidDataException("Missing parent."));
        var surfaceRole = launch.Behavior.StartsWith("gpu-surface", StringComparison.Ordinal);
        var gpuRole = surfaceRole || launch.Behavior.StartsWith("gpu-import", StringComparison.Ordinal);
        await using var app = App.Builder(_ => { }).Setup(c =>
        {
            c.UseAether(o =>
            { o.HostContractId = launch.Identity.HostContractId; o.GameBuildId = launch.Identity.GameBuildId; o.ReportDirectory = launch.ReportDirectory; });
        }).Build();
        using var boundedLifetime = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await app.StartAsync(boundedLifetime.Token);
        var lifetime = app.Services.Resolve<IAppLifetime>()!;
        await using var parent = new UnitParentProcessEventsHandler(app.Services.Resolve<IConfiguration>()!, lifetime,
            new AetherEvents<UnitParentProcessEventsHandler>());
        parent.StartListen(parentId);
        using var end = CancellationTokenSource.CreateLinkedTokenSource(lifetime.AppCancellationToken, boundedLifetime.Token);
        using var loaded = launch.Build == null ? null : GameBuildLoader.Load(launch.Build, launch.Identity.HostContractId);
        using var legacyGame = loaded == null ? new ProbeGame() : null;
        var game = loaded?.Session ?? legacyGame!;
        if (launch.Behavior is "capture-reject" or "restore-reject") game = new CheckpointFaultGame(game, launch.Behavior);
        if (loaded != null) Records.Write(launch.ReportDirectory, $"loaded-{launch.Identity.Generation}.json", new
        { BuildId = loaded.Artifact.BuildId, Path = loaded.LoadedAssemblyPath, Sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(loaded.LoadedAssemblyPath))), game.PatternColor });
        using var frames = new FrameProducer();
        var protocol = new RuntimeProtocol(launch.Identity, game, Environment.ProcessId, frames.Configure, () => frames.Dropped, launch.RequireRestore);
        PipesInvocationServer<IEngineControl>? server = null;
        var control = new FaultControl(protocol, launch, () => { server?.Dispose(); server = null; }, frames.Dispose, end.Token,
            () => boundedLifetime.CancelAfter(TimeSpan.FromMinutes(2)), () => ((ICheckpointGameSession)game).CaptureCheckpoint());
        var handler = new BoundedControlHandler(new EngineControlInvocationServerHandler(control,
            new AetherEvents<EngineControlInvocationServerHandler>()));
        Task tick = Task.CompletedTask, input = Task.CompletedTask, frameWork = Task.CompletedTask, gpuWork = Task.CompletedTask;
        try
        {
            if (launch.Behavior == "gpu-capabilities")
                Records.Write(launch.ReportDirectory, $"gpu-producer-{launch.Identity.Generation}.json", GpuCapabilityCollector.Vulkan(launch.Identity));
            if (surfaceRole)
            {
                var produces = 0;
                gpuWork = app.Services.Resolve<AetherHost>()!.Gpu.RunSurfaceProducerAsync(launch, end.Token, async (operation, cancellation) =>
                {
                    if (operation == "Produce") produces++;
                    Records.Write(launch.ReportDirectory, $"surface-{launch.Identity.Generation}-{operation}-{produces}.json", new { Pid = Environment.ProcessId, operation, produces });
                    if (operation == "Produce" && File.Exists(Path.Combine(launch.ReportDirectory, $"surface-gate-{launch.Identity.Generation}.json")))
                    {
                        Records.Write(launch.ReportDirectory, $"surface-gated-{launch.Identity.Generation}.json", new { produces });
                        while (!File.Exists(Path.Combine(launch.ReportDirectory, $"surface-release-{launch.Identity.Generation}.json"))) await Task.Delay(2, cancellation);
                    }
                    if (operation == "Produce" && (launch.Behavior == "gpu-surface-crash" || launch.Behavior == "gpu-surface-crash-second" && produces == 2) || operation == "Return" && launch.Behavior == "gpu-surface-crash-return") Environment.Exit(44);
                    if (operation == "Produce" && (launch.Behavior == "gpu-surface-hold" || launch.Behavior == "gpu-surface-hold-second" && produces == 2)) await Task.Delay(Timeout.InfiniteTimeSpan, cancellation);
                }, validation: Environment.GetEnvironmentVariable("TRUEMOON_GPU_VALIDATION") == "1");
            }
            else if (gpuRole) gpuWork = app.Services.Resolve<AetherHost>()!.Gpu.RunProducerProbeAsync(launch, end.Token, async (operation, cancellation) =>
            {
                Records.Write(launch.ReportDirectory, $"gpu-{launch.Identity.Generation}-{operation}.json", new { Pid = Environment.ProcessId, operation });
                if (operation == "Produce" && launch.Behavior == "gpu-import-crash") Environment.Exit(43);
                if (operation == "Produce" && launch.Behavior == "gpu-import-hold") await Task.Delay(Timeout.InfiniteTimeSpan, cancellation);
            }, validation: Environment.GetEnvironmentVariable("TRUEMOON_GPU_VALIDATION") == "1");
            else gpuWork = app.Services.Resolve<AetherHost>()!.Gpu.RunSurfaceProducerAsync(launch, end.Token,
                beforeOperation: async (operation, cancellation) => { if (operation == "Produce" && launch.Behavior == "no-frames") await Task.Delay(Timeout.InfiniteTimeSpan, cancellation); },
                validation: Environment.GetEnvironmentVariable("TRUEMOON_GPU_VALIDATION") == "1",
                frameProvider: (stamp, width, height) =>
                {
                    var captured = protocol.CaptureFrame();
                    return GpuFrameData.GamePattern(stamp, width, height, captured.Tick, captured.Color);
                });
            Records.Write(launch.ReportDirectory, $"launched-{launch.Identity.Generation}.json", new { Pid = Environment.ProcessId, Parent = parentId });
            if (launch.Behavior != "no-server") server = new(new AetherEvents<PipesInvocationServer<IEngineControl>>(), handler, launch.Endpoint);
            tick = TickAsync(protocol, end.Token);
            frameWork = launch.Behavior == "no-frames" ? Task.Delay(Timeout.InfiniteTimeSpan, end.Token) : frames.RunAsync(protocol, end.Token);
            _ = frameWork.ContinueWith(t => { Console.Error.WriteLine(t.Exception); end.Cancel(); }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            input = ReadStopAsync(launch.Behavior, lifetime, end.Token);
            await Task.Delay(Timeout.InfiniteTimeSpan, end.Token);
        }
        catch (OperationCanceledException) when (end.IsCancellationRequested) { }
        finally
        {
            end.Cancel(); server?.Dispose();
            try { await Task.WhenAll(tick, input, frameWork); } catch (OperationCanceledException) when (end.IsCancellationRequested) { }
            try { await gpuWork; } catch (Exception error) { if (gpuRole) Records.Write(launch.ReportDirectory, $"gpu-error-{launch.Identity.Generation}.json", new { Error = error.ToString() }); }
            await parent.StopAsync(); await parent.Completion;
            await app.StopAsync();
            {
                var gpu = app.Services.Resolve<AetherHost>()!.Gpu;
                Records.Write(launch.ReportDirectory, $"gpu-cleanup-{launch.Identity.Generation}.json", new { gpu.ResourceCount, gpu.OpenHandleCount, gpu.LastProducer, Error = gpu.CleanupError?.ToString() });
                if (surfaceRole || !gpuRole) Records.Write(launch.ReportDirectory, $"surface-cleanup-{launch.Identity.Generation}.json", new { gpu.ResourceCount, gpu.OpenHandleCount, gpu.LastSurfaceProducer, Error = gpu.CleanupError?.ToString() });
            }
            Records.Write(launch.ReportDirectory, $"runtime-closed-{launch.Identity.Generation}.json", new { Pid = Environment.ProcessId, ParentMonitorCompleted = parent.Completion.IsCompletedSuccessfully });
        }
        return 0;
    }
    private static async Task TickAsync(RuntimeProtocol protocol, CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1d / 60));
        while (await timer.WaitForNextTickAsync(token)) protocol.FixedTick();
    }
    private static async Task ReadStopAsync(string behavior, IAppLifetime lifetime, CancellationToken token)
    {
        while (await Console.In.ReadLineAsync(token) is { } line)
            if (line == UnitCodes.Exit && behavior is not ("ignore-stop" or "hang-stop")) { lifetime.Cancel(); return; }
        if (behavior is not ("ignore-stop" or "hang-stop")) lifetime.Cancel();
    }
}

internal sealed class FaultControl(RuntimeProtocol inner, SessionLaunch launch, Action disconnect, Action endFrames, CancellationToken lifetime,
    Action renewInactivityDeadline, Func<GameCheckpointState> capture) : IEngineControl
{
    public async Task<ControlReply> SendAsync(ControlRequest request, CancellationToken cancellationToken = default)
    {
        var status = request.Kind == (int)CommandKind.GetStatus;
        if (request.Kind == (int)CommandKind.RestoreCheckpoint && launch.Behavior == "restore-held")
        {
            Records.Write(launch.ReportDirectory, $"restore-held-{launch.Identity.Generation}.json", new { Pid = Environment.ProcessId });
            await Task.Delay(Timeout.InfiniteTimeSpan, lifetime);
        }
        if (request.Kind == (int)CommandKind.CaptureCheckpoint && launch.Behavior == "capture-timeout")
            await Task.Delay(Timeout.InfiniteTimeSpan, lifetime);
        if (request.Kind == (int)CommandKind.Stop && launch.Behavior == "hang-stop")
            await Task.Delay(Timeout.InfiniteTimeSpan, lifetime);
        if (status && launch.Behavior is "hang-status" or "disconnect-status")
            Records.Write(launch.ReportDirectory, $"held-{launch.Identity.Generation}.json", new { Pid = Environment.ProcessId });
        if (request.Kind == (int)CommandKind.Hello && launch.Behavior == "hang-hello" || status && launch.Behavior == "hang-status")
            await Task.Delay(Timeout.InfiniteTimeSpan, lifetime);
        if (status && launch.Behavior == "crash-status") Environment.Exit(42);
        if (status && launch.Behavior == "disconnect-status")
        {
            // Leave the runtime alive: this specifically tests transport failure, not process exit.
            _ = Task.Run(disconnect); await Task.Delay(Timeout.InfiniteTimeSpan, lifetime);
        }
        var reply = await inner.SendAsync(request, cancellationToken);
        if (request.Kind == (int)CommandKind.CaptureCheckpoint && reply.CheckpointOutcome == (int)CheckpointOutcome.Captured)
            Records.Write(launch.ReportDirectory, $"captured-{launch.Identity.Generation}-{request.Sequence}.json", GameCheckpointCodec.Decode(reply.Checkpoint!));
        if (request.Kind == (int)CommandKind.RestoreCheckpoint && reply.CheckpointOutcome == (int)CheckpointOutcome.Restored)
            Records.Write(launch.ReportDirectory, $"restored-{launch.Identity.Generation}.json", capture());
        renewInactivityDeadline();
        if (status && launch.Behavior == "frame-eof")
            _ = Task.Run(async () =>
            {
                try { await Task.Delay(200, lifetime); endFrames(); }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            });
        if (request.Kind == (int)CommandKind.Hello && launch.Behavior == "bad-hello") reply.Stamp.Nonce = new string('0', 64);
        if (status && launch.Behavior == "stale-status") reply.Stamp.Generation--;
        if (request.Kind == (int)CommandKind.LoadScene && launch.Behavior == "bad-scene") reply.Revision++;
        return reply;
    }
}

internal sealed class CheckpointFaultGame(IGameSession inner, string behavior) : ICheckpointGameSession
{
    public ulong TickNumber => inner.TickNumber;
    public long Revision => inner.Revision;
    public uint PatternColor => inner.PatternColor;
    public void Load(SceneSnapshot scene) => inner.Load(scene);
    public void Tick() => inner.Tick();
    public GameCheckpointState CaptureCheckpoint() => behavior == "capture-reject"
        ? throw new CheckpointRejectedException() : ((ICheckpointGameSession)inner).CaptureCheckpoint();
    public void RestoreCheckpoint(GameCheckpointState state)
    {
        if (behavior == "restore-reject") throw new CheckpointRejectedException(CheckpointOutcome.RestoreRejected);
        ((ICheckpointGameSession)inner).RestoreCheckpoint(state);
    }
    public void Dispose() { } // RuntimeHost retains ownership of the underlying game/loader.
}

public static class Records
{
    public static void Write(string directory, string name, object value)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name); var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(value)); File.Move(temporary, path, true);
    }
    public static async Task WaitAsync(string path, TimeSpan? timeout = null)
    {
        using var limit = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(10));
        while (!File.Exists(path)) await Task.Delay(10, limit.Token);
    }
}
