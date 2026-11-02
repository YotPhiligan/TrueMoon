using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Editor;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Projects;
using TrueMoon.Aether.Editing;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Sessions;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;

namespace AetherGuiConsumer;

// Exercises the public GameEditor prestart API after UI scene capture/build.
// No timing hook or production barrier is needed: only owned consumer inputs change.
internal static class PackageInputWorkflow
{
    internal static async Task<object> Run(string directory, HostedUiWindow window, UiSession ui,
        EditorView view, IApp app, string scenario, CancellationToken token)
    {
        var parts = scenario.Split('-'); var operation = parts[1]; var change = parts[2];
        var replacing = operation == "replace";
        var engine = app.Services.Resolve<AetherHost>()!; var editor = engine.Editor;
        var documents = app.Services.Resolve<EditorDocumentCommands>()!;
        var projects = app.Services.Resolve<EditorProjects>()!;
        var workspace = app.Services.Resolve<EditorWorkspace>()!;
        var playback = app.Services.Resolve<EditorScenePlayback>()!;
        var runtimes = (NativeRuntimeFactory)app.Services.Resolve<IRuntimeFactory>()!;
        var root = projects.Active!.Project.RootDirectory;
        var source = Path.Combine(Path.GetDirectoryName(projects.Active.Project.GameProjectPath)!, "Game.cs");
        var notice = Path.Combine(directory, "checkpoint.entered");
        var code = await File.ReadAllTextAsync(source, token);
        Program.Check(code.Contains("CaptureNotice = null", StringComparison.Ordinal), "Missing consumer checkpoint marker.");
        await File.WriteAllTextAsync(source, code.Replace("CaptureNotice = null", "CaptureNotice = " + JsonSerializer.Serialize(notice), StringComparison.Ordinal), token);
        var sourceHash = Hash(source);
        var archive = Path.Combine(root, "Packages", "game.packaged.1.0.0.nupkg");
        var lockFile = Path.Combine(root, "Shared", "packages.lock.json");
        var initialArchive = Hash(archive); var initialLock = Hash(lockFile);
        var maxLive = 0;
        await ui.InvokeAsync(() => Program.Check(documents.Execute(DocumentCommand.Open, "Original.aetherscene"), "Open rejected.")).WaitAsync(token);
        await documents.Pending.WaitAsync(token);
        Program.Check(documents.Read().Error == null, "Open failed.");
        var document = projects.Active.Documents!;
        await Until(() => workspace.EntityCount == 2);
        await ui.InvokeAsync(() => workspace.Select(Program.CubeId.ToString("D"))).WaitAsync(token);
        await Until(() => view.Inspector!.Properties.Values.Any(property => property.Property.Key == "x" && property.Input.IsEffectivelyEnabled));
        var field = (TextBox)view.Inspector!.Properties.Values.Single(property => property.Property.Key == "x").Input;
        await ui.InvokeAsync(() =>
        {
            ui.Focus(field); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
            ui.HandleInput(new(InputKind.Text, Text: "1.65")); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Enter, Control: true));
        }).WaitAsync(token);
        await Until(() => document.Read().IsDirty && document.Read().Content!.UndoCount == 1);
        var content = document.Read().Content!; var selection = workspace.SelectedId;
        await ui.InvokeAsync(() => Program.Check(replacing ? playback.RunSimulation() : playback.Execute(EditorCommand.Build), "Initial scene command rejected.")).WaitAsync(token);
        await playback.Pending.WaitAsync(token);
        Program.Check(playback.Error == null && engine.Supervisor.Status.State == (replacing ? SessionState.Playing : SessionState.Stopped), "Initial command failed: " + playback.Error);
        if (replacing) await editor.BuildAsync(token); // Select a candidate while retaining the old playing child.
        var candidate = editor.Status.Candidate!.Build; var version = editor.Status.ContextVersion; var context = editor.Status.Context!;
        var origin = engine.Supervisor.Status; var old = replacing ? runtimes.Leases.Single() : null;
        var oldPayload = Hash(Path.Combine(candidate.DirectoryPath, "Game.Packaged.dll"));
        Program.Check(candidate.Inputs?.Preparation != null && candidate.Compilation!.Projects.Count == 1 &&
            oldPayload == ArchivePayloadHash(archive), "Candidate lacks the selected package closure.");
        Program.Check(!File.Exists(notice), "Checkpoint started before the test mutation.");
        if (change == "stale") SceneGameSource.WritePackage(root, Path.Combine(root, "Shared"), alternate: true);
        else await File.AppendAllTextAsync(archive, "corrupt", token);
        var changedArchive = Hash(archive); var changedLock = Hash(lockFile);
        Program.Check(initialArchive != changedArchive && (change == "stale" ? initialLock != changedLock : initialLock == changedLock) && Hash(source) == sourceHash,
            "The package-only mutation was not isolated.");
        Task pending = Task.CompletedTask;
        await ui.InvokeAsync(() => { pending = replacing ? editor.ReplaceBuildAsync(version, token) : editor.StartBuildAsync(960, 540, version, token); }).WaitAsync(token);
        while (!pending.IsCompleted) { await ui.InvokeAsync(Preserved).WaitAsync(token); await Task.Delay(10, token); }
        Exception? rejected = null;
        try { await pending.WaitAsync(token); } catch (Exception error) { rejected = error; }
        Program.Check(change == "stale" ? rejected is GameBuildInputsChangedException : rejected is InvalidDataException,
            "Preflight failed for an unexpected reason or admitted stale input: " + rejected);
        await ui.InvokeAsync(Preserved).WaitAsync(token);
        Program.Check(!File.Exists(notice) && engine.Supervisor.Status.Identity == origin.Identity &&
            engine.Supervisor.Status.State == origin.State && (!replacing || engine.Supervisor.Status.Tick >= origin.Tick) &&
            runtimes.Leases.Count == (replacing ? 1 : 0) && (old == null || !old.ResourcesReleased), "Preflight paused/replaced the old game or started a new child.");
        var rejectedJob = engine.Builds.DiagnosticsDirectory!;
        Program.Check(change != "corrupt" || Directory.GetFiles(rejectedJob, "*.stdout.log").Length == 0, "Corrupt archive reached SDK execution.");
        Drained();
        var rejection = rejected!.GetType().Name;
        if (change == "corrupt") SceneGameSource.WritePackage(root, Path.Combine(root, "Shared"), alternate: true);
        editor.SetContext(context with { Build = context.Build with { SourceRevision = context.Build.SourceRevision + 1, ReuseArtifact = true } });
        var fresh = await editor.BuildAsync(token); var freshVersion = editor.Status.ContextVersion;
        var freshPayload = Hash(Path.Combine(fresh.DirectoryPath, "Game.Packaged.dll"));
        using (var reuse = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(engine.Builds.DiagnosticsDirectory!, "reuse.json"), token)))
            Program.Check(!reuse.RootElement.GetProperty("Reused").GetBoolean(), "Changed package reused the stale payload.");
        Program.Check(fresh.BuildId != candidate.BuildId && fresh.Inputs!.Fingerprint != candidate.Inputs!.Fingerprint && freshPayload != oldPayload &&
            freshPayload == ArchivePayloadHash(archive) && Hash(source) == sourceHash &&
            fresh.Inputs.Preparation!.Files.Any(file => file.Path == archive && file.Sha256 == Hash(archive)), "Fresh build did not consume the changed package.");
        var frames = window.PresentedFrames;
        if (replacing) await editor.ReplaceBuildAsync(freshVersion, token); else await editor.StartBuildAsync(960, 540, freshVersion, token);
        await Until(() => window.PresentedFrames > frames && engine.Supervisor.Status.State == (replacing ? SessionState.Playing : SessionState.Paused));
        Program.Check(engine.Supervisor.Status.Identity!.GameBuildId == fresh.BuildId && runtimes.Leases.Count == (replacing ? 2 : 1) &&
            (!replacing || old is { Reaped: true, ResourcesReleased: true } && File.Exists(notice)), "Fresh native startup/replacement failed or checkpoint marker was inactive.");
        await ui.InvokeAsync(Preserved).WaitAsync(token);
        Drained();
        if (replacing)
        {
            await ui.InvokeAsync(() => Program.Check(playback.Execute(EditorCommand.Stop), "Final Stop rejected.")).WaitAsync(token);
            await playback.Pending.WaitAsync(token);
        }
        else await editor.StopAsync();
        Program.Check(documents.Execute(DocumentCommand.SaveAs, "PackageChecked.aetherscene"), "Save after drain rejected.");
        await documents.Pending.WaitAsync(token);
        Program.Check(documents.Read().Error == null && !document.Read().IsDirty && runtimes.Leases.All(lease => lease.Reaped && lease.ResourcesReleased), "Final save/child drain failed.");
        Program.Check(await window.RequestCloseAsync().WaitAsync(token), "Final close rejected."); await window.Completion.WaitAsync(token);
        Program.Check(maxLive == 1 && engine.Builds.ProcessId == null, "Native preflight retained concurrent/late work.");
        return new { Operation = operation, Change = change, Profile = "GameEditorPreflight", Rejected = true, Rejection = rejection,
            PackageOnlyMutation = true, NoChildOrPauseBeforeReject = true, NoCheckpointBeforeReject = true, ParentPreserved = true,
            CompilerDrained = true, CorruptBeforeSdk = change == "corrupt", FreshBuildRequired = true, FreshPackageLoaded = true,
            MaxLive = maxLive, Children = runtimes.Leases.Count, RejectedJob = rejectedJob, OldBuildId = candidate.BuildId, FreshBuildId = fresh.BuildId,
            InitialArchiveSha256 = initialArchive, ChangedArchiveSha256 = changedArchive, InitialLockSha256 = initialLock, ChangedLockSha256 = changedLock,
            OldPayloadSha256 = oldPayload, FreshPayloadSha256 = freshPayload };

        void Preserved()
        {
            maxLive = Math.Max(maxLive, runtimes.Leases.Count(lease => !lease.ResourcesReleased));
            Program.Check(ReferenceEquals(content, document.Read().Content) && document.Read().IsDirty && content.UndoCount == 1 && workspace.SelectedId == selection,
                "Package preflight changed document/history/selection.");
        }
        void Drained()
        {
            Program.Check(engine.Builds.ProcessId == null && engine.Builds.ReadDiagnostics(16, 0).All(job => job.Completed && job.ResourcesReleased), "Compiler not drained.");
            foreach (var staging in new[] { "preparation", "capture", "graph", "work", "output" })
                Program.Check(!Directory.Exists(Path.Combine(engine.Builds.DiagnosticsDirectory!, staging)), "Package staging retained: " + staging);
        }
        async Task Until(Func<bool> condition)
        {
            var watch = Stopwatch.StartNew();
            while (true)
            {
                var done = false; await ui.InvokeAsync(() => { maxLive = Math.Max(maxLive, runtimes.Leases.Count(lease => !lease.ResourcesReleased)); done = condition(); }).WaitAsync(token);
                if (done) return;
                if (watch.Elapsed > TimeSpan.FromSeconds(30)) throw new TimeoutException("Package preflight UI condition timed out.");
                await Task.Delay(10, token);
            }
        }
    }
    private static string Hash(string path) { using var input = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(input)); }
    private static string ArchivePayloadHash(string path)
    {
        using var file = File.OpenRead(path); using var archive = new ZipArchive(file, ZipArchiveMode.Read);
        var entry = archive.GetEntry("lib/net10.0/Game.Packaged.dll")!; Program.Check(entry.Length is > 0 and < 1024 * 1024, "Package fixture DLL exceeds its bound.");
        using var input = entry.Open(); return Convert.ToHexString(SHA256.HashData(input));
    }
}
