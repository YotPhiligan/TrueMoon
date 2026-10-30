using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using SkiaSharp;
using TrueMoon;
using TrueMoon.Aether.Editor;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Diagnostics;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Projects;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;

namespace AetherGuiConsumer;

internal static class Workflow
{
    internal static async Task<object> Run(string directory, HostedUiWindow window, UiSession ui, EditorView view, IApp app, CancellationToken token)
    {
        var projects = app.Services.Resolve<EditorProjects>()!;
        var document = projects.Active!.Documents!;
        var documents = app.Services.Resolve<EditorDocumentCommands>()!;
        var workspace = app.Services.Resolve<EditorWorkspace>()!;
        var preview = app.Services.Resolve<EditorPreview>()!;
        var engine = app.Services.Resolve<AetherHost>()!;
        var writes = app.Services.Resolve<SceneWriteProbe>()!;
        var runtimes = (NativeRuntimeFactory)app.Services.Resolve<IRuntimeFactory>()!;
        var panel = view.Inspector!;
        var model = (EditorInspectorViewModel)panel.DataContext!;
        var captures = new List<object>(); var revisions = new List<long>(); var notificationThreads = new HashSet<int>();
        var owner = 0; var crashedPid = 0; var maxLive = 0;
        var source = Path.Combine(projects.Active.Project.ScenesDirectory, "Original.aetherscene");
        var sourceBytes = await File.ReadAllBytesAsync(source, token);
        await ui.InvokeAsync(() => { owner = Environment.CurrentManagedThreadId; model.PropertyChanged += Changed; }).WaitAsync(token);
        try
        {
            await Doc(DocumentCommand.Open, "Original.aetherscene");
            await ui.InvokeAsync(() => workspace.Select(Program.CubeId.ToString("D"))).WaitAsync(token);
            await Until(() => panel.Properties.Values.Any(p => p.Property.Key == "x" && p.Input.IsEffectivelyEnabled));
            var field = (TextBox)panel.Properties.Values.Single(p => p.Property.Key == "x").Input;
            var form = panel.Form;
            var original = document.Read().Content!;
            await Set(field, "NaN");
            await Until(() => !panel.Apply.IsEnabled && model.Fields[0].Error.Length != 0);
            Program.Check(ReferenceEquals(original, document.Read().Content) && !document.Read().IsDirty, "Invalid draft committed.");
            await ui.InvokeAsync(() => ui.SetTheme(Theme.Light with { FontFamily = "Noto Sans" })).WaitAsync(token);
            await Until(() => panel.Theme.Surface == Theme.Light.Surface);
            Program.Check(ReferenceEquals(field, panel.Properties.Values.Single(p => p.Property.Key == "x").Input) && field.Value == "NaN", "Theme discarded draft/rebuilt controls.");
            await Click(panel.Cancel); await Until(() => field.Value == "-1.5");
            await StartPreview(); await Preview(); await Capture("opened.png", -1.5f);
            await Edit(field, "1.5"); await Preview(); await Capture("edited.png", 1.5f);
            Program.Check(document.Read().Content!.UndoCount == 1, "Transform was not one history transaction.");
            await Click(panel.Undo); await Until(() => !document.Read().IsDirty); await Preview(); await Capture("undo.png", -1.5f);
            await Click(panel.Redo); await Until(() => document.Read().IsDirty); await Preview(); await Capture("redo.png", 1.5f);
            Program.Check(ReferenceEquals(field, panel.Properties.Values.Single(p => p.Property.Key == "x").Input) && ReferenceEquals(form, panel.Form),
                "Committed edits rebuilt the retained inspector.");

            var retained = document.Read().Content;
            var closing = window.RequestCloseAsync();
            await Until(() => documents.Read().AwaitingDecision);
            Program.Check(documents.Choose(DocumentCloseChoice.Cancel), "Cancel close choice was unavailable.");
            Program.Check(!await closing.WaitAsync(token) && !window.Completion.IsCompleted && ReferenceEquals(retained, document.Read().Content),
                "Cancelled close lost the window/document.");
            writes.FailNext();
            closing = window.RequestCloseAsync(); await Until(() => documents.Read().AwaitingDecision);
            Program.Check(documents.Choose(DocumentCloseChoice.Save), "Save close choice was unavailable.");
            Program.Check(!await closing.WaitAsync(token) && !window.Completion.IsCompleted && document.Read().IsDirty &&
                ReferenceEquals(retained, document.Read().Content) && documents.Read().Error?.ToString().Contains("Acceptance: failed save during close", StringComparison.Ordinal) == true,
                "Failed save/close did not preserve the document/history.");
            await Doc(DocumentCommand.SaveAs, "Edited.aetherscene");
            var currentSource = await File.ReadAllBytesAsync(source, token);
            Program.Check(!document.Read().IsDirty && sourceBytes.SequenceEqual(currentSource), "SaveAs mutated the source fixture.");
            await Doc(DocumentCommand.Close); await Doc(DocumentCommand.Open, "Edited.aetherscene");
            await ui.InvokeAsync(() => workspace.Select(Program.CubeId.ToString("D"))).WaitAsync(token);
            await Until(() => panel.Properties.Values.Any(p => p.Property.Key == "x" && ((TextBox)p.Input).Value == "1.5"));
            Program.Check(document.Read().Content!.UndoCount == 0 && !document.Read().IsDirty && document.Read().Content!.Document.Id == original.Document.Id,
                "Save/reopen lost identity/value or retained ephemeral history.");
            var hierarchy = view.Hierarchy ?? throw new InvalidOperationException("Missing hierarchy.");
            var hierarchyModel = (TrueMoon.Aether.Editor.Hierarchy.EditorHierarchyViewModel)hierarchy.DataContext!;
            await Until(() => hierarchyModel.CanRename);
            await ui.InvokeAsync(() =>
            {
                ui.Focus(hierarchy.Tree); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.F2));
                ui.HandleInput(new(InputKind.Text, Text: "Scene Guardian"));
                ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Enter));
            }).WaitAsync(token);
            await Until(() => document.Read().Content!.Resolved.Entities[new(Program.CubeId)].Name == "Scene Guardian");
            await ui.InvokeAsync(() => ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Down, Control: true))).WaitAsync(token);
            await Until(() => document.Read().Content!.Resolved.Roots[^1] == new EntityId(Program.CubeId));
            Program.Check(document.Read().Content!.UndoCount == 2 && workspace.SelectedId == Program.CubeId.ToString("D"), "Name/order did not share history and selection.");
            await Click(panel.Undo); await Until(() => document.Read().Content!.Resolved.Roots[0] == new EntityId(Program.CubeId));
            await Click(panel.Redo); await Until(() => document.Read().Content!.Resolved.Roots[^1] == new EntityId(Program.CubeId));
            await Doc(DocumentCommand.Save); await Doc(DocumentCommand.Close); await Doc(DocumentCommand.Open, "Edited.aetherscene");
            await ui.InvokeAsync(() => workspace.Select(Program.CubeId.ToString("D"))).WaitAsync(token);
            await Until(() => panel.Properties.Values.Any(p => p.Property.Key == "x" && ((TextBox)p.Input).Value == "1.5"));
            Program.Check(document.Read().Content!.Resolved.Entities[new(Program.CubeId)].Name == "Scene Guardian" &&
                document.Read().Content!.Resolved.Roots[^1] == new EntityId(Program.CubeId) && document.Read().Content!.UndoCount == 0 && !document.Read().IsDirty,
                "Name/order did not survive Save/reopen with a clean history.");
            var entityProof = await EntityOperations();
            field = (TextBox)panel.Properties.Values.Single(p => p.Property.Key == "x").Input;
            await StartPreview(); await Preview();
            await Capture("hierarchy-named.png", 1.5f);
            await Edit(field, "1.25"); await Preview();
            retained = document.Read().Content; var selected = workspace.SelectedId;
            var framesBeforeCrash = window.PresentedFrames;
            crashedPid = engine.Supervisor.Status.ProcessId!.Value;
            Program.Check(runtimes.Leases.Any(l => l.ProcessId == crashedPid && !l.ResourcesReleased), "Crash target is not this App's owned child.");
            await Until(() => runtimes.Leases.Single(l => l.ProcessId == crashedPid).ChildDiagnosticsStatus?.RuntimeObservation is
                { Collector: not null, GraphicsDevice: not null });
            using (var child = Process.GetProcessById(crashedPid)) { child.Kill(); await child.WaitForExitAsync(token); }
            await Until(() => engine.Supervisor.Status.State is SessionState.Faulted or SessionState.Stopped);
            await Until(() => window.PresentedFrames > framesBeforeCrash + 1);
            Program.Check(!window.Completion.IsCompleted && ReferenceEquals(retained, document.Read().Content) && document.Read().IsDirty &&
                document.Read().Content!.UndoCount == 1 && workspace.SelectedId == selected, "Runtime crash changed the document/history/selection or closed the UI.");
            await preview.StopAsync();
            Program.Check(preview.Show(document.Read().Content!.Resolved), "Preview recovery was not admitted.");
            await Preview(); await Capture("recovered.png", 1.25f);
            var consoleProof = await Diagnostics();
            await Doc(DocumentCommand.Save);
            Program.Check(!document.Read().IsDirty, "Save after recovery failed.");
            var savedPath = document.Read().Path!;
            Program.Check(await window.RequestCloseAsync().WaitAsync(token), "Clean close was vetoed.");
            await window.Completion.WaitAsync(token);
            Program.Check(notificationThreads.SetEquals([owner]) && runtimes.Leases.Count == 7 && maxLive == 1, "Owner-thread or child generation count changed.");
            return new { Captures = captures, PresentedRevisions = revisions, OwnerThread = owner, NotificationThreads = notificationThreads,
                MaxLive = maxLive, CrashedPid = crashedPid, CancelClosePreserved = true, FailedSavePreserved = true, CrashPreserved = true,
                Recovered = true, SourceUnchanged = true, StableInspector = true, HierarchyMetadataPersisted = true, EntityOperations = entityProof, SavedScene = savedPath, Diagnostics = consoleProof };
        }
        finally { model.PropertyChanged -= Changed; }

        void Changed(object? sender, System.ComponentModel.PropertyChangedEventArgs args) => notificationThreads.Add(Environment.CurrentManagedThreadId);
        void Progress(string text) { File.AppendAllText(Path.Combine(directory, "progress.log"), text + Environment.NewLine); Console.WriteLine(text); }
        async Task Until(Func<bool> condition, [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
        {
            var waiting = Stopwatch.StartNew();
            while (true)
            {
                var ready = false;
                await ui.InvokeAsync(() => { ready = condition(); maxLive = Math.Max(maxLive, runtimes.Leases.Count(l => !l.ResourcesReleased)); }).WaitAsync(token);
                if (ready) return;
                if (waiting.Elapsed > TimeSpan.FromSeconds(30))
                {
                    await ui.InvokeAsync(() =>
                    {
                        var console = view.Diagnostics; var data = console?.DataContext;
                        File.WriteAllText(Path.Combine(directory, "condition-timeout.json"), JsonSerializer.Serialize(new
                        {
                            Line = line, ui.NeedsUpdate, Open = data?.IsOpen, Visible = console?.IsVisible, data?.RetainedCount,
                            data?.Source, data?.Session, data?.Generation, data?.ExportStatus,
                            Rows = data?.Entries.Take(32).Select(entry => new { entry.Count, entry.Record.Sequence, entry.Record.EventName }),
                            SourceInput = console?.Source.Value, SessionInput = console?.Session.Value, GenerationInput = console?.Generation.Value
                        }));
                    }).WaitAsync(token);
                    throw new TimeoutException($"Workflow condition at line {line} did not complete in 30 seconds.");
                }
                await Task.Delay(10, token);
            }
        }
        async Task<object> EntityOperations()
        {
            Progress("Entity creation/deletion workflow");
            var hierarchy = view.Hierarchy!;
            await Doc(DocumentCommand.New); await Until(() => hierarchy.CreateButton.IsEffectivelyEnabled && document.Read().Content!.Document.Entities.Length == 0);
            await Click(hierarchy.CubeButton); await Until(() => document.Read().Content!.Document.Entities.Length == 1 && panel.Properties.Values.Any(property => property.Property.Key == "x"));
            var rootId = workspace.SelectedId!; var sceneId = document.Read().Content!.Document.Id;
            await ui.InvokeAsync(() => { ui.Focus(hierarchy.Tree); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.F10, Shift: true)); }).WaitAsync(token);
            await Until(() => view.Overlays.Popup is MenuPopup);
            await CapturePanel("entity-actions.png");
            var popup = (MenuPopup)view.Overlays.Popup!;
            await Click(popup.Children.OfType<MenuItem>().Single(item => item.ActionId == "create.child"));
            await Until(() => document.Read().Content!.Document.Entities.Length == 2 && workspace.SelectedId != rootId && panel.Properties.Values.Any(property => property.Property.Key == "x"));
            var childId = workspace.SelectedId!;
            var field = (TextBox)panel.Properties.Values.Single(property => property.Property.Key == "x").Input;
            await Edit(field, "2.25"); await Until(() => !model.HasDraft);
            await ui.InvokeAsync(() => { hierarchy.Tree.Select(rootId); ui.Focus(hierarchy.Tree); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.D, Control: true)); }).WaitAsync(token);
            await Until(() => document.Read().Content!.Document.Entities.Length == 4); var copied = workspace.SelectedId!;
            Program.Check(copied != rootId && document.Read().Content!.Resolved.Children[new(Guid.Parse(copied))].Length == 1, "Subtree duplication did not remap child identity.");
            await ui.InvokeAsync(() => ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Delete))).WaitAsync(token);
            await Until(() => document.Read().Content!.Document.Entities.Length == 2 && workspace.SelectedId == null);
            await ui.InvokeAsync(() => ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Z, Control: true))).WaitAsync(token);
            await Until(() => document.Read().Content!.Document.Entities.Length == 4);
            await ui.InvokeAsync(() => ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Y, Control: true))).WaitAsync(token);
            await Until(() => document.Read().Content!.Document.Entities.Length == 2);
            await Set(hierarchy.Search, childId); await ui.InvokeAsync(() => ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Enter))).WaitAsync(token);
            await Until(() => workspace.SelectedId == childId && hierarchy.Tree.IsExpanded(rootId));
            await Doc(DocumentCommand.SaveAs, "Created.aetherscene"); await Doc(DocumentCommand.Close); await Doc(DocumentCommand.Open, "Created.aetherscene");
            await ui.InvokeAsync(() => hierarchy.Tree.Select(childId)).WaitAsync(token);
            await Until(() => panel.Properties.Values.Any(property => property.Property.Key == "x" && ((TextBox)property.Input).Value == "2.25"));
            var restored = document.Read().Content!;
            Program.Check(restored.Document.Id == sceneId && restored.UndoCount == 0 && restored.Document.Entities.Length == 2 &&
                restored.Resolved.Entities[new(Guid.Parse(childId))].ParentId == new EntityId(Guid.Parse(rootId)) && !document.Read().IsDirty,
                "Created scene lost identities, parent, values or clean history after reopen.");
            await CapturePanel("created-entities.png"); var saved = document.Read().Path!;
            await Doc(DocumentCommand.Close); await Doc(DocumentCommand.Open, "Edited.aetherscene");
            await ui.InvokeAsync(() => workspace.Select(Program.CubeId.ToString("D"))).WaitAsync(token);
            await Until(() => panel.Properties.Values.Any(property => property.Property.Key == "x" && ((TextBox)property.Input).Value == "1.5"));
            await Set(hierarchy.Search, "");
            return new { CreatedScene = saved, Root = rootId, Child = childId, Duplicate = copied, RemappedSubtree = true, DeleteUndoRedo = true,
                SearchRevealed = true, ReopenedClean = true, Captures = new[] { "entity-actions.png", "created-entities.png" } };
        }
        async Task<object> Diagnostics()
        {
            Progress("Diagnostics console");
            var data = app.Services.Resolve<EditorDiagnosticsViewModel>()!;
            var console = view.Diagnostics ?? throw new InvalidOperationException("Missing console view.");
            var retained = document.Read().Content; var selected = workspace.SelectedId;
            var emitter = app.Services.Resolve<IEventsSourceFactory>()!.Create("TrueMoon.Acceptance.Console");
            var diagnosticThreads = new HashSet<int>();
            void Notification(object? sender, System.ComponentModel.PropertyChangedEventArgs args) => diagnosticThreads.Add(Environment.CurrentManagedThreadId);
            await ui.InvokeAsync(() =>
            {
                data.PropertyChanged += Notification;
                // Use the registered header command in the standard host; embedded consumers choose their own controls.
                if (view.Header is { } header) header.Menu.Groups.Single(group => group.Id == "view").Actions.Single(action => action.Id == "view.diagnostics").Command.Execute();
                else data.IsOpen = true;
                data.FollowTail = false;
            }).WaitAsync(token);
            try
            {
                var correlation = Guid.NewGuid().ToString("D");
                for (var i = 0; i < 21; i++)
                    emitter.Write(DiagnosticLevel.Warning, 8801, "Console.Native", "acceptance diagnostic PRIVATE_CONSOLE_TEXT", () =>
                        new Dictionary<string, string?> { ["SessionId"] = correlation, ["Generation"] = i == 20 ? "701" : "700", ["RuntimeRole"] = "Acceptance", ["Nonce"] = "PRIVATE_CONSOLE_NONCE" });
                await Until(() => data.Snapshot().Records.Count(record => record.EventName == "Console.Native") == 21 && console.IsVisible);
                Progress("Diagnostics live events visible");
                await Set(console.Source, "Acceptance.Console"); await Set(console.Session, correlation);
                await Until(() => data.Entries.Count == 2 && data.Entries[0].Count == 20);
                Progress("Diagnostics filters and groups");
                var oldEntry = data.Entries[0];
                await ui.InvokeAsync(() => { console.Events.Select(oldEntry.Id); console.Details.Select(0, 12); }).WaitAsync(token);
                await Set(console.Generation, "701"); await Until(() => data.Entries.Count == 1 && data.Entries[0].Record.Properties["Generation"] == "701");
                Program.Check(data.SelectedId == oldEntry.Id && console.Details.SelectionLength == 12, "Filtering discarded the inspected old generation.");
                await Set(console.Generation, "");
                Progress("Diagnostics inspected old generation");
                await Until(() => data.Processes?.Processes.Any(process => process.ProcessId == crashedPid &&
                    process.Delivery?.RuntimeObservation is { Collector: not null, GraphicsDevice: not null }) == true);
                var exportPath = Path.Combine(directory, "console-report.json"); await Set(console.ExportPath, exportPath); await Click(console.Export);
                await Until(() => !data.IsExporting && data.ExportStatus.Contains("сохранён", StringComparison.Ordinal));
                Progress("Diagnostics report exported");
                var bytes = await File.ReadAllBytesAsync(exportPath, token); using var report = JsonDocument.Parse(bytes);
                Program.Check(bytes.Length <= EditorDiagnosticReportWriter.DefaultMaximumBytes && report.RootElement.GetProperty("partial").GetBoolean() &&
                    !System.Text.Encoding.UTF8.GetString(bytes).Contains("PRIVATE_CONSOLE", StringComparison.Ordinal), "Console report leaked text/secrets or exceeded its bound.");
                var crashedObservation = report.RootElement.GetProperty("runtimeObservations").EnumerateArray().Single(row => row.GetProperty("ProcessId").GetInt32() == crashedPid);
                Program.Check(crashedObservation.GetProperty("CollectorAvailable").GetBoolean() && crashedObservation.GetProperty("DeviceAvailable").GetBoolean() &&
                    crashedObservation.GetProperty("GraphicsDevice").GetProperty("Backend").GetString() == "Vulkan" &&
                    crashedObservation.GetProperty("Collector").GetProperty("Sinks").GetArrayLength() > 0 &&
                    report.RootElement.GetProperty("presentation").GetProperty("Device").GetString()!.Length > 0,
                    "Report lost the last child sink/device observation or actual presentation device.");
                var imported = 0; var crashedHistory = false;
                await ui.InvokeAsync(() =>
                {
                    var history = data.Snapshot(); imported = history.Records.Count(record => record.Origin != null);
                    crashedHistory = history.Records.Any(record => record.ProcessId == crashedPid && record.Origin != null);
                    Program.Check(imported > 0 && crashedHistory, "Console lost retained child/crashed-generation diagnostics.");
                }).WaitAsync(token);
                await CapturePanel("diagnostics.png");
                await Set(console.Source, ""); await Set(console.Session, "");
                await Click(console.Mode);
                await Until(() => data.Mode == EditorDiagnosticMode.StandardOutput && data.Processes?.Processes.Any(process =>
                    process.ProcessId == crashedPid && process.StandardOutput?.Output.TotalCharacters > 0) == true);
                TrueMoon.Aether.Diagnostics.ProcessDiagnosticSnapshot raw = null!;
                await ui.InvokeAsync(() =>
                {
                    raw = data.Processes!.Processes.Single(process => process.ProcessId == crashedPid);
                    console.Events.Select(raw.Id);
                    Program.Check(data.Details.Contains("RAW STDOUT", StringComparison.Ordinal) && raw.Completed && raw.ResourcesReleased &&
                        raw.StandardOutput!.ReadFinished && raw.StandardOutput.FileFinished && !data.Rows.Any(row => row.Warning), "Raw stdout snapshot lost its identity, completion or stream classification.");
                }).WaitAsync(token);
                await CapturePanel("diagnostics-stdout.png");
                await Click(console.Mode); await Until(() => data.Mode == EditorDiagnosticMode.StandardError);
                await ui.InvokeAsync(() =>
                {
                    console.Events.Select(raw.Id);
                    Program.Check(data.Details.Contains("RAW STDERR", StringComparison.Ordinal) && raw.StandardError is { ReadFinished: true, FileFinished: true } &&
                        !data.Rows.Any(row => row.Warning), "Raw stderr availability/severity handling failed.");
                }).WaitAsync(token);
                await CapturePanel("diagnostics-stderr.png");
                await Click(console.Mode); await Until(() => data.Mode == EditorDiagnosticMode.Health && data.CollectorHealth?.Sinks.Any(sink => sink.Written > 0) == true);
                await ui.InvokeAsync(() =>
                {
                    console.Events.Select("process:" + raw.Id);
                    Program.Check(data.Details.Contains("Delivery:", StringComparison.Ordinal) && data.Details.Contains("file dropped", StringComparison.Ordinal) &&
                        data.CollectorHealth != null && data.Details.Contains("Child sink jsonl", StringComparison.Ordinal) &&
                        data.Details.Contains("Last runtime device: Vulkan", StringComparison.Ordinal), "Health view did not expose collector/delivery/capture independently.");
                }).WaitAsync(token);
                await CapturePanel("diagnostics-health.png");
                Progress("Diagnostics return to structured");
                for (var mode = 0; mode < 5 && data.Mode != EditorDiagnosticMode.Structured; mode++) await Click(console.Mode);
                await Until(() => data.Mode == EditorDiagnosticMode.Structured);
                Progress("Diagnostics structured selected");
                await Set(console.Source, "Acceptance.Console"); await Set(console.Session, correlation);
                Progress("Diagnostics structured filters restored");
                await Until(() => data.Entries.Count == 2 && data.SelectedId == oldEntry.Id);
                Progress("Diagnostics raw stdout/stderr and health");
                await Click(console.Close); await Until(() => !console.IsVisible);
                await ui.InvokeAsync(() => data.IsOpen = true).WaitAsync(token); await Until(() => console.IsVisible);
                Program.Check(ReferenceEquals(retained, document.Read().Content) && workspace.SelectedId == selected && data.SelectedId == oldEntry.Id &&
                    diagnosticThreads.SetEquals([owner]) && data.RetainedCount <= 512 && data.RetainedBytes <= 1024 * 1024, "Console changed domain state, owner thread or budgets.");
                await Click(console.Close);
                return new { PartialExport = exportPath, Bytes = bytes.Length, Imported = imported, CrashedHistory = crashedHistory,
                    Groups = 2, RetainedDetails = true, Reopened = true, OwnerThreads = diagnosticThreads, Capture = "diagnostics.png",
                    RawStdoutCharacters = raw.StandardOutput!.Output.TotalCharacters, RawStderrAvailable = raw.StandardError != null,
                    RawOldProcessReleased = raw.ResourcesReleased, CollectorHealthAvailable = data.CollectorHealth != null,
                    CrashedChildObservation = crashedObservation.Clone(), PresentationDevice = report.RootElement.GetProperty("presentation").GetProperty("Device").GetString(),
                    RawCaptures = new[] { "diagnostics-stdout.png", "diagnostics-stderr.png", "diagnostics-health.png" } };
            }
            finally { data.PropertyChanged -= Notification; }
        }
        async Task Doc(DocumentCommand command, string path = "")
        {
            Progress("Document " + command + " " + path);
            await Until(() => documents.CanExecute(command));
            await ui.InvokeAsync(() => Program.Check(documents.Execute(command, path), "Document action was rejected.")).WaitAsync(token);
            await documents.Pending.WaitAsync(token);
            Program.Check(documents.Read().Error == null, "Document action failed: " + documents.Read().Error);
            await Until(() => !documents.Read().Busy && ReferenceEquals(((EditorDocumentViewModel)view.Documents!.DataContext!).Document?.Content?.Document, document.Read().Content?.Document));
        }
        async Task StartPreview()
        {
            Progress("Start preview");
            await Click(view.StatusBar!.PreviewButton);
            await Until(() => view.Overlays.Popup is EditorStatusPopupView && !ui.NeedsUpdate);
            await Click(((EditorStatusPopupView)view.Overlays.Popup!).StartPreview);
            await ui.InvokeAsync(() => ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape))).WaitAsync(token);
        }
        async Task Preview()
        {
            await Until(() => preview.Read() is { Busy: false, Presented: { } stamp } && stamp.Revision == document.Read().Content!.Document.Revision || preview.Read().Error != null);
            Program.Check(preview.Read().Error == null && engine.Preview.LastPresented?.Document == preview.Read().Presented, "Preview failed: " + preview.Read().Error);
            revisions.Add(preview.Read().Presented!.Revision); Progress("Preview " + revisions[^1]);
        }
        async Task Edit(TextBox field, string value)
        {
            await Set(field, value);
            await ui.InvokeAsync(() => ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Enter, Control: true))).WaitAsync(token);
            await Until(() => document.Read().IsDirty && !model.HasDraft);
        }
        async Task Set(TextBox field, string value)
        {
            var invoke = ui.InvokeAsync(() =>
            {
                ClickInput(field); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
                ui.HandleInput(value.Length == 0 ? new(InputKind.KeyDown, Key: UiKey.Delete) : new(InputKind.Text, Text: value));
            }, token);
            try { await invoke.WaitAsync(TimeSpan.FromSeconds(10), token); }
            catch (Exception error) when (error is TimeoutException or OperationCanceledException)
            {
                // Directed diagnostics of this owned acceptance window; no mutation or reliance on private state for behavior.
                var failure = typeof(HostedUiWindow).GetField("_failure", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(window) as Exception;
                await File.WriteAllTextAsync(Path.Combine(directory, "ui-owner-failure.txt"), failure?.ToString() ?? "No HostedUiWindow failure captured.", CancellationToken.None);
                throw;
            }
            await Until(() => field.Value == value);
        }
        async Task Click(Button button) { await Until(() => button.IsEffectivelyEnabled); await ui.InvokeAsync(() => ClickInput(button)).WaitAsync(token); }
        void ClickInput(Element element)
        {
            var bounds = element.Bounds; Program.Check(bounds.Width > 0 && bounds.Height > 0, "Control has no actual layout.");
            ui.HandleInput(new(InputKind.PointerDown, bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2));
            ui.HandleInput(new(InputKind.PointerUp, bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2));
        }
        async Task CapturePanel(string name)
        {
            Progress("Capture " + name);
            var captured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void CaptureConsole(UiSession current)
            {
                if (current.NeedsUpdate) return;
                window.FramePresented -= CaptureConsole;
                try
                {
                    using var image = current.ReadbackOpenGLImage(); using var png = image.Encode(SKEncodedImageFormat.Png, 100);
                    using var output = File.Create(Path.Combine(directory, name)); png.SaveTo(output); captured.TrySetResult();
                }
                catch (Exception failure) { captured.TrySetException(failure); }
            }
            window.FramePresented += CaptureConsole; window.RequestFrame();
            try { await captured.Task.WaitAsync(token); } finally { window.FramePresented -= CaptureConsole; }
            Progress("Captured " + name);
        }
        async Task Capture(string name, float x)
        {
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void CaptureFrame(UiSession current)
            {
                if (current.NeedsUpdate || view.Canvas.DrawnFrame is not { } frame || engine.Preview.Describe(frame)?.Document != preview.Read().Presented) return;
                window.FramePresented -= CaptureFrame;
                try
                {
                    using var image = current.ReadbackOpenGLImage(); using var bitmap = SKBitmap.FromImage(image);
                    var bounds = view.Canvas.ImageBounds;
                    var projection = new PrimitiveScene(PreviewGameSession.Camera, 0, Vector3.One).CreateViewProjection(640, 360);
                    var clip = Vector4.Transform(new Vector4(x, 1, -1, 1), projection);
                    var pixel = bitmap.GetPixel((int)((bounds.X + bounds.Width * (clip.X / clip.W + 1) / 2) * current.Viewport.Scale),
                        (int)((bounds.Y + bounds.Height * (clip.Y / clip.W + 1) / 2) * current.Viewport.Scale));
                    Program.Check(Math.Abs(pixel.Red - 204) <= 2 && Math.Abs(pixel.Green - 51) <= 2 && Math.Abs(pixel.Blue - 38) <= 2,
                        "Actual preview pixel differs: " + pixel);
                    using var data = image.Encode(SKEncodedImageFormat.Png, 100); using var file = File.Create(Path.Combine(directory, name)); data.SaveTo(file);
                    captures.Add(new { Name = name, Pixel = pixel.ToString(), X = x, Revision = preview.Read().Presented!.Revision });
                    ready.TrySetResult();
                }
                catch (Exception error) { ready.TrySetException(error); }
            }
            window.FramePresented += CaptureFrame; window.RequestFrame();
            try { await ready.Task.WaitAsync(token); } finally { window.FramePresented -= CaptureFrame; }
        }
    }
}
