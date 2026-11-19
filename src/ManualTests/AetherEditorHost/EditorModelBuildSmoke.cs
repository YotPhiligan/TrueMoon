using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;
using TrueMoon.Aether.Abstractions.Rendering;
using TrueMoon.Aether.Editor.Tooling;
using TrueMoon.Aether.Editor.Tooling.Documents;
using TrueMoon.Aether.Editor.Tooling.Editing;
using TrueMoon.Aether.Editor.UI;
using TrueMoon.Aether.Editor.UI.Documents;
using TrueMoon.Aether.Nom;
using TrueMoon.Aether.Runtime.Builds;
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
        PausedPublication? publication, ModelImportWorkerDeployment? worker, string scenePath, bool persist, bool catalog, CancellationToken token)
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
            await EditorHeaderSmoke.UntilAsync(ui, () => view.Overlays.Popup is EditorStatusPopupView popup &&
                popup.ReadModelCatalog.IsEnabled && popup.ReadModelCatalog.Bounds.Width > 0 && !ui.NeedsUpdate, token, HoldStatus);
            await ui.InvokeAsync(() => EditorHeaderSmoke.Click(ui, ((EditorStatusPopupView)view.Overlays.Popup!).ReadModelCatalog)).WaitAsync(token);
            await commands.Pending.WaitAsync(token);
            await EditorHeaderSmoke.UntilAsync(ui, () => view.Overlays.Popup is EditorStatusPopupView popup &&
                popup.ModelCatalogRows.Children.OfType<CheckBox>().Select(check => (check.Value, check.IsChecked))
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
            catalogVerified, missingModelRejected, emptyRestored), baseline, updated);

        async Task ToggleCatalogModel(string name, Guid id, bool selected)
        {
            await OpenStatus(null);
            await EditorHeaderSmoke.UntilAsync(ui, () => view.Overlays.Popup is EditorStatusPopupView popup &&
                popup.ModelCatalogRows.Children.OfType<CheckBox>().Any(check => check.Value.StartsWith(name + " · ", StringComparison.Ordinal) &&
                    check.IsChecked != selected && check.IsEnabled && check.Bounds.Width > 0) && !ui.NeedsUpdate, token, HoldStatus);
            var pending = commands.Pending;
            await ui.InvokeAsync(() =>
            {
                var popup = (EditorStatusPopupView)view.Overlays.Popup!;
                var check = popup.ModelCatalogRows.Children.OfType<CheckBox>().Single(item => item.Value.StartsWith(name + " · ", StringComparison.Ordinal));
                EditorHeaderSmoke.Move(ui, check);
                EditorHeaderSmoke.Click(ui, check);
            }).WaitAsync(token);
            Check(!ReferenceEquals(pending, commands.Pending), $"Catalog did not admit choosing {name}.");
            await commands.Pending.WaitAsync(token);
            Check(commands.Read().Error == null && document.ReadModelImports(openingId).Any(item => item.SourceId == id) == selected,
                $"Catalog did not change selection for {name}: {commands.Read().Error}");
            await EditorHeaderSmoke.UntilAsync(ui, () => view.Overlays.Popup is EditorStatusPopupView popup &&
                popup.ModelCatalogRows.Children.OfType<CheckBox>().Any(check => check.Value.StartsWith(name + " · ", StringComparison.Ordinal) && check.IsChecked == selected), token, HoldStatus);
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
        var probeCatalog = args.Length == 9 && args[6] == "--model-catalog";
        var probePersistence = probeCatalog || args.Length == 9 && args[6] == "--persisted-imports";
        var probeReimport = probePersistence || args.Length == 9 && args[6] == "--reimport";
        var probeImports = probeReimport || args.Length == 7 && args[6] == "--imports";
        if (args.Length != 6 && !probeImports || args[0] is not ("cobalt" or "microsoft")) throw new ArgumentException("Invalid model delivery probe arguments.");
        var report = args[1];
        var project = args[2];
        var scenePath = args[3];
        var hostId = BuildIdentity.HostId;
        var oldBuild = GameBuildArtifact.Open(args[4], hostId);
        var newBuild = GameBuildArtifact.Open(args[5], hostId);
        var corruptBuild = CorruptCopy(oldBuild, report, hostId);
        var sceneHash = Hash(File.ReadAllBytes(scenePath));
        Check(oldBuild.BuildId != newBuild.BuildId, "Scene probe requires two distinct build IDs.");
        var compiler = Path.Combine(report, "editor-compiler");
        CopyArtifact(oldBuild, compiler);
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
            profile.Override("Aether:ReportDirectory", report);
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
            if (probePersistence)
            {
                context.UseAetherSceneModelSelectionPersistence();
                context.Services(services => services.Singleton<IFactory<SceneModelReimportSettings, SceneModelReimportRestoreArguments>>(_ =>
                    new Factory<SceneModelReimportSettings, SceneModelReimportRestoreArguments>(request =>
                        new(request.Ledger, worker!, Path.Combine(report, "scratch"), publication!))));
            }
            if (probeCatalog) context.UseAetherProjectModelCatalog();
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
                Check(content.Document.Entities.Length == 5 && !before.IsDirty, "Unexpected two-model scene.");
                var document = documents.Read().Project?.Documents ?? throw new InvalidOperationException("No project document session.");
                var ledgerFactory = app.Services.Resolve<IFactory<ModelImportLedgerSession, ModelImportLedgerArguments>>()!;
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
                    scenePath, probePersistence, probeCatalog, token);
                if (probePersistence)
                {
                    before = document.Read();
                    content = before.Content ?? throw new InvalidOperationException("Restored document is no longer active.");
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
            await ChooseBuild(corruptBuild.DirectoryPath, "Заменить текущую сборку");
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
                    manifest.Digest.ToString(), frame.Tick, frame.Mask);
                captures.Add(proof);
                return proof;
            }

            async Task CheckReleased(ulong generation)
            {
                var lease = native.Leases.Single(lease => lease.Identity.Generation == generation);
                Check(lease.Reaped && lease.ResourcesReleased, "Retired child or handles survived replacement.");
                using var child = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(report, $"runtime-{generation}.json"), token));
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

        async Task<(PixelMask Mask, ulong Tick)> Capture(string name, SessionIdentity identity, Guid sceneId, long revision, RenderContentHash digest)
        {
            var result = new TaskCompletionSource<(PixelMask Mask, ulong Tick)>(TaskCreationOptions.RunContinuationsAsynchronously);
            void Presented(UiSession ui)
            {
                if (result.Task.IsCompleted) return;
                try
                {
                    var canvas = EditorMainWindow.FromRoot(ui.Root).EditorView.Canvas;
                    if (!canvas.CanAcknowledgeDraw || canvas.DrawnFrame is not { } frame ||
                        frame.Presentation.Surface.Identity != identity ||
                        !ReferenceEquals(frame.Presentation, engine.Frames.Presentation)) return;
                    var stamp = frame.Metadata.RenderView ?? throw new InvalidDataException("Missing mesh viewport stamp.");
                    Check(frame.Presentation.Surface.Transport == ViewportTransportKind.Gpu && stamp.SceneId == sceneId &&
                        stamp.SourceRevision == revision && RenderContentHash.Parse(stamp.ManifestHash) == digest,
                        "GPU draw was not from the captured scene and selected build.");
                    using var image = ((SkiaOpenGLSurface)ui.Rendering).ReadbackImage();
                    using var png = image.Encode(SKEncodedImageFormat.Png, 100);
                    using (var file = File.Create(Path.Combine(report, name + ".png"))) png.SaveTo(file);
                    using var pixels = SKBitmap.FromImage(image);
                    var bounds = canvas.ImageBounds;
                    var scale = ui.Viewport.Scale;
                    var clip = new SKRectI((int)(bounds.X * scale), (int)(bounds.Y * scale),
                        (int)((bounds.X + bounds.Width) * scale), (int)((bounds.Y + bounds.Height) * scale));
                    result.TrySetResult((Measure(pixels, clip), stamp.Tick));
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

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed record PixelMask(int Width, int Height, int Left, int Right, int RedPixels, string Sha256, int GreenPixels, string GreenSha256);
    private sealed record FrameProof(string Name, string BuildId, ulong Generation, int Pid, string ManifestHash, ulong Tick, PixelMask Mask);
    private sealed record ModelImportProof(bool Completed, string BeforeEdit, string AfterEdit, string WhileMissing,
        string AfterReimport, string Blocked, bool MatchingContent, string OldBuild, string NewBuild,
        bool CancelledAfterLedger, bool MissingSourceRejected, IReadOnlyList<FrameProof> Captures, bool RestoredPriorGeneration,
        bool RestoredNewGeneration, bool ConflictRejected, bool CorruptRejected, bool CatalogSelectionVerified, bool MissingModelRejected,
        bool EmptySelectionRestored);

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
