using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Windows;
using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;
using TrueMoon.Aether.Editor.Tooling;
using TrueMoon.Aether.Editor.Tooling.Documents;
using TrueMoon.Aether.Editor.Tooling.Editing;
using TrueMoon.Aether.Editor.UI;
using TrueMoon.Aether.Editor.UI.Documents;
using TrueMoon.Aether.Editor.UI.Editing;
using TrueMoon.Aether.Editor.UI.Hierarchy;
using TrueMoon.Aether.Nom;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Runtime.Ecs.Rendering;
using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Tooling;
using TrueMoon.Aether.Tooling.Documents;
using TrueMoon.Aether.Tooling.Hosting;
using TrueMoon.Aether.Tooling.Importing;
using TrueMoon.Aether.Tooling.Projects;
using TrueMoon.Aether.Tooling.Scenes;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Nom;

namespace TrueMoon.ManualTests.AetherEditorHost;

internal static class EditorModelBuildSmoke
{
    private static async Task<(ModelImportProof Proof, GameBuildArtifact OldBuild, GameBuildArtifact NewBuild)> VerifyModelImportsAsync(
        string report, GameBuildArtifact oldBuild, GameBuildArtifact newBuild,
        SceneDocumentSession document, Guid openingId, IFactory<ModelImportLedgerSession, ModelImportLedgerArguments> ledgers,
        UiSession ui, EditorView view, AetherHost engine, EditorScenePlayback playback, EditorDocumentCommands commands,
        PausedPublication? publication, ModelImportWorkerDeployment? worker, string scenePath, bool persist, bool catalog,
        bool placeModel, CancellationToken token)
    {
        var redId = Guid.Parse("ce662048-9dcb-4c60-aaf0-213342054fa6");
        var greenId = Guid.Parse("65710c6d-6781-4476-9bb8-735fc518f008");
        var redBefore = await ReadImport(redId, "red-baseline.tmledger");
        var redLedger = new ModelImportLedgerArguments(redId, Path.Combine(report, "ledger", "red-baseline.tmledger"));
        var redAfter = await ReadImport(redId, "scene.tmledger");
        var green = await ReadImport(greenId, "green.tmledger");
        var buffer = Path.Combine(report, "source", "scene.bin");
        var edited = await File.ReadAllBytesAsync(buffer, token);
        var original = edited.ToArray();
        BinaryPrimitives.WriteSingleLittleEndian(original.AsSpan(16), 0f);
        await File.WriteAllBytesAsync(buffer, original, token);

        var oldContent = Freeze(oldBuild);
        var newContent = Freeze(newBuild);
        const string current = "Red · Актуальна\nGreen · Актуальна";
        const string stale = "Red · Импорт устарел\nGreen · Актуальна";
        const string unavailable = "Red · Исходники недоступны\nGreen · Актуальна";
        var catalogVerified = false;
        var missingModelRejected = false;
        var emptyRestored = false;
        if (catalog)
        {
            await OpenStatus(null);
            await ClickCatalog(popup => popup.ReadModelCatalog);
            await commands.Pending.WaitAsync(token);
            await EditorHeaderSmoke.UntilAsync(ui, () => view.Overlays.Popup is EditorStatusPopupView popup &&
                CatalogChecks(popup).Select(check => (check.Value, check.IsChecked))
                    .SequenceEqual(new[] { ("Green · source-green/scene.gltf", false), ("Red · source/scene.gltf", false) }), token, HoldStatus);
            await ToggleCatalogModel("Red", redId, selected: true);
            await Build();
            missingModelRejected = playback.Error?.Message.Contains($"Меш {green.Meshes.Single().AssetId:D} отсутствует", StringComparison.Ordinal) == true;
            Check(missingModelRejected, "Build admitted a placed model missing from the resource selection.");
            await ToggleCatalogModel("Green", greenId, selected: true);
            catalogVerified = document.ReadModelImports(openingId).Select(item => item.SourceId).ToHashSet().SetEquals([redId, greenId]);
            Check(catalogVerified, "Editor catalog did not apply both choices to the scene.");
        }
        else await Select(redBefore, oldContent);
        var checkpoint = Path.Combine(report, "Content", ".aether-model-imports", document.Read().Content!.Document.Id.Value.ToString("N") + ".tmimports");
        var initialSelection = persist ? await File.ReadAllBytesAsync(checkpoint, token) : [];
        var placement = placeModel ? await PlaceCatalogModel() : null;
        if (persist)
        {
            using var saved = JsonDocument.Parse(initialSelection);
            Check(saved.RootElement.GetProperty("models")[0].GetProperty("sourceDirectory").GetString() == "source" &&
                saved.RootElement.GetProperty("models")[0].GetProperty("ledger").GetString() == "ledger/red-baseline.tmledger",
                "Saved model selection contains nonportable paths.");
        }
        await Status(current);
        await Build();
        var previous = engine.Editor.Status;
        Check(playback.Error == null && previous.Context?.World != null && previous.Candidate != null &&
            Matches(previous.Candidate.Build.Artifact, oldContent), $"Initial Editor Build failed: {playback.Error}");

        await File.WriteAllBytesAsync(buffer, edited, token);
        await Build();
        var blocked = playback.Error?.Message;
        var afterBlock = engine.Editor.Status;
        Check(blocked?.Contains("«Red» устарела", StringComparison.Ordinal) == true &&
            previous.ContextVersion == afterBlock.ContextVersion && ReferenceEquals(previous.Candidate, afterBlock.Candidate),
            $"Editor Build did not reject changed sources before creating a new context: {blocked}");
        await Status(stale);
        await EditorHeaderSmoke.UntilAsync(ui, () => view.StatusBar!.DataContext!.ErrorDetails.Contains(blocked!, StringComparison.Ordinal), token);

        var missingRejected = false;
        var cancelledAfterLedger = false;
        var restoredPrior = false;
        var restoredNew = false;
        var conflictRejected = false;
        var corruptRejected = false;
        File.Move(buffer, buffer + ".missing");
        try
        {
            await Build();
            Check(playback.Error?.Message.Contains("«Red» недоступны", StringComparison.Ordinal) == true &&
                ReferenceEquals(previous.Candidate, engine.Editor.Status.Candidate), "Missing BIN did not block Build.");
            await Status(unavailable);
            if (publication != null)
            {
                await ClickReimport(unavailable);
                await commands.Pending.WaitAsync(token);
                missingRejected = commands.Read().Error != null && publication.Calls == 0 &&
                    (await ReadImport(redId, "red-baseline.tmledger")).Generation == redBefore.Generation;
                Check(missingRejected && ReferenceEquals(previous.Candidate, engine.Editor.Status.Candidate),
                    "Unavailable source changed the reimport baseline or build candidate.");
            }
        }
        finally { File.Move(buffer + ".missing", buffer); }
        await Status(stale);

        if (publication != null)
        {
            await ClickReimport(stale);
            var staged = await publication.Entered.Task.WaitAsync(token);
            Check(staged.Generation == redBefore.Generation + 1 && staged.Candidate.Revision == redAfter.Candidate.Revision,
                "The worker did not publish the expected ledger generation.");
            await EditorHeaderSmoke.UntilAsync(ui, () => view.StatusBar!.CancelButton is { IsVisible: true, IsEnabled: true }
                && !ui.NeedsUpdate, token);
            await ui.InvokeAsync(() => EditorHeaderSmoke.Click(ui, view.StatusBar!.CancelButton)).WaitAsync(token);
            await commands.Pending.WaitAsync(token);
            cancelledAfterLedger = document.ReadModelImports(openingId).Single(item => item.SourceId == redId).Generation == redBefore.Generation &&
                (await ReadImport(redId, "red-baseline.tmledger")).Generation == staged.Generation &&
                ReferenceEquals(previous.Candidate, engine.Editor.Status.Candidate);
            Check(cancelledAfterLedger, "Cancellation applied an unpublished selection or changed the previous build.");
            if (persist)
            {
                await Reopen(initialSelection);
                restoredPrior = document.ReadModelImports(openingId).Single(item => item.SourceId == redId).Generation == redBefore.Generation &&
                    (await ReadImport(redId, "red-baseline.tmledger")).Generation == staged.Generation;
                Check(restoredPrior, "Opening followed the newer ledger head instead of the retained selection.");
            }
            await ClickReimport(stale);
            await commands.Pending.WaitAsync(token);
            Check(commands.Read().Error == null && publication.Calls == 2, $"Reimport retry did not complete: {commands.Read().Error}");
            Check((await ReadImport(redId, "red-baseline.tmledger")).Generation == staged.Generation &&
                (await ReadImport(greenId, "green.tmledger")).Generation == green.Generation,
                "Retry published another ledger generation or changed a different model.");
        }
        else
        {
            try
            {
                await Select(redAfter, oldContent);
                throw new InvalidOperationException("Editor accepted old render content for the new import.");
            }
            catch (InvalidDataException) { }
            await Status(stale);
            await Select(redAfter, newContent);
        }
        await Status(current);
        if (persist)
        {
            var latestSelection = await File.ReadAllBytesAsync(checkpoint, token);
            Check(!latestSelection.SequenceEqual(initialSelection), "Successful reimport kept the old saved selection.");
            await Reopen(latestSelection);
            await Status(current);
            var restored = await document.CheckModelImportsAsync(openingId, token);
            restoredNew = restored != null && restored.AssetKeys.SequenceEqual(newContent.AssetKeys) &&
                document.ReadModelImports(openingId).Single(item => item.SourceId == greenId).Generation == green.Generation;
            Check(restoredNew, "Opening the new checkpoint changed the selected resources.");
            if (catalog)
            {
                await ToggleCatalogModel("Red", redId, selected: false);
                await ToggleCatalogModel("Green", greenId, selected: false);
                await Reopen(await File.ReadAllBytesAsync(checkpoint, token));
                emptyRestored = document.ReadModelImports(openingId).Count == 0;
                Check(emptyRestored, "Removing the last catalog model did not persist an empty scene selection.");
                await ToggleCatalogModel("Red", redId, selected: true);
                await ToggleCatalogModel("Green", greenId, selected: true);
                latestSelection = await File.ReadAllBytesAsync(checkpoint, token);
            }

            var competing = latestSelection.Append((byte)'\n').ToArray();
            await File.WriteAllBytesAsync(checkpoint, competing, token);
            try { await Select(redAfter, newContent); }
            catch (SceneFileConflictException) { conflictRejected = true; }
            Check(conflictRejected && (await File.ReadAllBytesAsync(checkpoint, token)).SequenceEqual(competing),
                "A stale opening overwrote another saved selection.");

            await File.WriteAllTextAsync(checkpoint, "{", token);
            try
            {
                Check(commands.Execute(DocumentCommand.Open, scenePath), "Editor declined testing an invalid checkpoint.");
                await commands.Pending.WaitAsync(token);
                corruptRejected = commands.Read().Error is InvalidDataException && document.Read().OpeningId == openingId;
                Check(corruptRejected, "A corrupt checkpoint replaced the current document.");
            }
            finally { await File.WriteAllBytesAsync(checkpoint, latestSelection, token); }
        }
        await Build();
        var updated = engine.Editor.Status.Candidate?.Build.Artifact;
        Check(playback.Error == null && updated != null && updated.BuildId != previous.Candidate!.Build.BuildId &&
            Matches(updated, newContent), $"Editor Build with the reimported model failed: {playback.Error}");

        var baseline = previous.Candidate!.Build.Artifact;
        return (new(true, current, stale, unavailable, current, blocked!, true, baseline.BuildId, updated!.BuildId,
            cancelledAfterLedger, missingRejected, [], restoredPrior, restoredNew, conflictRejected, corruptRejected,
            catalogVerified, missingModelRejected, emptyRestored) { Placement = placement }, baseline, updated);

        async Task<ModelPlacementProof> PlaceCatalogModel()
        {
            var baseline = document.Read().Content!;
            var originals = baseline.Document.Entities.Select(entity => entity.Id).ToHashSet();
            var expectedIds = redBefore.Instances.Select(instance => instance.Id).ToArray();
            const string action = "Добавить «Red» в сцену";
            await OpenStatus(null);
            var pending = commands.Pending;
            await ClickCatalog(popup => CatalogButtons(popup).SingleOrDefault(button => button.AccessibleName == action));
            await ui.InvokeAsync(() => view.Overlays.Close(false)).WaitAsync(token);
            Check(!ReferenceEquals(pending, commands.Pending), "Catalog did not admit placing Red.");
            await commands.Pending.WaitAsync(token);
            var state = document.Read();
            Check(commands.Read().Error == null && state.Content is { } placed && state.IsDirty &&
                placed.UndoCount == baseline.UndoCount + 1 && HasPlacedModel(placed.Document),
                $"Placement did not commit the expected instances in one history entry: {commands.Read().Error}");

            await EditorHeaderSmoke.UntilAsync(ui, () => view.Inspector!.DataContext!.CanUndo, token);
            await EditorHeaderSmoke.ClickActionAsync(ui, view, "edit", "edit.undo", token);
            state = document.Read();
            Check(state.Content is { } undone && !state.IsDirty && undone.ContentRevision == baseline.ContentRevision &&
                undone.Document.Entities.Select(entity => entity.Id).ToHashSet().SetEquals(originals) &&
                undone.Document.Definitions.Length == baseline.Document.Definitions.Length &&
                undone.Document.Schemas.Length == baseline.Document.Schemas.Length,
                "A single Undo did not restore the saved scene and its catalogs.");
            Check(document.ReadModelImports(openingId).Any(item => item.SourceId == redId && item.Generation == redBefore.Generation),
                "Undo changed the selected import.");

            await EditorHeaderSmoke.UntilAsync(ui, () => view.Inspector!.DataContext!.CanRedo, token);
            await EditorHeaderSmoke.ClickActionAsync(ui, view, "edit", "edit.redo", token);
            state = document.Read();
            var redone = state.Content ?? throw new InvalidOperationException("Redo lost the active scene.");
            Check(state.IsDirty && HasPlacedModel(redone.Document), "Redo did not restore the same model instances.");
            var revision = redone.Document.Revision;

            await EditorHeaderSmoke.ClickActionAsync(ui, view, "file", "document.Save", token);
            await commands.Pending.WaitAsync(token);
            Check(commands.Read().Error == null && !document.Read().IsDirty, $"Editor could not save the placed model: {commands.Read().Error}");
            var savedHash = Hash(await File.ReadAllBytesAsync(scenePath, token));
            await Reopen(initialSelection);
            var reopened = document.Read().Content!.Document;
            Check(reopened.Revision == revision && HasPlacedModel(reopened) &&
                document.ReadModelImports(openingId).Any(item => item.SourceId == redId && item.Generation == redBefore.Generation),
                "Reopening changed the placed entities or their selected import.");
            return new(reopened.Id.Value, revision, savedHash, expectedIds, redBefore.Generation);

            bool HasPlacedModel(SceneDocument scene) =>
                scene.Entities.Length == originals.Count + expectedIds.Length && originals.IsSubsetOf(scene.Entities.Select(entity => entity.Id)) &&
                scene.Entities.Where(entity => !originals.Contains(entity.Id)).Select(entity => entity.Id.Value).ToHashSet().SetEquals(expectedIds);
        }

        static IEnumerable<CheckBox> CatalogChecks(EditorStatusPopupView popup) =>
            popup.ModelCatalogRows.Children.OfType<HStack>().SelectMany(row => row.Children.OfType<CheckBox>());

        static IEnumerable<Button> CatalogButtons(EditorStatusPopupView popup) =>
            popup.ModelCatalogRows.Children.OfType<HStack>().SelectMany(row => row.Children.OfType<Button>());

        Task ClickCatalog(Func<EditorStatusPopupView, Button?> find) => EditorHeaderSmoke.UntilAsync(ui, () =>
        {
            // The window can lose focus while the test is awaiting the next UI dispatch.
            // Resolve the live target and send input in the same owner-thread callback.
            if (ui.NeedsUpdate || view.Overlays.Popup is not EditorStatusPopupView popup ||
                find(popup) is not { IsEnabled: true, Bounds.Width: > 0 } button) return false;
            EditorHeaderSmoke.Move(ui, button);
            EditorHeaderSmoke.Click(ui, button);
            return true;
        }, token, HoldStatus);

        async Task ToggleCatalogModel(string name, Guid id, bool selected)
        {
            await OpenStatus(null);
            var pending = commands.Pending;
            await ClickCatalog(popup => CatalogChecks(popup).SingleOrDefault(check =>
                check.Value.StartsWith(name + " · ", StringComparison.Ordinal) && check.IsChecked != selected));
            Check(!ReferenceEquals(pending, commands.Pending), $"Catalog did not admit choosing {name}.");
            await commands.Pending.WaitAsync(token);
            Check(commands.Read().Error == null && document.ReadModelImports(openingId).Any(item => item.SourceId == id) == selected,
                $"Catalog did not change selection for {name}: {commands.Read().Error}");
            await EditorHeaderSmoke.UntilAsync(ui, () => view.Overlays.Popup is EditorStatusPopupView popup &&
                CatalogChecks(popup).Any(check => check.Value.StartsWith(name + " · ", StringComparison.Ordinal) && check.IsChecked == selected), token, HoldStatus);
            await ui.InvokeAsync(() => view.Overlays.Close(false)).WaitAsync(token);
        }

        async Task Reopen(byte[] expected)
        {
            var written = File.GetLastWriteTimeUtc(checkpoint);
            Check(commands.Execute(DocumentCommand.Open, scenePath), "Editor declined reopening the scene.");
            await commands.Pending.WaitAsync(token);
            var now = document.Read().OpeningId;
            Check(commands.Read().Error == null && now != Guid.Empty && now != openingId && !document.Read().IsDirty,
                $"Scene did not reopen cleanly: {commands.Read().Error}");
            openingId = now;
            Check(File.GetLastWriteTimeUtc(checkpoint) == written && (await File.ReadAllBytesAsync(checkpoint, token)).SequenceEqual(expected),
                "Opening mutated the saved import checkpoint.");
        }

        async Task<PublishedModelImport> ReadImport(Guid id, string path)
        {
            await using var ledger = ledgers.Create(new(id, Path.Combine(report, "ledger", path)))!;
            return await ledger.OpenAsync(token) ?? throw new InvalidOperationException($"Missing ledger {path}.");
        }

        Task Select(PublishedModelImport red, GameBuildRenderContent frozen) => document.SelectModelImportsAsync(openingId,
            [new("Red", Watch(red, "source")) { Reimport = publication == null ? null :
                new(redLedger, worker!, Path.Combine(report, "scratch"), publication) },
             new("Green", Watch(green, "source-green"))], frozen, token);

        ModelSourceWatchArguments Watch(PublishedModelImport import, string path) =>
            new(new(import.Candidate.SourceId, Path.Combine(report, path), "scene.gltf"), import)
            { UseFileSystemNotifications = false, PollInterval = TimeSpan.FromSeconds(5) };

        async Task Status(string expected)
        {
            await OpenStatus(expected);
            await ui.InvokeAsync(() => ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape))).WaitAsync(token);
            await EditorHeaderSmoke.UntilAsync(ui, () => view.Overlays.Popup == null, token);
        }

        async Task OpenStatus(string? expected)
        {
            var footer = view.StatusBar!;
            await ui.InvokeAsync(() => view.Header!.Menu.CloseMenu()).WaitAsync(token);
            await EditorHeaderSmoke.UntilAsync(ui, () => (expected == null || footer.DataContext!.ModelImportDetails == expected) &&
                footer.SummaryButton.Bounds.Width > 0 && !ui.NeedsUpdate, token);
            await EditorHeaderSmoke.UntilAsync(ui, () => view.Overlays.Popup is EditorStatusPopupView popup &&
                (expected == null || popup.ModelImportDetails.IsVisible && popup.ModelImportDetails.Value == expected), token, HoldStatus);
        }

        // A native focus change can dismiss the transient popup. Reopen only the presentation.
        void HoldStatus()
        {
            if (view.Overlays.Popup is EditorStatusPopupView || view.StatusBar?.SummaryButton is not { IsVisible: true } button ||
                button.Bounds.Width <= 0) return;
            EditorHeaderSmoke.Move(ui, button);
            EditorHeaderSmoke.Click(ui, button);
        }

        async Task ClickReimport(string expected)
        {
            await OpenStatus(expected);
            const string label = "Переимпортировать «Red»";
            var before = commands.Pending;
            await EditorHeaderSmoke.UntilAsync(ui, () =>
            {
                if (ui.NeedsUpdate || view.Overlays.Popup is not EditorStatusPopupView popup ||
                    popup.ReimportActions.Children.OfType<Button>().SingleOrDefault(item => item.Value == label) is not
                        { IsEnabled: true, Bounds.Width: > 0 } button) return false;
                EditorHeaderSmoke.Move(ui, button);
                EditorHeaderSmoke.Click(ui, button);
                view.Overlays.Close(false);
                return true;
            }, token, HoldStatus);
            Check(!ReferenceEquals(commands.Pending, before), "Editor declined the reimport action.");
        }

        async Task Build()
        {
            await EditorHeaderSmoke.UntilAsync(ui, () => view.DataContext!.CanBuild, token);
            await EditorHeaderSmoke.ClickActionAsync(ui, view, "project", "runtime.Build", token);
            await playback.Pending.WaitAsync(token);
        }

        static GameBuildRenderContent Freeze(GameBuildArtifact artifact) =>
            new(artifact.RenderContent!.ProjectId, artifact.RenderContent.Entries);

        static bool Matches(GameBuildArtifact artifact, GameBuildRenderContent content) =>
            artifact.RenderContent?.Entries.Select(entry => entry.Key).SequenceEqual(content.AssetKeys) == true;
    }

    internal static async Task RunAsync(string[] args)
    {
        var initialMode = args.Length == 7 ? args[6] : null;
        var textureWorkflow = initialMode == "--initial-import-playing-texture-workflow";
        var replaceTexture = textureWorkflow || initialMode == "--initial-import-replace-texture";
        var reimportTexture = replaceTexture || initialMode == "--initial-import-reimport-texture";
        var saveAfterSlotUndo = initialMode == "--initial-import-save-overrides-after-undo";
        var transferMaterials = initialMode == "--initial-import-material-transfer";
        var cameraProjection = initialMode is "--initial-import-camera-save" or "--initial-import-camera-reopen";
        var meshVisibility = initialMode is "--initial-import-visibility-save" or "--initial-import-visibility-reopen";
        var saveInitialImport = transferMaterials || saveAfterSlotUndo || reimportTexture ||
            initialMode is "--initial-import-save" or "--initial-import-save-slot" or "--initial-import-save-texture" or "--initial-import-save-overrides" or "--initial-import-camera-save" or "--initial-import-visibility-save";
        var reopenInitialImport = initialMode is "--initial-import-reopen" or "--initial-import-reopen-slot" or "--initial-import-reopen-texture"
            or "--initial-import-reopen-overrides" or "--initial-import-camera-reopen" or "--initial-import-visibility-reopen";
        var verifyImportedTexture = reimportTexture || initialMode is "--initial-import-save-texture" or "--initial-import-reopen-texture";
        var persistMultipleOverrides = saveAfterSlotUndo || initialMode is "--initial-import-save-overrides" or "--initial-import-reopen-overrides";
        var persistSlotOverride = persistMultipleOverrides || initialMode is "--initial-import-save-slot" or "--initial-import-reopen-slot";
        var initialImport = saveInitialImport || reopenInitialImport;
        var saveInspector = args.Length == 7 && args[6] == "--render-inspector-save";
        var reopenInspector = args.Length == 7 && args[6] == "--render-inspector-reopen";
        var restartInspector = saveInspector || reopenInspector;
        var resetMaterial = args.Length == 7 && args[6] == "--render-material-reset";
        var materialSlots = args.Length == 7 && args[6] == "--render-material-slots";
        var referenceDiagnostics = args.Length == 7 && args[6] == "--render-reference-diagnostics";
        var probeRenderInspector = materialSlots || referenceDiagnostics || resetMaterial || restartInspector || args.Length == 7 && args[6] == "--render-inspector";
        var probePlacement = args.Length == 9 && args[6] == "--model-placement";
        var probeCatalog = probePlacement || args.Length == 9 && args[6] == "--model-catalog";
        var probePersistence = probeCatalog || args.Length == 9 && args[6] == "--persisted-imports";
        var probeReimport = probePersistence || args.Length == 9 && args[6] == "--reimport";
        var probeImports = probeRenderInspector || probeReimport || args.Length == 7 && args[6] == "--imports";
        if (args.Length != 6 && !probeImports && !initialImport || args[0] is not ("cobalt" or "microsoft")) throw new ArgumentException("Invalid model delivery probe arguments.");
        var report = args[1];
        var proofDirectory = reimportTexture ? Path.Combine(report, "import-reimported") :
            initialImport ? Path.Combine(report, saveInitialImport ? "import-saved" : "import-reopened") :
            restartInspector ? Path.Combine(report, saveInspector ? "editor-saved" : "editor-reopened") : report;
        var project = args[2];
        var scenePath = args[3];
        var hostId = BuildIdentity.HostId;
        var oldBuild = initialImport ? null! : GameBuildArtifact.Open(args[4], hostId);
        var newBuild = initialImport ? null! : GameBuildArtifact.Open(args[5], hostId);
        var corruptBuild = restartInspector || initialImport ? null : CorruptCopy(oldBuild, report, hostId);
        var sceneHash = Hash(File.ReadAllBytes(scenePath));
        Check(initialImport || oldBuild.BuildId != newBuild.BuildId, "Scene probe requires two distinct build IDs.");
        var compiler = Path.Combine(proofDirectory, "editor-compiler");
        if (restartInspector || initialImport)
        {
            Check(!Directory.Exists(proofDirectory), "Expected a fresh report and build directory for this Editor process.");
            Directory.CreateDirectory(compiler);
        }
        else CopyArtifact(oldBuild, compiler);
        PausedPublication? publication = null;
        ModelImportWorkerDeployment? worker = null;
        var initialWorker = initialImport
            ? new ModelImportWorkerDeployment(args[4], args[5], Directory.EnumerateFiles(args[4], "*", SearchOption.AllDirectories)
                .ToDictionary(path => Path.GetRelativePath(args[4], path).Replace('\\', '/'), path => Hash(File.ReadAllBytes(path))))
            : null;
        Action? cancelReplacement = null;
        SessionIdentity? cancelledCandidate = null;
        var cancelOnLoad = 0;
        var fonts = SkiaFontCatalog.Load("Noto Sans", new Dictionary<string, string>
        {
            ["Noto Sans"] = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "NotoSans-Regular.ttf")
        });
        var configuration = ConfigurationProfiles.Create(profile =>
        {
            profile.AddSchema(AetherConfigurationSchemas.Engine);
            profile.AddSchema(AetherConfigurationSchemas.Authoring);
            profile.AddSchema(EditorConfigurationSchemas.Options);
            profile.AddSchema(EditorConfigurationSchemas.Diagnostics);
            profile.AddSchema(EditorConfigurationSchemas.Window);
            profile.Override("Aether:ReportDirectory", proofDirectory);
            profile.Override("Aether:BuildDirectory", compiler);
            profile.Override("Aether:GpuValidation", true);
            profile.Override("Editor:ProjectPath", project);
        });
        await using var app = App.Builder(builder => { if (args[0] == "microsoft") builder.UseDI(); }).Setup(context =>
        {
            context.UseConfiguration(configuration);
            if (probeImports || initialImport)
            {
                context.UseAetherModelSourceCapture();
                context.UseAetherModelImport();
            }
            if (probeReimport || initialImport)
            {
                context.UseNom();
                context.UseAetherNomContent();
            }
            if (initialWorker != null)
                context.UseAetherNomProjectModelImports(initialWorker, Path.Combine(report, "scratch"));
            if (probePersistence || restartInspector || referenceDiagnostics || initialImport)
                context.UseAetherSceneModelSelectionPersistence();
            if (probePersistence)
            {
                context.Services(services => services.Singleton<IFactory<SceneModelReimportSettings, SceneModelReimportRestoreArguments>>(_ =>
                    new Factory<SceneModelReimportSettings, SceneModelReimportRestoreArguments>(request =>
                        new(request.Ledger, worker!, Path.Combine(report, "scratch"), publication!))));
            }
            if (probeCatalog || restartInspector || referenceDiagnostics || initialImport) context.UseAetherProjectModelCatalog();
            context.UseConfiguredAetherEditor(new AetherComposition
            {
                HostContractId = hostId, GameBuildId = new string('0', 64),
                RuntimeLaunch = NativeRuntimeLaunch.CurrentProcess("--runtime"),
                StatusChanged = status =>
                {
                    if (!initialImport && status is { State: SessionState.Starting, SceneLoaded: true, Identity: { } identity } &&
                        identity.GameBuildId == oldBuild.BuildId && Interlocked.Exchange(ref cancelOnLoad, 0) == 1)
                    {
                        cancelledCandidate = identity;
                        cancelReplacement!();
                    }
                }
            });
            context.UseConfiguredAetherEditorWindow(fonts, options => { options.Width = 1100; options.Height = 720; });
        }).Build();
        var engine = app.Services.Resolve<AetherHost>()!;
        var native = (NativeRuntimeFactory)app.Services.Resolve<IRuntimeFactory>()!;
        var documents = app.Services.Resolve<EditorDocumentCommands>()!;
        var playback = app.Services.Resolve<EditorScenePlayback>()!;
        cancelReplacement = () => playback.Execute(EditorCommand.Cancel);
        var window = app.Services.Resolve<HostedUiWindow>()!;
        var captures = new List<FrameProof>();
        var first = new TaskCompletionSource<UiSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        window.FramePresented += OnFirstFrame;
        void OnFirstFrame(UiSession ui) { window.FramePresented -= OnFirstFrame; first.TrySetResult(ui); }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var token = timeout.Token;
        IProjectAssetStore? modelStore = null;
        try
        {
            await app.StartAsync(token);
            await app.Services.Resolve<EditorWindow>()!.Ready.WaitAsync(token);
            var ui = await first.Task.WaitAsync(token);
            EditorView view = null!;
            await ui.InvokeAsync(() => view = EditorMainWindow.FromRoot(ui.Root).EditorView).WaitAsync(token);
            Check(documents.Execute(DocumentCommand.Open, scenePath), "Editor declined opening the placed scene.");
            await documents.Pending.WaitAsync(token);
            var before = documents.Read().Document ?? throw new InvalidOperationException("No active document.");
            var content = before.Content ?? throw new InvalidOperationException("Editor did not load the placed scene.");
            if (initialImport)
            {
                await VerifyInitialImport();
                window.Close();
                await window.Completion.WaitAsync(token);
                return;
            }
            if (probeImports)
            {
                Check(content.Document.Entities.Length == (materialSlots ? 2 : probePlacement || referenceDiagnostics ? 3 : 5) && !before.IsDirty,
                    "Unexpected fixture scene.");
                var document = documents.Read().Project?.Documents ?? throw new InvalidOperationException("No project document session.");
                var ledgerFactory = app.Services.Resolve<IFactory<ModelImportLedgerSession, ModelImportLedgerArguments>>()!;
                if (probeRenderInspector)
                {
                    await VerifyRenderInspector(document, ledgerFactory);
                    window.Close();
                    await window.Completion.WaitAsync(token);
                    return;
                }
                if (probeReimport)
                {
                    var storeFactory = app.Services.Resolve<IFactory<IProjectAssetStore, OpenAssetStoreArguments>>()!;
                    var publishers = app.Services.Resolve<IFactory<IModelImportContentPublisher, IProjectAssetStore>>()!;
                    modelStore = storeFactory.Create(new(Path.Combine(report, "nom"), oldBuild.RenderContent!.ProjectId,
                        Guid.Parse("ae24f96d-89a8-4ecc-bc22-4bd0cf2f093e"), AssetStoreOpenMode.Open, token))!;
                    publication = new(publishers.Create(modelStore)!);
                    worker = new(args[7], args[8], Directory.EnumerateFiles(args[7], "*", SearchOption.AllDirectories)
                        .ToDictionary(path => Path.GetRelativePath(args[7], path).Replace('\\', '/'), path => Hash(File.ReadAllBytes(path))));
                }
                if (probeCatalog)
                {
                    var projectFactory = app.Services.Resolve<IFactory<ProjectModelCatalog, ResolvedProject>>()!;
                    var catalog = projectFactory.Create(documents.Read().Project!.Project)!;
                    await Register("Red", "source", "red-baseline.tmledger", "ce662048-9dcb-4c60-aaf0-213342054fa6");
                    await Register("Green", "source-green", "green.tmledger", "65710c6d-6781-4476-9bb8-735fc518f008");

                    Task Register(string name, string folder, string ledger, string sourceId)
                    {
                        var id = Guid.Parse(sourceId);
                        return catalog.RegisterAsync(name, new(id, Path.Combine(report, folder), "scene.gltf"),
                            new(id, Path.Combine(report, "ledger", ledger)), token);
                    }
                }
                var (proof, baselineBuild, updatedBuild) = await VerifyModelImportsAsync(report, oldBuild, newBuild,
                    document, before.OpeningId, ledgerFactory, ui, view, engine, playback, documents, publication, worker,
                    scenePath, probePersistence, probeCatalog, probePlacement, token);
                if (probePersistence)
                {
                    before = document.Read();
                    content = before.Content ?? throw new InvalidOperationException("Restored document is no longer active.");
                }
                if (proof.Placement is { } placement)
                {
                    Check(sceneHash != placement.SceneSha256 && content.Document.Id.Value == placement.SceneId &&
                        content.Document.Revision == placement.SceneRevision, "Build did not retain the saved placement revision.");
                    sceneHash = placement.SceneSha256;
                }
                if (probeReimport)
                {
                    Check(playback.RunSelectedBuild(baselineBuild), "Editor declined running the previous build.");
                    await Complete();
                    var firstFrame = await CapturePlaying("reimport-before", baselineBuild, 0);
                    Check(playback.ReplaceSelectedBuild(updatedBuild, token), "Editor declined the new build.");
                    await Complete();
                    var changed = await CapturePlaying("reimport-after", updatedBuild, firstFrame.Tick);
                    await CheckReleased(firstFrame.Generation);
                    Check(playback.ReplaceSelectedBuild(baselineBuild, token), "Editor declined reopening the previous build.");
                    await Complete();
                    var restored = await CapturePlaying("reimport-before-reopened", baselineBuild, changed.Tick);
                    await CheckReleased(changed.Generation);
                    Check(firstFrame.Mask == restored.Mask && firstFrame.Mask.Sha256 != changed.Mask.Sha256 &&
                        firstFrame.Mask.GreenPixels > 100 && firstFrame.Mask.GreenSha256 == changed.Mask.GreenSha256,
                        "GPU render did not preserve the second model and exact previous geometry.");
                    Check(playback.Execute(EditorCommand.Stop), "Editor declined Stop after reimport proof.");
                    await Complete();
                    await CheckReleased(restored.Generation);
                    Check(engine.Gpu.ResourceCount == 0 && engine.Gpu.OpenHandleCount == 0, "Reimport playback retained GPU resources.");
                    proof = proof with { Captures = captures };
                }
                Check(ReferenceEquals(content, document.Read().Content) && !document.Read().IsDirty &&
                    sceneHash == Hash(File.ReadAllBytes(scenePath)), "Import selection or Build modified the authoring scene.");
                Check(documents.Execute(DocumentCommand.Close), "Editor declined closing the document.");
                await documents.Pending.WaitAsync(token);
                await EditorHeaderSmoke.UntilAsync(ui, () => view.StatusBar!.DataContext!.ModelImportDetails.Length == 0, token);
                Check(document.ReadModelImports(before.OpeningId).Count == 0, "Closing the scene retained selected imports.");
                window.Close();
                await window.Completion.WaitAsync(token);
                await File.WriteAllTextAsync(Path.Combine(report, "editor-imports.json"), JsonSerializer.Serialize(proof), token);
                return;
            }
            Check(content.Document.Entities.Length == 3 && !before.IsDirty, "Unexpected captured scene.");

            await ChooseBuild(oldBuild.BuildId.ToLowerInvariant(), "Запустить опубликованную сборку");
            await Complete();
            while (engine.Supervisor.Status.Tick < 5)
            {
                await engine.Supervisor.GetStatusAsync(token);
                await Task.Delay(16, token);
            }
            var original = await CapturePlaying("old", oldBuild, 5);
            Check(!playback.RunSelectedBuild(newBuild), "Editor admitted a second independent Start.");
            await ChooseBuild(newBuild.DirectoryPath, "Заменить текущую сборку");
            await Complete();
            var updated = await CapturePlaying("new", newBuild, original.Tick);
            Check(native.Leases.Count == 2 && updated.Generation > original.Generation,
                "Successful Replace did not activate exactly one new child.");
            await CheckReleased(original.Generation);

            var beforeCorruption = engine.Supervisor.Status.Identity;
            var previousSurface = engine.Frames.Presentation;
            await ChooseBuild(corruptBuild!.DirectoryPath, "Заменить текущую сборку");
            await playback.Pending.WaitAsync(token);
            Check(playback.Error is InvalidDataException corruption &&
                corruption.Message.Contains("render payload hash mismatch", StringComparison.Ordinal) &&
                engine.Supervisor.Status.Identity == beforeCorruption &&
                ReferenceEquals(previousSurface, engine.Frames.Presentation) && native.Leases.Count == 2,
                "Corrupt candidate changed the running child or viewport.");
            var afterCorruption = await CapturePlaying("after-corruption", newBuild, updated.Tick);

            Volatile.Write(ref cancelOnLoad, 1);
            await ChooseBuild(oldBuild.BuildId, "Заменить текущую сборку");
            await playback.Pending.WaitAsync(token);
            Check(cancelledCandidate is not null && playback.Error is GameReplacementException
                { Phase: "Candidate", RollbackAttempted: true, RollbackSucceeded: true, RecoveryError: null, InnerException: OperationCanceledException } &&
                native.Leases.Count == 4, "Cancellation after scene load did not recover the previous build exactly once.");
            var recovered = await CapturePlaying("after-cancel", newBuild, afterCorruption.Tick);
            await EditorHeaderSmoke.UntilAsync(ui, () => ((EditorDocumentViewModel)view.Documents!.DataContext!).ErrorText ==
                "Замена отменена. Предыдущая сборка восстановлена.", token);
            Check(recovered.Generation > cancelledCandidate!.Generation && cancelledCandidate.Generation > updated.Generation,
                "Cancellation did not advance the runtime generation on recovery.");
            await CheckReleased(updated.Generation);
            // The cancelled candidate had completed scene activation. Restore and surface configuration were still pending.
            Check(native.Leases[2].Identity == cancelledCandidate && native.Leases[2].Reaped && native.Leases[2].ResourcesReleased,
                "Cancelled candidate survived rollback.");

            await ChooseBuild(oldBuild.BuildId, "Заменить текущую сборку");
            await Complete();
            var reopened = await CapturePlaying("old-reopened", oldBuild, recovered.Tick);
            Check(native.Leases.Count == 5 && reopened.Generation > recovered.Generation, "Unexpected runtime generations.");
            await CheckReleased(recovered.Generation);

            Check(playback.Execute(EditorCommand.Stop), "Editor did not admit final Stop.");
            await Complete();
            Check(engine.Supervisor.Status.State == SessionState.Stopped && engine.Frames.Presentation == null &&
                engine.Gpu.ResourceCount == 0 && engine.Gpu.OpenHandleCount == 0,
                "Final Stop retained viewport resources.");
            await CheckReleased(reopened.Generation);

            var old = original.Mask;
            var next = updated.Mask;
            Check(old == reopened.Mask && next == afterCorruption.Mask && next == recovered.Mask &&
                old.Width == next.Width && old.Height == next.Height && Math.Abs(old.Left - next.Left) <= 1 &&
                next.Right > old.Right + 10 && next.RedPixels > old.RedPixels * 1.6,
                "GPU scene does not show the expected replacement or recovered geometry.");
            Check(!Directory.Exists(Path.Combine(compiler, "jobs")) &&
                AppDomain.CurrentDomain.GetAssemblies().All(assembly => assembly.GetName().Name != "DeliveryGame"),
                "Selecting a published artifact compiled or loaded game code in the parent.");
            window.Close();
            await window.Completion.WaitAsync(token);

            async Task ChooseBuild(string selection, string title, bool usePrepared = false)
            {
                var tools = view.Documents!;
                var model = (EditorDocumentViewModel)tools.DataContext!;
                await EditorHeaderSmoke.UntilAsync(ui, () => model.CanOpenBuild, token);
                await EditorHeaderSmoke.ClickActionAsync(ui, view, "project", "document.OpenBuild", token, confirmPath: false);
                await EditorHeaderSmoke.UntilAsync(ui, () => tools.IsVisible && tools.Actions.IsVisible && !ui.NeedsUpdate, token);
                await ui.InvokeAsync(() =>
                {
                    Check(model.PathRequest == DocumentCommand.OpenBuild && model.PathPromptText == title, "Wrong build selection prompt.");
                    if (!usePrepared)
                    {
                        ui.Focus(tools.PathInput);
                        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
                        ui.HandleInput(new(InputKind.Text, Text: selection));
                    }
                    Check(model.RequestedPath == selection, "Artifact path never reached its bound field.");
                }).WaitAsync(token);
                await EditorHeaderSmoke.ConfirmDocumentPathAsync(ui, view, token);
                await EditorHeaderSmoke.UntilAsync(ui, () => !model.IsPathPromptOpen, token);
            }

            async Task Complete()
            {
                await playback.Pending.WaitAsync(token);
                if (playback.Error is { } error) throw new InvalidOperationException("Editor scene command failed.", error);
            }

            async Task VerifyInitialImport()
            {
                const string source = "source/scene.gltf";
                var session = documents.Read().Project ?? throw new InvalidOperationException("Project is not open.");
                var document = session.Documents!;
                var catalogFile = Path.Combine(session.Project.ContentDirectory, ".aether-model-catalog.json");
                var ledgerFolder = Path.Combine(session.Project.ContentDirectory, ".aether-model-ledgers");
                var priorProof = Path.Combine(report, "import-saved", "editor-initial-model-import.json");
                var tools = view.Documents!;
                var model = (EditorDocumentViewModel)tools.DataContext!;
                var ledgers = app.Services.Resolve<IFactory<ModelImportLedgerSession, ModelImportLedgerArguments>>()!;
                var failedAdmission = false;
                if (saveInitialImport)
                {
                    Check(!File.Exists(catalogFile) && !Directory.Exists(ledgerFolder) && content.Document.Entities.Length == 1,
                        "The fixture already contained an import or placed model.");
                    await ReadCatalog();
                    await EditorHeaderSmoke.UntilAsync(ui, () => view.Overlays.Popup is EditorStatusPopupView popup &&
                        popup.ModelCatalogMessage.Value == "В каталог ещё не добавлены модели." &&
                        popup.ModelCatalogRows.Children.Count == 0, token, HoldCatalog);
                    await CloseCatalog();

                    var pending = documents.Pending;
                    await EnterPath(source);
                    await EditorHeaderSmoke.UntilAsync(ui, () =>
                    {
                        if (ui.NeedsUpdate || !tools.CancelPath.IsVisible || tools.CancelPath.Bounds.Width <= 0) return false;
                        EditorHeaderSmoke.Move(ui, tools.CancelPath);
                        EditorHeaderSmoke.Click(ui, tools.CancelPath);
                        return true;
                    }, token);
                    Check(ReferenceEquals(pending, documents.Pending) && !File.Exists(catalogFile), "Dismissing the prompt started an import.");
                    await Import("source/missing.gltf");
                    Check(documents.Read().Error is FileNotFoundException && !File.Exists(catalogFile),
                        $"A missing BIN did not stop the initial import: {documents.Read().Error}");

                    using (new FileStream(catalogFile + ".write-lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                    {
                        await Import(source);
                        Check(documents.Read().Error is IOException && !File.Exists(catalogFile),
                            $"A blocked registration exposed a catalog row: {documents.Read().Error}");
                    }
                    var pendingLedger = Directory.EnumerateFiles(ledgerFolder, "*.tmledger").Single();
                    var pendingHash = Hash(await File.ReadAllBytesAsync(pendingLedger, token));
                    failedAdmission = true;
                    await Import(source);
                    Check(documents.Read().Error == null && File.Exists(catalogFile) &&
                        Hash(await File.ReadAllBytesAsync(pendingLedger, token)) == pendingHash,
                        $"Retry failed to register the published ledger: {documents.Read().Error}");
                    var committedHash = Hash(await File.ReadAllBytesAsync(catalogFile, token));
                    await Import(@"source\scene.gltf");
                    Check(documents.Read().Error == null && Hash(await File.ReadAllBytesAsync(catalogFile, token)) == committedHash,
                        "Repeating an imported path changed the catalog.");
                    var item = documents.Read().ModelCatalog!.Single();
                    Check(item.SourcePath == source && Path.GetFileNameWithoutExtension(pendingLedger) == item.SourceId.ToString("N"),
                        "Catalog did not publish the retry's source identity.");

                    await ClickCatalog(popup => popup.ModelCatalogRows.Children.OfType<HStack>()
                        .SelectMany(row => row.Children.OfType<CheckBox>()).SingleOrDefault(check => check.Value.EndsWith(source, StringComparison.Ordinal) && !check.IsChecked));
                    await documents.Pending.WaitAsync(token);
                    Check(document.ReadModelImports(before.OpeningId).Single().SourceId == item.SourceId, "Catalog did not select the new source.");
                    await ClickCatalog(popup => popup.ModelCatalogRows.Children.OfType<HStack>()
                        .SelectMany(row => row.Children.OfType<Button>()).SingleOrDefault(button => button.AccessibleName == "Добавить «scene.gltf» в сцену"));
                    await documents.Pending.WaitAsync(token);
                    await CloseCatalog();
                    var placed = document.Read();
                    Check(documents.Read().Error == null && placed.IsDirty && placed.Content?.UndoCount == content.UndoCount + 1 &&
                        placed.Content.Document.Entities.Length == 2, $"Catalog did not place exactly one instance: {documents.Read().Error}");
                }
                else
                {
                    await ReadCatalog();
                    await EditorHeaderSmoke.UntilAsync(ui, () => view.Overlays.Popup is EditorStatusPopupView popup &&
                        popup.ModelCatalogRows.Children.OfType<HStack>().SelectMany(row => row.Children.OfType<CheckBox>())
                            .Any(check => check.Value.EndsWith(source, StringComparison.Ordinal) && check.IsChecked), token, HoldCatalog);
                    await CloseCatalog();
                }

                var row = documents.Read().ModelCatalog!.Single();
                Check(row.SourcePath == source, "Catalog source path changed.");
                var ledgerPath = Path.Combine(ledgerFolder, $"{row.SourceId:N}.tmledger");
                await using var ledger = ledgers.Create(new(row.SourceId, ledgerPath))!;
                var published = await ledger.OpenAsync(token) ?? throw new InvalidDataException("Committed catalog ledger is missing.");
                var mesh = published.Meshes.Single();
                var entityId = published.Instances.Single().Id;
                if (meshVisibility)
                {
                    await VerifyMeshVisibility();
                    return;
                }
                if (cameraProjection)
                {
                    await VerifyCameraProjection();
                    return;
                }
                if (transferMaterials)
                {
                    await VerifyMaterialTransfer();
                    return;
                }
                var leftMaterial = persistSlotOverride
                    ? published.Bindings.Single(item => item.Key.Locator == "builtin/material/default").Id
                    : mesh.Slots[0].MaterialId;
                var originalRight = mesh.Slots[1].MaterialId;
                var changedRight = mesh.Slots[0].MaterialId;
                var finalRight = persistMultipleOverrides && !saveAfterSlotUndo ? changedRight : originalRight;
                var rightMaterial = saveInitialImport ? originalRight : finalRight;
                var checkpoint = Path.Combine(session.Project.ContentDirectory, ".aether-model-imports", content.Document.Id.Value.ToString("N") + ".tmimports");
                InitialImportProof? applied = null, undone = null;
                if (persistSlotOverride) await VerifyOverrideInInspector();
                if (saveInitialImport)
                {
                    await EditorHeaderSmoke.ClickActionAsync(ui, view, "file", "document.Save", token);
                    await documents.Pending.WaitAsync(token);
                    Check(documents.Read().Error == null && !document.Read().IsDirty, $"Cannot save the placed scene: {documents.Read().Error}");
                }
                before = document.Read();
                content = before.Content!;
                sceneHash = Hash(await File.ReadAllBytesAsync(scenePath, token));
                var renderer = content.Resolved.Entities[new(entityId)].Components.Values.Single(value => value.TypeId == MeshRenderingComponents.MeshRendererId);
                var reference = renderer.Properties["bindings"].Value.GetString();
                Check(content.Document.Entities.Length == 2 && published.Generation == 1 &&
                    document.ReadModelImports(before.OpeningId).Single(item => item.SourceId == row.SourceId).Generation == published.Generation &&
                    renderer.Properties["mesh"].Value.GetString() == mesh.AssetId.ToString("D") &&
                    (reference != mesh.BindingSetId.ToString("D")) == persistSlotOverride &&
                    mesh.Slots.Length == 2 && mesh.Slots[0].SlotId != mesh.Slots[1].SlotId &&
                    mesh.Geometry.Slots.Select(slot => slot.SlotId).SequenceEqual(mesh.Slots.Select(slot => slot.SlotId)),
                    "The scene and restored import do not agree on the published mesh, slots and placement.");

                var result = await BuildAndCapture(published, "initial-model");
                if (persistMultipleOverrides && saveInitialImport)
                {
                    var expected = (saveAfterSlotUndo ? undone : applied)!;
                    Check(expected.Binding == result.Binding && expected.BindingReference == result.BindingReference &&
                        expected.Slots.SequenceEqual(result.Slots) && expected.Frame.Pixels == result.Frame.Pixels,
                        "Save did not preserve the selected overrides and the exact GPU frame.");
                    await File.WriteAllTextAsync(Path.Combine(proofDirectory, "editor-slot-override-history.json"),
                        JsonSerializer.Serialize(new { Applied = applied, Undone = undone }), token);
                }
                if (reimportTexture)
                {
                    await VerifyTextureReimport(result);
                    return;
                }
                if (reopenInitialImport)
                {
                    var prior = JsonSerializer.Deserialize<InitialImportProof>(await File.ReadAllBytesAsync(priorProof, token))!;
                    Check(prior.Completed && prior.RegistrationRetry && prior.EditorPid != result.EditorPid && prior.OpeningId != result.OpeningId &&
                        prior.SourceId == result.SourceId && prior.SourcePath == result.SourcePath && prior.Entity == result.Entity &&
                        prior.Generation == result.Generation && prior.SceneSha256 == result.SceneSha256 && prior.CatalogSha256 == result.CatalogSha256 &&
                        prior.LedgerSha256 == result.LedgerSha256 && prior.SelectionSha256 == result.SelectionSha256 &&
                        prior.OverrideVerified == result.OverrideVerified && prior.Mesh == result.Mesh && prior.Binding == result.Binding &&
                        prior.BindingReference == result.BindingReference &&
                        prior.Texture == result.Texture && prior.Slots.SequenceEqual(result.Slots) && prior.Frame.Mask == result.Frame.Mask &&
                        prior.Frame.Pixels != null && prior.Frame.Pixels == result.Frame.Pixels,
                        "The restarted Editor did not restore the exact catalog, scene, slot IDs and GPU frame.");
                }
                await File.WriteAllTextAsync(Path.Combine(proofDirectory, "editor-initial-model-import.json"), JsonSerializer.Serialize(result), token);

                async Task StartBuild()
                {
                    await EditorHeaderSmoke.UntilAsync(ui, () => view.DataContext!.CanBuild, token);
                    var pending = playback.Pending;
                    await EditorHeaderSmoke.ClickActionAsync(ui, view, "project", "runtime.Build", token);
                    Check(!ReferenceEquals(pending, playback.Pending), "The Build menu action did not admit a command.");
                    await playback.Pending.WaitAsync(token);
                }

                async Task<InitialImportProof> BuildAndCapture(PublishedModelImport publication, string name, uint? topLeft = null,
                    GameBuildArtifact? replacement = null, ulong minimumTick = 0)
                {
                    before = document.Read();
                    content = before.Content!;
                    if (replacement == null)
                    {
                        await StartBuild();
                        await Complete();
                    }
                    var artifact = replacement ?? engine.Editor.Status.Candidate?.Build.Artifact
                        ?? throw new InvalidOperationException("New Editor Build is missing.");
                    Check(Path.GetDirectoryName(artifact.DirectoryPath) == compiler, "The Editor used a preexisting build.");
                    var manifest = artifact.RenderContent!;
                    var bindingReference = ReadBindingReference();
                    var binding = Guid.TryParseExact(bindingReference, "D", out var direct)
                        ? manifest.Resolve(direct, RenderContentProfile.BindingSetKindId)
                        : manifest.Entries.Single(entry => entry.Key.KindId == RenderContentProfile.BindingSetKindId && entry.Key.AssetId != mesh.BindingSetId);
                    var bindingId = binding.Key.AssetId;
                    var exact = (MaterialBindingSetDefinition)binding.Definition;
                    var imported = (MaterialBindingSetDefinition)manifest.Resolve(mesh.BindingSetId, RenderContentProfile.BindingSetKindId).Definition;
                    var slots = exact.Bindings.Select(slot => new ImportedSlotProof(slot.SlotId, AssetProof.From(slot.Material))).ToArray();
                    var geometry = (MeshDefinition)manifest.Resolve(exact.Mesh).Definition;
                    Check(exact.Mesh == imported.Mesh && exact.Mesh.AssetId == mesh.AssetId && geometry.Slots.SequenceEqual(mesh.Geometry.Slots) &&
                        exact.Bindings.Length == 2 && exact.Bindings[0].SlotId == mesh.Slots[0].SlotId && exact.Bindings[0].Material.AssetId == leftMaterial &&
                        exact.Bindings[1].SlotId == mesh.Slots[1].SlotId && exact.Bindings[1].Material.AssetId == rightMaterial &&
                        (rightMaterial != originalRight || exact.Bindings[1] == imported.Bindings[1]) &&
                        exact.Bindings.All(slot => manifest.Resolve(slot.Material).Key == slot.Material),
                        "Build lost the slot IDs or their exact material keys.");
                    var textureKey = VerifyImportedTextureKey(verifyImportedTexture, publication, manifest, exact, publication.Meshes.Single());
                    var stores = app.Services.Resolve<IFactory<IProjectAssetStore, OpenAssetStoreArguments>>()!;
                    var nomId = session.Project.Description.Id;
                    await using (var store = stores.Create(new(Path.Combine(session.Project.ContentDirectory, ".aether-model-content"),
                        nomId, nomId, AssetStoreOpenMode.Open, token))!)
                        foreach (var entry in manifest.Entries.Where(entry => !persistSlotOverride || entry.Key.AssetId != bindingId))
                            Check(await store.FindResourceAsync(entry.Key.AssetId) is { } head && head.KindId == entry.Key.KindId &&
                                head.Digest == entry.Key.Revision.ToString(), "Nom did not retain the exact imported resource.");

                    if (replacement != null)
                        await ChooseBuild(artifact.BuildId, "Заменить текущую сборку", usePrepared: textureWorkflow);
                    else if (textureWorkflow)
                        await ChooseBuild(artifact.BuildId, "Запустить опубликованную сборку");
                    else
                        Check(playback.RunSelectedBuild(artifact), "Editor declined launching the new Build.");
                    await Complete();
                    var frame = await CapturePlaying(name, artifact, minimumTick, topLeft);
                    if (!replaceTexture) await StopAndCheck(frame.Generation, textureKey);
                    if (persistSlotOverride)
                        Check((rightMaterial == originalRight ? frame.Mask.RedPixels == 0 && frame.Mask.GreenPixels > 100 :
                            frame.Mask.RedPixels > 100 && frame.Mask.GreenPixels == 0 &&
                            frame.Pixels?.Slots?.RedRightPixels == frame.Mask.RedPixels) &&
                            frame.Pixels?.Slots is { WhitePixels: > 100 }, "The rebuilt GPU frame did not show the selected slot colors.");
                    return new(true, Environment.ProcessId, before.OpeningId, row.SourceId, row.SourcePath, entityId,
                        publication.Generation, sceneHash, Hash(await File.ReadAllBytesAsync(catalogFile, token)),
                        Hash(await File.ReadAllBytesAsync(ledgerPath, token)), AssetProof.From(exact.Mesh), AssetProof.From(binding.Key), slots, frame, failedAdmission)
                    {
                        SelectionSha256 = before.IsDirty && persistMultipleOverrides ? "" : Hash(await File.ReadAllBytesAsync(checkpoint, token)),
                        BindingReference = bindingReference,
                        OverrideVerified = persistSlotOverride,
                        Texture = textureKey is { } key ? AssetProof.From(key) : null
                    };
                }

                string ReadBindingReference() => document.Read().Content!.Resolved.Entities[new(entityId)].Components.Values
                    .Single(value => value.TypeId == MeshRenderingComponents.MeshRendererId).Properties["bindings"].Value.GetString();

                async Task VerifyTextureReimport(InitialImportProof initial)
                {
                    const string png = "textures/color.png";
                    var sourceDirectory = Path.Combine(report, "source");
                    var original = replaceTexture ? engine.Editor.OpenPublishedBuild(initial.Frame.BuildId) : null;
                    var beforeEdit = SourceHashes();
                    var currentText = $"{row.Name} · Актуальна";
                    var staleText = $"{row.Name} · Импорт устарел";
                    await ShowImportStatus(ModelImportFreshnessStatus.Current, currentText, published.Generation);
                    await CloseCatalog();
                    await File.WriteAllBytesAsync(Path.Combine(sourceDirectory, png),
                        await File.ReadAllBytesAsync(Path.Combine(report, "revised-color.png"), token), token);
                    var afterEdit = SourceHashes();
                    Check(beforeEdit.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(afterEdit.Keys) &&
                        beforeEdit.Where(entry => entry.Value != afterEdit[entry.Key]).Select(entry => entry.Key).SequenceEqual([png]),
                        "The PNG must be the only changed source file.");

                    await ShowImportStatus(ModelImportFreshnessStatus.Stale, staleText, published.Generation);
                    await CloseCatalog();
                    string? blocked = null;
                    FrameProof? afterBlockedBuild = null;
                    if (replaceTexture && !textureWorkflow)
                    {
                        // The Editor locks document commands during play. Publish through the document API.
                        await document.ReimportModelAsync(before.OpeningId, row.SourceId, published.Generation, token);
                    }
                    else
                    {
                        var retained = engine.Editor.Status;
                        await StartBuild();
                        blocked = playback.Error?.Message;
                        Check(blocked?.Contains($"«{row.Name}» устарела", StringComparison.Ordinal) == true &&
                            retained.ContextVersion == engine.Editor.Status.ContextVersion &&
                            ReferenceEquals(retained.Candidate, engine.Editor.Status.Candidate),
                            $"Stale texture admitted a Build or replaced the previous candidate: {blocked}");
                        if (textureWorkflow)
                        {
                            await EditorHeaderSmoke.UntilAsync(ui, () => tools.IsVisible &&
                                model.ErrorText.Contains(blocked!, StringComparison.Ordinal) &&
                                tools.DismissTools is { IsEffectivelyEnabled: true, Bounds.Width: > 0 } && !ui.NeedsUpdate, token);
                            await ui.InvokeAsync(() => EditorHeaderSmoke.Click(ui, tools.DismissTools)).WaitAsync(token);
                            await EditorHeaderSmoke.UntilAsync(ui, () => !tools.IsVisible && !ui.NeedsRender, token);
                            afterBlockedBuild = await CapturePlaying("texture-after-blocked-build", original!, initial.Frame.Tick);
                            CheckRetained(afterBlockedBuild);
                        }
                        await ReimportFromStatusPopup();
                    }

                    var revised = await ledger.OpenAsync(token) ?? throw new InvalidDataException("The revised model ledger is missing.");
                    Check(revised.Generation == published.Generation + 1 && revised.Candidate.InputRevision != published.Candidate.InputRevision &&
                        revised.Bindings.SequenceEqual(published.Bindings) && revised.Instances.SequenceEqual(published.Instances),
                        "PNG-only reimport changed the model identities or placement.");
                    var sameScene = document.Read();
                    Check(sameScene.OpeningId == initial.OpeningId && !sameScene.IsDirty &&
                        Hash(await File.ReadAllBytesAsync(scenePath, token)) == initial.SceneSha256,
                        "Reimport changed or reopened the authoring document.");
                    before = sameScene;
                    content = sameScene.Content!;
                    await ShowImportStatus(ModelImportFreshnessStatus.Current, currentText, revised.Generation);
                    await CloseCatalog();
                    FrameProof? retainedFrame = null;
                    GameBuildArtifact? replacement = null;
                    if (original != null)
                    {
                        var running = engine.Supervisor.Status;
                        if (textureWorkflow)
                        {
                            await StartBuild();
                            await Complete();
                            replacement = engine.Editor.Status.Candidate?.Build.Artifact
                                ?? throw new InvalidOperationException("Playing Build published no candidate.");
                        }
                        else
                        {
                            var request = session.CreateBuildRequest(checked(original.SourceRevision + 1)) with
                            {
                                TrackInputs = true, CaptureCompilation = true, CapturePreparation = true, CaptureProjectReferences = true,
                                RenderContent = await document.CheckModelImportsAsync(before.OpeningId, token)
                            };
                            replacement = (await engine.Builds.BuildAsync(request, token)).Artifact;
                        }
                        Check(running.State == SessionState.Playing && running.Identity?.Generation == initial.Frame.Generation,
                            "The original Runtime was not playing after reimport.");
                        var minimumTick = Math.Max(initial.Frame.Tick + 1, afterBlockedBuild?.Tick ?? 0);
                        retainedFrame = await CapturePlaying("texture-before-replace", original, minimumTick);
                        CheckRetained(retainedFrame);
                    }
                    var updated = await BuildAndCapture(revised, "texture-reimported", 0x2194D5FF, replacement, retainedFrame?.Tick ?? 0);
                    var oldTexture = initial.Texture ?? throw new InvalidDataException("Missing original texture.");
                    var newTexture = updated.Texture ?? throw new InvalidDataException("Missing revised texture.");
                    Check(oldTexture.AssetId == newTexture.AssetId && oldTexture.Revision != newTexture.Revision &&
                        initial.Frame.BuildId != updated.Frame.BuildId && updated.Mesh == initial.Mesh &&
                        updated.Binding.AssetId == initial.Binding.AssetId && updated.Binding.Revision != initial.Binding.Revision &&
                        updated.Slots[0].Id == initial.Slots[0].Id && updated.Slots[0].Material.AssetId == initial.Slots[0].Material.AssetId &&
                        updated.Slots[0].Material.Revision != initial.Slots[0].Material.Revision && updated.Slots[1] == initial.Slots[1] &&
                        updated.SelectionSha256 != initial.SelectionSha256 && updated.CatalogSha256 == initial.CatalogSha256,
                        "Reimport did not preserve IDs and update the exact texture/material/binding revisions.");
                    var initialPixels = initial.Frame.Pixels!;
                    var updatedPixels = updated.Frame.Pixels!;
                    var oldTiles = initialPixels.Texture!;
                    var newTiles = updatedPixels.Texture!;
                    Check(initialPixels.Sha256 != updatedPixels.Sha256 &&
                        initialPixels.Slots!.RightRgba == updatedPixels.Slots!.RightRgba &&
                        newTiles == oldTiles with { TopLeft = oldTiles.TopLeft with { Rgba = "2194D5FF" } },
                        "The new Build must change the expected texel and preserve the other regions.");
                    if (retainedFrame != null)
                    {
                        Check(native.Leases.Count == 2 && updated.Frame.Generation == retainedFrame.Generation + 1 &&
                            updated.Frame.Pid != retainedFrame.Pid, "Replace did not activate exactly one new playing child.");
                        var oldKey = original!.RenderContent!
                            .Resolve(oldTexture.AssetId, RenderContentProfile.BaseColorTextureKindId).Key;
                        await CheckReleased(initial.Frame.Generation, oldKey);
                        var newKey = replacement!.RenderContent!.Resolve(newTexture.AssetId, RenderContentProfile.BaseColorTextureKindId).Key;
                        await StopAndCheck(updated.Frame.Generation, newKey);
                    }

                    await File.WriteAllTextAsync(Path.Combine(proofDirectory, "editor-texture-reimport.json"), JsonSerializer.Serialize(new
                    {
                        Completed = true, Before = initial, After = updated, BuildBlocked = blocked, RetainedBeforeReplace = retainedFrame,
                        AfterBlockedBuild = afterBlockedBuild,
                        Statuses = new[] { currentText, staleText, currentText },
                        ChangedDependency = png, SourceBefore = beforeEdit, SourceAfter = afterEdit
                    }), token);

                    void CheckRetained(FrameProof frame) =>
                        Check(frame.Generation == initial.Frame.Generation && frame.Pid == initial.Frame.Pid &&
                            frame.Pixels == initial.Frame.Pixels && native.Leases.Count == 1,
                            "The playing child or its viewport changed before Replace.");

                    async Task ReimportFromStatusPopup()
                    {
                        await ShowImportStatus(ModelImportFreshnessStatus.Stale, staleText, published.Generation);
                        var pending = documents.Pending;
                        await EditorHeaderSmoke.UntilAsync(ui, () =>
                        {
                            if (ui.NeedsUpdate || view.Overlays.Popup is not EditorStatusPopupView popup ||
                                popup.ReimportActions.Children.OfType<Button>().SingleOrDefault(
                                    button => button.Value == $"Переимпортировать «{row.Name}»") is not { IsEffectivelyEnabled: true } action ||
                                !documents.CanExecute(DocumentCommand.ReimportModel)) return false;
                            ui.Focus(action);
                            ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Enter));
                            ui.HandleInput(new(InputKind.KeyUp, Key: UiKey.Enter));
                            return true;
                        }, token, HoldCatalog);
                        Check(!ReferenceEquals(pending, documents.Pending), "The Reimport button did not admit a document command.");
                        await documents.Pending.WaitAsync(token);
                        await CloseCatalog();
                        Check(documents.Read().Error == null,
                            $"Editor could not reimport the selected model: {documents.Read().Error}");
                    }

                    Dictionary<string, string> SourceHashes() => Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories)
                        .ToDictionary(path => Path.GetRelativePath(sourceDirectory, path).Replace('\\', '/'), path => Hash(File.ReadAllBytes(path)), StringComparer.Ordinal);

                    Task ShowImportStatus(ModelImportFreshnessStatus status, string display, long generation) =>
                        EditorHeaderSmoke.UntilAsync(ui, () => view.Overlays.Popup is EditorStatusPopupView popup &&
                            popup.ModelImportDetails is { IsVisible: true, Value: var text } && text == display &&
                            document.ReadModelImports(before.OpeningId).SingleOrDefault() is { } selected &&
                            selected.SourceId == row.SourceId && selected.Status == status && selected.Generation == generation, token, HoldCatalog);
                }

                async Task StopAndCheck(ulong generation, RenderAssetKey? texture)
                {
                    Check(playback.Execute(EditorCommand.Stop), "Editor declined Stop after viewport capture.");
                    await Complete();
                    await CheckReleased(generation, texture);
                    Check(engine.Supervisor.Status.State == SessionState.Stopped && engine.Frames.Presentation == null &&
                        engine.Gpu.ResourceCount == 0 && engine.Gpu.OpenHandleCount == 0, "Stop retained viewport resources.");
                }

                async Task VerifyMeshVisibility()
                {
                    const long matchingMask = 2;
                    const long otherMask = 1;
                    var rendererEntity = new EntityId(entityId);
                    var renderer = document.Read().Content!.Resolved.Entities[rendererEntity].Components.Values.Single(
                        item => item.TypeId == MeshRenderingComponents.MeshRendererId).Id;
                    var camera = document.Read().Content!.Resolved.Entities.Values.Single(entity =>
                        entity.Components.Values.Any(item => item.TypeId == MeshRenderingComponents.CameraId));
                    var cameraComponent = camera.Components.Values.Single(item => item.TypeId == MeshRenderingComponents.CameraId).Id;
                    var inspector = view.Inspector!;
                    var path = Path.Combine(proofDirectory, "editor-mesh-visibility.json");
                    await EditorHierarchySmoke.ClickItemAsync(ui, view, entityId.ToString("D"), token);

                    if (reopenInitialImport)
                    {
                        var savedPath = Path.Combine(report, "import-saved", "editor-mesh-visibility.json");
                        var saved = JsonSerializer.Deserialize<MeshVisibilityProof>(await File.ReadAllBytesAsync(savedPath, token))!;
                        Check(saved.Completed && saved.EditorPid != Environment.ProcessId && saved.Entity == entityId &&
                            saved.Renderer == renderer.Value && saved.Camera == cameraComponent.Value && saved.SceneSha256 == sceneHash &&
                            saved.Initial?.Pixels != null, "Reopening Editor did not preserve the scene and component identities.");
                        await ExpectRenderer(false, matchingMask, draft: false);
                        ExpectDocument(false, matchingMask, matchingMask);
                        var disabled = await BuildFrame("mesh-disabled", visible: false);

                        await ClickInspector(() => inspector.Properties[(renderer, "enabled")].Input);
                        await ExpectRenderer(true, matchingMask, draft: true);
                        await Apply();
                        await ExpectRenderer(true, matchingMask, draft: false);
                        ExpectDocument(true, matchingMask, matchingMask);
                        var matching = await BuildFrame("mesh-matching-layer", visible: true);
                        Check(matching.Pixels == saved.Initial!.Pixels, "Matching layers did not reproduce the exact initial frame.");

                        await EditorHierarchySmoke.ClickItemAsync(ui, view, camera.Id.Value.ToString("D"), token);
                        await EditorHeaderSmoke.UntilAsync(ui, () => LayerText(cameraComponent, matchingMask), token);
                        await EnterLayer(cameraComponent, otherMask.ToString(CultureInfo.InvariantCulture));
                        await Apply();
                        await EditorHeaderSmoke.UntilAsync(ui, () => LayerText(cameraComponent, otherMask) &&
                            inspector.DataContext?.HasDraft == false, token);
                        ExpectDocument(true, matchingMask, otherMask);
                        var mismatch = await BuildFrame("mesh-mismatched-layer", visible: false);
                        Check(disabled.Pixels == mismatch.Pixels && matching.Pixels != mismatch.Pixels &&
                            disabled.BuildId != matching.BuildId && matching.BuildId != mismatch.BuildId,
                            "Vulkan did not distinguish matching visibility from the two hidden states.");
                        await WriteProof(new(true, Environment.ProcessId, entityId, renderer.Value, cameraComponent.Value, sceneHash,
                            null, disabled, matching, mismatch));
                        return;
                    }

                    await ExpectRenderer(true, uint.MaxValue, draft: false);
                    ExpectDocument(true, uint.MaxValue, matchingMask);
                    var initial = await BuildFrame("mesh-visible", visible: true);
                    var unedited = document.Read().Content!;
                    await ClickInspector(() => inspector.Properties[(renderer, "enabled")].Input);
                    await ExpectRenderer(false, uint.MaxValue, draft: true);
                    Check(ReferenceEquals(unedited, document.Read().Content), "Toggling visibility committed before Apply.");
                    await Apply();
                    await ExpectRenderer(false, uint.MaxValue, draft: false);
                    var visibilityApplied = document.Read().Content!;
                    Check(visibilityApplied.UndoCount == unedited.UndoCount + 1, "Visibility did not have its own history operation.");
                    ExpectDocument(false, uint.MaxValue, matchingMask);

                    foreach (var invalid in new[] { "-1", "4294967296" })
                    {
                        await EnterLayer(renderer, invalid);
                        await EditorHeaderSmoke.UntilAsync(ui, () => inspector.Properties[(renderer, "layers")].Input is TextBox box &&
                            box.Value == invalid && box.HasError && !inspector.Apply.IsEnabled, token);
                        Check(ReferenceEquals(visibilityApplied, document.Read().Content), "An invalid layer mask changed the document.");
                    }
                    await EnterLayer(renderer, "0");
                    await ExpectRenderer(false, 0, draft: true);
                    await EditorHeaderSmoke.UntilAsync(ui, () => inspector.Apply.IsEnabled, token);
                    await EnterLayer(renderer, matchingMask.ToString(CultureInfo.InvariantCulture));
                    await ExpectRenderer(false, matchingMask, draft: true);
                    Check(ReferenceEquals(visibilityApplied, document.Read().Content), "Layer input committed before Apply.");
                    await Apply();
                    await ExpectRenderer(false, matchingMask, draft: false);
                    Check(document.Read().Content!.UndoCount == unedited.UndoCount + 2, "Layers did not have a separate history operation.");
                    ExpectDocument(false, matchingMask, matchingMask);

                    await StepHistory("edit.undo", false, uint.MaxValue);
                    await StepHistory("edit.undo", true, uint.MaxValue);
                    await StepHistory("edit.redo", false, uint.MaxValue);
                    await StepHistory("edit.redo", false, matchingMask);
                    await EditorHeaderSmoke.ClickActionAsync(ui, view, "file", "document.Save", token);
                    await documents.Pending.WaitAsync(token);
                    Check(documents.Read().Error == null && !document.Read().IsDirty, "The visibility and layers scene did not save.");
                    sceneHash = Hash(await File.ReadAllBytesAsync(scenePath, token));
                    await WriteProof(new(true, Environment.ProcessId, entityId, renderer.Value, cameraComponent.Value, sceneHash, initial));

                    async Task StepHistory(string action, bool enabled, long mask)
                    {
                        await EditorHeaderSmoke.ClickActionAsync(ui, view, "edit", action, token);
                        await ExpectRenderer(enabled, mask, draft: false);
                        ExpectDocument(enabled, mask, matchingMask);
                    }

                    Task Apply() => ClickInspector(() => inspector.Apply);
                    Task ExpectRenderer(bool enabled, long mask, bool draft) => EditorHeaderSmoke.UntilAsync(ui, () =>
                        inspector.DataContext?.HasDraft == draft &&
                        inspector.Properties.TryGetValue((renderer, "enabled"), out var field) &&
                        field.Property.Label == "Видимость" && field.Input is CheckBox box && box.IsChecked == enabled &&
                        LayerText(renderer, mask), token);

                    bool LayerText(ComponentId component, long mask) =>
                        inspector.Properties.TryGetValue((component, "layers"), out var field) &&
                        field.Property.Label == "Маска слоёв" && field.Input is TextBox box &&
                        box.Value == mask.ToString(CultureInfo.InvariantCulture) && !box.HasError;

                    async Task EnterLayer(ComponentId component, string text)
                    {
                        await ClickInspector(() => inspector.Properties[(component, "layers")].Input);
                        await ui.InvokeAsync(() =>
                        {
                            ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
                            ui.HandleInput(new(InputKind.Text, Text: text));
                        }).WaitAsync(token);
                    }

                    void ExpectDocument(bool enabled, long rendererMask, long cameraMask)
                    {
                        var scene = document.Read().Content!.Resolved;
                        var properties = scene.Entities[rendererEntity].Components[renderer].Properties;
                        Check(properties["enabled"].Value.GetBoolean() == enabled && properties["layers"].Value.GetInteger() == rendererMask &&
                            scene.Entities[camera.Id].Components[cameraComponent].Properties["layers"].Value.GetInteger() == cameraMask,
                            "The document did not contain the exact visibility and masks.");
                    }

                    async Task<FrameProof> BuildFrame(string capture, bool visible)
                    {
                        before = document.Read();
                        content = before.Content!;
                        await StartBuild();
                        await Complete();
                        var artifact = engine.Editor.Status.Candidate!.Build.Artifact;
                        Check(playback.RunSelectedBuild(artifact), "Editor declined playing the visibility scene.");
                        await Complete();
                        var frame = await CapturePlaying(capture, artifact, 0);
                        Check(frame.Pixels != null && (visible
                            ? frame.Mask.RedPixels > 100 && frame.Mask.GreenPixels > 100
                            : frame.Mask.RedPixels == 0 && frame.Mask.GreenPixels == 0),
                            $"Unexpected Vulkan visibility in {capture}: {frame.Mask.RedPixels}/{frame.Mask.GreenPixels} pixels.");
                        await StopAndCheck(frame.Generation, null);
                        return frame;
                    }

                    Task WriteProof(MeshVisibilityProof proof) =>
                        File.WriteAllTextAsync(path, JsonSerializer.Serialize(proof), token);
                }

                async Task VerifyCameraProjection()
                {
                    var camera = document.Read().Content!.Resolved.Entities.Values.Single(entity =>
                        entity.Components.Values.Any(component => component.TypeId == MeshRenderingComponents.CameraId));
                    var component = camera.Components.Values.Single(value => value.TypeId == MeshRenderingComponents.CameraId).Id;
                    var inspector = view.Inspector!;
                    var path = Path.Combine(proofDirectory, "editor-camera-projection.json");
                    var original = new CameraProjectionValues(Math.PI / 3, .1, 100);
                    var wider = new CameraProjectionValues(80 * (Math.PI / 180d), .25, 30);
                    await EditorHierarchySmoke.ClickItemAsync(ui, view, camera.Id.Value.ToString("D"), token);

                    if (reopenInitialImport)
                    {
                        var savedPath = Path.Combine(report, "import-saved", "editor-camera-projection.json");
                        var saved = JsonSerializer.Deserialize<CameraProjectionProof>(await File.ReadAllBytesAsync(savedPath, token))!;
                        await Expect("80", "0.25", "30", draft: false);
                        var restored = await BuildCameraFrame("camera-reopened", wider);
                        Check(saved.Completed && saved.EditorPid != Environment.ProcessId && saved.Camera == component.Value &&
                            saved.SceneSha256 == sceneHash && saved.Current.Values == restored.Values &&
                            saved.Current.Frame.Pixels != null && saved.Current.Frame.Pixels == restored.Frame.Pixels,
                            "Reopening Editor did not restore the camera settings and exact Vulkan pixels.");
                        await SaveProof(restored);
                        return;
                    }

                    await Expect("60", "0.1", "100", draft: false);
                    var initial = await BuildCameraFrame("camera-initial", original);
                    var unedited = document.Read().Content!;
                    await Enter("fov", "80");
                    await Enter("near", "40");
                    await Enter("far", "30");
                    await Expect("80", "40", "30", draft: true);
                    await EditorHeaderSmoke.UntilAsync(ui, () => !inspector.Apply.IsEnabled &&
                        inspector.Properties[(component, "far")].Property.HasError, token);
                    Check(ReferenceEquals(document.Read().Content, unedited), "Projection input modified the committed document.");

                    await Enter("near", "0.25");
                    await Expect("80", "0.25", "30", draft: true);
                    await ClickInspector(() => inspector.Apply);
                    var applied = document.Read().Content!;
                    Check(applied.UndoCount == unedited.UndoCount + 1, "The projection edits must be a single history operation.");
                    var changed = await BuildCameraFrame("camera-wide", wider);
                    Check(changed.Frame.Mask.RedPixels < initial.Frame.Mask.RedPixels &&
                        changed.Frame.Mask.GreenPixels < initial.Frame.Mask.GreenPixels &&
                        changed.Frame.Pixels!.Sha256 != initial.Frame.Pixels!.Sha256,
                        "The wider camera did not reduce both projected shapes on Vulkan.");

                    await EditorHeaderSmoke.ClickActionAsync(ui, view, "edit", "edit.undo", token);
                    await Expect("60", "0.1", "100", draft: false);
                    Check(document.Read().Content!.ContentRevision == unedited.ContentRevision, "Undo did not restore the camera values.");
                    var undone = await BuildCameraFrame("camera-undone", original);
                    Check(initial.Frame.Pixels == undone.Frame.Pixels, "Undo did not restore the exact Vulkan pixels.");
                    await EditorHeaderSmoke.ClickActionAsync(ui, view, "edit", "edit.redo", token);
                    await Expect("80", "0.25", "30", draft: false);
                    Check(document.Read().Content!.ContentRevision == applied.ContentRevision, "Redo did not restore the wider camera.");
                    await EditorHeaderSmoke.ClickActionAsync(ui, view, "file", "document.Save", token);
                    await documents.Pending.WaitAsync(token);
                    Check(documents.Read().Error == null && !document.Read().IsDirty, "The wider camera scene did not save.");
                    sceneHash = Hash(await File.ReadAllBytesAsync(scenePath, token));
                    await SaveProof(changed, initial, undone);

                    async Task Enter(string key, string value)
                    {
                        await ClickInspector(() => inspector.Properties[(component, key)].Input);
                        await ui.InvokeAsync(() =>
                        {
                            ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
                            ui.HandleInput(new(InputKind.Text, Text: value));
                        }).WaitAsync(token);
                    }

                    Task Expect(string angle, string near, string far, bool draft) =>
                        EditorHeaderSmoke.UntilAsync(ui, () => inspector.DataContext?.HasDraft == draft &&
                            Shows("fov", angle) && Shows("near", near) && Shows("far", far), token);

                    bool Shows(string key, string text) => inspector.Properties.TryGetValue((component, key), out var editor) &&
                        editor.Input is TextBox input && input.Value == text;

                    async Task<CameraProjectionFrame> BuildCameraFrame(string capture, CameraProjectionValues expected)
                    {
                        before = document.Read();
                        content = before.Content!;
                        var cameraState = content.Resolved.Entities[camera.Id].Components[component];
                        var values = new CameraProjectionValues(cameraState.Properties["fov"].Value.GetNumber(),
                            cameraState.Properties["near"].Value.GetNumber(), cameraState.Properties["far"].Value.GetNumber());
                        Check(values == expected, "Scene did not contain the exact vertical angle and both distances.");
                        await StartBuild();
                        await Complete();
                        var artifact = engine.Editor.Status.Candidate!.Build.Artifact;
                        Check(playback.RunSelectedBuild(artifact), "Editor declined playing the camera scene.");
                        await Complete();
                        var frame = await CapturePlaying(capture, artifact, 0);
                        await StopAndCheck(frame.Generation, null);
                        return new(values, frame);
                    }

                    Task SaveProof(CameraProjectionFrame current, CameraProjectionFrame? first = null, CameraProjectionFrame? undo = null) =>
                        File.WriteAllTextAsync(path, JsonSerializer.Serialize(new CameraProjectionProof(true, Environment.ProcessId,
                            component.Value, sceneHash, current, first, undo)), token);
                }

                async Task VerifyMaterialTransfer()
                {
                    var destination = await ImportChoice("source/target.gltf");
                    var partial = await ImportChoice("source/partial.gltf");
                    var inspector = view.Inspector!;
                    var component = document.Read().Content!.Resolved.Entities[new(entityId)].Components.Values
                        .Single(value => value.TypeId == MeshRenderingComponents.MeshRendererId).Id;
                    var white = published.Bindings.Single(item => item.Key.Locator == "builtin/material/default").Id;
                    var red = mesh.Slots[0].MaterialId;
                    await EditorHierarchySmoke.ClickItemAsync(ui, view, entityId.ToString("D"), token);
                    await EditorHeaderSmoke.UntilAsync(ui, () => Selection() is { Meshes.Count: 3, Enabled: true } selection &&
                        selection.Mesh == mesh.AssetId && selection.MeshesWithMaterialTransfer.Count == 0, token);
                    await ChooseSlot("left", white);
                    await ClickInspector(() => inspector.Apply);
                    await ChooseSlot("right", red);
                    await ClickInspector(() => inspector.Apply);
                    await EditorHeaderSmoke.UntilAsync(ui, () => Selection() is { } selection &&
                        selection.MeshesWithMaterialTransfer.SequenceEqual([destination.AssetId]), token);

                    var original = document.Read().Content!;
                    var meshField = inspector.Properties[(component, "mesh")];
                    await ChooseMesh(destination.AssetId, transfer: false);
                    await Expect(destination, destination.Slots[0].MaterialId, destination.Slots[1].MaterialId, draft: true);
                    Check(ReferenceEquals(document.Read().Content, original), "The ordinary mesh choice committed its draft.");
                    await ClickInspector(() => inspector.Cancel);
                    await Expect(mesh, white, red, draft: false);
                    await ChooseMesh(destination.AssetId, transfer: true);
                    await Expect(destination, white, red, draft: true);
                    Check(ReferenceEquals(document.Read().Content, original), "Transfer escaped the inspector draft.");
                    await ClickInspector(() => inspector.Apply);
                    var edit = document.Read().Content!;
                    Check(edit.UndoCount == original.UndoCount + 1, "Transfer must commit the mesh and binding as one edit.");
                    var transferred = await BuildTransferFrame(destination, "materials-transferred");

                    await EditorHeaderSmoke.ClickActionAsync(ui, view, "edit", "edit.undo", token);
                    await Expect(mesh, white, red, draft: false);
                    Check(document.Read().Content!.ContentRevision == original.ContentRevision, "Undo did not restore the original mesh and binding.");
                    var undo = await BuildTransferFrame(mesh, "material-transfer-undone");
                    Check(transferred.Frame.Pixels!.Slots!.LeftRgba == undo.Frame.Pixels!.Slots!.LeftRgba &&
                        transferred.Frame.Mask.RedPixels < undo.Frame.Mask.RedPixels &&
                        transferred.Frame.Pixels.Slots.RightRgba != undo.Frame.Pixels.Slots.RightRgba,
                        "Vulkan did not show the same materials on the new right-hand geometry.");
                    await EditorHeaderSmoke.ClickActionAsync(ui, view, "edit", "edit.redo", token);
                    await Expect(destination, white, red, draft: false);
                    Check(document.Read().Content!.ContentRevision == edit.ContentRevision, "Redo did not restore the transfer.");

                    await EditorHeaderSmoke.ClickActionAsync(ui, view, "edit", "edit.undo", token);
                    await Expect(mesh, white, red, draft: false);
                    await ChooseSlot("right", mesh.Slots[1].MaterialId);
                    await ClickInspector(() => inspector.Apply);
                    await EditorHeaderSmoke.UntilAsync(ui, () => Selection() is { } selection &&
                        selection.MeshesWithMaterialTransfer.Contains(partial.AssetId), token);
                    await ChooseMesh(partial.AssetId, transfer: true);
                    await Expect(partial, partial.Slots[0].MaterialId, white, draft: true);
                    await ClickInspector(() => inspector.Cancel);

                    await File.WriteAllTextAsync(Path.Combine(proofDirectory, "editor-material-transfer.json"), JsonSerializer.Serialize(new
                    {
                        Completed = true, Transferred = transferred, Undone = undo,
                        OrdinaryChoiceUsesDefaults = true, PartialMatchRejectsTwoOverrides = true, NamedSlotMovesAcrossIndices = true
                    }), token);

                    InspectorRenderAssetSelection? Selection() => inspector.DataContext?.RenderAssets;

                    Task Expect(PublishedModelMesh target, Guid first, Guid second, bool draft) =>
                        EditorHeaderSmoke.UntilAsync(ui, () => inspector.DataContext?.HasDraft == draft && Selection() is { Slots.Count: 2 } selection &&
                            selection.Mesh == target.AssetId && selection.Slots[0].Id == target.Slots[0].SlotId &&
                            selection.Slots[1].Id == target.Slots[1].SlotId && selection.Slots[0].Material == first &&
                            selection.Slots[1].Material == second &&
                            SlotButton(selection.Slots[0].Name)?.Value == SlotLabel(selection, 0, first) &&
                            SlotButton(selection.Slots[1].Name)?.Value == SlotLabel(selection, 1, second), token);

                    static string SlotLabel(InspectorRenderAssetSelection selection, int index, Guid material) =>
                        selection.Slots[index].Name + " · " + selection.Materials.Single(item => item.Id == material).Name;

                    Button? SlotButton(string name) => Descendants(inspector.Properties[(component, "bindings")].Row)
                        .OfType<Button>().SingleOrDefault(item => item.AccessibleName == "Материал слота " + name);

                    async Task ChooseSlot(string name, Guid material)
                    {
                        string option = "";
                        await ui.InvokeAsync(() => option = Selection()!.Materials.Single(item => item.Id == material).Name).WaitAsync(token);
                        await ClickInspector(() => SlotButton(name));
                        await ClickInspector(() => VisibleChoice(inspector.Properties[(component, "bindings")].Row,
                            $"Материал слота {name} · {option}"));
                        await EditorHeaderSmoke.UntilAsync(ui, () => inspector.DataContext!.CanApply &&
                            Selection()!.Slots.Single(item => item.Name == name).Material == material, token);
                    }

                    async Task ChooseMesh(Guid id, bool transfer)
                    {
                        string option = "";
                        await ui.InvokeAsync(() => option = Selection()!.Meshes.Single(item => item.Id == id).Name).WaitAsync(token);
                        await ClickInspector(() => meshField.Input);
                        var action = (transfer ? "Выбрать с переносом материалов · " : "Меш · ") + option;
                        await ClickInspector(() => VisibleChoice(meshField.Row, action));
                    }

                    Button? VisibleChoice(Element row, string label)
                    {
                        var list = Descendants(row).OfType<ScrollViewer>().Single();
                        var button = Descendants(list).OfType<Button>().SingleOrDefault(item => item.AccessibleName == label);
                        if (button is not { Bounds.Width: > 0, Bounds.Height: > 0 }) return null;
                        list.BringIntoView(button.Bounds);
                        return list.ChildClipBounds.Contains(button.Bounds.X + button.Bounds.Width / 2,
                            button.Bounds.Y + button.Bounds.Height / 2) ? button : null;
                    }

                    async Task<PublishedModelMesh> ImportChoice(string sourcePath)
                    {
                        await Import(sourcePath);
                        Check(documents.Read().Error == null, $"Could not import {sourcePath}: {documents.Read().Error}");
                        var selected = documents.Read().ModelCatalog!.Single(item => item.SourcePath == sourcePath);
                        await ClickCatalog(popup => popup.ModelCatalogRows.Children.OfType<HStack>()
                            .SelectMany(row => row.Children.OfType<CheckBox>()).SingleOrDefault(check => check.Value.EndsWith(sourcePath, StringComparison.Ordinal) && !check.IsChecked));
                        await documents.Pending.WaitAsync(token);
                        await CloseCatalog();
                        await using var imported = ledgers.Create(new(selected.SourceId, Path.Combine(ledgerFolder, $"{selected.SourceId:N}.tmledger")))!;
                        return (await imported.OpenAsync(token))!.Meshes.Single();
                    }

                    async Task<MaterialTransferFrame> BuildTransferFrame(PublishedModelMesh target, string capture)
                    {
                        before = document.Read();
                        content = before.Content!;
                        await StartBuild();
                        await Complete();
                        var artifact = engine.Editor.Status.Candidate!.Build.Artifact;
                        var manifest = artifact.RenderContent!;
                        var derived = manifest.Entries.Single(entry => entry.Definition is MaterialBindingSetDefinition binding &&
                            binding.Mesh.AssetId == target.AssetId && entry.Key.AssetId != target.BindingSetId);
                        var exact = (MaterialBindingSetDefinition)derived.Definition;
                        Check(manifest.Resolve(exact.Mesh).Key == exact.Mesh &&
                            exact.Bindings.Select(slot => slot.SlotId).SequenceEqual(target.Slots.Select(slot => slot.SlotId)) &&
                            exact.Bindings.Select(slot => slot.Material.AssetId).SequenceEqual([white, red]) &&
                            exact.Bindings.All(slot => manifest.Resolve(slot.Material).Key == slot.Material),
                            "Build did not pin the selected mesh, its slot IDs and both materials.");
                        Check(playback.RunSelectedBuild(artifact), "Editor declined playing the transferred binding.");
                        await Complete();
                        var frame = await CapturePlaying(capture, artifact, 0);
                        await StopAndCheck(frame.Generation, null);
                        Check(frame.Mask.GreenPixels == 0 && frame.Mask.RedPixels > 100 &&
                            frame.Pixels?.Slots is { WhitePixels: > 100 } pixels && pixels.RedRightPixels == frame.Mask.RedPixels,
                            "Vulkan did not show white on the left and red on the right.");
                        return new(AssetProof.From(exact.Mesh), AssetProof.From(derived.Key),
                            exact.Bindings.Select(slot => new ImportedSlotProof(slot.SlotId, AssetProof.From(slot.Material))).ToArray(), frame);
                    }
                }

                async Task VerifyOverrideInInspector()
                {
                    var inspector = view.Inspector!;
                    var unedited = document.Read().Content!;
                    var component = unedited.Resolved.Entities[new(entityId)].Components.Values.Single(
                        value => value.TypeId == MeshRenderingComponents.MeshRendererId).Id;
                    await EditorHierarchySmoke.ClickItemAsync(ui, view, entityId.ToString("D"), token);
                    if (saveInitialImport)
                    {
                        await SeeSlotMaterials(mesh.Slots[0].MaterialId, originalRight);
                        await ApplySlot("left", leftMaterial, leftMaterial, originalRight);
                        if (persistMultipleOverrides)
                        {
                            await SeeSlotMaterials(leftMaterial, originalRight);
                            var leftOnly = document.Read().Content!;
                            await ApplySlot("right", changedRight, leftMaterial, changedRight);
                            var combined = document.Read().Content!;
                            Check(combined.UndoCount == unedited.UndoCount + 2, "Expected two independent Apply transactions.");
                            await SeeSlotMaterials(leftMaterial, changedRight);
                            rightMaterial = changedRight;
                            applied = await BuildAndCapture(published, "both-applied");

                            await EditorHeaderSmoke.ClickActionAsync(ui, view, "edit", "edit.undo", token);
                            Check(document.Read().Content!.ContentRevision == leftOnly.ContentRevision &&
                                document.Read().Content!.RedoCount == 1, "Undo did not restore only the second Apply.");
                            rightMaterial = originalRight;
                            await SeeSlotMaterials(leftMaterial, originalRight);
                            undone = await BuildAndCapture(published, "second-undone");
                            var both = applied.Frame.Pixels!.Slots!;
                            var afterUndo = undone.Frame.Pixels!.Slots!;
                            Check(applied.Binding != undone.Binding && applied.Mesh == undone.Mesh &&
                                applied.Slots[0] == undone.Slots[0] && applied.Slots[1].Id == undone.Slots[1].Id &&
                                applied.Slots[1].Material != undone.Slots[1].Material &&
                                both.LeftRgba == afterUndo.LeftRgba && both.RightRgba != afterUndo.RightRgba,
                                "Undo changed the first material or its GPU pixels.");

                            if (!saveAfterSlotUndo)
                            {
                                await EditorHeaderSmoke.ClickActionAsync(ui, view, "edit", "edit.redo", token);
                                Check(document.Read().Content!.ContentRevision == combined.ContentRevision,
                                    "Redo did not restore the combined override.");
                                rightMaterial = changedRight;
                            }
                        }
                    }
                    await SeeSlotMaterials(leftMaterial, finalRight);

                    async Task ApplySlot(string name, Guid material, Guid expectedLeft, Guid expectedRight)
                    {
                        var previous = document.Read().Content!;
                        string? option = null;
                        await ui.InvokeAsync(() => option = inspector.DataContext!.RenderAssets!.Materials.Single(item => item.Id == material).Name)
                            .WaitAsync(token);
                        await ClickInspector(() => SlotButton(name));
                        await ClickInspector(() => SlotButton(name + " · " + option));
                        await EditorHeaderSmoke.UntilAsync(ui, () => inspector.DataContext is { CanApply: true, RenderAssets: { } draft } &&
                            draft.Slots.Count == 2 && draft.Slots[0].Material == expectedLeft && draft.Slots[1].Material == expectedRight, token);
                        Check(ReferenceEquals(document.Read().Content, previous), "Slot selection leaked from the draft before Apply.");
                        await ClickInspector(() => inspector.Apply);
                        Check(document.Read().Content!.UndoCount == previous.UndoCount + 1, "Apply did not create exactly one history entry.");
                    }

                    Task SeeSlotMaterials(Guid expectedLeft, Guid expectedRight) => EditorHeaderSmoke.UntilAsync(ui, () =>
                        inspector.DataContext is { HasDraft: false, RenderAssets: { Enabled: true } selection } &&
                        selection.Component == component && selection.Mesh == mesh.AssetId && selection.Slots.Count == 2 &&
                        Shows(selection, 0, "left", expectedLeft) && Shows(selection, 1, "right", expectedRight), token);

                    bool Shows(InspectorRenderAssetSelection selection, int index, string name, Guid material) =>
                        selection.Slots[index].Id == mesh.Slots[index].SlotId && selection.Slots[index].Material == material && selection.Slots[index].CanSelect &&
                        SlotButton(name)?.Value == name + " · " + selection.Materials.Single(item => item.Id == material).Name;

                    Button? SlotButton(string name) => inspector.Properties.TryGetValue((component, "bindings"), out var field)
                        ? Descendants(field.Row).OfType<Button>().SingleOrDefault(button => button.AccessibleName == "Материал слота " + name) : null;
                }

                async Task EnterPath(string path)
                {
                    await EditorHeaderSmoke.UntilAsync(ui, () => model.CanImportModel, token);
                    await EditorHeaderSmoke.ClickActionAsync(ui, view, "project", "document.ImportModel", token, confirmPath: false);
                    await EditorHeaderSmoke.UntilAsync(ui, () => tools.IsVisible && tools.Actions.IsVisible && !ui.NeedsUpdate, token);
                    await ui.InvokeAsync(() =>
                    {
                        Check(model.PathRequest == DocumentCommand.ImportModel, "Wrong model import prompt.");
                        ui.Focus(tools.PathInput);
                        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
                        ui.HandleInput(new(InputKind.Text, Text: path));
                        Check(model.RequestedPath == path, "Model source path did not reach the prompt.");
                    }).WaitAsync(token);
                }

                async Task Import(string path)
                {
                    var pending = documents.Pending;
                    await EnterPath(path);
                    await EditorHeaderSmoke.ConfirmDocumentPathAsync(ui, view, token);
                    Check(!ReferenceEquals(pending, documents.Pending), "Editor declined importing the source.");
                    await documents.Pending.WaitAsync(token);
                }

                async Task ReadCatalog()
                {
                    await ClickCatalog(popup => popup.ReadModelCatalog);
                    await documents.Pending.WaitAsync(token);
                    Check(documents.Read().Error == null, $"Cannot read the project catalog: {documents.Read().Error}");
                }

                Task ClickCatalog(Func<EditorStatusPopupView, Button?> find) => EditorHeaderSmoke.UntilAsync(ui, () =>
                {
                    if (ui.NeedsUpdate || view.Overlays.Popup is not EditorStatusPopupView popup ||
                        find(popup) is not { IsEnabled: true, Bounds.Width: > 0 } button) return false;
                    EditorHeaderSmoke.Move(ui, button);
                    EditorHeaderSmoke.Click(ui, button);
                    return true;
                }, token, HoldCatalog);

                void HoldCatalog()
                {
                    if (view.Overlays.Popup is EditorStatusPopupView || view.StatusBar?.SummaryButton is not { IsVisible: true, Bounds.Width: > 0 } button) return;
                    EditorHeaderSmoke.Move(ui, button);
                    EditorHeaderSmoke.Click(ui, button);
                }

                Task CloseCatalog() => ui.InvokeAsync(() => view.Overlays.Close(false)).WaitAsync(token);
            }

            async Task VerifyRenderInspector(SceneDocumentSession document, IFactory<ModelImportLedgerSession, ModelImportLedgerArguments> ledgers)
            {
                var inspector = view.Inspector!;
                var initialHash = sceneHash;
                var checkpoint = Path.Combine(report, "Content", ".aether-model-imports", content.Document.Id.Value.ToString("N") + ".tmimports");
                if (reopenInspector)
                {
                    var prior = JsonSerializer.Deserialize<InspectorRestartProof>(
                        await File.ReadAllBytesAsync(Path.Combine(report, "editor-saved", "inspector-restart.json"), token))
                        ?? throw new InvalidDataException("Missing saved inspector proof.");
                    var reopenedTarget = new EntityId(prior.Entity);
                    var reopenedRenderer = Renderer(reopenedTarget);
                    Check(Environment.ProcessId != prior.EditorPid && !before.IsDirty && before.OpeningId != prior.OpeningId &&
                        content.Document.Id.Value == prior.SceneId && content.Document.Revision == prior.SceneRevision &&
                        initialHash == prior.SceneSha256 && Hash(await File.ReadAllBytesAsync(checkpoint, token)) == prior.SelectionSha256 &&
                        Id(reopenedRenderer, "bindings") == prior.Binding.AssetId && Id(reopenedRenderer, "mesh") == prior.Mesh.AssetId,
                        "A new Editor did not restore the saved scene and binding ID.");
                    await VerifyInspectorChoice(reopenedTarget, reopenedRenderer.Id, prior.Mesh.AssetId, prior.Material.AssetId);
                    var reopenedFrame = await BuildAndShow("inspector-reopened");
                    var reopenedProof = await CreateRestartProof(reopenedTarget, prior.Mesh.AssetId, prior.Material.AssetId, prior.Binding.AssetId, reopenedFrame);
                    Check(reopenedProof.Mesh == prior.Mesh && reopenedProof.Material == prior.Material && reopenedProof.Binding == prior.Binding &&
                        reopenedProof.Slot == prior.Slot && reopenedProof.Imports.SequenceEqual(prior.Imports) &&
                        reopenedProof.SelectionSha256 == prior.SelectionSha256 && reopenedProof.Frame.Mask == prior.Frame.Mask &&
                        reopenedProof.Frame.Pixels != null && reopenedProof.Frame.Pixels == prior.Frame.Pixels,
                        "The rebuilt scene did not reproduce the exact resource keys, import generations and GPU pixels.");
                    await WriteRestartProof(reopenedProof);
                    return;
                }

                var red = await ReadImport("ce662048-9dcb-4c60-aaf0-213342054fa6", "red-baseline.tmledger");
                if (materialSlots)
                {
                    await VerifyMaterialSlots(red);
                    return;
                }
                var green = await ReadImport("65710c6d-6781-4476-9bb8-735fc518f008", "green.tmledger");
                var redMesh = red.Meshes.Single();
                var greenMesh = green.Meshes.Single();
                var target = new EntityId(red.Instances[0].Id);
                var sibling = new EntityId(red.Instances[1].Id);
                var renderer = Renderer(target);
                var opening = before.OpeningId;
                if (referenceDiagnostics)
                {
                    await VerifyReferenceDiagnostics();
                    return;
                }
                var saved = content;
                await document.SelectModelImportsAsync(opening,
                    [new("Red", Watch(red, "source")), new("Green", Watch(green, "source-green"))],
                    new(oldBuild.RenderContent!.ProjectId, oldBuild.RenderContent.Entries), token);

                var baseline = await BuildAndShow("inspector-before");
                await EditorHierarchySmoke.ClickItemAsync(ui, view, target.Value.ToString("D"), token);
                await EditorHeaderSmoke.UntilAsync(ui, () => inspector.DataContext!.RenderAssets is { Enabled: true } selection &&
                    selection.Mesh == redMesh.AssetId && selection.Material == redMesh.MaterialId, token);
                await Choose("mesh", "Меш", greenMesh.AssetId);
                Check(document.Read().Content == saved, "Choosing a mesh changed the committed scene before Apply.");
                await EditorHeaderSmoke.UntilAsync(ui, () => inspector.DataContext!.RenderAssets is { } selection &&
                    selection.Mesh == greenMesh.AssetId && selection.Material == greenMesh.MaterialId, token);
                await Choose("bindings", "Материал", redMesh.MaterialId);
                await EditorHeaderSmoke.UntilAsync(ui, () => inspector.DataContext!.CanApply &&
                    inspector.DataContext.RenderAssets?.Material == redMesh.MaterialId, token);
                await ClickInspector(() => inspector.Apply);
                var edit = document.Read().Content!;
                var rendererEdit = edit.Resolved.Entities[target].Components[renderer.Id];
                var binding = Id(rendererEdit, "bindings");
                Check(edit.UndoCount == saved.UndoCount + 1 && Id(rendererEdit, "mesh") == greenMesh.AssetId &&
                    binding != greenMesh.BindingSetId && Id(edit.Resolved.Entities[sibling].Components[renderer.Id], "mesh") == redMesh.AssetId &&
                    Id(edit.Resolved.Entities[sibling].Components[renderer.Id], "bindings") == redMesh.BindingSetId,
                    "Apply did not commit both choices once or modified the sibling sharing the definition.");
                if (saveInspector)
                {
                    await EditorHeaderSmoke.ClickActionAsync(ui, view, "file", "document.Save", token);
                    await documents.Pending.WaitAsync(token);
                    var savedState = document.Read();
                    sceneHash = Hash(await File.ReadAllBytesAsync(scenePath, token));
                    Check(documents.Read().Error == null && !savedState.IsDirty && ReferenceEquals(savedState.Content, edit) &&
                        sceneHash != initialHash, $"Editor did not save the edited renderer references: {documents.Read().Error}");
                    await VerifyInspectorChoice(target, renderer.Id, greenMesh.AssetId, redMesh.MaterialId);
                    var savedFrame = await BuildAndShow("inspector-saved");
                    Check(savedFrame.Frame.Mask.Sha256 != baseline.Frame.Mask.Sha256 && savedFrame.Frame.Pixels != null,
                        "The saved renderer edit was not visible in the GPU frame.");
                    await WriteRestartProof(await CreateRestartProof(target, greenMesh.AssetId, redMesh.MaterialId, binding, savedFrame));
                    return;
                }
                var changed = await BuildAndShow("inspector-after");
                Check(changed.Artifact.RenderContent?.Resolve(binding, RenderContentProfile.BindingSetKindId).Definition is MaterialBindingSetDefinition set &&
                    set.Mesh.AssetId == greenMesh.AssetId && set.Bindings.Single().Material.AssetId == redMesh.MaterialId,
                    "Build did not publish the requested independent material binding.");
                if (resetMaterial)
                {
                    var original = ProveMaterial(changed, binding, redMesh.MaterialId);
                    var resetButton = inspector.Properties[(renderer.Id, "bindings")].Reset;
                    await StageRestore();
                    await ClickInspector(() => inspector.Cancel);
                    await EditorHeaderSmoke.UntilAsync(ui, () => inspector.DataContext is { HasDraft: false, RenderAssets.Material: { } material }
                        && material == redMesh.MaterialId, token);
                    Check(ReferenceEquals(document.Read().Content, edit), "Cancel changed the committed material override.");

                    await StageRestore();
                    await ClickInspector(() => inspector.Apply);
                    var reset = document.Read().Content!;
                    Check(reset.UndoCount == edit.UndoCount + 1, "Restoring the material created more than one history entry.");
                    var resetFrame = ProveMaterial(await BuildAndShow("inspector-material-reset"), greenMesh.BindingSetId, greenMesh.MaterialId);
                    await VerifyInspectorChoice(target, renderer.Id, greenMesh.AssetId, greenMesh.MaterialId);
                    await ui.InvokeAsync(() => Check(!resetButton.IsEnabled, "Restore was available with the default material already selected.")).WaitAsync(token);

                    await EditorHeaderSmoke.ClickActionAsync(ui, view, "edit", "edit.undo", token);
                    Check(document.Read().Content!.ContentRevision == edit.ContentRevision, "Undo did not restore the override revision.");
                    var undoFrame = ProveMaterial(await BuildAndShow("inspector-material-undo"), binding, redMesh.MaterialId);
                    await EditorHeaderSmoke.ClickActionAsync(ui, view, "edit", "edit.redo", token);
                    Check(document.Read().Content!.ContentRevision == reset.ContentRevision, "Redo did not restore the reset revision.");
                    var redoFrame = ProveMaterial(await BuildAndShow("inspector-material-redo"), greenMesh.BindingSetId, greenMesh.MaterialId);

                    var steps = new[] { original, resetFrame, undoFrame, redoFrame };
                    var originalPixels = original.Frame.Pixels;
                    Check(steps.All(step => step.Mesh == original.Mesh && step.Slot == original.Slot &&
                            step.Frame.Pixels != null && step.Frame.Pixels.BlueSha256 == originalPixels?.BlueSha256) &&
                        originalPixels?.BlueSha256 != null && original.Frame.Mask.RedPixels - resetFrame.Frame.Mask.RedPixels > 100 &&
                        resetFrame.Frame.Mask.GreenPixels - original.Frame.Mask.GreenPixels > 100 &&
                        originalPixels != resetFrame.Frame.Pixels && originalPixels == undoFrame.Frame.Pixels &&
                        resetFrame.Frame.Pixels == redoFrame.Frame.Pixels && original.Material == undoFrame.Material &&
                        resetFrame.Material == redoFrame.Material && original.Binding == undoFrame.Binding && resetFrame.Binding == redoFrame.Binding &&
                        engine.Gpu.ResourceCount == 0 && engine.Gpu.OpenHandleCount == 0,
                        "Material reset changed geometry, failed to recolor the target, or did not round-trip through Undo and Redo.");
                    await File.WriteAllTextAsync(Path.Combine(report, "editor-render-material-reset.json"), JsonSerializer.Serialize(new
                    {
                        Completed = true, Captures = captures, States = steps, Entity = target.Value, Sibling = sibling.Value
                    }), token);
                    return;

                    async Task StageRestore()
                    {
                        await EditorHeaderSmoke.UntilAsync(ui, () => resetButton.IsEnabled && resetButton.Value == "Вернуть материал меша", token);
                        await ClickInspector(() => resetButton);
                        await EditorHeaderSmoke.UntilAsync(ui, () => inspector.DataContext is { CanApply: true, RenderAssets: { } draft } &&
                            draft.Mesh == greenMesh.AssetId && draft.Material == greenMesh.MaterialId, token);
                        Check(ReferenceEquals(document.Read().Content, edit), "Restore changed the committed scene before Apply.");
                    }

                    InspectorMaterialProof ProveMaterial((FrameProof Frame, GameBuildArtifact Artifact) run, Guid bindingId, Guid material)
                    {
                        var manifest = run.Artifact.RenderContent!;
                        var entry = manifest.Resolve(bindingId, RenderContentProfile.BindingSetKindId);
                        var bindingSet = (MaterialBindingSetDefinition)entry.Definition;
                        var slot = bindingSet.Bindings.Single();
                        var primary = Renderer(target);
                        var other = Renderer(sibling);
                        Check(Id(primary, "mesh") == greenMesh.AssetId && Id(primary, "bindings") == bindingId &&
                            Id(other, "mesh") == redMesh.AssetId && Id(other, "bindings") == redMesh.BindingSetId &&
                            bindingSet.Mesh.AssetId == greenMesh.AssetId && slot.Material.AssetId == material &&
                            manifest.Resolve(bindingSet.Mesh).Key == bindingSet.Mesh && manifest.Resolve(slot.Material).Key == slot.Material,
                            "The scene or captured manifest changed the selected mesh or the sibling's renderer.");
                        return new(run.Frame, AssetProof.From(bindingSet.Mesh), AssetProof.From(slot.Material), AssetProof.From(entry.Key), slot.SlotId);
                    }
                }
                await EditorHeaderSmoke.UntilAsync(ui, () => inspector.DataContext!.CanUndo, token);
                await EditorHeaderSmoke.ClickActionAsync(ui, view, "edit", "edit.undo", token);
                var undone = document.Read();
                Check(!undone.IsDirty && undone.Content?.ContentRevision == saved.ContentRevision &&
                    Id(undone.Content.Resolved.Entities[target].Components[renderer.Id], "mesh") == redMesh.AssetId &&
                    Id(undone.Content.Resolved.Entities[target].Components[renderer.Id], "bindings") == redMesh.BindingSetId,
                    "One Undo did not restore both references.");
                var restored = await BuildAndShow("inspector-undo");
                Check(baseline.Frame.Mask == restored.Frame.Mask && baseline.Frame.Mask.Sha256 != changed.Frame.Mask.Sha256 &&
                    baseline.Frame.Mask.GreenSha256 == changed.Frame.Mask.GreenSha256 && changed.Frame.Mask.RedPixels > 200 &&
                    engine.Gpu.ResourceCount == 0 && engine.Gpu.OpenHandleCount == 0,
                    "GPU pixels did not show the mesh/material edit and exact Undo restoration.");
                await File.WriteAllTextAsync(Path.Combine(report, "editor-render-inspector.json"), JsonSerializer.Serialize(new
                {
                    Completed = true, Captures = captures, Entity = target.Value, Mesh = greenMesh.AssetId,
                    Material = redMesh.MaterialId, Binding = binding
                }), token);

                async Task VerifyReferenceDiagnostics()
                {
                    var factory = app.Services.Resolve<IFactory<ProjectModelCatalog, ResolvedProject>>()!;
                    var catalog = factory.Create(documents.Read().Project!.Project)!;
                    var redId = red.Candidate.SourceId;
                    var greenId = green.Candidate.SourceId;
                    await catalog.RegisterAsync("Red", Watch(red, "source").Source,
                        new(redId, Path.Combine(report, "ledger", "red-baseline.tmledger")), token);
                    await catalog.RegisterAsync("Green", Watch(green, "source-green").Source,
                        new(greenId, Path.Combine(report, "ledger", "green.tmledger")), token);
                    await SetSelected(redId, true);
                    await SetSelected(greenId, true);
                    await VerifyInspectorChoice(target, renderer.Id, redMesh.AssetId, redMesh.MaterialId);
                    await Choose("bindings", "Материал", greenMesh.MaterialId);
                    await EditorHeaderSmoke.UntilAsync(ui, () => inspector.DataContext is { CanApply: true, RenderAssets.Material: { } material } &&
                        material == greenMesh.MaterialId, token);
                    await ClickInspector(() => inspector.Apply);
                    var committed = document.Read().Content!;
                    var binding = Id(committed.Resolved.Entities[target].Components[renderer.Id], "bindings");
                    Check(binding != redMesh.BindingSetId, "The scene did not acquire a material override.");
                    var initial = await BuildAndShow("inspector-diagnostics-before");
                    var previous = engine.Editor.Status;

                    await SetSelected(redId, false);
                    var missingMesh = await ExpectBlocked($"Меш {redMesh.AssetId:D} отсутствует");
                    await SetSelected(redId, true);
                    await SetSelected(greenId, false);
                    var missingBinding = await ExpectBlocked($"Привязка материала {binding:D} для меша {redMesh.AssetId:D} отсутствует");
                    await SetSelected(greenId, true);
                    await VerifyInspectorChoice(target, renderer.Id, redMesh.AssetId, greenMesh.MaterialId);
                    await EditorHeaderSmoke.UntilAsync(ui, () => !inspector.DataContext!.HasDiagnostics, token);
                    var restored = await BuildAndShow("inspector-diagnostics-recovered");
                    var expectedBinding = initial.Artifact.RenderContent!.Resolve(binding, RenderContentProfile.BindingSetKindId);
                    Check(ReferenceEquals(document.Read().Content, committed) &&
                        restored.Artifact.RenderContent!.Resolve(expectedBinding.Key).Key == expectedBinding.Key &&
                        initial.Frame.Mask == restored.Frame.Mask && initial.Frame.Pixels != null && initial.Frame.Pixels == restored.Frame.Pixels &&
                        engine.Gpu.ResourceCount == 0 && engine.Gpu.OpenHandleCount == 0,
                        "Selecting the missing imports did not recover the same binding and GPU pixels.");
                    await File.WriteAllTextAsync(Path.Combine(report, "editor-render-reference-diagnostics.json"), JsonSerializer.Serialize(new
                    {
                        Completed = true, Mesh = redMesh.AssetId, Material = greenMesh.MaterialId, Binding = binding,
                        MissingMesh = missingMesh, MissingBinding = missingBinding, Captures = captures
                    }), token);

                    Task SetSelected(Guid source, bool selected) => document.SetCatalogModelSelectedAsync(opening, source, selected, token);

                    async Task<string> ExpectBlocked(string diagnostic)
                    {
                        await EditorHeaderSmoke.UntilAsync(ui, () => inspector.DataContext!.DiagnosticsText.Contains(diagnostic, StringComparison.Ordinal) &&
                            inspector.Error.IsVisible && inspector.Error.Text.Contains(diagnostic, StringComparison.Ordinal) &&
                            view.DataContext!.CanBuild, token);
                        await EditorHeaderSmoke.ClickActionAsync(ui, view, "project", "runtime.Build", token);
                        await playback.Pending.WaitAsync(token);
                        var current = engine.Editor.Status;
                        var buildError = playback.Error?.Message;
                        Check(buildError?.Contains(diagnostic, StringComparison.Ordinal) == true &&
                            ReferenceEquals(document.Read().Content, committed) && current.ContextVersion == previous.ContextVersion &&
                            ReferenceEquals(current.Candidate, previous.Candidate),
                            $"Build did not reject the reference before changing the candidate. {buildError}");
                        return buildError!;
                    }
                }

                async Task VerifyMaterialSlots(PublishedModelImport model)
                {
                    var mesh = model.Meshes.Single();
                    var left = mesh.Slots[0];
                    var right = mesh.Slots[1];
                    var white = model.Bindings.Single(item => item.Key.Locator == "builtin/material/default").Id;
                    var target = new EntityId(model.Instances.Single().Id);
                    var renderer = Renderer(target);
                    var saved = content;
                    await document.SelectModelImportsAsync(before.OpeningId, [new("Two slots", Watch(model, "source"))],
                        new(oldBuild.RenderContent!.ProjectId, oldBuild.RenderContent.Entries), token);
                    var baseline = await BuildAndShow("slots-before");

                    await EditorHierarchySmoke.ClickItemAsync(ui, view, target.Value.ToString("D"), token);
                    await EditorHeaderSmoke.UntilAsync(ui, () => inspector.DataContext!.RenderAssets is { Enabled: true } selection &&
                        selection.Mesh == mesh.AssetId && selection.Slots.Count == 2 &&
                        selection.Slots[0] is { Name: "left", CanSelect: true } first && first.Id == left.SlotId && first.Material == left.MaterialId &&
                        selection.Slots[1] is { Name: "right", CanSelect: true } second && second.Id == right.SlotId && second.Material == right.MaterialId,
                        token);
                    string? option = null;
                    await ui.InvokeAsync(() => option = inspector.DataContext!.RenderAssets!.Materials.Single(item => item.Id == white).Name)
                        .WaitAsync(token);
                    await ClickInspector(() => FindSlotButton("Материал слота left"));
                    await ClickInspector(() => FindSlotButton("Материал слота left · " + option));
                    await EditorHeaderSmoke.UntilAsync(ui, () => inspector.DataContext is { CanApply: true, RenderAssets: { } draft } &&
                        draft.Slots[0].Material == white && draft.Slots[1].Material == right.MaterialId &&
                        FindSlotButton("Материал слота right") is { IsEnabled: true }, token);
                    Check(ReferenceEquals(document.Read().Content, saved), "Choosing the slot material committed it before Apply.");
                    await ClickInspector(() => inspector.Apply);
                    var edited = document.Read().Content!;
                    var variant = Id(edited.Resolved.Entities[target].Components[renderer.Id], "bindings");
                    Check(edited.UndoCount == saved.UndoCount + 1 && variant != mesh.BindingSetId, "Apply did not commit exactly one binding change.");
                    var changed = await BuildAndShow("slots-after");
                    var originalSet = ProveBinding(baseline.Artifact, mesh.BindingSetId, left.MaterialId);
                    var editedSet = ProveBinding(changed.Artifact, variant, white);
                    Check(originalSet.Mesh == editedSet.Mesh && originalSet.Bindings[1] == editedSet.Bindings[1] &&
                        baseline.Artifact.BuildId != changed.Artifact.BuildId && Id(Renderer(target), "mesh") == mesh.AssetId,
                        "The slot edit changed the mesh or the untouched material key.");
                    var beforePixels = baseline.Frame.Pixels?.Slots ?? throw new InvalidDataException("Missing baseline slot pixels.");
                    var afterPixels = changed.Frame.Pixels?.Slots ?? throw new InvalidDataException("Missing edited slot pixels.");
                    Check(baseline.Frame.Mask.RedPixels > 100 && changed.Frame.Mask.RedPixels == 0 &&
                        beforePixels.WhitePixels == 0 && afterPixels.WhitePixels == baseline.Frame.Mask.RedPixels &&
                        beforePixels.RedMask == afterPixels.WhiteMask && baseline.Frame.Mask.GreenPixels > 100 &&
                        baseline.Frame.Mask.GreenSha256 == changed.Frame.Mask.GreenSha256 && beforePixels.RightRgba == afterPixels.RightRgba &&
                        engine.Gpu.ResourceCount == 0 && engine.Gpu.OpenHandleCount == 0,
                        $"GPU slot comparison failed. Before: {beforePixels}. After: {afterPixels}.");
                    await File.WriteAllTextAsync(Path.Combine(report, "editor-render-material-slots.json"), JsonSerializer.Serialize(new
                    {
                        Completed = true, Captures = captures, Mesh = AssetProof.From(editedSet.Mesh), Binding = variant,
                        EditedSlot = left.SlotId, PreservedSlot = right.SlotId, PreservedMaterial = AssetProof.From(editedSet.Bindings[1].Material)
                    }), token);

                    Button? FindSlotButton(string name) => inspector.Properties.TryGetValue((renderer.Id, "bindings"), out var field)
                        ? Descendants(field.Row).OfType<Button>().SingleOrDefault(button => button.AccessibleName == name) : null;

                    MaterialBindingSetDefinition ProveBinding(GameBuildArtifact artifact, Guid id, Guid material)
                    {
                        var manifest = artifact.RenderContent!;
                        var entry = manifest.Resolve(id, RenderContentProfile.BindingSetKindId);
                        var set = (MaterialBindingSetDefinition)entry.Definition;
                        var geometry = (MeshDefinition)manifest.Resolve(set.Mesh).Definition;
                        Check(set.Mesh.AssetId == mesh.AssetId && set.Bindings.Select(slot => slot.SlotId).SequenceEqual([left.SlotId, right.SlotId]) &&
                            geometry.Slots.SequenceEqual(mesh.Geometry.Slots) && set.Bindings[0].Material.AssetId == material &&
                            set.Bindings[1].Material.AssetId == right.MaterialId && set.Bindings.All(slot => manifest.Resolve(slot.Material).Key == slot.Material),
                            "Editor Build did not capture both exact slots and their materials.");
                        return set;
                    }
                }

                async Task<(FrameProof Frame, GameBuildArtifact Artifact)> BuildAndShow(string name)
                {
                    before = document.Read();
                    content = before.Content!;
                    await EditorHeaderSmoke.UntilAsync(ui, () => view.DataContext!.CanBuild, token);
                    await EditorHeaderSmoke.ClickActionAsync(ui, view, "project", "runtime.Build", token);
                    await Complete();
                    var artifact = engine.Editor.Status.Candidate?.Build.Artifact ?? throw new InvalidOperationException("No inspector Build candidate.");
                    if (restartInspector)
                        Check(Path.GetDirectoryName(artifact.DirectoryPath) == compiler, "Build came from outside the current Editor's compiler directory.");
                    Check(playback.RunSelectedBuild(artifact), "Editor declined running the inspector Build.");
                    await Complete();
                    var frame = await CapturePlaying(name, artifact, 0);
                    Check(playback.Execute(EditorCommand.Stop), "Editor declined stopping the inspector Build.");
                    await Complete();
                    await CheckReleased(frame.Generation);
                    return (frame, artifact);
                }

                async Task VerifyInspectorChoice(EntityId entity, ComponentId component, Guid mesh, Guid material)
                {
                    var entityId = entity.Value.ToString("D");
                    await EditorHeaderSmoke.UntilAsync(ui, () => view.Hierarchy!.Tree.Items.Any(item => item.Id == entityId) &&
                        !ui.NeedsUpdate, token);
                    await EditorHierarchySmoke.ClickItemAsync(ui, view, entityId, token);
                    await EditorHeaderSmoke.UntilAsync(ui, () => inspector.DataContext is { HasDraft: false, RenderAssets: { Enabled: true } selection } &&
                        selection.Component == component && selection.Mesh == mesh && selection.Material == material &&
                        Shows("mesh", selection.Meshes.Single(choice => choice.Id == mesh).Name) &&
                        Shows("bindings", selection.Materials.Single(choice => choice.Id == material).Name), token);

                    bool Shows(string key, string name) => inspector.Properties.TryGetValue((component, key), out var field) &&
                        field.Input is Button button && button.Value == name;
                }

                async Task<InspectorRestartProof> CreateRestartProof(EntityId entity, Guid mesh, Guid material, Guid bindingId,
                    (FrameProof Frame, GameBuildArtifact Artifact) run)
                {
                    var manifest = run.Artifact.RenderContent ?? throw new InvalidDataException("Build did not contain render resources.");
                    var bindingEntry = manifest.Resolve(bindingId, RenderContentProfile.BindingSetKindId);
                    var bindings = (MaterialBindingSetDefinition)bindingEntry.Definition;
                    var slot = bindings.Bindings.Single();
                    Check(bindings.Mesh.AssetId == mesh && slot.Material.AssetId == material &&
                        manifest.Resolve(bindings.Mesh).Key == bindings.Mesh && manifest.Resolve(slot.Material).Key == slot.Material,
                        "Build did not pin the selected mesh and material.");
                    var imports = document.ReadModelImports(before.OpeningId).OrderBy(item => item.SourceId)
                        .Select(item => new ImportGeneration(item.SourceId, item.Generation)).ToArray();
                    Check(imports.Length == 2 && engine.Gpu.ResourceCount == 0 && engine.Gpu.OpenHandleCount == 0,
                        "Inspector Build lost a selected import or retained GPU resources.");
                    return new(Environment.ProcessId, before.OpeningId, content.Document.Id.Value, content.Document.Revision, sceneHash,
                        Hash(await File.ReadAllBytesAsync(checkpoint, token)), entity.Value, AssetProof.From(bindings.Mesh),
                        AssetProof.From(slot.Material), AssetProof.From(bindingEntry.Key), slot.SlotId, imports, run.Frame);
                }

                Task WriteRestartProof(InspectorRestartProof proof) => File.WriteAllTextAsync(
                    Path.Combine(proofDirectory, "inspector-restart.json"), JsonSerializer.Serialize(proof), token);

                async Task Choose(string key, string label, Guid assetId)
                {
                    string? option = null;
                    await ui.InvokeAsync(() =>
                    {
                        var selection = inspector.DataContext!.RenderAssets!;
                        option = label + " · " + (key == "mesh" ? selection.Meshes : selection.Materials).Single(item => item.Id == assetId).Name;
                    }).WaitAsync(token);
                    await ClickInspector(() => inspector.Properties[(renderer.Id, key)].Input);
                    await ClickInspector(() => Descendants(inspector.Properties[(renderer.Id, key)].Row)
                        .OfType<Button>().SingleOrDefault(button => button.AccessibleName == option));
                }

                static Guid Id(ResolvedComponent component, string key) => Guid.Parse(component.Properties[key].Value.GetString());
                ResolvedComponent Renderer(EntityId entity) => content.Resolved.Entities[entity].Components.Values.Single(
                    component => component.TypeId == MeshRenderingComponents.MeshRendererId);
                ModelSourceWatchArguments Watch(PublishedModelImport import, string folder) =>
                    new(new(import.Candidate.SourceId, Path.Combine(report, folder), "scene.gltf"), import);
                async Task<PublishedModelImport> ReadImport(string source, string filename)
                {
                    await using var ledger = ledgers.Create(new(Guid.Parse(source), Path.Combine(report, "ledger", filename)))!;
                    return await ledger.OpenAsync(token) ?? throw new InvalidOperationException("Inspector fixture ledger is missing.");
                }
            }

            Task ClickInspector(Func<Element?> find) => EditorHeaderSmoke.UntilAsync(ui, () =>
            {
                if (find() is not { IsEffectivelyEnabled: true, Bounds.Width: > 0, Bounds.Height: > 0 } element) return false;
                var scroll = view.Inspector!.Scroll;
                scroll.BringIntoView(element.Bounds);
                if (ui.NeedsUpdate || !scroll.ChildClipBounds.Contains(element.Bounds.X + element.Bounds.Width / 2,
                    element.Bounds.Y + element.Bounds.Height / 2)) return false;
                EditorHeaderSmoke.Move(ui, element);
                EditorHeaderSmoke.Click(ui, element);
                return true;
            }, token);

            static IEnumerable<Element> Descendants(Element element) =>
                element.Children.Concat(element.Children.SelectMany(Descendants));

            async Task<FrameProof> CapturePlaying(string name, GameBuildArtifact artifact, ulong minimumTick, uint? textureTopLeft = null)
            {
                await engine.Supervisor.GetStatusAsync(token);
                var status = engine.Supervisor.Status;
                var identity = status.Identity ?? throw new InvalidOperationException("No selected runtime identity.");
                Check(status.State == SessionState.Playing && identity.GameBuildId == artifact.BuildId &&
                    status.Tick >= minimumTick && engine.Supervisor.RequiredRenderViewVersion == 1,
                    $"Expected {artifact.BuildId} Playing at tick >= {minimumTick}, observed {identity.GameBuildId} {status.State} at tick {status.Tick}.");
                var manifest = artifact.RenderContent!;
                await EditorHeaderSmoke.UntilAsync(ui, () =>
                    view.Header!.Caption.AccessibleName.Contains(artifact.BuildId, StringComparison.Ordinal) &&
                    view.Header.Caption.Value.Contains(artifact.BuildId[..12], StringComparison.Ordinal), token);
                var frame = await Capture(name, identity, content.Document.Id.Value, content.Document.Revision, manifest.Digest, textureTopLeft);
                Check(frame.Tick >= minimumTick, "Presented frame predates the checkpoint.");
                var after = documents.Read().Document!;
                Check(ReferenceEquals(content, after.Content) && before.IsDirty == after.IsDirty &&
                    sceneHash == Hash(File.ReadAllBytes(scenePath)), "Replace changed the open authoring document.");
                var proof = new FrameProof(name, artifact.BuildId, identity.Generation, status.ProcessId!.Value,
                    manifest.Digest.ToString(), frame.Tick, frame.Mask) { Pixels = frame.Pixels };
                captures.Add(proof);
                return proof;
            }

            async Task CheckReleased(ulong generation, RenderAssetKey? texture = null)
            {
                var lease = native.Leases.Single(lease => lease.Identity.Generation == generation);
                Check(lease.Reaped && lease.ResourcesReleased, "Retired child or handles survived replacement.");
                using var child = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(proofDirectory, $"runtime-{generation}.json"), token));
                var stats = child.RootElement;
                var validation = stats.GetProperty("Validation");
                Check(stats.GetProperty("Pid").GetInt32() == lease.ProcessId && stats.GetProperty("Resources").GetInt32() == 0 &&
                    stats.GetProperty("Handles").GetInt32() == 0 && validation.GetProperty("ValidationEnabled").GetBoolean() &&
                    validation.GetProperty("Errors").GetInt32() == 0 && validation.GetProperty("Warnings").GetInt32() == 0,
                    "Incorrect child validation or resource cleanup.");
                if (texture is { } key)
                {
                    var resources = validation.GetProperty("MeshResources").EnumerateArray()
                        .Where(owner => owner.GetProperty("TextureUploads").GetInt32() != 0).Single();
                    Check(resources.GetProperty("TextureUploads").GetInt32() == 1 &&
                        resources.GetProperty("LastTextureRevision").GetString() == key.Revision.ToString() &&
                        resources.GetProperty("LiveUploads").GetInt32() == 0 &&
                        resources.GetProperty("TextureImagesCreated").GetInt32() == 1 &&
                        resources.GetProperty("TextureImagesReleased").GetInt32() == 1 &&
                        resources.GetProperty("TextureSamplersCreated").GetInt32() == 1 &&
                        resources.GetProperty("TextureSamplersReleased").GetInt32() == 1,
                        "Runtime used the wrong texture revision or retained native image/sampler ownership.");
                }
            }
        }
        finally
        {
            window.FramePresented -= OnFirstFrame;
            try { await documents.StopAsync(); }
            finally
            {
                try { if (modelStore != null) await modelStore.DisposeAsync(); }
                finally { await app.StopAsync(); }
            }
        }

        await File.WriteAllTextAsync(Path.Combine(report, "editor-model.json"), JsonSerializer.Serialize(new
        {
            Completed = true, Provider = args[0], SceneSha256 = sceneHash, Captures = captures,
            CancelledGeneration = cancelledCandidate!.Generation, CorruptRejectedBeforeHandoff = true, RollbackSucceeded = true
        }, new JsonSerializerOptions { WriteIndented = true }), token);

        async Task<(PixelMask Mask, ulong Tick, ViewportPixelProof? Pixels)> Capture(string name, SessionIdentity identity, Guid sceneId,
            long revision, RenderContentHash digest, uint? textureTopLeft = null)
        {
            var result = new TaskCompletionSource<(PixelMask Mask, ulong Tick, ViewportPixelProof? Pixels)>(TaskCreationOptions.RunContinuationsAsynchronously);
            void Presented(UiSession ui)
            {
                if (result.Task.IsCompleted) return;
                try
                {
                    var view = EditorMainWindow.FromRoot(ui.Root).EditorView;
                    var canvas = view.Canvas;
                    if (!canvas.CanAcknowledgeDraw || canvas.DrawnFrame is not { } frame ||
                        frame.Presentation.Surface.Identity != identity ||
                        !ReferenceEquals(frame.Presentation, engine.Frames.Presentation)) return;
                    var stamp = frame.Metadata.RenderView ?? throw new InvalidDataException("Missing mesh viewport stamp.");
                    Check(frame.Presentation.Surface.Transport == ViewportTransportKind.Gpu && stamp.SceneId == sceneId &&
                        stamp.SourceRevision == revision && RenderContentHash.Parse(stamp.ManifestHash) == digest,
                        "GPU draw was not from the captured scene and selected build.");
                    using var image = ((SkiaOpenGLSurface)ui.Rendering).ReadbackImage();
                    using var png = image.Encode(SKEncodedImageFormat.Png, 100);
                    using (var file = File.Create(Path.Combine(proofDirectory, name + ".png"))) png.SaveTo(file);
                    using var pixels = SKBitmap.FromImage(image);
                    var bounds = canvas.ImageBounds;
                    var scale = ui.Viewport.Scale;
                    var clip = new SKRectI((int)(bounds.X * scale), (int)(bounds.Y * scale),
                        (int)((bounds.X + bounds.Width) * scale), (int)((bounds.Y + bounds.Height) * scale));
                    var allowSingleRedOccurrence = resetMaterial || referenceDiagnostics || initialImport;
                    var compareSlots = materialSlots || persistSlotOverride || reimportTexture || transferMaterials;
                    var redSlot = persistMultipleOverrides || transferMaterials;
                    var mask = Measure(pixels, clip, allowSingleRedOccurrence, compareSlots, redSlot, meshVisibility);
                    var comparePixels = name is "inspector-saved" or "inspector-reopened" || resetMaterial && name != "inspector-before" ||
                        referenceDiagnostics || materialSlots || initialImport;
                    var exact = comparePixels ? MeasureViewportPixels(view, pixels, clip, scale, mask, resetMaterial, allowSingleRedOccurrence,
                        compareSlots, verifyImportedTexture ? textureTopLeft ?? 0xD51006FF : null, redSlot, meshVisibility) : null;
                    result.TrySetResult((mask, stamp.Tick, exact));
                }
                catch (Exception error) { result.TrySetException(error); }
            }
            window.FramePresented += Presented;
            try
            {
                window.RequestFrame();
                return await result.Task.WaitAsync(token);
            }
            finally { window.FramePresented -= Presented; }
        }
    }

    private static RenderAssetKey? VerifyImportedTextureKey(bool verifyImportedTexture, PublishedModelImport published,
        RenderContentManifest manifest, MaterialBindingSetDefinition exact, PublishedModelMesh mesh)
    {
        RenderAssetKey? textureKey = null;
        if (verifyImportedTexture)
        {
            var publishedTexture = published.Texture ?? throw new InvalidDataException("Catalog ledger lost the external texture.");
            var texture = manifest.Resolve(publishedTexture.AssetId, RenderContentProfile.BaseColorTextureKindId);
            textureKey = texture.Key;
            var left = (OpaqueUnlitMaterialDefinition)manifest.Resolve(exact.Bindings[0].Material).Definition;
            var right = (OpaqueUnlitMaterialDefinition)manifest.Resolve(exact.Bindings[1].Material).Definition;
            Check(texture.Definition.Revision == publishedTexture.Definition.Revision &&
                  exact.Bindings[0].Material.AssetId == publishedTexture.MaterialId &&
                  left.BaseColorTexture == textureKey && left.Sampler == RenderTextureSampler.NearestClamp &&
                  right.BaseColorTexture == null &&
                  ((MeshDefinition)manifest.Resolve(exact.Mesh).Definition).Uv0.SequenceEqual(mesh.Geometry.Uv0),
                "Build did not preserve the exact imported texture, sampler and UV0 assignment.");
        }

        return textureKey;
    }

    private static GameBuildArtifact CorruptCopy(GameBuildArtifact artifact, string report, string hostId)
    {
        var directory = CopyArtifact(artifact, Path.Combine(report, "corrupt"));
        var copy = GameBuildArtifact.Open(directory, hostId, artifact.BuildId);
        var payload = Path.Combine(directory, GameBuildArtifact.RenderContentFile);
        var bytes = File.ReadAllBytes(payload);
        bytes[^1] ^= 1;
        File.WriteAllBytes(payload, bytes);
        return copy;
    }

    private static string CopyArtifact(GameBuildArtifact artifact, string root)
    {
        var directory = Directory.CreateDirectory(Path.Combine(root, artifact.BuildId)).FullName;
        foreach (var path in Directory.EnumerateFiles(artifact.DirectoryPath))
            File.Copy(path, Path.Combine(directory, Path.GetFileName(path)));
        return directory;
    }

    private static PixelMask Measure(SKBitmap pixels, SKRectI clip, bool allowSingleRedOccurrence,
        bool materialSlots = false, bool redSlot = false, bool allowHidden = false)
    {
        Check(clip.Left >= 0 && clip.Top >= 0 && clip.Right <= pixels.Width && clip.Bottom <= pixels.Height &&
            clip.Width > 0 && clip.Height > 0, "Invalid GPU image bounds.");
        var mask = new byte[clip.Width * clip.Height];
        var greenMask = new byte[mask.Length];
        var green = 0;
        var upper = 0;
        var lower = 0;
        var left = clip.Width;
        var right = -1;
        for (var y = 0; y < clip.Height; y++)
        for (var x = 0; x < clip.Width; x++)
        {
            var color = pixels.GetPixel(clip.Left + x, clip.Top + y);
            if (color.Green > 200 && color.Red < 25 && color.Blue < 25)
            {
                greenMask[y * clip.Width + x] = 1;
                green++;
            }
            if (color.Red <= 200 || color.Green >= 25 || color.Blue >= 25) continue;
            mask[y * clip.Width + x] = 1;
            if (y < clip.Height / 2) upper++;
            else lower++;
            left = Math.Min(left, x);
            right = Math.Max(right, x);
        }
        Check(allowHidden || (materialSlots ? green > 100 || redSlot && upper + lower > 100 :
            allowSingleRedOccurrence ? upper + lower > 100 && green > 100 : upper > 100 && lower > 100),
            "Expected occurrences in the GPU viewport.");
        return new(clip.Width, clip.Height, left, right, upper + lower, Hash(mask), green, Hash(greenMask));
    }

    private static ViewportPixelProof MeasureViewportPixels(EditorView view, SKBitmap pixels, SKRectI image,
        float scale, PixelMask mask,
        bool materialReset, bool allowSingleRedOccurrence, bool materialSlots, uint? textureTopLeft, bool redSlot,
        bool allowHidden)
    {
        // The Editor chrome overlays the viewport. Compare a rectangle clear of the panels and their shadows.
        const float clearance = 12;
        var hierarchy = view.HierarchyIsland.Bounds;
        var inspector = view.InspectorIsland.Bounds;
        var header = view.Header!.Bounds;
        var status = view.StatusBar!.Bounds;
        var crop = new SKRectI(
            Math.Max(image.Left, (int)MathF.Ceiling((hierarchy.X + hierarchy.Width + clearance) * scale)),
            Math.Max(image.Top, (int)MathF.Ceiling((header.Y + header.Height + clearance) * scale)),
            Math.Min(image.Right, (int)MathF.Floor((inspector.X - clearance) * scale)),
            Math.Min(image.Bottom, (int)MathF.Floor((status.Y - clearance) * scale)));
        var cropped = Measure(pixels, crop, allowSingleRedOccurrence, materialSlots, redSlot, allowHidden);
        Check(cropped.RedPixels == mask.RedPixels && cropped.GreenPixels == mask.GreenPixels,
            "The unobstructed viewport crop does not contain all visible model pixels.");
        var rgba = new byte[checked(crop.Width * crop.Height * 4)];
        // The fixture's red and green materials have identical blue values, including on blended edges.
        var blue = materialReset ? new byte[rgba.Length / 4] : null;
        var index = 0;
        for (var y = crop.Top; y < crop.Bottom; y++)
        for (var x = crop.Left; x < crop.Right; x++)
        {
            var pixel = pixels.GetPixel(x, y);
            if (blue != null) blue[index / 4] = pixel.Blue;
            rgba[index++] = pixel.Red;
            rgba[index++] = pixel.Green;
            rgba[index++] = pixel.Blue;
            rgba[index++] = pixel.Alpha;
        }
        return new(crop.Left, crop.Top, crop.Width, crop.Height, Hash(rgba))
        {
            BlueSha256 = blue == null ? null : Hash(blue),
            Slots = materialSlots ? MeasureSlots(rgba, crop.Width, crop.Height, cropped) : null,
            Texture = textureTopLeft is { } color ? MeasureTextureTiles(rgba, crop.Width, color) : null
        };
    }

    private static TextureTileProof MeasureTextureTiles(byte[] rgba, int width, uint expectedTopLeft)
    {
        var topLeft = Tile(expectedTopLeft);
        var topRight = Tile(0xE40911FF);
        var bottomLeft = Tile(0xF2160BFF);
        var bottomRight = Tile(0xFC0316FF);
        Check(topLeft.X < topRight.X && bottomLeft.X < bottomRight.X &&
            topLeft.Y < bottomLeft.Y && topRight.Y < bottomRight.Y &&
            topRight.X < width / 2 && bottomRight.X < width / 2,
            "The four imported texels did not appear in their expected viewport regions.");
        return new(topLeft, topRight, bottomLeft, bottomRight);

        TextureTile Tile(uint color)
        {
            long sumX = 0, sumY = 0;
            var count = 0;
            for (var offset = 0; offset < rgba.Length; offset += 4)
            {
                if (BinaryPrimitives.ReadUInt32BigEndian(rgba.AsSpan(offset, 4)) != color) continue;
                var pixel = offset / 4;
                sumX += pixel % width;
                sumY += pixel / width;
                count++;
            }
            Check(count > 100, $"Expected texel {color:X8} in the Editor's Vulkan viewport, got {count} pixels.");
            return new(color.ToString("X8"), count, (int)(sumX / count), (int)(sumY / count));
        }
    }

    private static SlotPixelProof MeasureSlots(byte[] rgba, int width, int height, PixelMask cropped)
    {
        var split = width / 2;
        var left = new byte[split * height * 4];
        var right = new byte[(width - split) * height * 4];
        var white = new byte[width * height];
        var whiteCount = 0;
        var greenCount = 0;
        var redRight = 0;
        for (var y = 0; y < height; y++)
        {
            var row = y * width;
            rgba.AsSpan(row * 4, split * 4).CopyTo(left.AsSpan(y * split * 4));
            rgba.AsSpan((row + split) * 4, (width - split) * 4).CopyTo(right.AsSpan(y * (width - split) * 4));
            for (var x = 0; x < width; x++)
            {
                var i = (row + x) * 4;
                var red = rgba[i];
                var green = rgba[i + 1];
                var blue = rgba[i + 2];
                if (red < 25 && green > 200 && blue < 25 && x >= split) greenCount++;
                if (red > 200 && green < 25 && blue < 25 && x >= split) redRight++;
                if (red <= 200 || green <= 200 || blue <= 200) continue;
                Check(x < split, "Replacement slot pixels escaped the left viewport region.");
                white[row + x] = 1;
                whiteCount++;
            }
        }
        Check(greenCount == cropped.GreenPixels, "The preserved green slot is outside the compared region.");
        return new(cropped.Sha256, whiteCount, Hash(white), Hash(right), Hash(left), redRight);
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed record PixelMask(int Width, int Height, int Left, int Right, int RedPixels, string Sha256, int GreenPixels, string GreenSha256);
    private sealed record ViewportPixelProof(int X, int Y, int Width, int Height, string Sha256)
    {
        public string? BlueSha256 { get; init; }
        public SlotPixelProof? Slots { get; init; }
        public TextureTileProof? Texture { get; init; }
    }
    private sealed record SlotPixelProof(string RedMask, int WhitePixels, string WhiteMask, string RightRgba, string LeftRgba, int RedRightPixels);
    private sealed record TextureTile(string Rgba, int Count, int X, int Y);
    private sealed record TextureTileProof(TextureTile TopLeft, TextureTile TopRight, TextureTile BottomLeft, TextureTile BottomRight);
    private sealed record FrameProof(string Name, string BuildId, ulong Generation, int Pid, string ManifestHash, ulong Tick, PixelMask Mask)
    {
        public ViewportPixelProof? Pixels { get; init; }
    }
    private sealed record AssetProof(Guid ProjectId, Guid AssetId, Guid KindId, int SchemaVersion, string Revision)
    {
        internal static AssetProof From(RenderAssetKey key) => new(key.ProjectId, key.AssetId, key.KindId, key.SchemaVersion, key.Revision.ToString());
    }
    private sealed record ImportGeneration(Guid SourceId, long Generation);
    private sealed record InspectorMaterialProof(FrameProof Frame, AssetProof Mesh, AssetProof Material, AssetProof Binding, Guid Slot);
    private sealed record InspectorRestartProof(int EditorPid, Guid OpeningId, Guid SceneId, long SceneRevision, string SceneSha256,
        string SelectionSha256, Guid Entity, AssetProof Mesh, AssetProof Material, AssetProof Binding, Guid Slot, ImportGeneration[] Imports, FrameProof Frame);
    private sealed record ImportedSlotProof(Guid Id, AssetProof Material);
    private sealed record MaterialTransferFrame(AssetProof Mesh, AssetProof Binding, ImportedSlotProof[] Slots, FrameProof Frame);
    private sealed record CameraProjectionValues(double Angle, double Near, double Far);
    private sealed record MeshVisibilityProof(bool Completed, int EditorPid, Guid Entity, Guid Renderer, Guid Camera, string SceneSha256,
        FrameProof? Initial = null, FrameProof? Disabled = null, FrameProof? Matching = null, FrameProof? Mismatch = null);
    private sealed record CameraProjectionFrame(CameraProjectionValues Values, FrameProof Frame);
    private sealed record CameraProjectionProof(bool Completed, int EditorPid, Guid Camera, string SceneSha256,
        CameraProjectionFrame Current, CameraProjectionFrame? Initial, CameraProjectionFrame? Undone);
    private sealed record InitialImportProof(bool Completed, int EditorPid, Guid OpeningId, Guid SourceId, string SourcePath, Guid Entity,
        long Generation, string SceneSha256, string CatalogSha256, string LedgerSha256, AssetProof Mesh, AssetProof Binding,
        ImportedSlotProof[] Slots, FrameProof Frame, bool RegistrationRetry)
    {
        public string SelectionSha256 { get; init; } = "";
        public string BindingReference { get; init; } = "";
        public bool OverrideVerified { get; init; }
        public AssetProof? Texture { get; init; }
    }
    private sealed record ModelImportProof(bool Completed, string BeforeEdit, string AfterEdit, string WhileMissing,
        string AfterReimport, string Blocked, bool MatchingContent, string OldBuild, string NewBuild,
        bool CancelledAfterLedger, bool MissingSourceRejected, IReadOnlyList<FrameProof> Captures, bool RestoredPriorGeneration,
        bool RestoredNewGeneration, bool ConflictRejected, bool CorruptRejected, bool CatalogSelectionVerified, bool MissingModelRejected,
        bool EmptySelectionRestored)
    {
        public ModelPlacementProof? Placement { get; init; }
    }

    private sealed record ModelPlacementProof(Guid SceneId, long SceneRevision, string SceneSha256, Guid[] InstanceIds, long Generation);

    private sealed class PausedPublication(IModelImportContentPublisher inner) : IModelImportContentPublisher
    {
        private int _calls;
        public int Calls => Volatile.Read(ref _calls);
        public Guid ProjectId => inner.ProjectId;
        public TaskCompletionSource<PublishedModelImport> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<GameBuildRenderContent> PublishAsync(PublishedModelImport model, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _calls) == 1)
            {
                Entered.TrySetResult(model);
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            return await inner.PublishAsync(model, cancellationToken);
        }
    }
}
