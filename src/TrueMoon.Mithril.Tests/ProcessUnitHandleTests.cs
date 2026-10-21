using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using TrueMoon.Diagnostics;
using TrueMoon.Mithril.Units;

namespace TrueMoon.Mithril.Tests;

public class ProcessUnitHandleTests
{
    [Fact]
    public async Task Start_PreCancelled_DoesNotCreateLaunchInfoOrProcess()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await using var handle = new MissingProcessHandle();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handle.StartAsync(cancellation.Token));
        Assert.Equal(0, handle.InfoCalls);
        Assert.Null(handle.OwnedProcess);
        Assert.True(await handle.StopAsync());
    }

    [Fact]
    public async Task Start_CancelledDuringLaunchPreparation_DoesNotStartProcess()
    {
        using var cancellation = new CancellationTokenSource();
        await using var handle = new MissingProcessHandle { Preparing = cancellation.Cancel };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handle.StartAsync(cancellation.Token));
        Assert.Equal(1, handle.InfoCalls);
        Assert.Null(handle.OwnedProcess);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task Start_NegativeTerminationDelay_RejectsBeforeLaunch(int delay)
    {
        await using var handle = new MissingProcessHandle(delay);
        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => handle.StartAsync());
        Assert.Equal("TerminationDelay", error.ParamName);
        Assert.Equal(0, handle.InfoCalls);
        Assert.Null(handle.OwnedProcess);
    }

    [Fact]
    public async Task Start_MissingExecutable_ReleasesFailedProcessAndPreservesError()
    {
        await using var handle = new MissingProcessHandle();
        await Assert.ThrowsAsync<Win32Exception>(() => handle.StartAsync());
        Assert.Equal(1, handle.InfoCalls);
        Assert.Null(handle.OwnedProcess);
        Assert.True(await handle.StopAsync());
        await Assert.ThrowsAsync<Win32Exception>(() => handle.StartAsync());
        Assert.Equal(2, handle.InfoCalls);
        Assert.Null(handle.OwnedProcess);
    }

    [Fact]
    public async Task Dispose_BeforeStart_IsRepeatableAndRejectsStart()
    {
        var handle = new MissingProcessHandle();
        handle.Dispose();
        await handle.DisposeAsync();
        Assert.True(await handle.StopAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => handle.StartAsync());
        Assert.Equal(0, handle.InfoCalls);
        Assert.Null(handle.OwnedProcess);
    }

    [Theory]
    [InlineData("graceful", true)]
    [InlineData("timeout", false)]
    [InlineData("cancel-stop", false)]
    [InlineData("dispose", true)]
    [InlineData("async-dispose", true)]
    [InlineData("already-exited", false)]
    [InlineData("same-name", true)]
    [InlineData("duplicate-start", true)]
    [InlineData("throwing-sink", true)]
    [InlineData("closed-stdin", false)]
    public async Task OwnedChild_ShutdownScenario_ReapsProcessAndReleasesHandle(string scenario, bool graceful)
    {
        var fixture = Path.Combine(AppContext.BaseDirectory, "process-fixture", "MithrilLifecycleProbe.dll");
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(fixture);
        start.ArgumentList.Add(scenario);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Probe failed to start.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(25));
            var stdout = await output.WaitAsync(TimeSpan.FromSeconds(5));
            var stderr = await error.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(process.ExitCode == 0, $"Probe exit {process.ExitCode}: {stderr}\n{stdout}");
            Assert.Empty(stderr);
            var result = Assert.Single(stdout.Split('\n'), line => line.StartsWith("RESULT ", StringComparison.Ordinal));
            using var report = JsonDocument.Parse(result[7..]);
            var root = report.RootElement;
            Assert.Equal(scenario, root.GetProperty("Scenario").GetString());
            Assert.Equal(process.Id, root.GetProperty("ParentPid").GetInt32());
            Assert.NotEqual(process.Id, root.GetProperty("ChildPid").GetInt32());
            Assert.Equal(7, root.GetProperty("UnitId").GetInt32());
            Assert.Equal(process.Id, root.GetProperty("ChildParentPid").GetInt32());
            Assert.True(root.GetProperty("ExitSeen").GetBoolean());
            Assert.True(root.GetProperty("HandleReleased").GetBoolean());
            Assert.True(root.GetProperty("RepeatedStopSucceeded").GetBoolean());
            Assert.Equal(graceful, root.GetProperty("GracefulAck").GetBoolean());
            Assert.Equal(scenario == "already-exited" ? 1 : 0, root.GetProperty("ExitCallbacks").GetInt32());
            Assert.Equal(scenario == "same-name", root.GetProperty("SentinelSurvived").GetBoolean());
            var exitCode = root.GetProperty("ExitCode").GetInt32();
            if (scenario is "timeout" or "cancel-stop") Assert.NotEqual(0, exitCode);
            else Assert.Equal(0, exitCode);
            var traces = root.GetProperty("Diagnostics").Deserialize<UnitProcessDiagnostic[]>()!;
            var started = Assert.Single(traces, x => x.Stage == UnitProcessStage.Started);
            Assert.Equal(root.GetProperty("ChildPid").GetInt32(), started.ProcessId);
            Assert.Equal(process.Id, started.ParentProcessId);
            Assert.Equal(1, started.Generation);
            Assert.NotEqual(Guid.Empty, started.RunId);
            Assert.All(traces, x => Assert.Equal(started.RunId, x.RunId));
            var released = Assert.Single(traces, x => x.Stage == UnitProcessStage.ProcessReleased);
            Assert.Equal(started.ProcessId, released.ProcessId);
            Assert.Equal(exitCode, released.ExitCode);
            Assert.Equal(scenario == "already-exited" ? "NaturalExit" : scenario is "timeout" or "cancel-stop" ? "Killed" : "Graceful", released.Reason);
            Assert.Equal(scenario is "timeout" or "cancel-stop" ? 1 : 0, traces.Count(x => x.Stage == UnitProcessStage.Killing));
            Assert.Equal(scenario == "already-exited" ? 1 : 0, traces.Count(x => x.Stage == UnitProcessStage.Terminal));
            Assert.DoesNotContain(traces, x => x.Stage is UnitProcessStage.StopFailed or UnitProcessStage.SequenceFailed);
            Assert.Equal(UnitProcessStage.Stopped, traces[^1].Stage);
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

    private sealed class MissingProcessHandle(int? delay = null)
        : ChildProcessUnitHandle(new UnitConfiguration(1) { RestartPolicy = UnitRestartPolicy.Never, TerminationDelay = delay }, NullEvents.Instance)
    {
        internal int InfoCalls { get; private set; }
        internal Action? Preparing { get; init; }
        internal Process? OwnedProcess => Process;
        protected override Task<ProcessStartInfo> GetProcessStartInfoAsync(CancellationToken cancellationToken = default)
        {
            InfoCalls++;
            Preparing?.Invoke();
            return Task.FromResult(new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "missing-mithril-process.exe"))
            {
                UseShellExecute = false, RedirectStandardInput = true, CreateNoWindow = true
            });
        }
    }

    private sealed class NullEvents : IEventsSource
    {
        internal static readonly NullEvents Instance = new();
        public string Name => "MithrilTests";
        public Activity StartActivity(string? details = null, string? category = null, string? caller = null) => new(Name);
        public void StopActivity(Activity activity) => activity.Stop();
        public void Write(Func<object>? func, string? category = null, string? caller = null) { }
    }
}
