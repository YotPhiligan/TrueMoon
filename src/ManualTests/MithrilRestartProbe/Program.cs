using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Diagnostics;
using TrueMoon.Mithril;
using TrueMoon.Mithril.Units;

if (args is ["--child", var childDirectory, var generation])
    return await Fixture.ChildAsync(childDirectory, int.Parse(generation));
if (args is not [var scenario, var directory] || !Fixture.Cases.ContainsKey(scenario)) return 2;
try
{
    Directory.CreateDirectory(directory);
    Console.WriteLine("RESULT " + JsonSerializer.Serialize(await Fixture.RunAsync(scenario, directory)));
    return 0;
}
catch (Exception error) { Console.Error.WriteLine(error); return 1; }

internal static class Fixture
{
    internal static readonly Dictionary<string, (UnitRestartPolicy Policy, int[] Codes)> Cases = new()
    {
        ["never-normal"] = (UnitRestartPolicy.Never, [0]), ["never-crash"] = (UnitRestartPolicy.Never, [23]),
        ["once-normal"] = (UnitRestartPolicy.Once, [0, 0]), ["once-crash"] = (UnitRestartPolicy.Once, [23, 23]),
        ["oncrash-normal"] = (UnitRestartPolicy.OnCrash, [0]), ["oncrash-mixed"] = (UnitRestartPolicy.OnCrash, [23, 0]),
        ["oncrash-budget"] = (UnitRestartPolicy.OnCrash, [23, 23, 23]),
        ["always-normal"] = (UnitRestartPolicy.Always, [0, 0, 0]), ["always-crash"] = (UnitRestartPolicy.Always, [23, 23, 23]),
        ["limit-zero"] = (UnitRestartPolicy.Always, [23]), ["default-budget-delay"] = (UnitRestartPolicy.Always, [0, 0, 0, 0]),
        ["delay"] = (UnitRestartPolicy.Once, [0, 0]), ["stop-delay"] = (UnitRestartPolicy.Always, [0]),
        ["stop-preparation"] = (UnitRestartPolicy.Always, [0]), ["stop-restarted"] = (UnitRestartPolicy.Always, [0, 0]),
        ["dispose-delay"] = (UnitRestartPolicy.Always, [0]), ["restart-failure"] = (UnitRestartPolicy.Always, [0]),
        ["controller-lifetime"] = (UnitRestartPolicy.Once, [0, 0]),
        ["reentrant-terminal-dispose"] = (UnitRestartPolicy.Never, [0]), ["concurrent-stop"] = (UnitRestartPolicy.Always, [0, 0])
    };

