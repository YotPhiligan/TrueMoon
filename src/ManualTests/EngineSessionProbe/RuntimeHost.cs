using TrueMoon.Aether;
using System.Text.Json;
using TrueMoon.Aether.Contracts;
using Game.Logic;
using TrueMoon;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
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
        if (launch.Identity.HostContractId != BuildIdentity.HostId || launch.Identity.GameBuildId != BuildIdentity.GameId ||
            !launch.Endpoint.StartsWith("engine-", StringComparison.Ordinal) || launch.Endpoint.Length > 100)
            throw new InvalidDataException("Bootstrap build/endpoint mismatch.");
        var parentId = int.Parse(Environment.GetEnvironmentVariable("-p") ?? throw new InvalidDataException("Missing parent."));
        await using var app = App.Builder(_ => { }).Build();
        using var boundedLifetime = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await app.StartAsync(boundedLifetime.Token);
        var lifetime = app.Services.Resolve<IAppLifetime>()!;
        await using var parent = new UnitParentProcessEventsHandler(app.Services.Resolve<IConfiguration>()!, lifetime,
            new AetherEvents<UnitParentProcessEventsHandler>());
        parent.StartListen(parentId);
        using var end = CancellationTokenSource.CreateLinkedTokenSource(lifetime.AppCancellationToken, boundedLifetime.Token);
        using var game = new ProbeGame();
        using var frames = new FrameProducer();
        var protocol = new RuntimeProtocol(launch.Identity, game, Environment.ProcessId, frames.Configure, () => frames.Dropped);
        PipesInvocationServer<IEngineControl>? server = null;
        var control = new FaultControl(protocol, launch, () => { server?.Dispose(); server = null; }, frames.Dispose, end.Token);
        var handler = new BoundedControlHandler(new EngineControlInvocationServerHandler(control,
            new AetherEvents<EngineControlInvocationServerHandler>()));
        Task tick = Task.CompletedTask, input = Task.CompletedTask, frameWork = Task.CompletedTask;
        try
        {
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
            await parent.StopAsync(); await parent.Completion;
            await app.StopAsync();
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

internal sealed class FaultControl(RuntimeProtocol inner, SessionLaunch launch, Action disconnect, Action endFrames, CancellationToken lifetime) : IEngineControl
{
    public async Task<ControlReply> SendAsync(ControlRequest request, CancellationToken cancellationToken = default)
    {
        var status = request.Kind == (int)CommandKind.GetStatus;
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
