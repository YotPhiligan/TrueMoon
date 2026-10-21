using System.Diagnostics;
using System.Text.Json;

namespace TrueMoon.Enerit.Tests.Pipes;
public class AppUnitsProcessTests
{
    public static IEnumerable<object[]> Cases => from provider in new[] { "cobalt", "di" }
        from scenario in new[] { "payload", "concurrent", "cancel-request", "remote-error", "restart", "budget", "stop-pending", "startup-failure", "parent-exit" }
        select new object[] { scenario, provider };
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task AppUnits_TypedPipes_RestartCancellationFailureAndShutdown(string scenario, string provider)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "app-pipes-results", Guid.NewGuid().ToString("N"));
        var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "app-pipes-fixture", "AppUnitsPipesProbe.dll"));
        info.ArgumentList.Add(scenario); info.ArgumentList.Add(provider); info.ArgumentList.Add(directory);
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(40));
            var stdout = await output.WaitAsync(TimeSpan.FromSeconds(5)); var stderr = await errors.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(process.ExitCode == 0, $"Exit{process.ExitCode}: {stderr}\n{stdout}"); Assert.Empty(stderr);
            var line = Assert.Single(stdout.Split('\n'), l => l.StartsWith("RESULT ", StringComparison.Ordinal));
            using var document = JsonDocument.Parse(line[7..]); var result = document.RootElement;
            Assert.Equal(scenario, result.GetProperty("Scenario").GetString()); Assert.Equal(provider, result.GetProperty("Provider").GetString());
            Assert.Equal(process.Id, result.GetProperty("ParentPid").GetInt32());
            var pids = result.GetProperty("ChildPids").EnumerateArray().Select(x => x.GetInt32()).ToArray();
            Assert.Equal(scenario is "restart" or "budget" ? 2 : 1, pids.Length);
            Assert.Equal(pids.Length, pids.Distinct().Count()); Assert.DoesNotContain(process.Id, pids);
            var codes = result.GetProperty("ExitCodes").EnumerateArray().Select(x => x.GetInt32()).ToArray(); Assert.Equal(pids.Length, codes.Length);
            if (scenario is "restart" or "budget") Assert.NotEqual(0, codes[0]);
            if (scenario == "budget") Assert.NotEqual(0, codes[^1]); else Assert.Equal(0, codes[^1]);
            Assert.Equal(scenario is "restart" or "budget" ? 1 : 0, result.GetProperty("Restarts").GetInt32());
            Assert.Equal(scenario is "restart" or "budget" or "stop-pending" ? 1 : 0, result.GetProperty("FailedRequests").GetInt32());
            Assert.Equal(scenario == "budget", result.GetProperty("AppCancelled").GetBoolean()); Assert.True(result.GetProperty("Cleanup").GetBoolean());
            Assert.Equal(scenario != "parent-exit", result.GetProperty("ClientCleanupObserved").GetBoolean());
            Assert.Equal(scenario == "parent-exit" ? -1 : 0, result.GetProperty("Pending").GetInt32());
            Assert.Equal(scenario != "parent-exit", result.GetProperty("WorkersCompleted").GetBoolean());
            var expectedChecks = scenario switch { "payload" or "remote-error" => 3, "concurrent" => 33, "cancel-request" or "restart" or "budget" => 2, _ => 1 };
            Assert.Equal(expectedChecks, result.GetProperty("Checks").GetInt32());
        }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); } }
    }
}