    internal static async Task<int> ChildAsync(string directory, int generation)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var endpoint = Environment.GetEnvironmentVariable(UnitParentProcessEventsHandler.ControlPipeKey)!;
        using var pipe = new NamedPipeServerStream(endpoint, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var command = ReadCommandAsync(pipe, deadline.Token);
        var release = ReadReleaseAsync(Path.Combine(directory, $"release-{generation}"), deadline.Token);
        WriteJson(Path.Combine(directory, $"ready-{generation}.json"), new Identity(Environment.ProcessId,
            int.Parse(Environment.GetEnvironmentVariable("-u")!), int.Parse(Environment.GetEnvironmentVariable("-p")!), endpoint));
        try { return await await Task.WhenAny(command, release); }
        finally
        {
            deadline.Cancel();
            try { await Task.WhenAll(command, release); } catch (OperationCanceledException) { }
        }
    }
    private static async Task<int> ReadCommandAsync(NamedPipeServerStream pipe, CancellationToken token)
    {
        await pipe.WaitForConnectionAsync(token);
        using var reader = new StreamReader(pipe, leaveOpen: true);
        while (await reader.ReadLineAsync(token) is { } line) if (line == UnitCodes.Exit) return 0;
        return 0;
    }
    private static async Task<int> ReadReleaseAsync(string path, CancellationToken token)
    {
        while (!File.Exists(path)) await Task.Delay(10, token);
        return int.Parse(await File.ReadAllTextAsync(path, token));
    }
    private static void WriteJson(string path, object value)
    {
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(value));
        File.Move(path + ".tmp", path);
    }

    internal static async Task<object> RunAsync(string scenario, string directory)
    {
        var diagnostics = new ConcurrentQueue<UnitProcessDiagnostic>();
        var diagnosticConfig = new DiagnosticsConfiguration();
        diagnosticConfig.AddFilter(typeof(UnitsController).FullName!);
        diagnosticConfig.OnEvent(e =>
        {
            if (e.Category == "UnitProcess" && e.Payload is UnitProcessDiagnostic trace) diagnostics.Enqueue(trace);
        });
        using var subscription = new DiagnosticSubscription(diagnosticConfig);
        using var events = new EventsSource<UnitsController>();
        var (policy, codes) = Cases[scenario];
        var stopDelay = scenario is "stop-delay" or "dispose-delay";
        var config = new UnitConfiguration(7)
        {
            HostingPolicy = UnitHostingPolicy.ChildProcess, LifetimePolicy = UnitLifetimePolicy.App,
            StartupPolicy = UnitStartupPolicy.Immediate, RestartPolicy = scenario == "default-budget-delay" ? null : policy,
            RestartLimit = scenario == "limit-zero" ? 0 : scenario == "default-budget-delay" ? null : 2,
            RestartDelay = stopDelay ? 60000 : scenario == "default-budget-delay" ? null : scenario == "delay" ? 100 : 0,
            TerminationDelay = 3000, IsControlAppLifetime = scenario == "controller-lifetime"
        };
        using var lifetime = new Lifetime();
        await using var handle = new ObservedHandle(config, directory, events)
        { FailRestart = scenario == "restart-failure", BlockPreparation = scenario == "stop-preparation" };
        await using var controller = new Controller(lifetime, config, handle, events);
        var callbacks = 0;
        if (scenario != "controller-lifetime") handle.OnExit(_ =>
        { Interlocked.Increment(ref callbacks); if (scenario == "reentrant-terminal-dispose") handle.Dispose(); });
        var observers = new List<Process>(); var identities = new List<Identity>();
        try
        {
            if (scenario == "controller-lifetime") await controller.StartAsync();
            else Check(await handle.StartAsync() is true, "OS start failed.");
            for (var generation = 1; generation <= codes.Length; generation++)
            {
                var identity = await ReadyAsync(directory, generation);
                var observer = Process.GetProcessById(identity.Pid); _ = observer.SafeHandle;
                observers.Add(observer); identities.Add(identity);
                Check(identity.Unit == 7 && identity.Parent == Environment.ProcessId, "Launch identity mismatch.");
                Check(identities.Select(x => x.Pid).Distinct().Count() == generation, "Restart reused a process PID.");
                Check(identities.Select(x => x.Endpoint).Distinct().Count() == generation, "Restart reused a control endpoint.");
                if (scenario == "controller-lifetime" && generation == 2)
                    Check(lifetime.Calls == 0 && !lifetime.AppCancellationToken.IsCancellationRequested, "Intermediate exit cancelled App.");
                if (generation == 2 && scenario is "stop-restarted" or "concurrent-stop")
                {
                    if (scenario == "concurrent-stop")
                    {
                        var stopped = await Task.WhenAll(handle.StopAsync(), handle.StopAsync()).WaitAsync(TimeSpan.FromSeconds(5));
                        Check(stopped.All(x => x is true), "Concurrent stop failed.");
                    }
                    else Check(await handle.StopAsync() is true, "Restarted process stop failed.");
                }
                else WriteJson(Path.Combine(directory, $"release-{generation}"), codes[generation - 1]);
                await observer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                Check(observer.ExitCode == codes[generation - 1], "Child exit code mismatch.");
            }
            if (stopDelay)
            {
                await handle.ExitObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
                if (scenario == "dispose-delay") await handle.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
                else Check(await handle.StopAsync().WaitAsync(TimeSpan.FromSeconds(3)) is true, "Delay stop failed.");
                Check(handle.InfoCalls == 1 && handle.RestartCount == 0, "Stop launched a scheduled restart.");
            }
            if (scenario == "stop-preparation")
            {
                await handle.Preparing.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Check(await handle.StopAsync().WaitAsync(TimeSpan.FromSeconds(3)) is true, "Preparation stop failed.");
                Check(!File.Exists(Path.Combine(directory, "ready-2.json")), "Cancelled preparation launched child.");
            }
            await handle.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            Check(handle.OwnedProcess is null, "Process handle retained after sequence.");
            if (scenario == "restart-failure")
            {
                Check(handle.LastRestartError is System.ComponentModel.Win32Exception && handle.RestartCount == 1, "Automatic failure was not preserved/bounded.");
                Check(ReferenceEquals(handle.LastRestartError, diagnostics.Single(x => x.Stage == UnitProcessStage.SequenceFailed).Error),
                    "Diagnostics replaced the original restart failure.");
            }
            else Check(handle.LastRestartError is null, "Unexpected monitor/restart error.");
            var intentional = scenario is "stop-delay" or "dispose-delay" or "stop-preparation" or "stop-restarted" or "concurrent-stop";
            Check(callbacks == (scenario == "controller-lifetime" || intentional ? 0 : 1), "Terminal callback count mismatch.");
            if (scenario == "controller-lifetime") { Check(lifetime.Calls == 1, "Final exit did not cancel App once."); await controller.StopAsync(); }
            var expectedRestarts = scenario is "restart-failure" or "stop-preparation" ? 1 : codes.Length - 1;
            Check(handle.RestartCount == expectedRestarts, "Restart budget mismatch.");
            if (scenario is "delay" or "default-budget-delay")
                Check(handle.Delays.All(d => d >= (config.RestartDelay ?? 250) - 5), "Configured restart delay omitted.");
            Check(await handle.StopAsync() is true && await handle.StopAsync() is true, "Repeated stop failed.");
            return new { Scenario = scenario, ParentPid = Environment.ProcessId, Pids = identities.Select(x => x.Pid),
                ExitCodes = observers.Select(x => x.ExitCode).ToArray(), Restarts = handle.RestartCount, Attempts = handle.InfoCalls,
                Callbacks = scenario == "controller-lifetime" ? lifetime.Calls : callbacks, Cleanup = true, Completion = handle.Completion.IsCompletedSuccessfully,
                DelayMilliseconds = handle.Delays.ToArray(), Error = handle.LastRestartError?.GetType().Name,
                Diagnostics = diagnostics.Select(x => new { Stage = x.Stage.ToString(), x.RunId, x.UnitIndex, x.UnitName,
                    x.Generation, x.ProcessId, x.ParentProcessId, x.RestartCount, RestartPolicy = x.RestartPolicy.ToString(),
                    x.RestartLimit, x.RestartDelay, x.TerminationDelay, x.Reason, x.ExitCode, ErrorType = x.Error?.GetType().Name }).ToArray() };
        }
        finally
        {
            foreach (var observer in observers)
            { if (!observer.HasExited) { observer.Kill(entireProcessTree: true); await observer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); } observer.Dispose(); }
        }
    }
    private static async Task<Identity> ReadyAsync(string directory, int generation)
    {
        var path = Path.Combine(directory, $"ready-{generation}.json");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        while (!File.Exists(path)) await Task.Delay(10, deadline.Token);
        return JsonSerializer.Deserialize<Identity>(await File.ReadAllTextAsync(path))!;
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private sealed record Identity(int Pid, int Unit, int Parent, string Endpoint);

    private sealed class ObservedHandle(IUnitConfiguration config, string directory, IEventsSource events) : ChildProcessUnitHandle(config, events)
    {
        internal Process? OwnedProcess => Process;
        internal int InfoCalls;
        internal bool FailRestart, BlockPreparation;
        internal readonly TaskCompletionSource ExitObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Preparing = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly ConcurrentQueue<double> Delays = new();
        private long _lastExit;
        protected override void OnProcessExited(int? exitCode, DateTime? exitTime)
        { Volatile.Write(ref _lastExit, Stopwatch.GetTimestamp()); ExitObserved.TrySetResult(); }
        protected override async Task<ProcessStartInfo> GetProcessStartInfoAsync(CancellationToken token = default)
        {
            var generation = Interlocked.Increment(ref InfoCalls);
            if (generation > 1) Delays.Enqueue(Stopwatch.GetElapsedTime(Volatile.Read(ref _lastExit)).TotalMilliseconds);
            if (generation == 2 && BlockPreparation) { Preparing.TrySetResult(); await Task.Delay(Timeout.Infinite, token); }
            var info = await base.GetProcessStartInfoAsync(token);
            if (generation == 2 && FailRestart) info.FileName = Path.Combine(directory, "missing-owned.exe");
            info.ArgumentList.Add("--child"); info.ArgumentList.Add(directory); info.ArgumentList.Add(generation.ToString());
            return info;
        }
    }
    private sealed class Controller(Lifetime lifetime, IUnitConfiguration config, ObservedHandle handle, IEventsSource<UnitsController> events)
        : UnitsController(events, lifetime, [config])
    { protected override IUnitHandle CreateHandle(IUnitConfiguration configuration) => handle; }
    private sealed class Lifetime : IAppLifetime, IDisposable
    {
        private readonly CancellationTokenSource _source = new();
        internal int Calls;
        public CancellationToken AppCancellationToken => _source.Token;
        public void Cancel() { Interlocked.Increment(ref Calls); _source.Cancel(); }
        public void OnStopped(Action action) { }
        public void OnStopping(Action action) { }
        public void Dispose() => _source.Dispose();
    }
}
