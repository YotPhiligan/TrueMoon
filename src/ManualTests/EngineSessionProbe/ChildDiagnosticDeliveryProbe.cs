using System.Diagnostics;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Aether;
using TrueMoon.Aether.Diagnostics;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;

namespace EngineSessionProbe;

// A real host/preview runtime graph without a game artifact, window or GPU surface.
internal static class ChildDiagnosticDeliveryProbe
{
    private const string Behavior = "child-diagnostic-delivery";
    private const string Source = "TrueMoon.ChildDeliveryProbe";
    internal static bool IsChild() => RuntimeBootstrap.ReadEnvironment(allowLegacyGame: true).Launch.Behavior == Behavior;
    private static ChildDiagnosticDeliveryOptions Limits() => new()
    {
        MaxBatchRecords = 4, MaxBatchBytes = 8192, RecordsPerSecond = 32, BytesPerSecond = 64 * 1024,
        PollInterval = TimeSpan.FromMilliseconds(20), RequestTimeout = TimeSpan.FromMilliseconds(250), ShutdownTimeout = TimeSpan.FromSeconds(1)
    };
    private static DiagnosticPipelineOptions Pipeline(string root) => new()
    {
        MinimumLevel = DiagnosticLevel.Warning, MemoryCapacity = 128, MemoryByteCapacity = 256 * 1024,
        QueueCapacity = 8, ReservedErrorCapacity = 1, QueueByteCapacity = 32 * 1024,
        JsonDirectory = root, JsonUseRunDirectories = true, JsonFileSizeBytes = 32768, JsonMaxFiles = 2,
        JsonRetention = new() { MaxRuns = 2, MaxTotalBytes = 128 * 1024 }, Emergency = new() { Enabled = false }
    };
    internal static async Task<int> RunChildAsync()
    {
        var bootstrap = RuntimeBootstrap.ReadEnvironment();
        var launch = bootstrap.Launch; var parentId = bootstrap.ParentProcessId; var started = DateTimeOffset.UtcNow;
        await using var app = App.Build(c =>
        {
            c.UseDiagnostics(Pipeline(Path.Combine(launch.ReportDirectory, "child-local", launch.Identity.Generation.ToString())));
            c.UseAether(o =>
            {
                o.HostContractId = launch.Identity.HostContractId; o.GameBuildId = launch.Identity.GameBuildId; o.ReportDirectory = launch.ReportDirectory;
                o.ChildDiagnostics = launch.DiagnosticDelivery ?? new() { Enabled = false };
            });
            c.UseAetherPreviewRuntime(launch, parentId);
        });
        await app.StartAsync();
        var runtime = app.Services.Resolve<AetherRuntimeHost>()!; var pipeline = app.Services.Resolve<DiagnosticPipeline>()!;
        var source = app.Services.Resolve<IEventsSourceFactory>()!.Create(Source);
        using var trace = new Activity("delivery-child").SetIdFormat(ActivityIdFormat.W3C).Start();
        // Console.In's synchronous reader may block before RunAsync first yields.
        var work = Task.Run(() => runtime.RunAsync());
        var flood = Task.Run(async () =>
        {
            await Task.WhenAll(Console.Out.WriteAsync(new string('O', 96 * 1024)), Console.Error.WriteAsync(new string('E', 96 * 1024)));
            var emitted = 0;
            while (!work.IsCompleted)
            {
                for (var i = 0; i < 256; i++)
                {
                    var index = ++emitted;
                    source.Write(DiagnosticLevel.Warning, 790, "Child.Delivery.Flood", "bounded child delivery", () => new Dictionary<string, string?>
                    {
                        ["Index"] = index.ToString(), ["Nonce"] = launch.Identity.Nonce, ["Endpoint"] = launch.Endpoint,
                        ["SessionId"] = "forged-child-context", ["Generation"] = "forged-child-context"
                    });
                }
                await Task.Delay(10);
            }
            return emitted;
        });
        try { await work; }
        finally { await runtime.DisposeAsync(); }
        var count = await flood; await app.StopAsync(); await pipeline.FlushAsync();
        Records.Write(launch.ReportDirectory, $"delivery-child-{launch.Identity.Generation}.json", new
        {
            ProcessId = Environment.ProcessId, ApplicationId = pipeline.ApplicationId, Started = started, Finished = DateTimeOffset.UtcNow,
            TraceId = trace.TraceId.ToHexString(), SpanId = trace.SpanId.ToHexString(), Emitted = count, pipeline.MemoryEvicted,
            Sender = runtime.ChildDiagnosticsStatus, LocalFiles = Directory.GetFiles(pipeline.JsonDirectory!, "*.jsonl", SearchOption.AllDirectories)
                .Select(x => new { Name = Path.GetFileName(x), Bytes = new FileInfo(x).Length }).ToArray(),
            GameLoaded = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Game.Logic")
        });
        return 0;
    }

