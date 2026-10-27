using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Aether;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Mithril;

namespace EngineSessionProbe;

// Headless executable acceptance: no compiler, frame surface, UI or copied fault runtime.
internal static class RuntimeLaunchProbe
{
    internal static async Task<int> RunAsync(string runtimePath, string directory)
    {
        if (Directory.Exists(directory)) throw new IOException("Use a fresh runtime-launch proof directory.");
        Directory.CreateDirectory(directory);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(100));
        var generations = new List<object>(); var cases = new List<object>();
        NativeRuntimeLaunch? runtime = null; string? hostId = null; object? artifactReport = null; object? sameExecutable = null;
        Exception? failure = null;
        try
        {
            runtime = Command(runtimePath);
            var hostEntry = Assembly.GetEntryAssembly()!.Location;
            Probe.Check(!SamePath(hostEntry, runtime.RuntimeAssemblyPath), "Proof requires different managed host/runtime entry assemblies.");
            Probe.Check(!GameLoaded(), "Proof parent already loaded Game.Logic.");
            hostId = BuildIdentity.ForRuntime(runtime.RuntimeAssemblyPath);
            Probe.Check(hostId != BuildIdentity.HostId, "Independent runtime unexpectedly has the same-executable fingerprint.");
            await using (var app = CreateApp(runtime, hostId, directory))
            {
                await app.StartAsync(deadline.Token);
                var factory = (NativeRuntimeFactory)app.Services.Resolve<IRuntimeFactory>()!;
                var session = Guid.NewGuid();
                var first = Preview(hostId, session, 1, Path.Combine(directory, "preview-1"));
                var second = Preview(hostId, session, 2, Path.Combine(directory, "preview-2"));
                Probe.Check(first.Identity.Nonce != second.Identity.Nonce && first.Endpoint != second.Endpoint,
                    "Preview generations reused a private capability.");
                generations.Add(await RunGenerationAsync(factory, first, runtime.RuntimeAssemblyPath, false, deadline.Token));
                generations.Add(await RunGenerationAsync(factory, second, runtime.RuntimeAssemblyPath, false, deadline.Token));

                // Publish bytes already built with the probe. Never resolve a game Type in this parent.
                var artifact = GameBuildArtifact.Publish(AppContext.BaseDirectory, Path.Combine(directory, "artifacts"),
                    hostId, "Game.Logic.dll", "Game.Logic.ProbeGame", 7, cancellationToken: deadline.Token);
                artifactReport = new { artifact.BuildId, artifact.DirectoryPath, artifact.EntryAssembly,
                    Sha256 = FileHash(Path.Combine(artifact.DirectoryPath, artifact.EntryAssembly)), CompilerStarted = false };
                var game = new SessionLaunch(new(session, 3, hostId, artifact.BuildId, Nonce()),
                    Endpoint(), Path.Combine(directory, "selected-game"), Build: new(artifact.DirectoryPath, artifact.BuildId));
                generations.Add(await RunGenerationAsync(factory, game, runtime.RuntimeAssemblyPath, false, deadline.Token));
                cases.Add(await RejectHostFingerprintAsync(factory, hostId, directory, deadline.Token));
                cases.Add(await RejectChildAsync(runtime, Preview(ChangeHash(hostId), Guid.NewGuid(), 1,
                    Path.Combine(directory, "child-host-fingerprint")) with
                { RuntimeIdentity = new(hostEntry, runtime.RuntimeAssemblyPath) }, "child-host-fingerprint", directory, deadline.Token));
                // Deliberately bypass NativeRuntimeFactory: the actual child must reject a valid
                // same-entry fingerprint whose claimed runtime entry is the parent, not itself.
                cases.Add(await RejectChildAsync(runtime, Preview(BuildIdentity.HostId, Guid.NewGuid(), 1,
                    Path.Combine(directory, "child-actual-entry")) with
                { RuntimeIdentity = new(hostEntry, hostEntry) }, "child-actual-entry", directory, deadline.Token));
                cases.Add(await StartupFailureAsync(runtime, artifact, Path.Combine(directory, "startup-cancel"), true, deadline.Token));
                var invalidCommand = new NativeRuntimeLaunch(runtime.ExecutablePath, runtime.RuntimeAssemblyPath,
                    [.. runtime.Arguments, "--invalid-runtime-option"], runtime.WorkingDirectory);
                cases.Add(await StartupFailureAsync(invalidCommand, artifact, Path.Combine(directory, "immediate-exit"), false, deadline.Token));
                await app.StopAsync();
                Probe.Check(factory.Leases.All(lease => lease.Reaped && lease.ResourcesReleased), "Independent factory retained owned resources.");
            }
            sameExecutable = await SameExecutableAsync(Path.Combine(directory, "same-executable"), deadline.Token);
            Probe.Check(!GameLoaded(), "Proof parent loaded the selected game.");
            Probe.Check(hostId == BuildIdentity.ForRuntime(runtime.RuntimeAssemblyPath), "Host/runtime binaries changed during proof.");
        }
        catch (Exception error) { failure = error; }
        finally
        {
            Records.Write(directory, "runtime-launch-report.json", new
            {
                Passed = failure == null, Scenario = "independent-runtime-launch", HostPid = Environment.ProcessId,
                HostAssemblyPath = Assembly.GetEntryAssembly()!.Location, RuntimeAssemblyPath = runtime?.RuntimeAssemblyPath,
                RuntimeCommandKind = runtimePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? "apphost" : "dotnet",
                HostContractId = hostId, SameExecutableHostId = BuildIdentity.HostId,
                RuntimeDirectoryHasGameFile = runtime != null && File.Exists(Path.Combine(Path.GetDirectoryName(runtime.RuntimeAssemblyPath)!, "Game.Logic.dll")),
                GameLoadedInParent = GameLoaded(), Generations = generations, Cases = cases,
                Artifact = artifactReport, SameExecutable = sameExecutable, Error = failure?.ToString()
            });
        }
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        return 0;
    }

    private static NativeRuntimeLaunch Command(string path)
    {
        if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return new(path, Path.ChangeExtension(path, ".dll"));
        if (!path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Select a runtime .dll or Windows apphost .exe.", nameof(path));
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Missing parent executable path.");
        if (!string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("For a runtime DLL, launch this proof parent through dotnet; otherwise pass the runtime apphost .exe.");
        return new(executable, path, [path]);
    }

    private static IApp CreateApp(NativeRuntimeLaunch runtime, string hostId, string directory, Action<SessionStatus>? observer = null)
        => App.Build(context =>
        {
            context.UseDiagnostics(new DiagnosticPipelineOptions
            {
                JsonDirectory = Path.Combine(directory, "diagnostics", "host"), JsonUseRunDirectories = true
            });
            context.UseAether(options =>
            {
                options.HostContractId = hostId; options.GameBuildId = BuildIdentity.PreviewId;
                options.ReportDirectory = directory; options.RuntimeLaunch = runtime; options.StatusChanged = observer;
                options.Deadlines = new() { Startup = TimeSpan.FromSeconds(15) };
            });
        });

    private static async Task<object> RunGenerationAsync(NativeRuntimeFactory factory, SessionLaunch launch,
        string runtimeAssembly, bool sameExecutable, CancellationToken token)
    {
        await using var lease = (NativeRuntimeLease)factory.Create(launch);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(token); limit.CancelAfter(TimeSpan.FromSeconds(15));
        ulong sequence = 0; ulong tick = 0;
        async Task<ControlReply> Exchange(CommandKind kind)
        {
            var request = ControlRequest.Create(launch.Identity, ++sequence, kind);
            if (kind == CommandKind.LoadScene) { request.SceneId = "runtime-launch-proof"; request.Revision = 7; request.Entities = []; }
            var reply = await lease.SendAsync(request, limit.Token).WaitAsync(limit.Token);
            SessionSupervisor.ValidateReply(launch.Identity, sequence, kind, lease.ProcessId, reply);
            return reply;
        }
        try
        {
            await lease.LaunchAsync(limit.Token);
            Probe.Check(lease.ProcessId > 0 && lease.ProcessId != Environment.ProcessId, "Runtime is not a separate owned process.");
            Probe.Check((await Exchange(CommandKind.Hello)).Phase == "HelloAccepted", "Hello was not accepted.");
            var loaded = await Exchange(CommandKind.LoadScene);
            Probe.Check(loaded.Phase == "Paused" && loaded.Revision == 7 && loaded.Tick == 0, "Scene acknowledgement changed.");
            Probe.Check((await Exchange(CommandKind.SetMode)).Phase == "Paused", "Paused mode was not applied.");
            var status = await Exchange(CommandKind.GetStatus);
            Probe.Check(status.Phase == "Paused" && status.Revision == 7 && status.Tick == 0, "Fresh runtime state changed.");
            if (launch.Role == RuntimeRole.Game)
            {
                var stepped = await Exchange(CommandKind.Step); tick = unchecked((ulong)stepped.Tick);
                Probe.Check(stepped.Phase == "Paused" && tick == 1, "Selected game did not execute exactly one tick.");
            }
            Probe.Check((await Exchange(CommandKind.Stop)).Phase == "Stopping", "Stop acknowledgement missing.");
        }
        finally { await lease.StopAsync(); }
        Probe.Check(lease.Reaped && lease.ResourcesReleased, "Successful runtime was not reaped/released.");
        var metadataFile = Path.Combine(launch.ReportDirectory, $"{(sameExecutable ? "preview-runtime" : "runtime")}-{launch.Identity.Generation}.json");
        var metadata = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(metadataFile, token));
        Probe.Check(metadata.GetProperty("Pid").GetInt32() == lease.ProcessId &&
            metadata.GetProperty("Role").GetInt32() == (int)launch.Role &&
            metadata.GetProperty("Resources").GetInt32() == 0 && metadata.GetProperty("Handles").GetInt32() == 0,
            "Runtime completion identity/resource metadata changed.");
        Probe.Check(metadata.GetProperty("GameLoaded").GetBoolean() == (launch.Role == RuntimeRole.Game), "Runtime game-load role isolation failed.");
        if (!sameExecutable)
        {
            Probe.Check(SamePath(metadata.GetProperty("EntryAssemblyPath").GetString()!, runtimeAssembly) &&
                SamePath(metadata.GetProperty("RuntimeAssemblyPath").GetString()!, runtimeAssembly) &&
                SamePath(metadata.GetProperty("HostAssemblyPath").GetString()!, Assembly.GetEntryAssembly()!.Location) &&
                metadata.GetProperty("HostContractId").GetString() == launch.Identity.HostContractId,
                "Runtime did not execute the selected counterpart.");
            Probe.Check(!metadata.GetProperty("EditorLoaded").GetBoolean() && !metadata.GetProperty("UiLoaded").GetBoolean(), "Standalone runtime loaded Editor/UI.");
            Probe.Check(metadata.GetProperty("Error").ValueKind == JsonValueKind.Null, "Runtime cleanup reported an error.");
            if (launch.Build is { } build)
            {
                var paths = metadata.GetProperty("GameAssemblyPaths").EnumerateArray().Select(value => value.GetString()!).ToArray();
                Probe.Check(paths.Length == 1 && SamePath(paths[0], Path.Combine(build.DirectoryPath, "Game.Logic.dll")), "Runtime loaded game from outside the selected artifact.");
            }
        }
        return new { launch.Identity.SessionId, launch.Identity.Generation, Role = launch.Role.ToString(), lease.ProcessId, lease.Reaped, lease.ResourcesReleased,
            Controls = sequence, Tick = tick, HelloAccepted = true, SceneLoaded = true, MetadataFile = metadataFile, Metadata = metadata,
            lease.StandardError, lease.StandardOutput };
    }

    private static async Task<object> RejectHostFingerprintAsync(NativeRuntimeFactory factory, string hostId, string directory, CancellationToken token)
    {
        var launch = Preview(ChangeHash(hostId), Guid.NewGuid(), 1, Path.Combine(directory, "host-fingerprint"));
        await using var lease = (NativeRuntimeLease)factory.Create(launch);
        Exception error;
        try { error = await ExpectedFailureAsync(() => lease.LaunchAsync(token)); }
        finally { await lease.StopAsync(); }
        Probe.Check(error is InvalidDataException && lease.ProcessId == 0 && lease.Reaped && lease.ResourcesReleased,
            "Wrong host fingerprint was not rejected before process start with confirmed cleanup.");
        return new { Name = "host-fingerprint", Passed = true, lease.ProcessId, lease.Reaped, lease.ResourcesReleased,
            ErrorType = error.GetType().Name, RejectedBeforeProcess = true };
    }

    private static async Task<object> RejectChildAsync(NativeRuntimeLaunch runtime, SessionLaunch launch, string name,
        string directory, CancellationToken token)
    {
        Directory.CreateDirectory(launch.ReportDirectory);
        var info = new ProcessStartInfo(runtime.ExecutablePath)
        {
            WorkingDirectory = runtime.WorkingDirectory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in runtime.Arguments) info.ArgumentList.Add(argument);
        info.Environment[NativeRuntimeFactory.BootstrapKey] = RuntimeBootstrap.Encode(launch);
        info.Environment["-p"] = Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        info.Environment.Remove(UnitParentProcessEventsHandler.ControlPipeKey);
        using var process = new Process { StartInfo = info };
        token.ThrowIfCancellationRequested();
        Probe.Check(process.Start(), "Direct negative runtime did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        var stdoutFile = Path.Combine(directory, name + ".stdout.log"); var stderrFile = Path.Combine(directory, name + ".stderr.log");
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(token); limit.CancelAfter(TimeSpan.FromSeconds(15));
        try { await process.WaitForExitAsync(limit.Token); }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
            await File.WriteAllTextAsync(stdoutFile, await stdout.WaitAsync(TimeSpan.FromSeconds(5)));
            await File.WriteAllTextAsync(stderrFile, await stderr.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        var error = await stderr;
        Probe.Check(process.ExitCode == 1 && error.Contains(nameof(InvalidDataException), StringComparison.Ordinal) &&
            error.Contains("fingerprint", StringComparison.OrdinalIgnoreCase), "Child did not independently reject the bootstrap fingerprint/actual entry.");
        Probe.Check(!Directory.Exists(Path.Combine(launch.ReportDirectory, "diagnostics")) &&
            !File.Exists(Path.Combine(launch.ReportDirectory, $"runtime-{launch.Identity.Generation}.json")), "Rejected bootstrap created App resources/completion metadata.");
        return new { Name = name, Passed = true, ProcessId = process.Id, Reaped = process.HasExited, ResourcesReleased = true,
            process.ExitCode, RejectedBeforeApp = true, ParentPrecheckBypassed = true, StdoutFile = stdoutFile, StderrFile = stderrFile };
    }

    private static async Task<object> StartupFailureAsync(NativeRuntimeLaunch runtime, GameBuildArtifact artifact,
        string directory, bool cancel, CancellationToken token)
    {
        using var startup = CancellationTokenSource.CreateLinkedTokenSource(token);
        startup.CancelAfter(TimeSpan.FromSeconds(15));
        var cancellationAfterLaunch = false;
        await using var app = CreateApp(runtime, artifact.HostContractId, directory, status =>
        {
            if (cancel && status.State == SessionState.Starting && status.ProcessId is > 0)
            { cancellationAfterLaunch = true; startup.Cancel(); }
        });
        await app.StartAsync(token);
        var engine = app.Services.Resolve<AetherHost>()!;
        var factory = (NativeRuntimeFactory)app.Services.Resolve<IRuntimeFactory>()!;
        var error = await ExpectedFailureAsync(() => engine.Supervisor.StartAsync(new SceneSnapshot("runtime-launch-proof", 7, []), artifact, token: startup.Token));
        var status = engine.Supervisor.Status;
        var lease = factory.Leases.Single();
        Probe.Check(status.State == SessionState.Faulted && !status.SceneLoaded && !status.HelloAccepted &&
            status.Failure?.Phase == "Hello" && lease.ProcessId > 0, "Failed startup published readiness or lost its phase/owned PID.");
        if (cancel) Probe.Check(cancellationAfterLaunch && error is OperationCanceledException, "Startup cancellation did not occur after an actual launch.");
        else Probe.Check(error is IOException or ObjectDisposedException, "Immediate child exit lost its transport/exit failure.");
        Probe.Check(!app.Services.Resolve<IAppLifetime>()!.AppCancellationToken.IsCancellationRequested, "Runtime startup failure cancelled the parent App.");
        await engine.Supervisor.StopAsync(); await app.StopAsync();
        Probe.Check(lease.Reaped && lease.ResourcesReleased, "Failed startup retained an owned process.");
        return new { Name = cancel ? "startup-cancel" : "immediate-exit", Passed = true, lease.ProcessId, lease.Reaped, lease.ResourcesReleased,
            ErrorType = error.GetType().Name, Phase = status.Failure!.Phase, status.HelloAccepted, status.SceneLoaded,
            CancellationAfterLaunch = cancellationAfterLaunch, lease.StandardError, lease.StandardOutput };
    }

    private static async Task<object> SameExecutableAsync(string directory, CancellationToken token)
    {
        var runtime = NativeRuntimeLaunch.CurrentProcess("--runtime");
        var hostId = BuildIdentity.ForRuntime(runtime.RuntimeAssemblyPath);
        Probe.Check(hostId == BuildIdentity.HostId, "Explicit same-executable identity is no longer the legacy HostId.");
        await using var app = CreateApp(runtime, hostId, directory);
        await app.StartAsync(token);
        var factory = (NativeRuntimeFactory)app.Services.Resolve<IRuntimeFactory>()!;
        var generation = await RunGenerationAsync(factory, Preview(hostId, Guid.NewGuid(), 1, directory), runtime.RuntimeAssemblyPath, true, token);
        await app.StopAsync();
        return new { Passed = true, IdentityMatchesLegacyHostId = true, HostContractId = hostId,
            HostAssemblyPath = Assembly.GetEntryAssembly()!.Location, runtime.RuntimeAssemblyPath, Generation = generation };
    }

    private static SessionLaunch Preview(string hostId, Guid session, ulong generation, string directory)
        => new(new(session, generation, hostId, BuildIdentity.PreviewId, Nonce()), Endpoint(), directory, Role: RuntimeRole.EditPreview);
    private static string Nonce() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static string Endpoint() => "engine-runtime-proof-" + Guid.NewGuid().ToString("N");
    private static string ChangeHash(string value) => (value[0] == '0' ? "1" : "0") + value[1..];
    private static bool GameLoaded() => AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == "Game.Logic");
    private static bool SamePath(string first, string second) => string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
    private static string FileHash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static async Task<Exception> ExpectedFailureAsync(Func<Task> operation)
    {
        try { await operation(); }
        catch (Exception error) { return error; }
        throw new InvalidOperationException("Expected the runtime operation to fail.");
    }
}
