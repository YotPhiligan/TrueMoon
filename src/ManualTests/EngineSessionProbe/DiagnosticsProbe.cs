using System.Diagnostics;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Aether;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Sessions;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;

namespace EngineSessionProbe;

internal static class DiagnosticsProbe
{
    internal static async Task<object> RunAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        await using var app = App.Build(context =>
        {
            context.UseDiagnostics(new DiagnosticPipelineOptions
            {
                JsonDirectory = Path.Combine(directory, "events"),
                MemoryCapacity = 256, QueueCapacity = 32, ReservedErrorCapacity = 4
            });
            context.UseAether(options =>
            {
                options.HostContractId = BuildIdentity.HostId; options.GameBuildId = BuildIdentity.GameId;
                options.ReportDirectory = directory;
            });
        });
        await app.StartAsync();
        var host = app.Services.Resolve<TrueMoon.Aether.Hosting.AetherHost>()!;
        var factory = (NativeRuntimeFactory)app.Services.Resolve<IRuntimeFactory>()!;
        var pipeline = app.Services.Resolve<DiagnosticPipeline>()!;
        var captures = new List<object>();
        try
        {
            // More generations than the four file slots exercise retention reuse.
            for (var generation = 1; generation <= 6; generation++)
            {
                await host.Supervisor.StartAsync(new("diagnostic-scene", generation, []), "diagnostics-output");
                var lease = factory.Leases.Last();
                await Records.WaitAsync(Path.Combine(directory, "output-ready-" + generation + ".json"));
                using var wait = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                while (lease.StandardOutputStatus?.Text.EndsWith("stdout-tail", StringComparison.Ordinal) != true ||
                       lease.StandardErrorStatus?.Text.EndsWith("stderr-tail", StringComparison.Ordinal) != true)
                    await Task.Delay(5, wait.Token);
                var watch = Stopwatch.StartNew();
                await host.Supervisor.StepAsync();
                await host.Supervisor.StopAsync();
                var stdout = lease.StandardOutputStatus!; var stderr = lease.StandardErrorStatus!;
                Probe.Check(stdout.TotalCharacters > 2 * 1024 * 1024 && stderr.TotalCharacters > 2 * 1024 * 1024,
                    "Both pipes must drain more than two Mi characters.");
                Probe.Check(stdout.Text.Length <= 32768 && stderr.Text.Length <= 32768 &&
                    stdout.DroppedCharacters > 0 && stderr.DroppedCharacters > 0, "Runtime tail was not bounded.");
                Probe.Check(stdout.FileBytesWritten <= 1024 * 1024 && stderr.FileBytesWritten <= 1024 * 1024 &&
                    stdout.FileBytesDropped > 0 && stderr.FileBytesDropped > 0, "Runtime file budget/loss missing.");
                Probe.Check(stdout.FileError is null && stderr.FileError is null && lease.Reaped && lease.ResourcesReleased,
                    "Capture or process cleanup failed.");
                captures.Add(new { Generation = generation, lease.ProcessId, stdout.TotalCharacters,
                    StdoutDropped = stdout.DroppedCharacters, StderrDropped = stderr.DroppedCharacters, StopMilliseconds = watch.Elapsed.TotalMilliseconds });
            }
            var failure = await Probe.FailureAsync(host.Supervisor.StartAsync(new("diagnostic-scene", 7, []), "launch-failure"));
            Probe.Check(failure is not null && host.Supervisor.Status.Failure?.Phase == "Launch", "Launch cause hidden.");
        }
        finally { await host.Supervisor.StopAsync(); await app.StopAsync(); }
        await pipeline.FlushAsync();
        var records = pipeline.Read();
        Probe.Check(records.Any(record => record.EventName == "Session.StateChanged" && record.Level == DiagnosticLevel.Error &&
            record.Properties.GetValueOrDefault("Phase") == "Launch"), "Structured session failure missing.");
        Probe.Check(records.Any(record => record.EventName == "Runtime.Process" && record.Properties.ContainsKey("RunId") &&
            record.Properties.ContainsKey("SessionId")), "Process/session correlation missing.");
        Probe.Check(records.All(record => !record.Properties.ContainsKey("Nonce") && !record.Properties.ContainsKey("Endpoint")), "Private bootstrap fields leaked.");
        var files = Directory.GetFiles(Path.Combine(directory, "runtime-output"), "*.log");
        Probe.Check(files.Length == 8 && files.All(file => new FileInfo(file).Length <= 1024 * 1024), "Runtime file retention exceeded.");
        var result = new { Passed = true, Scenario = "diagnostics", EditorPid = Environment.ProcessId,
            Generations = captures, Files = files.Select(file => new { Name = Path.GetFileName(file), Bytes = new FileInfo(file).Length }),
            StructuredRecords = records.Count, SinkHealth = pipeline.Health(),
            Reaped = factory.Leases.All(lease => lease.Reaped && lease.ResourcesReleased) };
        Records.Write(directory, "diagnostics.json", result);
        return result;
    }
}