    internal static async Task<int> RunAsync(string directory)
    {
        Directory.CreateDirectory(directory); var slow = new HeldSink(); var broken = new BrokenSink();
        var options = Pipeline(Path.Combine(directory, "host-local")); options.MemoryCapacity = 512;
        options.SinkFactories = [new Factory<IDiagnosticSink>(() => slow), new Factory<IDiagnosticSink>(() => broken)];
        await using var app = App.Build(c =>
        {
            c.UseDiagnostics(options);
            c.UseAether(o =>
            {
                o.HostContractId = BuildIdentity.HostId; o.GameBuildId = BuildIdentity.PreviewId; o.ReportDirectory = directory; o.ChildDiagnostics = Limits();
                o.RuntimeLaunch = NativeRuntimeLaunch.CurrentProcess("--runtime");
            });
        });
        await app.StartAsync();
        var pipeline = app.Services.Resolve<DiagnosticPipeline>()!;
        var factory = (NativeRuntimeFactory)app.Services.Resolve<IRuntimeFactory>()!;
        var session = Guid.NewGuid(); var generations = new List<object>();
        try
        {
            for (ulong generation = 1; generation <= 3; generation++)
            {
                var identity = new SessionIdentity(session, generation, BuildIdentity.HostId, BuildIdentity.PreviewId, Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"));
                var launch = new SessionLaunch(identity, "engine-delivery-proof-" + Guid.NewGuid().ToString("N"), directory, Behavior, Role: RuntimeRole.EditPreview);
                await using var lease = (NativeRuntimeLease)factory.Create(launch);
                using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                try
                {
                    await lease.LaunchAsync(limit.Token); ulong sequence = 0;
                    async Task<ControlReply> Exchange(CommandKind kind)
                    {
                        var request = ControlRequest.Create(identity, ++sequence, kind);
                        if (kind == CommandKind.LoadScene) { request.SceneId = "delivery-scene"; request.Revision = 73; request.Entities = []; }
                        var reply = await lease.SendAsync(request, limit.Token);
                        Probe.Check(reply.Stamp.Identity() == identity && reply.Stamp.Sequence == unchecked((long)sequence) && reply.ProcessId == lease.ProcessId, "Control correlation changed under delivery flood.");
                        return reply;
                    }
                    Probe.Check((await Exchange(CommandKind.Hello)).Phase == "HelloAccepted", "Hello failed during delivery startup.");
                    Probe.Check((await Exchange(CommandKind.LoadScene)).Revision == 73, "Scene failed during delivery flood.");
                    await Probe.WaitAsync(() => pipeline.Read().Any(r => r.Origin != null && r.ProcessId == lease.ProcessId && r.EventName == "Child.Delivery.Flood") && lease.ChildDiagnosticsStatus?.ChildReportedLost > 0,
                        TimeSpan.FromSeconds(10));
                    for (var command = 0; command < 30; command++)
                    {
                        var reply = await Exchange(command % 2 == 0 ? CommandKind.GetStatus : CommandKind.SetMode);
                        Probe.Check(reply.Phase == "Paused" && reply.Revision == 73 && reply.Tick == 0, "Control state changed under flood.");
                    }
                    var imported = pipeline.Read().Where(r => r.Origin != null && r.ProcessId == lease.ProcessId && r.EventName == "Child.Delivery.Flood").ToArray();
                    Probe.Check(imported.Length > 0, "No child diagnostic arrived.");
                    Probe.Check(imported.All(r => r.ApplicationId == pipeline.ApplicationId && r.Origin!.ApplicationId != pipeline.ApplicationId && r.Origin.Sequence > 0 &&
                        r.Properties["SessionId"] == session.ToString("D") && r.Properties["Generation"] == generation.ToString() && r.Properties["RuntimeRole"] == "EditPreview" &&
                        !r.Properties.Keys.Any(k => k.Equals("Nonce", StringComparison.OrdinalIgnoreCase) || k.Equals("Endpoint", StringComparison.OrdinalIgnoreCase))), "Imported provenance/context or sensitive keys changed.");
                    await Exchange(CommandKind.Stop); await lease.StopAsync();
                    Probe.Check(lease.Reaped && lease.ResourcesReleased && lease.ChildDiagnosticsStatus?.Released == true, "Lease/receiver not released after process reap.");
                    await Records.WaitAsync(Path.Combine(directory, $"delivery-child-{generation}.json"));
                    generations.Add(new
                    {
                        Generation = generation, lease.ProcessId, lease.Reaped, lease.ResourcesReleased, Receiver = lease.ChildDiagnosticsStatus,
                        Controls = sequence, Imported = imported, Stdout = lease.StandardOutputStatus, Stderr = lease.StandardErrorStatus
                    });
                }
                finally { await lease.StopAsync(); }
            }
            Probe.Check(slow.Entered.Task.IsCompleted && pipeline.Health().Any(x => x.Name == "custom-0" && x.Dropped > 0), "Slow sink did not exercise bounded drops.");
            Probe.Check(pipeline.Health().Any(x => x.Name == "custom-1" && x.Failures > 0), "Broken sink did not exercise failure isolation.");
        }
        finally { slow.Release.TrySetResult(); await app.StopAsync(); }
        await pipeline.FlushAsync();
        var result = new { Passed = true, Scenario = "child-diagnostic-delivery", HostPid = Environment.ProcessId, HostApplicationId = pipeline.ApplicationId,
            SessionId = session, Generations = generations, Health = pipeline.Health(), RawFiles = Directory.GetFiles(Path.Combine(directory, "runtime-output"))
                .Select(x => new { Name = Path.GetFileName(x), Bytes = new FileInfo(x).Length }).ToArray() };
        Records.Write(directory, "delivery-report.json", result); Console.WriteLine(JsonSerializer.Serialize(result)); return 0;
    }

    private sealed class HeldSink : IDiagnosticSink
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async ValueTask WriteAsync(DiagnosticRecord record, CancellationToken token) { Entered.TrySetResult(); await Release.Task; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class BrokenSink : IDiagnosticSink
    {
        public ValueTask WriteAsync(DiagnosticRecord record, CancellationToken token) => ValueTask.FromException(new IOException("injected delivery proof sink"));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
