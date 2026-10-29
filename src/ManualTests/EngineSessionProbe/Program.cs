using TrueMoon.Aether;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using TrueMoon.Aether.Scenes;
using EngineSessionProbe;
using TrueMoon;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Sessions;
using TrueMoon.Dependencies;
using TrueMoon.Mithril;
using EngineSessionProbe.Measurements;

try
{
    if (args is ["--stdio-child", var streamMode, var streamDirectory])
        return await StandardStreamProbe.RunChildAsync(streamMode, streamDirectory);
    if (args is ["--runtime"])
        return ChildDiagnosticDeliveryProbe.IsChild() ? await ChildDiagnosticDeliveryProbe.RunChildAsync()
            : PreviewRuntimeEntry.IsPreview() ? await PreviewRuntimeEntry.RunAsync() : await RuntimeHost.RunAsync();
    if (args is ["--runtime-launch-proof", var runtimePath, var runtimeLaunchDirectory])
        return await RuntimeLaunchProbe.RunAsync(Path.GetFullPath(runtimePath), Path.GetFullPath(runtimeLaunchDirectory));
    if (args is ["--child-diagnostics-proof", var deliveryDirectory]) return await ChildDiagnosticDeliveryProbe.RunAsync(Path.GetFullPath(deliveryDirectory));
    if (args is ["--edit-preview-proof", var previewDirectory]) return await EditPreviewProbe.RunAsync(Path.GetFullPath(previewDirectory));
    if (args is ["--scene"]) return await SceneInputProbe.RunAsync(null);
    if (args is ["--room"]) return await SceneInputProbe.RunAsync(null, room: true);
    if (args is ["--interaction-proof", var interactionDirectory]) return await RoomViewportProbe.RunAsync(Path.GetFullPath(interactionDirectory), interactionProof: true);
    if (args is ["--room-proof", var roomDirectory]) return await RoomViewportProbe.RunAsync(Path.GetFullPath(roomDirectory));
    if (args is ["--input-proof", var inputDirectory]) return await SceneInputProbe.RunAsync(Path.GetFullPath(inputDirectory));
    if (args is ["--scene-proof", var sceneDirectory]) return await SceneViewportProbe.RunAsync(Path.GetFullPath(sceneDirectory));
    if (args is ["--viewport-baseline", var mode, var baselineDirectory, var warmup, var interval])
        return await ViewportBaseline.RunAsync(Path.GetFullPath(baselineDirectory), mode, int.Parse(warmup), int.Parse(interval));
    if (args is ["--parent-wait", var parentDirectory]) return await Probe.RunParentAsync(parentDirectory);
    if (args is ["--viewport"] or ["--viewport", _]) return await ViewportProbe.RunAsync(args.Length == 2 ? Path.GetFullPath(args[1]) : null);
    if (args is ["--viewport-project", var project]) return await ViewportProbe.RunAsync(null, project);
    if (args is ["--viewport-gpu"]) return await ViewportProbe.RunAsync(null, gpu: true);
    if (args is ["--viewport-gpu-project", var gpuProject]) return await ViewportProbe.RunAsync(null, gpuProject, gpu: true);
    if (args is ["--editor-proof", var proofDirectory]) return await EditorViewportProbe.RunAsync(Path.GetFullPath(proofDirectory));
    if (args is ["--editor-gpu-proof", var gpuProofDirectory]) return await EditorViewportProbe.RunAsync(Path.GetFullPath(gpuProofDirectory), gpu: true);
    if (args is ["--gpu-capabilities", var gpuDirectory]) return await GpuCapabilityProbe.RunAsync(Path.GetFullPath(gpuDirectory));
    if (args is ["--gpu-import", var importDirectory]) return await GpuImportProbe.RunAsync(Path.GetFullPath(importDirectory));
    if (args is ["--gpu-surface", var surfaceDirectory]) return await GpuSurfaceProbe.RunAsync(Path.GetFullPath(surfaceDirectory));
    if (args is ["--gpu-viewport", var viewportDirectory]) return await GpuViewportProbe.RunAsync(Path.GetFullPath(viewportDirectory));
    if (args is not [var scenario, var directory] || !Probe.Scenarios.Contains(scenario))
    { Console.WriteLine("EngineSessionProbe <scenario> <report-directory>"); return 2; }
    var result = await Probe.RunAsync(scenario, Path.GetFullPath(directory));
    Records.Write(directory, "report.json", result);
    Console.WriteLine(JsonSerializer.Serialize(result)); return 0;
}
catch (Exception error) { Console.Error.WriteLine(error); return 1; }

