using System.Diagnostics;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Enerit;
using TrueMoon.Enerit.IO;
using TrueMoon.Enerit.IO.Pipes;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Mithril;
using TrueMoon.Threading;
using AppUnitsPipesProbe;

try
{
    if (Environment.GetEnvironmentVariable("-u") is not null) return await Fixture.ChildAsync();
    if (args is ["--parent", var parentProvider, var parentDirectory])
    {
        Fixture.EnvironmentFor(parentProvider, parentDirectory);
        await using var app = Fixture.Build(parentProvider, "parent-exit");
        await app.StartAsync();
        var ready = await Records.ReadyAsync(parentDirectory, 1);
        await Fixture.ConnectAsync(app.Services.Resolve<IInvocationClient<IProbeService>>()!);
        _ = app.Services.Resolve<IProbeService>()!.HoldAsync();
        await Records.WaitAsync(Path.Combine(parentDirectory, "waiting-1.json"));
        Records.Write(Path.Combine(parentDirectory, "parent-ready.json"), new { Pid = Environment.ProcessId, Child = ready.Pid });
        await Task.Delay(Timeout.Infinite);
        return 0;
    }
    if (args is not [var scenario, var provider, var directory] || !Fixture.Scenarios.Contains(scenario)) return 2;
    Fixture.EnvironmentFor(provider, directory, scenario);
    Console.WriteLine("RESULT " + JsonSerializer.Serialize(await Fixture.RunAsync(scenario, provider, directory)));
    return 0;
}
catch (Exception error) { Console.Error.WriteLine(error); return 1; }

