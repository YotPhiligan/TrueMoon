using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Diagnostics;
using TrueMoon.Mithril;
using TrueMoon.Mithril.Units;

if (args is ["--child", var mode])
    return await RunChildAsync(mode);
if (args is not [var scenario] || !Probe.Scenarios.Contains(scenario))
{
    Console.Error.WriteLine($"Usage: MithrilLifecycleProbe <{string.Join('|', Probe.Scenarios)}>");
    return 2;
}
try
{
    var report = await Probe.RunAsync(scenario);
    Console.WriteLine("RESULT " + JsonSerializer.Serialize(report));
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine(error);
    return 1;
}

static async Task<int> RunChildAsync(string mode)
{
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        Pid = Environment.ProcessId,
        UnitId = Environment.GetEnvironmentVariable("-u"),
        ParentId = Environment.GetEnvironmentVariable("-p")
    }));
    try
    {
        while (await Console.In.ReadLineAsync(deadline.Token) is { } line)
        {
            if (mode == "exit" && line == "exit-now") return 0;
            if (line != UnitCodes.Exit || mode == "ignore") continue;
            Console.WriteLine("ACK qe");
            return 0;
        }
        // A broken parent pipe must not turn into an unbounded EOF spin.
        return 3;
    }
    catch (OperationCanceledException) { return 4; }
}

internal static class Probe
{
    internal static readonly string[] Scenarios =
    ["graceful", "timeout", "cancel-stop", "dispose", "async-dispose", "already-exited", "same-name", "duplicate-start", "throwing-sink"];