namespace EngineSessionProbe
{
    public sealed class AuthoringState
    {
        public long Revision { get; set; } = 7;
        public bool Dirty { get; set; } = true;
        public string Selection { get; set; } = "switch";
        public List<string> Panels { get; set; } = ["scene", "inspector", "unsaved-note"];
        public SceneSnapshot Snapshot() => new("demo-room", Revision, [new() { Id = "switch", X = 1, Interactive = true }]);
    }
    public static class Probe
    {
        public static readonly string[] Scenarios = ["repeat", "stale", "bad-hello", "bad-scene", "startup-timeout", "startup-cancel",
            "command-timeout", "command-cancel", "stop-pending", "crash", "disconnect", "heartbeat", "forced-stop", "hang-stop", "launch-failure", "parent-exit", "app-stop", "build-artifacts", "checkpoint", "replacement", "diagnostics"];
        private static IApp CreateApp(NativeRuntimeFactory factory, AuthoringState authoring, string directory,
            SessionDeadlines? deadlines = null, Action<SessionStatus>? trace = null) => App.Builder(_ => { })
            .Setup(c =>
            {
                c.UseAether(o =>
                {
                    o.HostContractId = BuildIdentity.HostId; o.GameBuildId = BuildIdentity.GameId;
                    o.ReportDirectory = directory; o.Deadlines = deadlines ?? new();
                    o.StatusChanged = trace; o.RuntimeFactory = _ => factory;
                });
                c.Services(r => r.Singleton<AuthoringState>(_ => authoring));
            }).Build();