namespace AppUnitsPipesProbe
{
    internal static class Fixture
    {
        internal static readonly string[] Scenarios = ["payload", "concurrent", "cancel-request", "remote-error", "restart", "budget", "stop-pending", "startup-failure", "parent-exit", "saturated-stop", "saturated-kill"];
        internal static void EnvironmentFor(string provider, string directory, string scenario = "parent-exit")
        {
            Directory.CreateDirectory(directory);
            Environment.SetEnvironmentVariable("TM_APP_PIPES_PROVIDER", provider);
            Environment.SetEnvironmentVariable("TM_APP_PIPES_DIR", directory);
            Environment.SetEnvironmentVariable("TM_APP_PIPES_ENDPOINT", "app-pipes-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("TM_APP_PIPES_SCENARIO", scenario);
        }
        internal static IApp Build(string provider, string scenario, Action<UnitProcessDiagnostic>? onProcess = null, Action? onControl = null)
        {
            var saturated = Environment.GetEnvironmentVariable("TM_APP_PIPES_SCENARIO") is "saturated-stop" or "saturated-kill";
            var builder = provider == "di" ? App.Builder(c => c.UseDI()) : App.Builder(_ => { });
            return builder.Setup(c =>
            {
                c.UseDiagnostics(d => d.OnEvent(e =>
                {
                    if (e.Category == "UnitProcess" && e.Payload is UnitProcessDiagnostic trace) onProcess?.Invoke(trace);
                    if (saturated && Environment.GetEnvironmentVariable("-u") is not null && e.Payload is "exit requested")
                        onControl?.Invoke();
                }));
                if (saturated)
                    c.PipesServiceTransport(_ => Environment.GetEnvironmentVariable("TM_APP_PIPES_ENDPOINT")!, new PipesTransportOptions
                    {
                        MaxPendingRequests = 2, MaxQueuedRequests = 1, MaxConcurrentRequests = 2,
                        MaxBufferedRequestBytes = 1024, MaxConnections = 2, RequestTimeout = TimeSpan.FromSeconds(30)
                    });
                else c.PipesServiceTransport(_ => Environment.GetEnvironmentVariable("TM_APP_PIPES_ENDPOINT")!);
                if (Environment.GetEnvironmentVariable("-u") is null) c.UseInvocationService<IProbeService>();
                c.AddUnit(u => u.Services(r => r.Singleton<UnitState>().Composite<ProbeService, IProbeService, IStartable, IStoppable>())
                    .ListenInvocationService<IProbeService, ProbeService>(),
                    u =>
                    {
                        u.RestartPolicy = scenario is "restart" or "budget" ? UnitRestartPolicy.OnCrash : UnitRestartPolicy.Never;
                        u.RestartLimit = 1; u.RestartDelay = 50; u.TerminationDelay = 3000;
                        if (scenario is "restart" or "budget") u.ControlAppLifetime();
                        if (scenario == "startup-failure") u.StartDelayed();
                    });
                if (scenario == "startup-failure") c.AddUnit(_ => { }, u => { u.RestartPolicy = UnitRestartPolicy.Never; u.ExecuteExternal(Path.Combine(AppContext.BaseDirectory, "missing-owned.exe")); });
            }).Build();
        }
        internal static async Task<int> ChildAsync()
        {
            var provider = Environment.GetEnvironmentVariable("TM_APP_PIPES_PROVIDER")!;
            UnitState state = null!;
            await using var app = Build(provider, "child", onControl: () =>
                Records.Write(Path.Combine(state.Directory, "control-requested.json"),
                    new { Pid = Environment.ProcessId, ActiveHandlers = Volatile.Read(ref state.ActiveHolds) }));
            state = app.Services.Resolve<UnitState>()!;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await app.StartAsync(timeout.Token);
            await app.Services.Resolve<IAppLifetimeHandler>()!.WaitAsync(timeout.Token);
            if (state.Scenario is "saturated-stop" or "saturated-kill")
                Records.Write(Path.Combine(state.Directory, "control-stop.json"), new { Pid = Environment.ProcessId, EnteredHandlers = state.Holds });
            await app.StopAsync();
            var server = (PipesInvocationServer<IProbeService>)app.Services.Resolve<IInvocationServer<IProbeService>>()!;
            CheckServer(server);
            if (state.Scenario is "saturated-stop" or "saturated-kill")
                Records.Write(Path.Combine(state.Directory, "server-stopped.json"), new
                {
                    Pid = Environment.ProcessId, server.ActiveRequestCount, server.BufferedRequestBytes,
                    server.ConnectionCount, WorkersCompleted = server.Completion.IsCompletedSuccessfully
                });
            var listener = app.Services.Resolve<UnitParentProcessEventsHandler>()!;
            await listener.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            return 0;
        }
        internal static async Task<object> RunAsync(string scenario, string provider, string directory)
        {
            if (scenario == "parent-exit") return await ParentExitAsync(provider, directory);
            if (scenario is "saturated-stop" or "saturated-kill") return await SaturatedStopAsync(scenario, provider, directory);
            var diagnostics = new ConcurrentQueue<UnitProcessDiagnostic>();
            await using var app = Build(provider, scenario, diagnostics.Enqueue);
            var service = app.Services.Resolve<IProbeService>()!;
            var client = app.Services.Resolve<IInvocationClient<IProbeService>>()!;
            var controller = app.Services.Resolve<IUnitsController>()!;
            var observers = new List<Process>(); var identities = new List<Ready>();
            var failedRequests = 0; var checks = 0;
            try
            {
                if (scenario == "startup-failure") await controller.SpawnUnitAsync(1); else await app.StartAsync();
                var first = await ObserveAsync(directory, 1, observers, identities);
                var unitHandle = (TrueMoon.Mithril.Units.ProcessUnitHandle)((System.Collections.IDictionary)typeof(UnitsController).GetField("_unitHandles", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(app.Services.Resolve<UnitsController>())!)[1]!;
                await ConnectAsync(client);
                checks += await InspectAsync(service, first, 42, "Луна 🌙");
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                if (scenario == "concurrent")
                {
                    var responses = await Task.WhenAll(Enumerable.Range(0, 32).Select(i => service.InspectAsync(i, null, timeout.Token)));
                    Check(responses.Select(r => r.Value).SequenceEqual(Enumerable.Range(100, 32)), "Concurrent result mismatch.");
                    Check(responses.All(r => r.Pid == first.Pid && r.Text is null), "Concurrent identity/null mismatch."); checks += 32;
                }
                if (scenario == "remote-error")
                {
                    var error = await FailureAsync(service.AckAsync(true, timeout.Token));
                    Check(error is InvocationException && error.Message.Contains("composition-remote-failure"), "Remote cause lost.");
                    await service.AckAsync(false, timeout.Token); checks++;
                    checks += await InspectAsync(service, first, -9, "");
                }
                if (scenario == "payload")
                { checks += await InspectAsync(service, first, 0, null); await service.AckAsync(false, timeout.Token); checks++; }
                if (scenario is "cancel-request" or "restart" or "budget" or "stop-pending")
                {
                    using var local = new CancellationTokenSource();
                    var pending = service.HoldAsync(local.Token);
                    await Records.WaitAsync(Path.Combine(directory, "waiting-1.json"));
                    if (scenario == "cancel-request")
                    {
                        local.Cancel(); Check(await FailureAsync(pending) is OperationCanceledException, "Local cancellation lost.");
                        File.WriteAllText(Path.Combine(directory, "release-1"), "release");
                        checks += await InspectAsync(service, first, 19, "after-cancel");
                    }
                    else if (scenario == "stop-pending") await app.StopAsync();
                    else
                    {
                        observers[0].Kill(entireProcessTree: false);
                        await observers[0].WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                        Check(observers[0].ExitCode != 0, "Crash exit expected.");
                    }
                    if (scenario != "cancel-request")
                    { Check(await FailureAsync(pending) is IOException or ObjectDisposedException, "Pending failure was hidden/retried/timed out."); failedRequests++; }
                    if (scenario is "restart" or "budget")
                    {
                        var second = await ObserveAsync(directory, 2, observers, identities);
                        Check(second.ControlEndpoint != first.ControlEndpoint && second.Pid != first.Pid, "Generation identity reused.");
                        Check(!app.Services.Resolve<IAppLifetime>()!.AppCancellationToken.IsCancellationRequested, "Intermediate exit cancelled App.");
                        await ConnectAsync(client); checks += await InspectAsync(service, second, 52, "after-restart");
                        if (scenario == "budget")
                        {
                            observers[1].Kill(entireProcessTree: false);
                            await observers[1].WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                            Check(observers[1].ExitCode != 0, "Budget crash expected.");
                            await app.Services.Resolve<IAppLifetimeHandler>()!.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                            Check(app.Services.Resolve<IAppLifetime>()!.AppCancellationToken.IsCancellationRequested, "Terminal exit did not cancel App.");
                        }
                    }
                }
                if (scenario == "startup-failure")
                {
                    var error = await FailureAsync(app.StartAsync());
                    Check(error is System.ComponentModel.Win32Exception, "Startup error changed.");
                }
                await app.StopAsync();
                foreach (var observer in observers) await observer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                if (scenario != "budget")
                {
                    Check(observers[^1].ExitCode == 0, "Final child did not stop gracefully.");
                    await CheckStoppedAsync(directory, identities[^1]);
                }
                if (scenario is "restart" or "budget") Check(!File.Exists(Path.Combine(directory, "ready-3.json")), "Budget exceeded.");
                CheckClient(client); CheckController(app.Services.Resolve<UnitsController>()!);
                Check(unitHandle.Completion.IsCompletedSuccessfully && unitHandle.RestartCount == (scenario is "restart" or "budget" ? 1 : 0), "Restart monitor/budget mismatch.");
                await app.StopAsync();
                var appCancelled = app.Services.Resolve<IAppLifetime>()!.AppCancellationToken.IsCancellationRequested;
                await app.DisposeAsync();
                var unitTraces = diagnostics.Where(x => x.UnitIndex == 1).ToArray();
                Check(unitTraces.Length > 0 && unitTraces.Select(x => x.RunId).Distinct().Count() == 1 && unitTraces[0].RunId != Guid.Empty,
                    "Unit diagnostic sequence identity missing.");
                var starts = unitTraces.Where(x => x.Stage == UnitProcessStage.Started).ToArray();
                Check(starts.Select(x => x.ProcessId).SequenceEqual(identities.Select(x => (int?)x.Pid)) &&
                    starts.Select(x => x.Generation).SequenceEqual(Enumerable.Range(1, identities.Count)), "Diagnostic PID/generation mismatch.");
                Check(unitTraces.All(x => x.ParentProcessId == Environment.ProcessId && x.RestartLimit == 1 && x.RestartDelay == 50 && x.TerminationDelay == 3000 &&
                    x.RestartPolicy == (scenario is "restart" or "budget" ? UnitRestartPolicy.OnCrash : UnitRestartPolicy.Never)), "Diagnostic effective configuration mismatch.");
                Check(unitTraces.Count(x => x.Stage == UnitProcessStage.ProcessReleased) == identities.Count &&
                    unitTraces.Any(x => x.Stage == UnitProcessStage.Stopped) &&
                    !unitTraces.Any(x => x.Stage is UnitProcessStage.StopFailed or UnitProcessStage.SequenceFailed), "Diagnostic cleanup mismatch.");
                Check(unitTraces.Count(x => x.Stage == UnitProcessStage.RestartScheduled) == (scenario is "restart" or "budget" ? 1 : 0), "Diagnostic restart decision mismatch.");
                Check(unitTraces.Count(x => x.Stage == UnitProcessStage.Terminal) == (scenario == "budget" ? 1 : 0), "Diagnostic terminal count mismatch.");
                if (scenario == "budget") Check(unitTraces.Single(x => x.Stage == UnitProcessStage.Terminal).Reason == "RestartLimit", "Budget reason lost.");
                if (scenario == "startup-failure") Check(diagnostics.Any(x => x.UnitIndex == 2 && x.Stage == UnitProcessStage.LaunchFailed &&
                    x.Error is System.ComponentModel.Win32Exception && x.ProcessId is null), "Failed external launch diagnostics missing.");
                return new { Scenario = scenario, Provider = provider, ParentPid = Environment.ProcessId,
                    ChildPids = identities.Select(i => i.Pid).ToArray(), ExitCodes = observers.Select(p => p.ExitCode).ToArray(),
                    Checks = checks, FailedRequests = failedRequests, Cleanup = true, Pending = 0, WorkersCompleted = true,
                    Restarts = unitHandle.RestartCount, ClientCleanupObserved = true, AppCancelled = appCancelled,
                    ProcessDiagnosticsChecked = true, ProcessDiagnosticCount = diagnostics.Count };
            }
            finally
            {
                foreach (var observer in observers)
                { if (!observer.HasExited) { observer.Kill(entireProcessTree: true); await observer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); } observer.Dispose(); }
            }
        }
        private static async Task<object> SaturatedStopAsync(string scenario, string provider, string directory)
        {
            var diagnostics = new ConcurrentQueue<UnitProcessDiagnostic>();
            await using var app = Build(provider, scenario, diagnostics.Enqueue);
            var observers = new List<Process>(); var identities = new List<Ready>();
            try
            {
                await app.StartAsync();
                var ready = await ObserveAsync(directory, 1, observers, identities);
                var child = observers.Single();
                var service = app.Services.Resolve<IProbeService>()!;
                var client = (PipesInvocationClient<IProbeService>)app.Services.Resolve<IInvocationClient<IProbeService>>()!;
                var controller = app.Services.Resolve<UnitsController>()!;
                var handles = (System.Collections.IDictionary)typeof(UnitsController).GetField("_unitHandles", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(controller)!;
                var handle = (TrueMoon.Mithril.Units.ProcessUnitHandle)handles[1]!;
                Check(!string.IsNullOrWhiteSpace(ready.ControlEndpoint) && ready.ControlEndpoint != ready.Endpoint && ready.ControlEndpoint != "tm_" + ready.Endpoint,
                    "Lifecycle control shares the saturated domain endpoint.");
                await ConnectAsync(client);
                var first = service.HoldAsync();
                var second = service.HoldAsync();
                for (var invocation = 1; invocation <= 2; invocation++)
                {
                    var path = Path.Combine(directory, $"waiting-1-{invocation}.json");
                    await Records.WaitAsync(path);
                    using var entered = JsonDocument.Parse(await File.ReadAllTextAsync(path));
                    Check(entered.RootElement.GetProperty("Pid").GetInt32() == ready.Pid &&
                        entered.RootElement.GetProperty("Invocation").GetInt32() == invocation, "Load handler belongs to another child.");
                }
                var queued = service.HoldAsync();
                using (var admissionTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(8)))
                    while (client.QueuedRequestCount != 1) await Task.Delay(10, admissionTimeout.Token);
                var pendingBefore = client.PendingRequestCount; var queuedBefore = client.QueuedRequestCount;
                Check(pendingBefore == 2 && queuedBefore == 1, "Client load did not fill admission and queue.");
                var overload = await FailureAsync(service.InspectAsync(-1, "must-not-run"));
                Check(overload is PipesInvocationOverloadedException, "Excess request did not fail at bounded client admission.");

                var elapsed = Stopwatch.StartNew();
                // Keep domain IPC open until Mithril has delivered control and reaped its loaded child.
                await controller.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));
                await app.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));
                elapsed.Stop();
                var failures = new List<Exception>();
                foreach (var request in new Task[] { first, second, queued })
                {
                    var error = await FailureAsync(request);
                    Check(error is IOException or ObjectDisposedException or OperationCanceledException,
                        "Stopping a saturated client hid, retried or timed out a request.");
                    failures.Add(error);
                }
                await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                await client.Completion.WaitAsync(TimeSpan.FromSeconds(5));
                CheckClient(client); CheckController(controller);
                Check(handle.Completion.IsCompletedSuccessfully && handle.RestartCount == 0 && handle.LastRestartError is null,
                    "Owned exit monitor did not drain cleanly.");
                var ownedProcess = typeof(TrueMoon.Mithril.Units.ProcessUnitHandle).GetField("Process", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(handle);
                Check(ownedProcess is null, "Owned process wrapper was not released after reap.");
                Check(!File.Exists(Path.Combine(directory, "waiting-1-3.json")) && !File.Exists(Path.Combine(directory, "ready-2.json")),
                    "Queued work dispatched during stop or a stopped child restarted.");
                using var controlRequest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "control-requested.json")));
                using var controlStop = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "control-stop.json")));
                Check(controlRequest.RootElement.GetProperty("Pid").GetInt32() == ready.Pid &&
                    controlRequest.RootElement.GetProperty("ActiveHandlers").GetInt32() == 2 &&
                    controlStop.RootElement.GetProperty("Pid").GetInt32() == ready.Pid &&
                    controlStop.RootElement.GetProperty("EnteredHandlers").GetInt32() == 2,
                    "Independent lifecycle control did not reach the loaded child.");
                var traces = diagnostics.Where(x => x.UnitIndex == 1).ToArray();
                var released = traces.Single(x => x.Stage == UnitProcessStage.ProcessReleased);
                var killed = scenario == "saturated-kill";
                Check(released.ProcessId == ready.Pid && released.ExitCode == child.ExitCode &&
                    released.Reason == (killed ? "Killed" : "Graceful"), "Wrong process release outcome.");
                Check(traces.Count(x => x.Stage == UnitProcessStage.Killing) == (killed ? 1 : 0) &&
                    traces.Where(x => x.Stage == UnitProcessStage.Killing).All(x => x.ProcessId == ready.Pid), "Forced termination decision mismatch.");
                Check(traces.Any(x => x.Stage == UnitProcessStage.StopRequested && x.ProcessId == ready.Pid) &&
                    traces.Any(x => x.Stage == UnitProcessStage.Stopped && x.ProcessId == ready.Pid) &&
                    !traces.Any(x => x.Stage is UnitProcessStage.StopFailed or UnitProcessStage.SequenceFailed or UnitProcessStage.RestartScheduled),
                    "Saturated shutdown diagnostics contain an ownership failure.");
                Check(traces.All(x => x.ParentProcessId == Environment.ProcessId && x.TerminationDelay == 3000), "Owned process identity or graceful budget changed.");
                var handlersDrained = 0;
                for (var invocation = 1; invocation <= 2; invocation++)
                {
                    var path = Path.Combine(directory, $"handler-left-1-{invocation}.json");
                    if (killed) Check(!File.Exists(path), "Uncooperative handler unexpectedly drained.");
                    else
                    {
                        using var left = JsonDocument.Parse(await File.ReadAllTextAsync(path));
                        Check(left.RootElement.GetProperty("Pid").GetInt32() == ready.Pid &&
                            left.RootElement.GetProperty("Invocation").GetInt32() == invocation &&
                            left.RootElement.GetProperty("Cancelled").GetBoolean(), "Cooperative handler did not observe shutdown cancellation.");
                        handlersDrained++;
                    }
                }
                var serverDrained = File.Exists(Path.Combine(directory, "server-stopped.json"));
                if (killed)
                    Check(child.ExitCode != 0 && !serverDrained, "Uncooperative child escaped forced termination.");
                else
                {
                    Check(child.ExitCode == 0 && serverDrained, "Cooperative child did not complete transport drain.");
                    await CheckStoppedAsync(directory, ready);
                    using var stopped = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "server-stopped.json")));
                    Check(stopped.RootElement.GetProperty("Pid").GetInt32() == ready.Pid &&
                        stopped.RootElement.GetProperty("ActiveRequestCount").GetInt32() == 0 &&
                        stopped.RootElement.GetProperty("BufferedRequestBytes").GetInt64() == 0 &&
                        stopped.RootElement.GetProperty("ConnectionCount").GetInt32() == 0 &&
                        stopped.RootElement.GetProperty("WorkersCompleted").GetBoolean(), "Cooperative server resources remain live.");
                }
                Check(elapsed.Elapsed < TimeSpan.FromSeconds(10), "IPC drain prevented bounded process stop.");
                await app.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
                return new
                {
                    Scenario = scenario, Provider = provider, ParentPid = Environment.ProcessId,
                    ChildPids = new[] { ready.Pid }, ExitCodes = new[] { child.ExitCode },
                    PendingBefore = pendingBefore, QueuedBefore = queuedBefore,
                    OverloadError = overload.GetType().Name, RequestErrors = failures.Select(x => x.GetType().Name).ToArray(),
                    FailedRequests = failures.Count, Pending = client.PendingRequestCount, Queued = client.QueuedRequestCount,
                    WorkersCompleted = client.Completion.IsCompletedSuccessfully, ClientCleanupObserved = true,
                    Cleanup = ownedProcess is null && handles.Count == 0, Restarts = handle.RestartCount,
                    AppCancelled = app.Services.Resolve<IAppLifetime>()!.AppCancellationToken.IsCancellationRequested,
                    ControlEndpoint = ready.ControlEndpoint, DomainEndpoint = ready.Endpoint, ControlObservedPid = ready.Pid,
                    EnteredHandlers = 2, ActiveHandlersAtControl = controlRequest.RootElement.GetProperty("ActiveHandlers").GetInt32(),
                    HandlersDrained = handlersDrained, ServerDrained = serverDrained,
                    KillCount = traces.Count(x => x.Stage == UnitProcessStage.Killing), ReleaseOutcome = released.Reason,
                    ReleasedPid = released.ProcessId, MonitorCompleted = handle.Completion.IsCompletedSuccessfully,
                    TerminationDelay = 3000, StopMilliseconds = elapsed.ElapsedMilliseconds
                };
            }
            finally
            {
                foreach (var observer in observers)
                {
                    if (!observer.HasExited) { observer.Kill(entireProcessTree: true); await observer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
                    observer.Dispose();
                }
            }
        }
        private static async Task<Ready> ObserveAsync(string directory, int generation, List<Process> observers, List<Ready> identities)
        {
            var ready = await Records.ReadyAsync(directory, generation);
            Check(ready.Parent == Environment.ProcessId && ready.Generation == generation && ready.Provider == Environment.GetEnvironmentVariable("TM_APP_PIPES_PROVIDER") && ready.Endpoint == Environment.GetEnvironmentVariable("TM_APP_PIPES_ENDPOINT"), "Unit identity mismatch.");
            var process = Process.GetProcessById(ready.Pid); _ = process.SafeHandle; observers.Add(process); identities.Add(ready); return ready;
        }
        internal static async Task ConnectAsync(IInvocationClient<IProbeService> client)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            while (!((PipesInvocationClient<IProbeService>)client).IsConnected) await Task.Delay(10, timeout.Token);
        }
        private static async Task<int> InspectAsync(IProbeService service, Ready ready, int value, string? text)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var reply = await service.InspectAsync(value, text, timeout.Token);
            Check(reply.Value == value + 100 && reply.Text == (text is null ? null : "remote:" + text) && reply.Pid == ready.Pid && reply.Generation == ready.Generation, "Typed payload/generation mismatch."); return 1;
        }
        private static async Task<Exception> FailureAsync(Task task)
        { try { await task.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception error) { return error; } throw new InvalidOperationException("Expected failure did not occur."); }
        private static async Task CheckStoppedAsync(string directory, Ready ready)
        {
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, $"stopped-{ready.Generation}.json")));
            Check(document.RootElement.GetProperty("Pid").GetInt32() == ready.Pid && document.RootElement.GetProperty("Stops").GetInt32() == 1, "Service shutdown missing/repeated.");
        }
        private static void CheckClient(IInvocationClient<IProbeService> client)
        {
            var pipes = (PipesInvocationClient<IProbeService>)client;
            Check(pipes.PendingRequestCount == 0 && pipes.QueuedRequestCount == 0, "Client admission not empty after App.Stop.");
            Check(pipes.Completion.IsCompletedSuccessfully, "Client workers live after App.Stop.");
        }
        private static void CheckServer(IInvocationServer<IProbeService> server)
        {
            var pipes = (PipesInvocationServer<IProbeService>)server;
            Check(pipes.ActiveRequestCount == 0 && pipes.BufferedRequestBytes == 0 && pipes.ConnectionCount == 0, "Server resources live after App.Stop.");
            Check(pipes.Completion.IsCompletedSuccessfully, "Server workers incomplete.");
        }
        private static void CheckController(UnitsController controller)
            => Check(((System.Collections.IDictionary)typeof(UnitsController).GetField("_unitHandles", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(controller)!).Count == 0, "Controller retains handles.");
        private static async Task<object> ParentExitAsync(string provider, string directory)
        {
            var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
            if (Path.GetFileNameWithoutExtension(info.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) info.ArgumentList.Add(Assembly.GetEntryAssembly()!.Location);
            info.ArgumentList.Add("--parent"); info.ArgumentList.Add(provider); info.ArgumentList.Add(directory);
            using var parent = Process.Start(info)!; var errors = parent.StandardError.ReadToEndAsync(); Process? child = null;
            try
            {
                await Records.WaitAsync(Path.Combine(directory, "parent-ready.json"));
                var ready = await Records.ReadyAsync(directory, 1); child = Process.GetProcessById(ready.Pid); _ = child.SafeHandle;
                Check(ready.Parent == parent.Id, "Supervisor parent identity mismatch.");
                parent.Kill(entireProcessTree: false); await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); Check(child.ExitCode == 0, "Child failed parent-exit cleanup.");
                await CheckStoppedAsync(directory, ready); Check(string.IsNullOrEmpty(await errors), "Parent stderr not empty.");
                return new { Scenario = "parent-exit", Provider = provider, ParentPid = Environment.ProcessId, ChildPids = new[] { child.Id },
                    ExitCodes = new[] { child.ExitCode }, Checks = 1, FailedRequests = 0, Cleanup = true, Pending = -1, WorkersCompleted = false, Restarts = 0, ClientCleanupObserved = false, AppCancelled = false };
            }
            finally
            {
                if (!parent.HasExited) { parent.Kill(entireProcessTree: true); await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
                if (child is not null) { if (!child.HasExited) { child.Kill(entireProcessTree: true); await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); } child.Dispose(); }
            }
        }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
