using System.Diagnostics;
using System.Text.Json;
using System.Security.Cryptography;
using TrueMoon;
using TrueMoon.Aether.Editor;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Projects;
using TrueMoon.Aether.Editing;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Builds;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;

namespace AetherGuiConsumer;

// Uses the real compiler, RPC, child and native viewport. Only the consumer game's
// RestoreCheckpoint holds a bounded file barrier; the framework has no acceptance hooks.
internal static class ReplaceInterruptWorkflow
{
    internal static async Task<object> Run(string directory, HostedUiWindow window, UiSession ui,
        EditorView view, IApp app, string scenario, CancellationToken token)
    {
        var parts = scenario.Split('-'); var phase = parts[1]; var action = parts[2];
        var engine = app.Services.Resolve<AetherHost>()!;
        var documents = app.Services.Resolve<EditorDocumentCommands>()!;
        var projects = app.Services.Resolve<EditorProjects>()!;
        var workspace = app.Services.Resolve<EditorWorkspace>()!;
        var playback = app.Services.Resolve<EditorScenePlayback>()!;
        var runtimes = (NativeRuntimeFactory)app.Services.Resolve<IRuntimeFactory>()!;
        var maxLive = 0;
        void Progress(string message) => File.AppendAllText(Path.Combine(directory, "progress.log"), message + Environment.NewLine);
        await ui.InvokeAsync(() => Program.Check(documents.Execute(DocumentCommand.Open, "Original.aetherscene"), "Open was rejected.")).WaitAsync(token);
        await documents.Pending.WaitAsync(token);
        Program.Check(documents.Read().Error == null, "Open failed: " + documents.Read().Error);
        var document = projects.Active!.Documents!;
        await Until(() => workspace.EntityCount == 2);
        await ui.InvokeAsync(() => workspace.Select(Program.CubeId.ToString("D"))).WaitAsync(token);
        await Until(() => view.Inspector!.Properties.Values.Any(p => p.Property.Key == "x" && p.Input.IsEffectivelyEnabled));
        var field = (TextBox)view.Inspector!.Properties.Values.Single(p => p.Property.Key == "x").Input;
        await ui.InvokeAsync(() =>
        {
            ui.Focus(field); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
            ui.HandleInput(new(InputKind.Text, Text: "1.65"));
            ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Enter, Control: true));
        }).WaitAsync(token);
        await Until(() => document.Read().IsDirty && document.Read().Content!.UndoCount == 1);
        var content = document.Read().Content!; var selected = workspace.SelectedId;
        await ui.InvokeAsync(() => Program.Check(playback.RunSimulation(), "Start was rejected.")).WaitAsync(token);
        await playback.Pending.WaitAsync(token);
        Program.Check(playback.Error == null && engine.Supervisor.Status.State == SessionState.Playing, "Game startup failed: " + playback.Error);
        var origin = engine.Supervisor.Status; var old = runtimes.Leases.Single();
        Program.Check(old.ProcessId == origin.ProcessId && !old.ResourcesReleased, "Missing actual old game.");
        var startup = engine.Builds.ReadDiagnostics(16, 0).Single(job => job.Source == "Build" && job.GameBuildId == origin.Identity!.GameBuildId);
        var startupDirectory = Path.Combine(Path.GetDirectoryName(engine.Builds.DiagnosticsDirectory!)!, startup.JobId!);
        var originalBuild = GameBuildArtifact.Open(Path.Combine(Directory.GetParent(startupDirectory)!.Parent!.FullName, origin.Identity!.GameBuildId),
            origin.Identity.HostContractId, origin.Identity.GameBuildId);
        using (var capture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(startupDirectory, "capture.json"), token)))
        {
            var dependencies = capture.RootElement.GetProperty("Projects");
            Program.Check(dependencies.GetArrayLength() == 1 && dependencies[0].GetProperty("EntryAssembly").GetString() == "Game.Shared.dll" &&
                engine.Editor.Status.Context!.Build.CaptureProjectReferences, "Interrupt fixture did not capture its private project graph.");
            Program.Check(engine.Editor.Status.Context!.Build.PackageDirectory != null && capture.RootElement.GetProperty("Files").EnumerateArray().Any(file =>
                file.GetProperty("Path").GetString()!.EndsWith(Path.Combine("packages", "game.packaged", "1.0.0", "Game.Packaged.dll"))),
                "Interrupt fixture did not capture its locked package DLL.");
        }
        var source = Path.Combine(Path.GetDirectoryName(projects.Active.Project.GameProjectPath)!, "Game.cs");
        var code = await File.ReadAllTextAsync(source, token);
        var gate = Path.Combine(directory, "restore");
        var candidate = code.Replace("0xff2080c0", "0xff2080c2", StringComparison.Ordinal);
        if (phase == "restore") candidate = candidate.Replace("RestoreGate = null", "RestoreGate = " + JsonSerializer.Serialize(gate), StringComparison.Ordinal);
        Program.Check(candidate != code, "Candidate code did not change.");
        await File.WriteAllTextAsync(source, candidate, token);
        Progress("Replace " + phase + "/" + action);
        await ui.InvokeAsync(() => Program.Check(playback.Execute(EditorCommand.Replace), "Replace was rejected.")).WaitAsync(token);
        Task<bool>? exiting = null;
        int? compilerPid = null; int? candidatePid = null;
        string? compilerJob = null; string? sdkLog = null; string? payloadHash = null;
        string? packageArchiveHash = null; string? packagePayloadHash = null;
        DateTime? sdkStartedUtc = null; DateTime? sdkLogCreatedUtc = null;
        var interrupted = false;
        GameBuildArtifact? observedCandidate = null;
        // Observe the phase and submit the interrupt in the same UI owner invocation.
        // Bind the dependency/root-query marker to the live PID, not an earlier SDK phase.
        try
        {
            await Until(() =>
            {
                if (interrupted) return true;
                var status = engine.Editor.Status;
                observedCandidate ??= status.Candidate?.Build;
                var observed = phase switch
                {
                    "build" => status.Operation == GameEditorOperation.Build && ObserveSdk(dependency: true),
                    "verify" => status.Operation == GameEditorOperation.Replace && ObserveSdk(dependency: false) &&
                        document.Read().Operation == DocumentOperation.Runtime,
                    "restore" => RestoreEntered(),
                    _ => throw new InvalidOperationException("Unknown interrupt phase.")
                };
                if (!observed) return false;
                if (phase == "restore")
                {
                    Program.Check(observedCandidate != null && engine.Supervisor.Status.Identity?.GameBuildId == observedCandidate.BuildId,
                        "Restore child does not match the candidate observed during preflight.");
                    ObservePackage(null, observedCandidate!);
                    compilerJob = engine.Builds.DiagnosticsDirectory;
                    Program.Check(old is { Reaped: true, ResourcesReleased: true } && runtimes.Leases.Count == 2 &&
                        runtimes.Leases.Last().ProcessId == candidatePid, "Restore preceded old-child reap or targeted another process.");
                }
                else Program.Check(engine.Supervisor.Status.Identity == origin.Identity && !old.ResourcesReleased &&
                    engine.Supervisor.Status.State == SessionState.Playing && runtimes.Leases.Count == 1, "Preparation changed the old runtime.");
                if (action == "exit") exiting = window.RequestCloseAsync();
                else Program.Check(playback.Execute(action == "cancel" ? EditorCommand.Cancel : EditorCommand.Stop), "Interrupt was rejected.");
                Program.Check(!playback.Pending.IsCompleted && !playback.CanExecute(EditorCommand.Replace) &&
                    !documents.CanExecute(DocumentCommand.Save) && !documents.Execute(DocumentCommand.New), "Admission reopened before actual drain.");
                interrupted = true;
                return true;
            });
            if (phase == "restore")
            {
                // Native restore is still held while the parent begins cancellation/cleanup.
                Program.Check(!playback.Pending.IsCompleted, "Replace completed before restore barrier release.");
                await File.WriteAllTextAsync(gate + ".release", "release", token);
            }
            await playback.Pending.WaitAsync(token);
            if (action == "exit")
            {
                await Until(() => documents.Read().AwaitingDecision || exiting!.IsCompleted);
                Program.Check(ReferenceEquals(content, document.Read().Content) && document.Read().IsDirty &&
                    content.UndoCount == 1 && workspace.SelectedId == selected, "Exit drain changed the parent before its close decision.");
                Program.Check(documents.Choose(DocumentCloseChoice.Discard), "Dirty exit decision was not offered.");
                Program.Check(await exiting!.WaitAsync(token), "Exit was vetoed.");
                await window.Completion.WaitAsync(token);
            }
            else Program.Check(ReferenceEquals(content, document.Read().Content) && document.Read().IsDirty &&
                content.UndoCount == 1 && workspace.SelectedId == selected, "Interrupt changed authoring state/history/selection.");
        }
        finally { if (phase == "restore") File.WriteAllText(gate + ".release", "release"); }
        Program.Check(engine.Builds.ProcessId == null && (compilerPid == null || Exited(compilerPid.Value)), "Compiler process did not drain.");
        if (compilerJob != null)
            foreach (var staging in new[] { "preparation", "capture", "graph", "work", "output" })
                Program.Check(!Directory.Exists(Path.Combine(compilerJob, staging)), "Interrupted graph staging did not drain: " + staging);
        Program.Check(engine.Builds.ReadDiagnostics(16, 0).All(job => job.Completed && job.ResourcesReleased), "Compiler diagnostics still expose an active owner.");
        var failure = playback.Error as GameReplacementException;
        var statusAfter = engine.Supervisor.Status;
        var expectedChildren = phase == "restore" ? (action == "cancel" ? 3 : 2) : 1;
        Program.Check(runtimes.Leases.Count == expectedChildren, "Interrupt launched an unexpected late/recovery child.");
        if (action == "cancel")
        {
            if (phase == "restore")
                Program.Check(failure is { RollbackAttempted: true, RollbackSucceeded: true, RecoveryError: null } &&
                    statusAfter.Identity != origin.Identity && statusAfter.Identity!.GameBuildId == origin.Identity!.GameBuildId &&
                    statusAfter.State == SessionState.Playing && statusAfter.Tick >= origin.Tick &&
                    runtimes.Leases.Take(2).All(l => l.Reaped && l.ResourcesReleased) &&
                    playback.OwnsRuntime && playback.CanExecute(EditorCommand.Replace), "Cancel failed to recover old code/state/mode/ownership.");
            else Program.Check(failure == null && playback.Error == null && statusAfter.Identity == origin.Identity &&
                statusAfter.State == SessionState.Playing && playback.OwnsRuntime && playback.CanExecute(EditorCommand.Replace),
                "Preparation Cancel did not retain the old game.");
            await ui.InvokeAsync(() => Program.Check(playback.Execute(EditorCommand.Stop), "Final Stop was rejected.")).WaitAsync(token);
            await playback.Pending.WaitAsync(token);
        }
        else
        {
            Program.Check(statusAfter.State == SessionState.Stopped && (phase != "restore" ||
                failure is { RollbackAttempted: false, RollbackSucceeded: false, RecoveryError: null }), "Stop/exit allowed recovery or retained a running game.");
            if (action == "stop") Program.Check(!playback.OwnsRuntime && documents.CanExecute(DocumentCommand.Save), "Stop did not reopen document admission.");
        }
        if (action != "exit")
        {
            Program.Check(documents.Execute(DocumentCommand.SaveAs, "Interrupted.aetherscene"), "Save after drain was rejected.");
            await documents.Pending.WaitAsync(token);
            Program.Check(documents.Read().Error == null && !document.Read().IsDirty, "Save after drain failed.");
            Program.Check(await window.RequestCloseAsync().WaitAsync(token), "Final close was rejected.");
            await window.Completion.WaitAsync(token);
        }
        Program.Check(runtimes.Leases.Count == expectedChildren && runtimes.Leases.All(l => l.Reaped && l.ResourcesReleased) &&
            engine.Editor.Status.Operation == GameEditorOperation.Idle && engine.Builds.ProcessId == null && maxLive == 1, "Final drain admitted late work or retained a child.");
        Progress("Passed");
        return new { Phase = phase, Action = action, StageObserved = true, AdmissionClosed = true, CompilerDrained = true,
            ParentPreserved = true, RecoveryPolicy = true, NoLateChild = true, MaxLive = maxLive,
            ProjectGraphConfirmed = true, PackageConfirmed = true, StagingDrained = true,
            PackageArchiveSha512 = packageArchiveHash, PackagePayloadSha256 = packagePayloadHash,
            CompilerStage = phase == "build" ? "DependencySdkQuery" : phase == "verify" ? "GraphPrestartQuery" : "CandidateRestore",
            CompilerJob = compilerJob, SdkLog = sdkLog, SdkStartedUtc = sdkStartedUtc, SdkLogCreatedUtc = sdkLogCreatedUtc,
            GraphPayloadSha256 = payloadHash,
            Children = expectedChildren, OldPid = old.ProcessId, CandidatePid = candidatePid, CompilerPid = compilerPid,
            Recovered = failure?.RollbackSucceeded == true };

        bool ObserveSdk(bool dependency)
        {
            if (engine.Builds.ProcessId is not { } pid || engine.Builds.DiagnosticsDirectory is not { } job) return false;
            var pattern = dependency ? "graph-*-query.stdout.log" : "verify-query.stdout.log";
            var logs = Directory.GetFiles(job, pattern);
            if (logs.Length != 1) return false;
            try
            {
                using var process = Process.GetProcessById(pid);
                var started = process.StartTime.ToUniversalTime(); var created = File.GetCreationTimeUtc(logs[0]);
                var current = engine.Builds.ReadDiagnostics(1, 0).Single();
                if (process.HasExited || engine.Builds.ProcessId != pid || current.ProcessId != pid || current.Completed ||
                    current.StandardOutput?.ReadFinished != false || created < started) return false;
                if (!dependency)
                {
                    var candidateBuild = engine.Editor.Status.Candidate!.Build;
                    Program.Check(candidateBuild.Compilation!.Projects.Count == 1, "Candidate lost graph provenance.");
                    var staged = Path.Combine(job, "graph", "output", "Game.Shared.dll");
                    var published = Path.Combine(candidateBuild.DirectoryPath, "Game.Shared.dll");
                    Program.Check(new FileInfo(staged).Length is > 0 and < 1024 * 1024, "Missing bounded staged graph payload.");
                    var bytes = File.ReadAllBytes(staged);
                    Program.Check(bytes.SequenceEqual(File.ReadAllBytes(published)), "Graph verification staged an unselected private payload.");
                    payloadHash = Convert.ToHexString(SHA256.HashData(bytes));
                }
                ObservePackage(Path.Combine(job, "preparation", dependency ? "before" : "verify"),
                    dependency ? originalBuild : engine.Editor.Status.Candidate!.Build);
                if (process.HasExited || engine.Builds.ProcessId != pid) return false;
                compilerPid = pid; compilerJob = job; sdkLog = Path.GetFileName(logs[0]);
                sdkStartedUtc = started; sdkLogCreatedUtc = created;
                return true;
            }
            catch (ArgumentException) { return false; } // The observed owned process exited before its timestamp could be read.
            catch (InvalidOperationException) when (engine.Builds.ProcessId != pid) { return false; }
            catch (System.ComponentModel.Win32Exception) when (engine.Builds.ProcessId != pid || Exited(pid)) { return false; }
        }
        void ObservePackage(string? preparation, GameBuildArtifact selected)
        {
            var original = Path.Combine(projects.Active!.Project.RootDirectory, "Packages", "game.packaged.1.0.0.nupkg");
            var source = preparation == null ? original : Directory.GetFiles(preparation, "game.packaged.1.0.0.nupkg", SearchOption.AllDirectories).Single();
            var bytes = File.ReadAllBytes(source);
            Program.Check(bytes.Length is > 0 and < 1024 * 1024 && bytes.SequenceEqual(File.ReadAllBytes(original)), "Interrupted SDK did not use the captured package archive.");
            packageArchiveHash = Convert.ToBase64String(SHA512.HashData(bytes));
            using var locked = JsonDocument.Parse(File.ReadAllText(Path.Combine(projects.Active.Project.RootDirectory, "Shared", "packages.lock.json")));
            Program.Check(locked.RootElement.GetProperty("dependencies").GetProperty("net10.0").GetProperty("Game.Packaged").GetProperty("contentHash").GetString() == packageArchiveHash,
                "Observed archive differs from its locked content hash.");
            var published = File.ReadAllBytes(Path.Combine(selected.DirectoryPath, "Game.Packaged.dll"));
            var payload = preparation == null ? published : File.ReadAllBytes(Path.Combine(preparation, "packages", "game.packaged", "1.0.0", "Game.Packaged.dll"));
            Program.Check(payload.Length is > 0 and < 1024 * 1024 && payload.SequenceEqual(published), "Interrupted SDK used an unselected package DLL.");
            packagePayloadHash = Convert.ToHexString(SHA256.HashData(payload));
        }
        bool RestoreEntered()
        {
            try
            {
                if (!File.Exists(gate + ".entered") || !int.TryParse(File.ReadAllText(gate + ".entered"), out var pid)) return false;
                candidatePid = pid; return true;
            }
            catch (IOException) { return false; } // Wait for the child's short marker write to finish.
        }

        async Task Until(Func<bool> condition)
        {
            var wait = Stopwatch.StartNew();
            while (true)
            {
                var done = false;
                await ui.InvokeAsync(() =>
                {
                    maxLive = Math.Max(maxLive, runtimes.Leases.Count(l => !l.ResourcesReleased));
                    done = condition();
                }).WaitAsync(token);
                if (done) return;
                if (wait.Elapsed > TimeSpan.FromSeconds(30)) throw new TimeoutException("Native interrupt phase did not arrive: " + phase + "/" + action);
                await Task.Delay(10, token);
            }
        }
    }
    private static bool Exited(int pid)
    {
        try { using var process = Process.GetProcessById(pid); return process.HasExited; }
        catch (ArgumentException) { return true; }
    }
}
