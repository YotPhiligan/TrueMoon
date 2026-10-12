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
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
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
