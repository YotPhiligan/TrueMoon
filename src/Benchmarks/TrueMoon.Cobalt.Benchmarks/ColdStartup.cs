using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;
using TrueMoon.Services;

namespace TrueMoon.Cobalt.Benchmarks;

internal static class ColdStartup
{
    // One invocation per fresh process. Module initializers and process/runtime launch
    // precede this boundary; this is first in-process App Build+Start, not OS cold boot.
    public static async Task MeasureAsync(Provider? provider = null)
    {
        var scenario = provider == null ? new Scenario() : null;
        var comparison = provider == null ? null : new ComparisonScenario(provider.Value);
        var before = GC.GetTotalAllocatedBytes(precise: true);
        var started = Stopwatch.GetTimestamp();
        var app = comparison == null ? scenario!.CreateApp() : comparison.CreateApp();
        try
        {
            await app.StartAsync();
            var elapsed = Stopwatch.GetElapsedTime(started);
            var allocated = GC.GetTotalAllocatedBytes(precise: true) - before;
            var worker = ((IServiceResolver)app.Services).Resolve<Worker>();
            WorkloadVerification.Require(worker.Starts == 1 && worker.Stops == 0, "Cold startup incomplete.");
            await app.StopAsync();
            await app.DisposeAsync();
            var expectedDisposals = provider == Provider.Microsoft ? 3 : 1;
            WorkloadVerification.Require(worker.Stops == 1 && worker.DisposeCalls == expectedDisposals && worker.Resource.DisposeCalls == 1 &&
                (comparison?.Graph.Settings ?? scenario!.Settings).DisposeCalls == 0, "Cold sample cleanup failed.");
            Console.WriteLine(JsonSerializer.Serialize(new {
                Completed = true, Graph = "cobalt-app-v1", ProcessId = Environment.ProcessId,
                Provider = (provider ?? global::TrueMoon.Cobalt.Benchmarks.Provider.Cobalt).ToString(),
                BuildStartMilliseconds = elapsed.TotalMilliseconds, BuildStartAllocatedBytes = allocated,
                Runtime = RuntimeInformation.FrameworkDescription, OS = RuntimeInformation.OSDescription,
                Architecture = RuntimeInformation.ProcessArchitecture.ToString(), ServerGC = GCSettings.IsServerGC
            }));
        }
        finally { await app.DisposeAsync(); }
    }
}
