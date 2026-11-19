using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;
using TrueMoon.Aether.Abstractions.Rendering;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Editor.Tooling;
using TrueMoon.Aether.Editor.Tooling.Documents;
using TrueMoon.Aether.Editor.Tooling.Editing;
using TrueMoon.Aether.Editor.UI;
using TrueMoon.Aether.Editor.UI.Documents;
using TrueMoon.Aether.Nom;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Runtime.Ecs.Rendering;
using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Runtime.Sessions;
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
            missingModelRejected = playback.Error?.Message.Contains("выберите импорт в каталоге", StringComparison.Ordinal) == true;
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
            var button = view.StatusBar!.SummaryButton;
            await ui.InvokeAsync(() => view.Header!.Menu.CloseMenu()).WaitAsync(token);
            await EditorHeaderSmoke.UntilAsync(ui, () => (expected == null || view.StatusBar.DataContext!.ModelImportDetails == expected) &&
                button.Bounds.Width > 0 && !ui.NeedsUpdate, token);
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
            await EditorHeaderSmoke.UntilAsync(ui, () => view.Overlays.Popup is EditorStatusPopupView popup &&
                popup.ReimportActions.Children.OfType<Button>().Any(button => button.Value == label && button.IsEnabled && button.Bounds.Width > 0)
                && !ui.NeedsUpdate, token, HoldStatus);
            var before = commands.Pending;
            await ui.InvokeAsync(() =>
            {
                var popup = (EditorStatusPopupView)view.Overlays.Popup!;
                var button = popup.ReimportActions.Children.OfType<Button>().Single(item => item.Value == label);
                EditorHeaderSmoke.Move(ui, button);
                EditorHeaderSmoke.Click(ui, button);
                view.Overlays.Close(false);
            }).WaitAsync(token);
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
        var saveInspector = args.Length == 7 && args[6] == "--render-inspector-save";
        var reopenInspector = args.Length == 7 && args[6] == "--render-inspector-reopen";
        var restartInspector = saveInspector || reopenInspector;
        var probeRenderInspector = restartInspector || args.Length == 7 && args[6] == "--render-inspector";
        var probePlacement = args.Length == 9 && args[6] == "--model-placement";
        var probeCatalog = probePlacement || args.Length == 9 && args[6] == "--model-catalog";
        var probePersistence = probeCatalog || args.Length == 9 && args[6] == "--persisted-imports";
        var probeReimport = probePersistence || args.Length == 9 && args[6] == "--reimport";
        var probeImports = probeRenderInspector || probeReimport || args.Length == 7 && args[6] == "--imports";
        if (args.Length != 6 && !probeImports || args[0] is not ("cobalt" or "microsoft")) throw new ArgumentException("Invalid model delivery probe arguments.");
        var report = args[1];
        var proofDirectory = restartInspector ? Path.Combine(report, saveInspector ? "editor-saved" : "editor-reopened") : report;
        var project = args[2];
        var scenePath = args[3];
        var hostId = BuildIdentity.HostId;
        var oldBuild = GameBuildArtifact.Open(args[4], hostId);
        var newBuild = GameBuildArtifact.Open(args[5], hostId);
        var corruptBuild = restartInspector ? null : CorruptCopy(oldBuild, report, hostId);
        var sceneHash = Hash(File.ReadAllBytes(scenePath));
        Check(oldBuild.BuildId != newBuild.BuildId, "Scene probe requires two distinct build IDs.");
        var compiler = Path.Combine(proofDirectory, "editor-compiler");
        if (restartInspector)
        {
            Check(!Directory.Exists(proofDirectory), "Expected a fresh report and build directory for this Editor process.");
            Directory.CreateDirectory(compiler);
        }
        else CopyArtifact(oldBuild, compiler);
        PausedPublication? publication = null;
        ModelImportWorkerDeployment? worker = null;
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
            if (probeImports)
            {
                context.UseAetherModelSourceCapture();
                context.UseAetherModelImport();
            }
            if (probeReimport)
            {
                context.UseNom();
                context.UseAetherNomContent();
            }
            if (probePersistence || restartInspector)
                context.UseAetherSceneModelSelectionPersistence();
            if (probePersistence)
            {
                context.Services(services => services.Singleton<IFactory<SceneModelReimportSettings, SceneModelReimportRestoreArguments>>(_ =>
                    new Factory<SceneModelReimportSettings, SceneModelReimportRestoreArguments>(request =>
                        new(request.Ledger, worker!, Path.Combine(report, "scratch"), publication!))));
            }
            if (probeCatalog || restartInspector) context.UseAetherProjectModelCatalog();
            context.UseConfiguredAetherEditor(new AetherComposition
            {
                HostContractId = hostId, GameBuildId = new string('0', 64),
                RuntimeLaunch = NativeRuntimeLaunch.CurrentProcess("--runtime"),
                StatusChanged = status =>
                {
                    if (status is { State: SessionState.Starting, SceneLoaded: true, Identity: { } identity } &&
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
            if (probeImports)
            {
                Check(content.Document.Entities.Length == (probePlacement ? 3 : 5) && !before.IsDirty, "Unexpected fixture scene.");
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

            async Task ChooseBuild(string selection, string title)
            {
                var tools = view.Documents!;
                var model = (EditorDocumentViewModel)tools.DataContext!;
                await EditorHeaderSmoke.UntilAsync(ui, () => model.CanOpenBuild, token);
                await EditorHeaderSmoke.ClickActionAsync(ui, view, "project", "document.OpenBuild", token, confirmPath: false);
                await EditorHeaderSmoke.UntilAsync(ui, () => tools.IsVisible && tools.Actions.IsVisible && !ui.NeedsUpdate, token);
                await ui.InvokeAsync(() =>
                {
                    Check(model.PathRequest == DocumentCommand.OpenBuild && model.PathPromptText == title, "Wrong build selection prompt.");
                    ui.Focus(tools.PathInput);
                    ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
                    ui.HandleInput(new(InputKind.Text, Text: selection));
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
                var green = await ReadImport("65710c6d-6781-4476-9bb8-735fc518f008", "green.tmledger");
                var redMesh = red.Meshes.Single();
                var greenMesh = green.Meshes.Single();
                var target = new EntityId(red.Instances[0].Id);
                var sibling = new EntityId(red.Instances[1].Id);
                var renderer = Renderer(target);
                var opening = before.OpeningId;
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

                Task ClickInspector(Func<Element?> find) => EditorHeaderSmoke.UntilAsync(ui, () =>
                {
                    if (find() is not { IsEffectivelyEnabled: true, Bounds.Width: > 0, Bounds.Height: > 0 } element) return false;
                    inspector.Scroll.BringIntoView(element.Bounds);
                    if (ui.NeedsUpdate || !inspector.Scroll.ChildClipBounds.Contains(element.Bounds.X + element.Bounds.Width / 2,
                        element.Bounds.Y + element.Bounds.Height / 2)) return false;
                    EditorHeaderSmoke.Move(ui, element);
                    EditorHeaderSmoke.Click(ui, element);
                    return true;
                }, token);

                static IEnumerable<Element> Descendants(Element element) =>
                    element.Children.Concat(element.Children.SelectMany(Descendants));
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

            async Task<FrameProof> CapturePlaying(string name, GameBuildArtifact artifact, ulong minimumTick)
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
                var frame = await Capture(name, identity, content.Document.Id.Value, content.Document.Revision, manifest.Digest);
                Check(frame.Tick >= minimumTick, "Presented frame predates the checkpoint.");
                var after = documents.Read().Document!;
                Check(ReferenceEquals(content, after.Content) && before.IsDirty == after.IsDirty &&
                    sceneHash == Hash(File.ReadAllBytes(scenePath)), "Replace changed the open authoring document.");
                var proof = new FrameProof(name, artifact.BuildId, identity.Generation, status.ProcessId!.Value,
                    manifest.Digest.ToString(), frame.Tick, frame.Mask) { Pixels = frame.Pixels };
                captures.Add(proof);
                return proof;
            }

            async Task CheckReleased(ulong generation)
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

        async Task<(PixelMask Mask, ulong Tick, ViewportPixelProof? Pixels)> Capture(string name, SessionIdentity identity, Guid sceneId, long revision, RenderContentHash digest)
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
                    var mask = Measure(pixels, clip);
                    var exact = name is "inspector-saved" or "inspector-reopened" ? MeasureViewportPixels(view, pixels, clip, scale, mask) : null;
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

    private static PixelMask Measure(SKBitmap pixels, SKRectI clip)
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
        Check(upper > 100 && lower > 100, "Expected both occurrences in the GPU viewport.");
        return new(clip.Width, clip.Height, left, right, upper + lower, Hash(mask), green, Hash(greenMask));
    }

    private static ViewportPixelProof MeasureViewportPixels(EditorView view, SKBitmap pixels, SKRectI image, float scale, PixelMask mask)
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
        var cropped = Measure(pixels, crop);
        Check(cropped.RedPixels == mask.RedPixels && cropped.GreenPixels == mask.GreenPixels,
            "The unobstructed viewport crop does not contain all visible model pixels.");
        var rgba = new byte[checked(crop.Width * crop.Height * 4)];
        var index = 0;
        for (var y = crop.Top; y < crop.Bottom; y++)
        for (var x = crop.Left; x < crop.Right; x++)
        {
            var pixel = pixels.GetPixel(x, y);
            rgba[index++] = pixel.Red;
            rgba[index++] = pixel.Green;
            rgba[index++] = pixel.Blue;
            rgba[index++] = pixel.Alpha;
        }
        return new(crop.Left, crop.Top, crop.Width, crop.Height, Hash(rgba));
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed record PixelMask(int Width, int Height, int Left, int Right, int RedPixels, string Sha256, int GreenPixels, string GreenSha256);
    private sealed record ViewportPixelProof(int X, int Y, int Width, int Height, string Sha256);
    private sealed record FrameProof(string Name, string BuildId, ulong Generation, int Pid, string ManifestHash, ulong Tick, PixelMask Mask)
    {
        public ViewportPixelProof? Pixels { get; init; }
    }
    private sealed record AssetProof(Guid ProjectId, Guid AssetId, Guid KindId, int SchemaVersion, string Revision)
    {
        internal static AssetProof From(RenderAssetKey key) => new(key.ProjectId, key.AssetId, key.KindId, key.SchemaVersion, key.Revision.ToString());
    }
    private sealed record ImportGeneration(Guid SourceId, long Generation);
    private sealed record InspectorRestartProof(int EditorPid, Guid OpeningId, Guid SceneId, long SceneRevision, string SceneSha256,
        string SelectionSha256, Guid Entity, AssetProof Mesh, AssetProof Material, AssetProof Binding, Guid Slot, ImportGeneration[] Imports, FrameProof Frame);
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
