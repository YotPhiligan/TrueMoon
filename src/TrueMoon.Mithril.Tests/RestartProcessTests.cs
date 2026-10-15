using System.Diagnostics;
using System.Text.Json;
using TrueMoon.Mithril.Units;

namespace TrueMoon.Mithril.Tests;

public class RestartProcessTests
{
    public static IEnumerable<object[]> Cases =>
    [
        ["never-normal", new[] { 0 }, 0, 1], ["never-crash", new[] { 23 }, 0, 1],
        ["once-normal", new[] { 0, 0 }, 1, 1], ["once-crash", new[] { 23, 23 }, 1, 1],
        ["oncrash-normal", new[] { 0 }, 0, 1], ["oncrash-mixed", new[] { 23, 0 }, 1, 1],
        ["oncrash-budget", new[] { 23, 23, 23 }, 2, 1],
        ["always-normal", new[] { 0, 0, 0 }, 2, 1], ["always-crash", new[] { 23, 23, 23 }, 2, 1],
        ["limit-zero", new[] { 23 }, 0, 1], ["default-budget-delay", new[] { 0, 0, 0, 0 }, 3, 1],
        ["delay", new[] { 0, 0 }, 1, 1], ["stop-delay", new[] { 0 }, 0, 0],
        ["stop-preparation", new[] { 0 }, 1, 0], ["stop-restarted", new[] { 0, 0 }, 1, 0],
        ["dispose-delay", new[] { 0 }, 0, 0], ["restart-failure", new[] { 0 }, 1, 1],
        ["controller-lifetime", new[] { 0, 0 }, 1, 1], ["reentrant-terminal-dispose", new[] { 0 }, 0, 1],
        ["concurrent-stop", new[] { 0, 0 }, 1, 0]
    ];

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Restart_OwnedSequence_EnforcesPolicyBudgetDelayAndStop(string scenario, int[] exitCodes, int restarts, int callbacks)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "restart-results", Guid.NewGuid().ToString("N"));
        var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "restart-fixture", "MithrilRestartProbe.dll"));
        info.ArgumentList.Add(scenario); info.ArgumentList.Add(directory);
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(35));
            var stdout = await output.WaitAsync(TimeSpan.FromSeconds(5)); var stderr = await errors.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(process.ExitCode == 0, $"Exit{process.ExitCode}: {stderr}\n{stdout}");
            Assert.Empty(stderr);
            var line = Assert.Single(stdout.Split('\n'), l => l.StartsWith("RESULT ", StringComparison.Ordinal));
            using var document = JsonDocument.Parse(line[7..]); var result = document.RootElement;
            Assert.Equal(scenario, result.GetProperty("Scenario").GetString());
            Assert.Equal(process.Id, result.GetProperty("ParentPid").GetInt32());
            Assert.Equal(exitCodes, result.GetProperty("ExitCodes").EnumerateArray().Select(x => x.GetInt32()));
            var pids = result.GetProperty("Pids").EnumerateArray().Select(x => x.GetInt32()).ToArray();
            Assert.Equal(exitCodes.Length, pids.Length);
            Assert.Equal(pids.Length, pids.Distinct().Count());
            Assert.DoesNotContain(process.Id, pids);
            Assert.Equal(restarts, result.GetProperty("Restarts").GetInt32());
            Assert.Equal(callbacks, result.GetProperty("Callbacks").GetInt32());
            Assert.Equal(scenario is "restart-failure" or "stop-preparation" ? 2 : exitCodes.Length, result.GetProperty("Attempts").GetInt32());
            Assert.True(result.GetProperty("Cleanup").GetBoolean());
            Assert.True(result.GetProperty("Completion").GetBoolean());
            if (scenario == "restart-failure") Assert.Equal("Win32Exception", result.GetProperty("Error").GetString());
            else Assert.Equal(JsonValueKind.Null, result.GetProperty("Error").ValueKind);
            if (scenario is "delay" or "default-budget-delay")
                Assert.All(result.GetProperty("DelayMilliseconds").EnumerateArray(), delay => Assert.True(delay.GetDouble() >= (scenario == "delay" ? 95 : 245)));
            AssertDiagnostics(result, scenario, exitCodes, restarts, callbacks);
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
        }
    }

    private static void AssertDiagnostics(JsonElement result, string scenario, int[] codes, int restarts, int callbacks)
    {
        var traces = result.GetProperty("Diagnostics").EnumerateArray().ToArray();
        JsonElement[] Stage(string stage) => traces.Where(x => x.GetProperty("Stage").GetString() == stage).ToArray();
        var runId = Assert.Single(traces.Select(x => x.GetProperty("RunId").GetGuid()).Distinct());
        Assert.NotEqual(Guid.Empty, runId);
        var expectedPolicy = scenario.StartsWith("never-", StringComparison.Ordinal) || scenario == "reentrant-terminal-dispose" ? "Never"
            : scenario.StartsWith("once-", StringComparison.Ordinal) || scenario is "delay" or "controller-lifetime" ? "Once"
            : scenario.StartsWith("oncrash-", StringComparison.Ordinal) ? "OnCrash" : "Always";
        Assert.All(traces, trace =>
        {
            Assert.Equal(7, trace.GetProperty("UnitIndex").GetInt32());
            Assert.Equal("unit7", trace.GetProperty("UnitName").GetString());
            Assert.Equal(result.GetProperty("ParentPid").GetInt32(), trace.GetProperty("ParentProcessId").GetInt32());
            Assert.Equal(scenario == "default-budget-delay" ? 3 : scenario == "limit-zero" ? 0 : 2, trace.GetProperty("RestartLimit").GetInt32());
            Assert.Equal(scenario is "stop-delay" or "dispose-delay" ? 60000 : scenario == "default-budget-delay" ? 250 : scenario == "delay" ? 100 : 0,
                trace.GetProperty("RestartDelay").GetInt32());
            Assert.Equal(3000, trace.GetProperty("TerminationDelay").GetInt32());
            Assert.Equal(expectedPolicy, trace.GetProperty("RestartPolicy").GetString());
        });
        var started = Stage("Started");
        Assert.Equal(codes.Length, started.Length);
        Assert.Equal(Enumerable.Range(1, codes.Length), started.Select(x => x.GetProperty("Generation").GetInt32()));
        Assert.Equal(result.GetProperty("Pids").EnumerateArray().Select(x => x.GetInt32()), started.Select(x => x.GetProperty("ProcessId").GetInt32()));
        Assert.Equal(Enumerable.Range(0, codes.Length), started.Select(x => x.GetProperty("RestartCount").GetInt32()));
        Assert.Equal(started.Length, Stage("ProcessReleased").Length);
        Assert.Equal(started.Select(x => x.GetProperty("ProcessId").GetInt32()), Stage("ProcessReleased").Select(x => x.GetProperty("ProcessId").GetInt32()));
        Assert.Equal(codes, Stage("ProcessReleased").Select(x => x.GetProperty("ExitCode").GetInt32()));
        Assert.Equal(restarts, Stage("Restarting").Length);
        Assert.Equal(restarts + (scenario is "stop-delay" or "dispose-delay" ? 1 : 0), Stage("RestartScheduled").Length);
        var stoppedChild = scenario is "stop-restarted" or "concurrent-stop";
        Assert.Equal(codes.Take(codes.Length - (stoppedChild ? 1 : 0)), Stage("Exited").Select(x => x.GetProperty("ExitCode").GetInt32()));
        Assert.Equal(callbacks, Stage("Terminal").Length);
        if (callbacks == 1)
        {
            var expectedReason = scenario == "restart-failure" ? "Failure" : scenario is "limit-zero" or "default-budget-delay" or "oncrash-budget" or "always-normal" or "always-crash" ? "RestartLimit" : "Policy";
            Assert.Equal(expectedReason, Stage("Terminal")[0].GetProperty("Reason").GetString());
        }
        var failed = Stage("LaunchFailed");
        Assert.Equal(scenario is "restart-failure" or "stop-preparation" ? 1 : 0, failed.Length);
        if (failed.Length == 1)
        {
            Assert.Equal(2, failed[0].GetProperty("Generation").GetInt32());
            Assert.Equal(JsonValueKind.Null, failed[0].GetProperty("ProcessId").ValueKind);
            Assert.Equal(scenario == "stop-preparation" ? "Cancelled" : "LaunchFailure", failed[0].GetProperty("Reason").GetString());
        }
        Assert.Equal(scenario == "restart-failure" ? 1 : 0, Stage("SequenceFailed").Length);
        if (scenario == "restart-failure")
        {
            var failure = Assert.Single(Stage("SequenceFailed"));
            Assert.Equal("RestartFailure", failure.GetProperty("Reason").GetString());
            Assert.Equal("Win32Exception", failure.GetProperty("ErrorType").GetString());
        }
        Assert.Empty(Stage("StopFailed"));
        Assert.Empty(Stage("Killing"));
        Assert.NotEmpty(Stage("Stopped"));
        var firstStop = Array.FindIndex(traces, x => x.GetProperty("Stage").GetString() == "StopRequested");
        Assert.True(firstStop >= 0);
        Assert.DoesNotContain(traces.Skip(firstStop), x => x.GetProperty("Stage").GetString() is "LaunchStarting" or "Started" or "Restarting");
        // Each successful attempt is ordered independently of concurrent/reentrant Stop notifications.
        foreach (var start in started)
        {
            var generation = start.GetProperty("Generation").GetInt32();
            var attempt = traces.Where(x => x.GetProperty("Generation").GetInt32() == generation).Select(x => x.GetProperty("Stage").GetString()).ToArray();
            Assert.True(Array.IndexOf(attempt, "LaunchStarting") < Array.IndexOf(attempt, "Started"));
            Assert.True(Array.IndexOf(attempt, "Started") < Array.IndexOf(attempt, "ProcessReleased"));
        }
    }

    [Theory]
    [InlineData(true, -1)]
    [InlineData(true, int.MinValue)]
    [InlineData(false, -1)]
    [InlineData(false, int.MinValue)]
    public async Task Restart_NegativeSettings_RejectBeforeAnyLaunchInHandleAndController(bool limit, int value)
    {
        var config = new UnitConfiguration(1) { RestartPolicy = UnitRestartPolicy.Never };
        if (limit) config.RestartLimit = value; else config.RestartDelay = value;
        using var lifetime = new TestLifetime();
        var parameter = limit ? "RestartLimit" : "RestartDelay";
        Assert.Equal(parameter, Assert.Throws<ArgumentOutOfRangeException>(() => new UnitsController(new TestEvents<UnitsController>(), lifetime, [config])).ParamName);
        await using var handle = new GuardHandle(config);
        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => handle.StartAsync());
        Assert.Equal(parameter, error.ParamName);
        Assert.Equal(0, handle.InfoCalls);
        Assert.Null(handle.OwnedProcess);
    }
    private sealed class GuardHandle(IUnitConfiguration config) : ProcessUnitHandle(config, new TestEvents<UnitsController>())
    {
        internal int InfoCalls;
        internal Process? OwnedProcess => Process;
        protected override Task<ProcessStartInfo> GetProcessStartInfoAsync(CancellationToken token = default)
        { InfoCalls++; throw new InvalidOperationException("Guard should precede launch preparation."); }
    }
}