        public static async Task<object> RunAsync(string scenario, string directory)
        {
            Directory.CreateDirectory(directory);
            if (scenario == "parent-exit") return await ParentExitAsync(directory);
            if (scenario == "build-artifacts") return await BuildProbe.RunAsync(directory);
            if (scenario == "checkpoint") return await CheckpointProbe.RunAsync(directory);
            if (scenario == "replacement") return await ReplacementProbe.RunAsync(directory);
            if (scenario == "diagnostics") return await DiagnosticsProbe.RunAsync(directory);
            var factory = new NativeRuntimeFactory(NativeRuntimeLaunch.CurrentProcess("--runtime")); var state = new AuthoringState();
            var before = JsonSerializer.Serialize(state); var trace = new List<object>();
            var deadlines = scenario == "startup-timeout" ? new SessionDeadlines { Startup = TimeSpan.FromSeconds(1) } : new SessionDeadlines();
            await using var app = CreateApp(factory, state, directory, deadlines,
                s => { lock (trace) trace.Add(new { State = s.State.ToString(), Generation = s.Identity?.Generation, Pid = s.ProcessId,
                    s.HelloAccepted, s.SceneLoaded, s.Tick, Phase = s.Failure?.Phase, Error = s.Failure?.Cause.ToString(), CleanupError = s.Failure?.CleanupError?.ToString() }); });
            await app.StartAsync();
            var supervisor = app.Services.Resolve<AetherHost>()!.Supervisor;
            var lifetime = app.Services.Resolve<IAppLifetime>()!;
            var failures = 0;
            try
            {
                if (scenario == "repeat")
                {
                    Guid? session = null;
                    for (ulong generation = 1; generation <= 20; generation++)
                    {
                        await supervisor.StartAsync(state.Snapshot());
                        var ready = supervisor.Status;
                        Check(ready.State == SessionState.Paused && ready.HelloAccepted && ready.SceneLoaded, "Readiness stages missing.");
                        Check(ready.Identity!.Generation == generation, "Generation reused/skipped.");
                        session ??= ready.Identity.SessionId; Check(ready.Identity.SessionId == session, "Editor session identity changed.");
                        Check((await supervisor.StepAsync()).Tick == 1, "Single step did not execute exactly one tick.");
                        await supervisor.SetPlayingAsync(true); Check(supervisor.Status.State == SessionState.Playing, "Play acknowledgement missing.");
                        await supervisor.SetPlayingAsync(false);
                        await supervisor.StopAsync(); Check(supervisor.Status.State == SessionState.Stopped, "Stop did not complete.");
                        Check(!lifetime.AppCancellationToken.IsCancellationRequested, "Child exit cancelled Editor App.");
                    }
                }
                else if (scenario == "app-stop")
                {
                    await supervisor.StartAsync(state.Snapshot());
                    await app.StopAsync();
                    Check(factory.Leases.All(l => l.Reaped && l.ResourcesReleased), "App Stop retained an owned child.");
                    Check(await FailureAsync(supervisor.StartAsync(state.Snapshot())) is ObjectDisposedException,
                        "App Stop allowed a new engine generation.");
                }
                else if (scenario is "bad-hello" or "bad-scene" or "startup-timeout" or "startup-cancel" or "launch-failure")
                {
                    using var cancellation = new CancellationTokenSource();
                    var behavior = scenario switch { "startup-timeout" or "startup-cancel" => "hang-hello", _ => scenario };
                    var starting = supervisor.StartAsync(state.Snapshot(), behavior, cancellation.Token);
                    if (scenario == "startup-cancel")
                    {
                        await Records.WaitAsync(Path.Combine(directory, "launched-1.json")); cancellation.Cancel();
                    }
                    var error = await FailureAsync(starting); failures++;
                    Check(supervisor.Status.State == SessionState.Faulted && supervisor.Status.Failure != null, "Startup failure hidden.");
                    Check(!supervisor.Status.SceneLoaded, "Failed startup published Ready.");
                    if (scenario is "startup-timeout" or "startup-cancel") Check(error is OperationCanceledException, "Cancellation/deadline cause lost.");
                    else if (scenario != "launch-failure") Check(error is InvalidDataException, "Identity/scene mismatch not rejected.");
                    Check(supervisor.Status.Failure!.Phase == (scenario == "bad-scene" ? "LoadScene" : scenario == "launch-failure" ? "Launch" : "Hello"), "Startup failure phase wrong.");
                    await supervisor.StartAsync(state.Snapshot()); await supervisor.StopAsync();
                    Check(supervisor.Status.Identity!.Generation == 2, "Failed generation reused.");
                }
                else
                {
                    if (scenario == "stale") { await supervisor.StartAsync(state.Snapshot()); await supervisor.StopAsync(); }
                    var behavior = scenario switch
                    {
                        "stale" => "stale-status", "crash" => "crash-status", "disconnect" => "disconnect-status",
                        "command-timeout" or "command-cancel" or "stop-pending" or "heartbeat" => "hang-status",
                        "forced-stop" => "ignore-stop", "hang-stop" => "hang-stop", _ => throw new ArgumentException("Unknown scenario.")
                    };
                    await supervisor.StartAsync(state.Snapshot(), behavior);
                    if (scenario is "forced-stop" or "hang-stop")
                    {
                        var stopTime = Stopwatch.StartNew();
                        await supervisor.StopAsync();
                        Check(factory.Diagnostics.Any(d => d.Stage == UnitProcessStage.Killing), "Forced stop did not exercise owned kill.");
                        if (scenario == "hang-stop")
                        {
                            Check(stopTime.Elapsed < TimeSpan.FromSeconds(5.5), "RPC and process stop received separate 3s graceful budgets.");
                            Check(supervisor.Status.Failure?.Phase == "StopAcknowledgement", "Missing Stop acknowledgement deadline cause."); failures++;
                        }
                    }
                    else if (scenario == "heartbeat")
                    {
                        await WaitAsync(() => supervisor.Status.State is SessionState.Faulted or SessionState.CleanupFailed, TimeSpan.FromSeconds(9));
                        // Failure is published before cleanup completes; Stop serializes behind it.
                        await supervisor.StopAsync();
                        Check(supervisor.Status.Failure?.Phase == "Heartbeat", "Missing heartbeat failure."); failures++;
                    }
                    else
                    {
                        using var cancellation = new CancellationTokenSource();
                        var pending = supervisor.GetStatusAsync(cancellation.Token);
                        if (scenario is "command-cancel" or "stop-pending")
                        {
                            await Records.WaitAsync(Path.Combine(directory, $"held-{supervisor.Status.Identity!.Generation}.json"));
                            if (scenario == "command-cancel") cancellation.Cancel(); else await supervisor.StopAsync();
                        }
                        var error = await FailureAsync(pending); failures++;
                        Check(supervisor.Status.Failure?.Phase == "GetStatus", "Command failure phase missing.");
                        if (scenario == "stale") Check(error is InvalidDataException, "Stale generation accepted.");
                        if (scenario is "command-timeout" or "command-cancel" or "stop-pending") Check(error is OperationCanceledException, "Pending local wait not cancelled.");
                        if (scenario is "crash" or "disconnect") Check(error is IOException or ObjectDisposedException, "Disconnect cause hidden.");
                    }
                    Check(factory.Leases.All(l => l.Reaped && l.ResourcesReleased), "Failed generation not reaped/released.");
                    await supervisor.StartAsync(state.Snapshot()); await supervisor.StopAsync();
                }
                Check(!lifetime.AppCancellationToken.IsCancellationRequested, "Editor lifetime cancelled by runtime failure.");
                Check(ReferenceEquals(app.Services.Resolve<AuthoringState>(), state) && JsonSerializer.Serialize(state) == before, "Authoring state changed.");
                Check(!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Game.Logic"), "Editor loaded the child-only Game.Logic assembly.");
            }
            finally { await supervisor.StopAsync(); await app.StopAsync(); }
            Check(factory.Leases.All(l => l.Reaped && l.ResourcesReleased), "Owned resources remain after shutdown.");
            return new { Passed = true, Scenario = scenario, EditorPid = Environment.ProcessId, FailuresObserved = failures,
                AuthoringPreserved = JsonSerializer.Serialize(state) == before, Generation = supervisor.Status.Identity?.Generation,
                Leases = factory.Leases.Select(l => new { l.ProcessId, l.Reaped, l.ResourcesReleased, l.StandardError, l.StandardOutput }).ToArray(),
                Diagnostics = factory.Diagnostics.Select(d => new { Stage = d.Stage.ToString(), d.ProcessId, d.Reason, d.ExitCode }).ToArray(), Trace = trace.ToArray() };
        }
        public static async Task<int> RunParentAsync(string directory)
        {
            var factory = new NativeRuntimeFactory(NativeRuntimeLaunch.CurrentProcess("--runtime")); var state = new AuthoringState();
            await using var app = CreateApp(factory, state, directory); await app.StartAsync();
            var supervisor = app.Services.Resolve<AetherHost>()!.Supervisor;
            await supervisor.StartAsync(state.Snapshot());
            Check(!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Game.Logic"), "Parent Editor loaded Game.Logic.");
            Records.Write(directory, "parent-ready.json", new { ParentPid = Environment.ProcessId, ChildPid = supervisor.Status.ProcessId });
            await Task.Delay(TimeSpan.FromSeconds(90)); return 3;
        }
        private static async Task<object> ParentExitAsync(string directory)
        {
            using var parent = new Process { StartInfo = OwnStartInfo("--parent-wait", directory) };
            parent.Start(); var stdout = parent.StandardOutput.ReadToEndAsync(); var stderr = parent.StandardError.ReadToEndAsync();
            int childId = 0;
            try
            {
                await Records.WaitAsync(Path.Combine(directory, "parent-ready.json"));
                using var ready = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "parent-ready.json")));
                childId = ready.RootElement.GetProperty("ChildPid").GetInt32();
                // Kill only this owned parent. Tree-kill would invalidate the parent-exit proof.
                parent.Kill(entireProcessTree: false); await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                await Records.WaitAsync(Path.Combine(directory, "runtime-closed-1.json"));
                await WaitAsync(() => !CensusContains(childId), TimeSpan.FromSeconds(5));
                return new { Passed = true, Scenario = "parent-exit", ParentPid = parent.Id, ChildPid = childId,
                    ParentReaped = parent.HasExited, RuntimeExited = true, Stdout = await stdout, Stderr = await stderr };
            }
            finally
            {
                if (!parent.HasExited) { parent.Kill(entireProcessTree: true); await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
            }
        }
        // Read-only census for an orphan-exit assertion, never an ownership/kill path.
        private static bool CensusContains(int pid)
        {
            var found = false;
            foreach (var process in Process.GetProcesses()) { using (process) { if (process.Id == pid) found = true; } }
            return found;
        }
        public static ProcessStartInfo OwnStartInfo(params string[] arguments)
        {
            var info = new ProcessStartInfo(Environment.ProcessPath!)
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            if (string.Equals(Path.GetFileNameWithoutExtension(info.FileName), "dotnet", StringComparison.OrdinalIgnoreCase))
                info.ArgumentList.Add(Assembly.GetEntryAssembly()!.Location);
            foreach (var argument in arguments) info.ArgumentList.Add(argument);
            return info;
        }
        public static async Task WaitAsync(Func<bool> condition, TimeSpan timeout)
        {
            using var limit = new CancellationTokenSource(timeout);
            while (!condition()) await Task.Delay(10, limit.Token);
        }
        internal static async Task<Exception> FailureAsync(Task task)
        {
            try { await task.WaitAsync(TimeSpan.FromSeconds(25)); }
            catch (Exception error) when (error is not TimeoutException) { return error; }
            throw new InvalidOperationException("Expected operation failure.");
        }
        internal static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
