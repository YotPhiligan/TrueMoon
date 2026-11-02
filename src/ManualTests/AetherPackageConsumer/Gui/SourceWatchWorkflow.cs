using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using SkiaSharp;
using TrueMoon;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Editor;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Projects;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Sessions;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;

namespace AetherGuiConsumer;

internal static class SourceWatchWorkflow
{
    internal static async Task<object> Run(string directory, HostedUiWindow window, UiSession ui, EditorView view, IApp app, CancellationToken token)
    {
        var projects = app.Services.Resolve<EditorProjects>()!;
        var documents = app.Services.Resolve<EditorDocumentCommands>()!;
        var workspace = app.Services.Resolve<EditorWorkspace>()!;
        var engine = app.Services.Resolve<AetherHost>()!;
        var playback = app.Services.Resolve<EditorScenePlayback>()!;
        var runtimes = (NativeRuntimeFactory)app.Services.Resolve<IRuntimeFactory>()!;
        var model = (EditorViewModel)view.DataContext!;
        var status = (EditorStatusBarViewModel)view.StatusBar!.DataContext!;
        var project = projects.Active!; var watch = project.SourceWatch ?? throw new InvalidOperationException("No project observer.");
        var owner = 0; var threads = new ConcurrentDictionary<int, byte>(); var samples = new List<EditorSourceSnapshot>();
        var badge = view.StatusBar.SourceButton;
        await ui.InvokeAsync(() => { owner = Environment.CurrentManagedThreadId; model.PropertyChanged += Changed; status.PropertyChanged += Changed; }).WaitAsync(token);
        try
        {
            Progress("Open document");
            Program.Check(watch.Read() is { State: EditorSourceState.NoBuild, WatcherCount: 0 }, "Project open activated filesystem tracking.");
            await Doc(DocumentCommand.Open, "Original.aetherscene");
            await ui.InvokeAsync(() => workspace.Select(Program.CubeId.ToString("D"))).WaitAsync(token);
            await Until(() => view.Inspector!.Properties.Values.Any(property => property.Property.Key == "x"));
            var inspector = view.Inspector!; var transform = (EditorInspectorViewModel)inspector.DataContext!;
            var originalFile = Path.Combine(Path.GetDirectoryName(project.Project.GameProjectPath)!, "Game.cs");
            var original = await File.ReadAllTextAsync(originalFile, token);
            await File.WriteAllTextAsync(originalFile, original + "\n// SourceWatch.A\n", token);

            Progress("Initial Build");
            await Build(); await State(EditorSourceState.ObservedUnchanged); samples.Add(watch.Read());
            await Until(() => inspector.Properties.Values.Any(property => property.Property.Key == "x" &&
                property.Input is TextBox input && input.IsAttached && input.IsEffectivelyEnabled && input.Value == "-1.5"));
            var field = (TextBox)inspector.Properties.Values.Single(property => property.Property.Key == "x").Input;
            var form = inspector.Form; var document = project.Documents!.Read();
            Program.Check(model.Source?.OwnerKey == project.OwnerKey && model.Source?.BaselineVersion == watch.Read().BaselineVersion,
                "Owner-frame pull did not publish the current source baseline.");
            await Capture("source-same.png");
            Progress("Touch and preflight");
            var content = await File.ReadAllTextAsync(originalFile, token); var stamp = File.GetLastWriteTimeUtc(originalFile);
            var observed = watch.Read().ObservationVersion;
            File.SetLastWriteTimeUtc(originalFile, stamp.AddMinutes(-1));
            await Until(() => watch.Read().ObservationVersion > observed && watch.Read().State == EditorSourceState.ObservedUnchanged);
            Program.Check(watch.Read().SourceFingerprint == samples[0].SourceFingerprint, "Timestamp touch changed source identity.");

            // A previously observed Same never bypasses the existing authoritative compiler preflight.
            var candidate = engine.Editor.Status.Candidate!; var children = runtimes.Leases.Count;
            await File.WriteAllTextAsync(originalFile, content.Replace("SourceWatch.A", "SourceWatch.B", StringComparison.Ordinal), token);
            File.SetLastWriteTimeUtc(originalFile, stamp);
            Exception? rejection = null;
            try { await engine.Editor.StartBuildAsync(960, 540, candidate.ContextVersion, token); }
            catch (GameBuildInputsChangedException error) { rejection = error; }
            Program.Check(rejection != null && runtimes.Leases.Count == children && engine.Supervisor.Status.State == SessionState.Stopped,
                "Same observation replaced preflight or spawned a stale game.");
            await State(EditorSourceState.Changed); samples.Add(watch.Read());
            Progress("Changed and retained draft");
            await Until(() => field.IsAttached && field.IsEffectivelyEnabled);
            await File.WriteAllTextAsync(originalFile, content, token); File.SetLastWriteTimeUtc(originalFile, stamp);

            // Source publication and a theme update must preserve the existing focused inspector draft/tree/history.
            await ui.InvokeAsync(() =>
            {
                ui.Focus(field); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
                ui.HandleInput(new(InputKind.Text, Text: "NaN"));
                ui.SetTheme(Theme.Light with { FontFamily = "Noto Sans" });
            }).WaitAsync(token);
            await Until(() => field.Value == "NaN" && inspector.Theme.Surface == Theme.Light.Surface);
            await ui.InvokeAsync(() => Program.Check(field.IsFocused, "Source/theme update lost focused draft.")).WaitAsync(token);
            await Capture("source-changed.png");
            Program.Check(watch.Read().State == EditorSourceState.Changed && field.Value == "NaN" &&
                ReferenceEquals(field, inspector.Properties.Values.Single(property => property.Property.Key == "x").Input) &&
                ReferenceEquals(form, inspector.Form) && ReferenceEquals(badge, view.StatusBar.SourceButton) &&
                ReferenceEquals(document.Content, project.Documents.Read().Content) && project.Documents.Read().IsDirty == document.IsDirty,
                "Source/theme update lost retained draft, controls or document state, or reset Changed after revert.");
            await ui.InvokeAsync(transform.Cancel).WaitAsync(token);

            Progress("New baseline and game");
            await Build(); await State(EditorSourceState.ObservedUnchanged);
            var reset = watch.Read(); Program.Check(reset.BaselineVersion > samples[0].BaselineVersion, "Successful Build did not reset baseline.");
            await ui.InvokeAsync(() => { Program.Check(model.CanRun, "Run is unavailable."); model.RunSimulation(); }).WaitAsync(token);
            await playback.Pending.WaitAsync(token); Program.Check(playback.Error == null, "Run failed: " + playback.Error);
            await Until(() => engine.Supervisor.Status.State == SessionState.Playing && model.RuntimeState == SessionState.Playing);
            await State(EditorSourceState.ObservedUnchanged);
            var identity = engine.Supervisor.Status.Identity;
            Progress("Playing coverage error");
            // Coverage errors remain source state; they do not become GameEditor.Error or stop the game.
            using (var locked = new FileStream(originalFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                await State(EditorSourceState.Unavailable);
                Program.Check(engine.Supervisor.Status.Identity == identity && engine.Supervisor.Status.State == SessionState.Playing && engine.Editor.Status.Error == null,
                    "Source coverage error changed game identity/mode or command error.");
            }
            await State(EditorSourceState.ObservedUnchanged);
            Progress("Playing source change");
            await File.WriteAllTextAsync(originalFile, content.Replace("SourceWatch.A", "SourceWatch.C", StringComparison.Ordinal), token);
            File.SetLastWriteTimeUtc(originalFile, stamp);
            await State(EditorSourceState.Changed); samples.Add(watch.Read());
            Program.Check(engine.Supervisor.Status.Identity == identity && engine.Supervisor.Status.State == SessionState.Playing &&
                status.Kind == EditorStatusKind.Running && model.CanStop && engine.Builds.ProcessId == null,
                "Source Changed replaced runtime status/admission or triggered automatic Build/Stop.");
            await Capture("source-playing-changed.png");
            await ui.InvokeAsync(() => model.Execute(EditorCommand.Stop)).WaitAsync(token);
            await playback.Pending.WaitAsync(token);
            Program.Check(engine.Supervisor.Status.State == SessionState.Stopped && watch.Read().State == EditorSourceState.Changed,
                "Game Stop stopped/reset the project observer.");

            var manifest = project.Project.ManifestPath;
            Progress("Project close/reopen");
            await Doc(DocumentCommand.CloseProject);
            Program.Check(project.IsClosed && watch.Read() is { State: EditorSourceState.Stopped, WatcherCount: 0 }, "Project close did not drain tracking.");
            await Doc(DocumentCommand.OpenProject, manifest);
            var reopened = projects.Active!;
            Program.Check(reopened.OwnerKey != project.OwnerKey && reopened.SourceWatch!.Read() is { State: EditorSourceState.NoBuild, WatcherCount: 0 },
                "Reopen restored old source provenance or handles.");
            Program.Check(threads.Count == 1 && threads.ContainsKey(owner), "Source presentation notified outside the owner thread.");
            await ui.InvokeAsync(() => { model.PropertyChanged -= Changed; status.PropertyChanged -= Changed; }).WaitAsync(token);
            Program.Check(await window.RequestCloseAsync().WaitAsync(token), "Clean source-workflow close was vetoed.");
            return new { Profile = "SourceWatch", OwnerThread = owner, NotificationThreads = threads.Keys.ToArray(), Samples = samples,
                MetadataTouchSame = true, SameLengthSameTimestampChanged = true, StickyChanged = true, RetainedDraftAndControls = true,
                PreflightRequired = true, SourceErrorIndependent = true, RunningIdentityPreserved = true, CloseDrained = true,
                ReopenNoBuild = true, NativeNotifications = watch.Read().Mode == EditorSourceObservationMode.NotificationsAndPolling };
        }
        finally
        {
            if (!window.Completion.IsCompleted)
                await ui.InvokeAsync(() => { model.PropertyChanged -= Changed; status.PropertyChanged -= Changed; }).WaitAsync(CancellationToken.None);
        }

        void Changed(object? sender, PropertyChangedEventArgs args) => threads.TryAdd(Environment.CurrentManagedThreadId, 0);
        async Task Build()
        {
            await Until(() => model.CanBuild);
            await ui.InvokeAsync(() => model.Execute(EditorCommand.Build)).WaitAsync(token);
            await playback.Pending.WaitAsync(token); Program.Check(playback.Error == null, "Build failed: " + playback.Error);
        }
        async Task State(EditorSourceState state) => await Until(() => watch.Read().State == state && model.Source?.State == state &&
            view.StatusBar.SourceButton.Value == status.SourceSummary);
        async Task Doc(DocumentCommand command, string path = "")
        {
            await Until(() => documents.CanExecute(command));
            await ui.InvokeAsync(() => Program.Check(documents.Execute(command, path), "Document command was rejected.")).WaitAsync(token);
            await documents.Pending.WaitAsync(token); Program.Check(documents.Read().Error == null, "Document command failed: " + documents.Read().Error);
            await Until(() => !documents.Read().Busy && ReferenceEquals(workspace.Project, projects.Active) &&
                ReferenceEquals(((EditorDocumentViewModel)view.Documents!.DataContext!).Document?.Content?.Document,
                    projects.Active?.Documents?.Read().Content?.Document));
        }
        void Progress(string stage) => File.AppendAllText(Path.Combine(directory, "source-progress.txt"), DateTimeOffset.UtcNow.ToString("O") + " " + stage + Environment.NewLine);
        async Task Until(Func<bool> condition)
        {
            var clock = Stopwatch.StartNew();
            while (!condition())
            {
                token.ThrowIfCancellationRequested();
                if (clock.Elapsed > TimeSpan.FromSeconds(40)) throw new TimeoutException("Source workflow: " + watch.Read() + "; playback: " + playback.Error);
                await Task.Delay(20, token);
            }
        }
        async Task Capture(string name)
        {
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Frame(UiSession current)
            {
                if (current.NeedsUpdate) return; window.FramePresented -= Frame;
                try
                {
                    using var image = current.ReadbackOpenGLImage(); using var png = image.Encode(SKEncodedImageFormat.Png, 100);
                    using var output = File.Create(Path.Combine(directory, name)); png.SaveTo(output); ready.TrySetResult();
                }
                catch (Exception error) { ready.TrySetException(error); }
            }
            window.FramePresented += Frame; window.RequestFrame();
            try { await ready.Task.WaitAsync(token); } finally { window.FramePresented -= Frame; }
        }
    }
}