    internal static async Task<Report> RunAsync(string scenario)
    {
        ProbeEvents.Instance.ThrowAfterWrite = scenario == "throwing-sink";
        var configuration = new UnitConfiguration(7)
        {
            HostingPolicy = UnitHostingPolicy.ChildProcess,
            StartupPolicy = UnitStartupPolicy.Immediate,
            LifetimePolicy = UnitLifetimePolicy.App,
            RestartPolicy = UnitRestartPolicy.Never,
            TerminationDelay = scenario is "timeout" or "cancel-stop" ? 150 : 3000
        };
        await using var handle = new ObservedChild(configuration,
            scenario is "timeout" or "cancel-stop" ? "ignore" : scenario == "already-exited" ? "exit" : "graceful");
        var exits = 0;
        var unexpectedExit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        handle.OnExit(_ => { Interlocked.Increment(ref exits); unexpectedExit.TrySetResult(); });
        Ensure(await handle.StartAsync() is true, "StartAsync failed.");
        var process = handle.OwnedProcess!;
        using var observer = Process.GetProcessById(process.Id);
        // Retain the OS handle while the child is alive, including after its PID disappears.
        _ = observer.SafeHandle;
        var line = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5));
        using var identity = JsonDocument.Parse(line ?? throw new InvalidOperationException("No child readiness record."));
        Ensure(identity.RootElement.GetProperty("Pid").GetInt32() == observer.Id, "Child PID mismatch.");
        Ensure(identity.RootElement.GetProperty("UnitId").GetString() == "7", "Unit identity was not forwarded.");
        Ensure(identity.RootElement.GetProperty("ParentId").GetString() == $"{Environment.ProcessId}", "Parent identity mismatch.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        var survived = false;
        if (scenario == "same-name")
        {
            // This owned sentinel has the same apphost name, but is not owned by this controller.
            // The controller must not enumerate/kill other instances by executable name.
            using var controller = new UnitsController(ProbeEvents.Instance, new ProbeLifetime());
            await controller.StartAsync();
            observer.Refresh();
            survived = !observer.HasExited;
            Ensure(survived, "Controller killed another same-name application instance.");
        }
        if (scenario == "duplicate-start")
        {
            try
            {
                await handle.StartAsync();
                throw new InvalidOperationException("Duplicate StartAsync was accepted.");
            }
            catch (InvalidOperationException exception) when (exception.Message.Contains("already started")) { }
            Ensure(handle.OwnedProcess!.Id == observer.Id, "Duplicate start replaced the owned process.");
        }
        if (scenario == "already-exited")
        {
            await process.StandardInput.WriteLineAsync("exit-now");
            await process.StandardInput.FlushAsync();
            await observer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await unexpectedExit.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Ensure(exits == 1, "Natural exit did not emit exactly one callback.");
        }
        var stopwatch = Stopwatch.StartNew();
        switch (scenario)
        {
            case "dispose": handle.Dispose(); break;
            case "async-dispose": await handle.DisposeAsync(); break;
            default:
                using (var cancellation = new CancellationTokenSource())
                {
                    if (scenario == "cancel-stop") cancellation.Cancel();
                    Ensure(await handle.StopAsync(cancellation.Token) is true, "StopAsync failed.");
                }
                break;
        }
        stopwatch.Stop();
        observer.Refresh();
        Ensure(observer.HasExited, "Stop/dispose returned before child exit.");
        Ensure(handle.OwnedProcess is null, "Process handle was not released.");
        Ensure(await handle.StopAsync() is true, "Repeated stop failed.");
        handle.Dispose();
        await handle.DisposeAsync();
        var text = await output.WaitAsync(TimeSpan.FromSeconds(5));
        Ensure(string.IsNullOrEmpty(await error.WaitAsync(TimeSpan.FromSeconds(5))), "Child wrote stderr.");
        var graceful = text.Contains("ACK qe", StringComparison.Ordinal);
        var forced = scenario is "timeout" or "cancel-stop";
        Ensure(graceful == (!forced && scenario != "already-exited"),
            "Graceful acknowledgement did not match shutdown mode.");
        Ensure(forced ? observer.ExitCode != 0 : observer.ExitCode == 0, "Unexpected child exit code.");
        if (scenario != "already-exited") Ensure(exits == 0, "Intentional stop emitted an unexpected exit callback.");
        return new Report(scenario, Environment.ProcessId, observer.Id, 7, Environment.ProcessId,
            observer.ExitCode, true, handle.OwnedProcess is null, graceful, true, exits, survived, stopwatch.ElapsedMilliseconds,
            ProbeEvents.Instance.Diagnostics.ToArray());
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

internal sealed record Report(string Scenario, int ParentPid, int ChildPid, int UnitId, int ChildParentPid,
    int ExitCode, bool ExitSeen, bool HandleReleased, bool GracefulAck, bool RepeatedStopSucceeded,
    int ExitCallbacks, bool SentinelSurvived, long StopMilliseconds, UnitProcessDiagnostic[] Diagnostics);

internal sealed class ObservedChild(IUnitConfiguration configuration, string mode)
    : ChildProcessUnitHandle(configuration, ProbeEvents.Instance)
{
    internal Process? OwnedProcess => Process;
    protected override async Task<ProcessStartInfo> GetProcessStartInfoAsync(CancellationToken cancellationToken = default)
    {
        var info = await base.GetProcessStartInfoAsync(cancellationToken);
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        return info;
    }
    protected override void ProcessLaunchArguments(Collection<string> collection)
    {
        base.ProcessLaunchArguments(collection);
        collection.Add("--child");
        collection.Add(mode);
    }
}

internal sealed class ProbeEvents : IEventsSource<UnitsController>
{
    internal static readonly ProbeEvents Instance = new();
    internal readonly ConcurrentQueue<UnitProcessDiagnostic> Diagnostics = new();
    internal bool ThrowAfterWrite;
    public string Name => "MithrilProbe";
    public Activity StartActivity(string? details = null, string? category = null, string? caller = null) => new(Name);
    public void StopActivity(Activity activity) => activity.Stop();
    public void Write(Func<object>? func, string? category = null, string? caller = null)
    {
        if (category == "UnitProcess" && func?.Invoke() is UnitProcessDiagnostic trace) Diagnostics.Enqueue(trace);
        if (ThrowAfterWrite) throw new InvalidOperationException("Probe diagnostic sink failure.");
    }
}

internal sealed class ProbeLifetime : IAppLifetime
{
    public CancellationToken AppCancellationToken => CancellationToken.None;
    public void Cancel() { }
    public void OnStopped(Action action) { }
    public void OnStopping(Action action) { }
}
