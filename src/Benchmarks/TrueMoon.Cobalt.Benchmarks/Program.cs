using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using TrueMoon.Cobalt.Benchmarks;

if (args is ["--verify"])
{
    await WorkloadVerification.VerifyAsync();
    Console.WriteLine("COBALT_BASELINE_VERIFIED");
    return 0;
}
if (args is ["--cold-sample"])
{
    await ColdStartup.MeasureAsync();
    return 0;
}
if (args is ["--verify-provider", var name])
{
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(await ComparisonVerification.VerifyAsync(ProviderSelection.Parse(name))));
    return 0;
}
if (args is ["--cold-sample", var provider])
{
    await ColdStartup.MeasureAsync(ProviderSelection.Parse(provider));
    return 0;
}
var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly)
    .Run(args, DefaultConfig.Instance.AddExporter(JsonExporter.Full)
        .AddJob(Job.Default.WithPowerPlan(PowerPlan.UserPowerPlan).AsMutator())).ToArray();
if (args.Contains("--help") || args.Contains("--list")) return 0;
return summaries.Length > 0 && summaries.All(s => !s.HasCriticalValidationErrors &&
    s.Reports.All(r => r.Success && r.ResultStatistics != null)) ? 0 : 1;
