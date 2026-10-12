using System.Diagnostics;
using System.Text.Json;

namespace TrueMoon.Mithril.Tests;

public class CompositionProcessTests
{
    public static IEnumerable<object[]> Cases =>
        from provider in new[] { "cobalt", "di" }
        from scenario in new[] { "main", "composition", "external", "startup-failure", "cancel-start", "control-eof", "parent-exit" }
        select new object[] { scenario, provider };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task AppUnits_OwnedProcessesAndLocalHosting_CleanupOnBothProviders(string scenario, string provider)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "composition-results", Guid.NewGuid().ToString("N"));
        var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "composition-fixture", "MithrilCompositionProbe.dll"));
        info.ArgumentList.Add(scenario); info.ArgumentList.Add(provider); info.ArgumentList.Add(directory);
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(40));
            var stdout = await output.WaitAsync(TimeSpan.FromSeconds(5)); var stderr = await errors.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(process.ExitCode == 0, $"Exit{process.ExitCode}: {stderr}\n{stdout}");
            Assert.Empty(stderr);
            var line = Assert.Single(stdout.Split('\n'), l => l.StartsWith("RESULT ", StringComparison.Ordinal));
            using var report = JsonDocument.Parse(line[7..]); var result = report.RootElement;
            Assert.Equal(scenario, result.GetProperty("Scenario").GetString());
            Assert.Equal(provider, result.GetProperty("Provider").GetString());
            Assert.Equal(process.Id, result.GetProperty("ParentPid").GetInt32());
            Assert.Equal(0, result.GetProperty("ExitCode").GetInt32());
            Assert.True(result.GetProperty("Cleanup").GetBoolean());
            Assert.True(result.GetProperty("ServiceStopped").GetBoolean());
            Assert.Equal(scenario == "main", result.GetProperty("SameProcess").GetBoolean());
            if (scenario == "main") Assert.Equal(process.Id, result.GetProperty("ChildPid").GetInt32());
            else Assert.NotEqual(process.Id, result.GetProperty("ChildPid").GetInt32());
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
        }
    }
}
