using System.Diagnostics;
using System.Text.Json;

namespace TrueMoon.Enerit.Tests.Pipes;

public class PipesProcessTests
{
    [Theory]
    [InlineData("payload", 26)]
    [InlineData("concurrent", 33)]
    [InlineData("cancellation", 6)]
    [InlineData("disconnect", 3)]
    [InlineData("client-dispose", 3)]
    [InlineData("server-rebind", 3)]
    public async Task OwnProcesses_GeneratedPipesContract_CleansPendingWorkersAndChild(string scenario, int checks)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "enerit-fixture", "EneritPipesProbe.dll"));
        start.ArgumentList.Add(scenario);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Probe start failed.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(40));
            var stdout = await output.WaitAsync(TimeSpan.FromSeconds(5));
            var stderr = await error.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(process.ExitCode == 0, $"Exit {process.ExitCode}: {stderr}\n{stdout}");
            Assert.Empty(stderr);
            var record = Assert.Single(stdout.Split('\n'), line => line.StartsWith("RESULT ", StringComparison.Ordinal));
            using var report = JsonDocument.Parse(record[7..]);
            var result = report.RootElement;
            Assert.Equal(scenario, result.GetProperty("Scenario").GetString());
            Assert.Equal(checks, result.GetProperty("Checks").GetInt32());
            Assert.Equal(process.Id, result.GetProperty("ParentPid").GetInt32());
            Assert.NotEqual(process.Id, result.GetProperty("ChildPid").GetInt32());
            Assert.Contains($"enerit-probe-{process.Id}-", result.GetProperty("Endpoint").GetString());
            Assert.Equal(0, result.GetProperty("PendingAfterDispose").GetInt32());
            Assert.True(result.GetProperty("ClientWorkersCompleted").GetBoolean());
            Assert.True(result.GetProperty("ChildExited").GetBoolean());
            Assert.Equal(0, result.GetProperty("ChildExitCode").GetInt32());
            Assert.True(result.GetProperty("HandleReleased").GetBoolean());
            if (scenario == "server-rebind") Assert.True(result.GetProperty("Connections").GetInt32() >= 2);
            if (scenario == "cancellation")
            {
                Assert.Equal(1, result.GetProperty("Connections").GetInt32());
                Assert.Equal(1, result.GetProperty("LateResponses").GetInt32());
            }
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
    }
}
