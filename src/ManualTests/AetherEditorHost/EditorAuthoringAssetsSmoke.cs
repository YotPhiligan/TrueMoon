using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;
using TrueMoon.Aether.Editor.Tooling;
using TrueMoon.Aether.Editor.Tooling.Documents;
using TrueMoon.Aether.Editor.Tooling.Editing;
using TrueMoon.Aether.Editor.UI;
using TrueMoon.Aether.Editor.UI.Documents;
using TrueMoon.Aether.Editor.UI.Editing;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Runtime.Ecs.Rendering;
using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Runtime.Preview;
using TrueMoon.Aether.Runtime.Rendering.Snapshots;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Tooling.Hosting;
using TrueMoon.Aether.Tooling.Importing;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Argentis.Input;

namespace TrueMoon.ManualTests.AetherEditorHost;

// Directed UI/model commands and actual Vulkan producer -> GL editor draw. No CPU frame substitute.
internal static class EditorAuthoringAssetsSmoke
{
    private const string SourceA = "source/scene.gltf", SourceB = "source/replacement.gltf";
    private const string TextureA = "source/textures/color.png", TextureB = "source/textures/replacement.png";
    private const uint Green = 0x00FF00FF, Magenta = 0xFF00FFFF, Blue = 0x0000FFFF;
    private static readonly uint[] PaletteA = [0xD51006FF, 0xE40911FF, 0xF2160BFF, 0xFC0316FF];
    private static readonly uint[] PaletteB = [0x2194D5FF, 0x7B730CFF, 0x304D90FF, 0x6D18AAFF];

    internal static async Task RunAsync(bool reopen, string root, string output, string compiler, string scenePath,
        HostedUiWindow window, UiSession ui, EditorView view, AetherHost engine, NativeRuntimeFactory native,
        EditorDocumentCommands commands, EditorScenePlayback playback, EditorSession editor,
        IFactory<ModelImportLedgerSession, ModelImportLedgerArguments> ledgers, CancellationToken token)
    {
        var project = commands.Read().Project!;
        var document = project.Documents!;
        var inspector = view.Inspector!.DataContext!;
        var textures = inspector.Textures!;
        var meshes = inspector.Meshes!;
        var materials = inspector.Materials!;
        var scenes = Path.GetDirectoryName(scenePath)!;
        var pinned = Path.Combine(scenes, "pinned-A.aetherscene");
        var prior = Path.Combine(root, "import-saved", "editor-authoring-assets-save.json");
        var frames = new List<FrameProof>();
        var clicks = new List<ClickProof>();
        var children = new List<ChildProof>();
        var processes = new Dictionary<ulong, Process>();
        var noBuildBeforePreview = false;
        var withheldDrained = false;
        SourceOrderProof? sourceOrder = null;
        try
        {
            if (reopen)
            {
                var save = JsonSerializer.Deserialize<PassProof>(await File.ReadAllBytesAsync(prior, token))!;
                Require(save.EditorPid != Environment.ProcessId && save.SceneSha256 == FileHash(scenePath) && save.PinnedSha256 == FileHash(pinned),
                    "Restart did not reopen the unchanged saved authoring scene and frozen A scene.");
                Require(!File.Exists(Path.Combine(root, SourceA)) && !File.Exists(Path.Combine(root, SourceB)), "Live model sources survived the save pass.");
                var keys = await ReadKeys(save.Mesh, save.Left, save.Right, save.Texture, save.Textured, save.Solid);
                var reopened = await Preview("reopened", keys, PaletteB, Blue, replaced: true);
                var priorEdit = save.Frames.Single(f => f.Name == "redo");
                SameKeys(priorEdit.Keys!, keys);
                SameColors(priorEdit, reopened);
                Require(reopened.PreviewStamp!.DocumentEpoch != priorEdit.PreviewStamp!.DocumentEpoch &&
                    reopened.PreviewStamp.SceneId == priorEdit.PreviewStamp.SceneId &&
                    reopened.PreviewStamp.SceneRevision == priorEdit.PreviewStamp.SceneRevision,
                    "Restart did not create a new opening for the same committed scene revision.");
                await StopPreview();

                // Hold first presentation while a real render child is admitted, then close the document.
                engine.Preview.SetPresentationSuspended(true);
                try
                {
                    var count = native.Leases.Count;
                    await OnUi(() => inspector.ShowPreview());
                    await Until(() => native.Leases.Count > count && engine.Frames.Presentation is { } live &&
                        engine.Supervisor.Status.SceneLoaded && view.Canvas.DrawnFrame is { } draw && ReferenceEquals(draw.Presentation, live));
                    var pending = engine.Frames.Presentation!;
                    ObserveProcess(pending.Surface.Identity.Generation, engine.Supervisor.Status.ProcessId!.Value);
                    Require(!pending.FirstPresented.IsCompleted && engine.Preview.PresentedView == null,
                        "Activation, upload or a withheld draw acknowledged first presentation.");
                    await Doc(DocumentCommand.Close);
                    Require(pending.FirstPresented.IsCompleted && !pending.FirstPresented.IsCompletedSuccessfully,
                        "Close did not cancel the admitted but unacknowledged presentation.");
                    await CheckDrain();
                    withheldDrained = true;
                }
                finally { engine.Preview.SetPresentationSuspended(false); }

                await Doc(DocumentCommand.Open, pinned);
                var old = save.Frames.Single(f => f.Name == "play-A");
                var artifact = GameBuildArtifact.Open(Path.Combine(root, "import-saved", "editor-compiler", old.BuildId!), BuildIdentity.HostId, old.BuildId);
                // Do not allow selected Play to succeed by accidentally re-reading any current authored blob.
                var parked = new List<(string Path, string Parked)>();
                try
                {
                    foreach (var name in new[] { ".aether-mesh-data", ".aether-texture-data" })
                    {
                        var path = Path.Combine(project.Project.ContentDirectory, name);
                        var away = path + ".proof-withheld";
                        Directory.Move(path, away);
                        parked.Add((path, away));
                    }
                    var actual = await Play("pinned-A", artifact, old.Keys!, PaletteA, Magenta, replaced: false);
                    Require(actual.BuildId == old.BuildId && actual.GameStamp!.ManifestHash == old.GameStamp!.ManifestHash,
                        "Selected Play changed the frozen A Build or exact manifest.");
                    SameKeys(old.Keys!, actual.Keys!);
                    SameColors(old, actual);
                }
                finally { foreach (var (path, away) in parked) Directory.Move(away, path); }
                await Finish();
                await Write("editor-authoring-assets.json", new { Completed = true, Save = save,
                    Reopened = Proof(save.Mesh, save.Left, save.Right, save.Texture, save.Textured, save.Solid) });
                return;
            }

            await Doc(DocumentCommand.Open, Path.Combine(scenes, "cubes.aetherscene"));
            var cubes = await Preview("cubes", null, null, 0, replaced: false);
            await StopPreview();
            await Doc(DocumentCommand.Open, scenePath);
            var sceneBeforeAssets = document.Read().Content;
            await Doc(DocumentCommand.ImportModel, SourceA);
            await Doc(DocumentCommand.ImportModel, SourceB);
            var sourceB = commands.Read().ModelCatalog!.Single(row => row.SourcePath == SourceB);
            var ledgerPath = Path.Combine(project.Project.ContentDirectory, ".aether-model-ledgers", $"{sourceB.SourceId:N}.tmledger");
            await using (var ledger = ledgers.Create(new(sourceB.SourceId, ledgerPath))!)
                sourceOrder = await ReverseImportedSlots(ledger, token);

            await OnUi(() => textures.IsOpen = true);
            await Until(() => textures.CanCreate);
            await OnUi(() => { textures.New(); textures.Name = "Proof PNG"; textures.SourcePath = TextureA; textures.Import(); });
            await Until(() => textures.Items.Count == 1 && !textures.HasDraft);
            var texture = await ReadUi(() => textures.Items.Single());
            Require(texture.Width == 2 && texture.Height == 2 && texture.SourcePath == TextureA, "Unexpected imported PNG metadata.");
            await OnUi(() => textures.IsOpen = false);

            await OnUi(() => meshes.IsOpen = true);
            await Until(() => meshes.CanCreateDraft && meshes.Sources.Count == 2);
            await OnUi(() => { meshes.New(); meshes.Name = "Proof panel"; });
            await ChooseGeometry(SourceA);
            await OnUi(meshes.Create);
            await Until(() => meshes.Items.Count == 1 && !meshes.HasDraft);
            var mesh = await ReadUi(() => meshes.Items.Single());
            Require(mesh.SourcePath == SourceA && mesh.HasUv0 && mesh.Slots.Select(s => s.Name).SequenceEqual(["left", "right"]),
                "Named mesh did not freeze the labeled source geometry.");
            var left = mesh.Slots[0].Id;
            var right = mesh.Slots[1].Id;
            await OnUi(() => meshes.IsOpen = false);

            var textured = await Material("Textured left", 1, 1, 1, texture.Id);
            var solid = await Material("Solid left", 1, 0, 1, null);
            await OnUi(() => materials.IsOpen = false);
            Require(ReferenceEquals(sceneBeforeAssets, document.Read().Content), "Asset authoring changed the scene before assignment.");
            await Assign(1, mesh.Id, left, textured);
            await Assign(2, mesh.Id, left, solid);
            await Doc(DocumentCommand.Save);
            File.Copy(scenePath, pinned);
            var committed = document.Read().Content!;

            var a = await ReadKeys(mesh.Id, left, right, texture.Id, textured, solid);
            var mixed = await Preview("mixed-A", a, PaletteA, Magenta, replaced: false);
            SameColors(cubes, mixed, "front-cube", "control-cube");
            Require(Color(mixed, "front-cube") != Color(mixed, "texture-0") && Color(mixed, "control-cube") != Color(mixed, "clear"),
                "Mixed rendering did not retain the covering and control cube pixels.");
            await ClickRegion(mixed, "first-right", FixtureId(1, 1));
            await ClickRegion(mixed, "clear", null);
            await ClickRegion(mixed, "front-cube", FixtureId(3, 1));
            await ClickRegion(mixed, "clear", null);
            noBuildBeforePreview = engine.Editor.Status.Candidate == null && !Directory.EnumerateFileSystemEntries(compiler).Any();
            Require(noBuildBeforePreview, "A Build existed before native Edit Preview was drawn.");
            var builtA = await Build(a);
            await Play("play-A", builtA, a, PaletteA, Magenta, replaced: false);
            // Build/Play stopped Edit Preview; re-enable it only after committing replacement B.

            await OnUi(() => { textures.IsOpen = true; textures.Select(texture.Id); textures.SourcePath = TextureB; textures.ReplaceSource(); });
            await Until(() => textures.Selected is { } item && item.SourcePath == TextureB && item.Revision != texture.Revision && !textures.HasDraft);
            Require(await ReadUi(() => textures.Selected!.Id == texture.Id), "Texture replacement changed its persistent ID.");
            await OnUi(() => textures.IsOpen = false);
            var b = await ReadKeys(mesh.Id, left, right, texture.Id, textured, solid);
            var textureFrame = await Preview("texture-B", b, PaletteB, Magenta, replaced: false);
            ChangedIntent(mixed, textureFrame);
            Require(a.Texture.Id == b.Texture.Id && a.Texture != b.Texture && a.Mesh == b.Mesh && a.Second == b.Second &&
                a.First.Right == b.First.Right && Revised(a.First.Left, b.First.Left) && Revised(a.First.Binding, b.First.Binding),
                "Texture replacement changed unrelated exact keys or left a stale texture/material/binding revision.");
            SameColors(mixed, textureFrame, "first-right", "second-left", "second-right", "front-cube", "control-cube");

            await OnUi(() => { meshes.IsOpen = true; meshes.Select(mesh.Id); });
            await ChooseGeometry(SourceB);
            await OnUi(meshes.ReplaceGeometry);
            await Until(() => meshes.Selected?.SourcePath == SourceB && !meshes.HasDraft);
            var replacement = await ReadUi(() => meshes.Selected!);
            Require(replacement.Id == mesh.Id && replacement.Bounds != mesh.Bounds && replacement.HasUv0 &&
                replacement.Slots.SequenceEqual(mesh.Slots.Reverse()), "Geometry replacement did not reverse ordered stable slots or change bounds.");
            await OnUi(() => meshes.IsOpen = false);
            var c = await ReadKeys(mesh.Id, left, right, texture.Id, textured, solid);
            var geometryFrame = await Capture("geometry-B", c, PaletteB, Magenta, replaced: true, after: textureFrame);
            ChangedIntent(textureFrame, geometryFrame);
            Require(Revised(b.Mesh, c.Mesh) && b.Texture == c.Texture && b.First.Left == c.First.Left && b.Second.Left == c.Second.Left &&
                b.First.Right == c.First.Right && b.Second.Right == c.Second.Right && Revised(b.First.Binding, c.First.Binding) &&
                Revised(b.Second.Binding, c.Second.Binding), "Geometry replacement lost effective slot materials or changed persistent keys.");
            SameColors(textureFrame, geometryFrame, "texture-0", "texture-1", "texture-2", "texture-3", "first-right", "second-left", "second-right");
            await ClickRegion(geometryFrame, "first-right", FixtureId(1, 1));
            await ClickRegion(geometryFrame, "first-footprint-left", null);

            await OnUi(() => { materials.IsOpen = true; materials.Open(solid); materials.Red = "0"; materials.Save(); });
            await Until(() => materials.CanUndo && !materials.HasDraft);
            var d = await ReadKeys(mesh.Id, left, right, texture.Id, textured, solid);
            var edited = await Capture("material-B", d, PaletteB, Blue, replaced: true, after: geometryFrame);
            ChangedIntent(geometryFrame, edited);
            Require(c.Mesh == d.Mesh && c.Texture == d.Texture && c.First == d.First && c.Second.Right == d.Second.Right &&
                Revised(c.Second.Left, d.Second.Left) && Revised(c.Second.Binding, d.Second.Binding), "Material save changed unrelated exact keys.");
            await OnUi(materials.Undo);
            await Until(() => materials.CanRedo && !materials.HasDraft);
            var undoKeys = await ReadKeys(mesh.Id, left, right, texture.Id, textured, solid);
            SameKeys(c, undoKeys);
            var undo = await Capture("undo", undoKeys, PaletteB, Magenta, replaced: true, after: edited);
            ChangedIntent(edited, undo);
            SameColors(geometryFrame, undo);
            await OnUi(materials.Redo);
            await Until(() => !materials.CanRedo && materials.CanUndo && !materials.HasDraft);
            var redoKeys = await ReadKeys(mesh.Id, left, right, texture.Id, textured, solid);
            SameKeys(d, redoKeys);
            var redo = await Capture("redo", redoKeys, PaletteB, Blue, replaced: true, after: undo);
            ChangedIntent(undo, redo);
            SameColors(edited, redo);
            await OnUi(() => materials.IsOpen = false);
            Require(ReferenceEquals(committed, document.Read().Content) && !document.Read().IsDirty && FileHash(scenePath) == FileHash(pinned),
                "Asset receipts, material undo or redo touched scene identity, history or disk bytes.");

            await OnUi(() =>
            {
                var bounds = view.Canvas.ImageBounds;
                var x = bounds.X + bounds.Width / 2;
                var y = bounds.Y + bounds.Height / 2;
                Require(ui.HandleInput(new(InputKind.PointerDown, x, y, Button: 1)).PointerCaptured, "Orbit did not capture viewport input.");
                ui.HandleInput(new(InputKind.PointerMove, x + 12, y + 5));
                ui.HandleInput(new(InputKind.PointerUp, x + 12, y + 5, Button: 1));
                Require(editor.ReadPresentedView(view) == null, "Queued camera accepted the old draw.");
            });
            var orbit = await Capture("orbit", d, PaletteB, Blue, replaced: true, navigation: true, after: redo);
            SameChild(redo, orbit);
            Require(orbit.PreviewStamp!.CameraRevision > redo.PreviewStamp!.CameraRevision && orbit.Surface == redo.Surface,
                "Orbit did not replace just the camera stamp.");
            await engine.Preview.ResizeAsync(720, 440, token);
            var resize = await Capture("resize", d, PaletteB, Blue, replaced: true, navigation: true, after: orbit);
            SameChild(orbit, resize);
            Require(resize.Surface != orbit.Surface && resize.Width == 720 && resize.Height == 440 &&
                resize.PreviewStamp!.CameraRevision == orbit.PreviewStamp.CameraRevision, "Resize did not replace just the surface stamp.");

            // Remove source files before Build B and the second Editor process. The authored blobs remain the only live input.
            Directory.Delete(Path.Combine(root, "source"), recursive: true);
            var builtB = await Build(d);
            Require(builtB.BuildId != builtA.BuildId, "Build B reused pinned A despite different exact resources.");
            await Play("play-B", builtB, d, PaletteB, Blue, replaced: true);
            await Finish();
            await Write("editor-authoring-assets-save.json", Proof(mesh.Id, left, right, texture.Id, textured, solid));
        }
        finally { foreach (var process in processes.Values) process.Dispose(); }

        Task OnUi(Action action) => ui.InvokeAsync(action).WaitAsync(token);
        async Task<T> ReadUi<T>(Func<T> query)
        {
            T value = default!;
            await OnUi(() => value = query());
            return value;
        }
        Task Until(Func<bool> ready) => EditorHeaderSmoke.UntilAsync(ui, ready, token);
        async Task Doc(DocumentCommand command, string path = "")
        {
            await Until(() => commands.CanExecute(command) && commands.Execute(command, path));
            await commands.Pending.WaitAsync(token);
            if (commands.Read().Error is { } error) throw new InvalidOperationException($"{command} failed: {path}", error);
        }
        async Task ChooseGeometry(string path)
        {
            var source = commands.Read().ModelCatalog!.Single(row => row.SourcePath == path);
            await Until(() => meshes.CanEditGeometry && meshes.Sources.Any(row => row.Id == source.SourceId));
            await OnUi(() => meshes.SelectSource(meshes.Sources.Single(row => row.Id == source.SourceId)));
            await Until(() => meshes.SelectedSource == source.SourceId && meshes.Geometries.Count == 1);
            await OnUi(() => meshes.SelectGeometry(meshes.Geometries.Single()));
        }
        async Task<Guid> Material(string name, int r, int g, int b, Guid? texture)
        {
            await OnUi(() => materials.IsOpen = true);
            await Until(() => materials.CanCreate && (texture == null || materials.Textures.Any(row => row.Id == texture)));
            await OnUi(() =>
            {
                materials.New(); materials.Name = name;
                materials.Red = r == 0 ? "0" : "1"; materials.Green = g == 0 ? "0" : "1"; materials.Blue = b == 0 ? "0" : "1";
                materials.TextureId = texture;
                Require(materials.CanSave, "The intended Unlit material draft was not writable.");
                materials.Save();
            });
            await Until(() => materials.Items.Count(row => row.Name == name) == 1 && !materials.HasDraft);
            return await ReadUi(() => materials.Items.Single(row => row.Name == name).Id);
        }
        async Task Assign(int number, Guid meshId, Guid leftSlot, Guid material)
        {
            var id = FixtureId(number, 1);
            await EditorHierarchySmoke.ClickItemAsync(ui, view, id.ToString("D"), token);
            await Until(() => inspector.RenderAssets is { Enabled: true } assets && assets.Meshes.Any(row => row.Id == meshId));
            await OnUi(() =>
            {
                var component = inspector.Components.Single(row => row.TypeId == MeshRenderingComponents.MeshRendererId);
                var field = component.Properties.Single(row => row.Key == "mesh");
                inspector.SelectRenderAsset(inspector.RenderAssets!, field, meshId);
                inspector.Apply();
            });
            await Until(() => inspector.RenderAssets is { Enabled: true } assets && assets.Mesh == meshId && assets.Slots.Any(slot => slot.Id == leftSlot));
            await OnUi(() => { inspector.SelectRenderSlotMaterial(inspector.RenderAssets!, leftSlot, material); inspector.Apply(); });
            await Until(() => inspector.RenderAssets?.Slots.Any(slot => slot.Id == leftSlot && slot.Material == material) == true);
        }
        async Task<ExactKeys> ReadKeys(Guid meshId, Guid left, Guid right, Guid texture, Guid textured, Guid solid)
        {
            var opening = document.Read();
            Require(document.ReadModelImports(opening.OpeningId).Count == 0, "A source import was selected for the named-mesh fixture.");
            var closure = await document.CheckModelImportsAsync(opening.OpeningId, token) ?? throw new InvalidDataException("Exact closure missing.");
            var meshKey = Find(meshId, RenderContentProfile.MeshKindId);
            var textureKey = Find(texture, RenderContentProfile.BaseColorTextureKindId);
            var leftTextured = Find(textured, RenderContentProfile.MaterialKindId);
            var leftSolid = Find(solid, RenderContentProfile.MaterialKindId);
            var rightKey = closure.AssetKeys.Single(key => key.KindId == RenderContentProfile.MaterialKindId && key.AssetId != textured && key.AssetId != solid);
            var first = Binding(1, leftTextured);
            var second = Binding(2, leftSolid);
            Require(closure.AssetKeys.Length == 7 && first.Binding != second.Binding,
                "Expected only one exact mesh, two effective bindings, three materials and one PNG.");
            return new(Key.From(meshKey), Key.From(textureKey), left, right, first, second);

            RenderAssetKey Find(Guid id, Guid kind) => closure.AssetKeys.Single(key => key.AssetId == id && key.KindId == kind);
            BindingKeys Binding(int instance, RenderAssetKey leftMaterial)
            {
                var renderer = opening.Content!.Resolved.Entities[new(FixtureId(instance, 1))].Components.Values
                    .Single(component => component.TypeId == MeshRenderingComponents.MeshRendererId);
                Require(renderer.Properties["mesh"].Value.GetString() == meshId.ToString("D"), "Renderer does not carry the named mesh ID.");
                var binding = Guid.ParseExact(renderer.Properties["bindings"].Value.GetString()!, "D");
                return new(Key.From(Find(binding, RenderContentProfile.BindingSetKindId)), Key.From(leftMaterial), Key.From(rightKey));
            }
        }
        async Task<GameBuildArtifact> Build(ExactKeys keys)
        {
            await Until(() => view.DataContext!.CanBuild);
            await EditorHeaderSmoke.ClickActionAsync(ui, view, "project", "runtime.Build", token);
            await CompletePlay();
            await CheckDrain();
            var artifact = engine.Editor.Status.Candidate!.Build.Artifact;
            Require(Path.GetDirectoryName(artifact.DirectoryPath) == compiler, "Fresh Build did not originate in the proof compiler directory.");
            VerifyArtifact(artifact.RenderContent!, keys);
            return artifact;
        }
        async Task<FrameProof> Play(string name, GameBuildArtifact build, ExactKeys keys, uint[] palette, uint solid, bool replaced)
        {
            var manifest = build.RenderContent ?? throw new InvalidDataException("Selected artifact lacks a render manifest.");
            VerifyArtifact(manifest, keys);
            await OnUi(() => Require(playback.RunSelectedBuild(build), "Editor declined selected Build Play."));
            await CompletePlay();
            var frame = await Capture(name, keys, palette, solid, replaced, build: build);
            await OnUi(() => Require(playback.Execute(EditorCommand.Stop), "Game Stop was rejected."));
            await CompletePlay();
            await CheckDrain();
            return frame;
        }
        async Task CompletePlay()
        {
            await playback.Pending.WaitAsync(token);
            if (playback.Error is { } error) throw new InvalidOperationException("Editor Build/Play command failed.", error);
        }
        async Task<FrameProof> Preview(string name, ExactKeys? keys, uint[]? palette, uint solid, bool replaced)
        {
            await Until(() => inspector.CanPreview);
            await OnUi(inspector.ShowPreview);
            return await Capture(name, keys, palette, solid, replaced);
        }
        Task ClickRegion(FrameProof frame, string region, Guid? expected)
        {
            var sample = frame.Regions.Single(item => item.Name == region);
            var x = (sample.X + .5f) / frame.Scale;
            var y = (sample.Y + .5f) / frame.Scale;
            return Until(() =>
            {
                var receipt = editor.ReadPresentedView(view);
                if (receipt == null || receipt.State.Stamp != frame.PreviewStamp ||
                    receipt.Frame.Presentation.Surface.Generation != frame.Surface || receipt.LayoutRevision != frame.Layout)
                    return false;

                var target = $"{frame.Name}/{region}";
                Require(ui.HandleInput(new(InputKind.PointerDown, x, y)).PointerCaptured, $"{target}: click did not capture.");
                var release = ui.HandleInput(new(InputKind.PointerUp, x, y));
                Require(release.Handled && !release.PointerCaptured, $"{target}: click did not release.");
                var selected = inspector.Selection.EntityId?.Value;
                Require(view.Hierarchy!.DataContext.SelectedId == expected?.ToString("D") && selected == expected,
                    $"{target}: expected selection {expected?.ToString("D") ?? "none"}, inspector has {selected?.ToString("D") ?? "none"}.");
                if (expected == FixtureId(1, 1))
                    Require(inspector.Components.Any(row => row.TypeId == MeshRenderingComponents.MeshRendererId && row.Id.Value == FixtureId(1, 4)),
                        $"{target}: the selected mesh occurrence is missing from the inspector.");
                clicks.Add(new(frame.Name, region, selected, receipt.State.Stamp.ContentId, receipt.Frame.Metadata.FrameId));
                return true;
            });
        }
        async Task StopPreview()
        {
            var stop = await ReadUi(inspector.StopPreviewAsync);
            await stop.WaitAsync(token);
            await CheckDrain();
        }
        async Task<FrameProof> Capture(string name, ExactKeys? keys, uint[]? palette, uint solid, bool replaced,
            bool navigation = false, FrameProof? after = null, GameBuildArtifact? build = null)
        {
            var expected = document.Read();
            var file = Path.Combine(output, name + ".png");
            var completion = new TaskCompletionSource<FrameProof>(TaskCreationOptions.RunContinuationsAsynchronously);
            void Presented(UiSession current)
            {
                if (completion.Task.IsCompleted || current.NeedsRender) return;
                try
                {
                    var canvas = view.Canvas;
                    if (!canvas.CanAcknowledgeDraw || canvas.DrawnFrame is not { } frame || !ReferenceEquals(frame.Presentation, engine.Frames.Presentation)) return;
                    var status = engine.Supervisor.Status;
                    var surface = frame.Presentation.Surface;
                    if (status.Identity != surface.Identity || status.ProcessId is not { } pid || surface.Transport != ViewportTransportKind.Gpu) return;
                    ObserveProcess(surface.Identity.Generation, pid);
                    var previewStamp = frame.Metadata.PreviewView;
                    var gameStamp = frame.Metadata.RenderView;
                    var camera = PreviewCamera.Default;
                    ulong layout = 0;
                    if (build == null)
                    {
                        var receipt = editor.ReadPresentedView(view);
                        if (receipt == null || previewStamp != receipt.State.Stamp || gameStamp != null || receipt.Frame.Metadata.FrameId != frame.Metadata.FrameId) return;
                        if (after != null && (navigation ? previewStamp == after.PreviewStamp && surface.Generation == after.Surface : previewStamp!.ContentId == after.PreviewStamp!.ContentId)) return;
                        Require(surface.Identity.GameBuildId == BuildIdentity.PreviewId && frame.Metadata.Tick == 0 && status.Tick == 0 &&
                            previewStamp!.SceneId == expected.Content!.Document.Id.Value && previewStamp.SceneRevision == expected.Content.Document.Revision &&
                            previewStamp.DocumentEpoch == expected.OpeningId, "Preview presented a foreign scene/opening or a gameplay tick.");
                        Require(keys == null || receipt.Picks.Primitives.Length == 2, "Mixed preview lost primitive pick rows.");
                        camera = receipt.State.Camera;
                        layout = receipt.LayoutRevision;
                    }
                    else
                    {
                        if (previewStamp != null || gameStamp == null || status.State != SessionState.Playing || surface.Identity.GameBuildId != build.BuildId) return;
                        Require(RenderContentHash.Parse(gameStamp.ManifestHash) == build.RenderContent!.Digest && gameStamp.SceneId == expected.Content!.Document.Id.Value &&
                            gameStamp.SourceRevision == expected.Content.Document.Revision && gameStamp.SurfaceGeneration == surface.Generation,
                            "Play draw is not from the selected build/scene/manifest/surface.");
                    }
                    var projection = build == null ? camera.CreateProjection(surface.Width, surface.Height) :
                        new CameraProjection(PerspectiveProjection.Default, Matrix4x4.CreateTranslation(0, 1, 5), surface.Width, surface.Height);
                    using var image = current.ReadbackOpenGLImage();
                    using var bitmap = SKBitmap.FromImage(image);
                    var imageBounds = canvas.ImageBounds;
                    var scale = current.Viewport.Scale;
                    var regions = Measure(bitmap, projection, imageBounds.X, imageBounds.Y, imageBounds.Width, imageBounds.Height,
                        scale, build == null, palette, solid, replaced, navigation);
                    using var png = image.Encode(SKEncodedImageFormat.Png, 100);
                    using (var fileStream = File.Create(file)) png.SaveTo(fileStream);
                    completion.TrySetResult(new(name, build?.BuildId, pid, surface.Identity.Generation, surface.Generation, frame.Metadata.FrameId,
                        frame.Metadata.Tick, layout, surface.Width, surface.Height, scale, previewStamp, gameStamp, FileHash(file), keys, regions));
                }
                catch (Exception error) { completion.TrySetException(error); }
            }
            window.FramePresented += Presented;
            try
            {
                window.RequestFrame();
                var frame = await completion.Task.WaitAsync(token);
                frames.Add(frame);
                return frame;
            }
            finally { window.FramePresented -= Presented; }
        }
        void ObserveProcess(ulong generation, int pid)
        {
            if (processes.TryGetValue(generation, out var observed))
            {
                Require(observed.Id == pid, "An owned runtime generation changed PID.");
                return;
            }
            var process = Process.GetProcessById(pid);
            try { _ = process.Handle; processes.Add(generation, process); }
            catch { process.Dispose(); throw; }
        }
        async Task CheckDrain()
        {
            Require(engine.Supervisor.Status.State == SessionState.Stopped && engine.Frames.Presentation == null && engine.Preview.PresentedView == null &&
                engine.Gpu.ResourceCount == 0 && engine.Gpu.OpenHandleCount == 0, "Stop/Build/close retained a viewport receipt, surface or GPU resource.");
            foreach (var lease in native.Leases.Where(lease => children.All(child => child.Generation != lease.Identity.Generation)))
            {
                var generation = lease.Identity.Generation;
                Require(lease.Reaped && lease.ResourcesReleased && processes.ContainsKey(generation), "Unobserved or un-reaped owned process.");
                var process = processes[generation];
                await process.WaitForExitAsync(token);
                using var json = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, $"runtime-{generation}.json"), token));
                var child = json.RootElement;
                var validation = child.GetProperty("Validation");
                var role = child.GetProperty("Role").GetString();
                var buildId = child.GetProperty("BuildId").GetString();
                var isPreview = lease.Identity.GameBuildId == BuildIdentity.PreviewId;
                Require(process.ExitCode == 0 && child.GetProperty("Pid").GetInt32() == process.Id && process.Id == lease.ProcessId &&
                    role == (isPreview ? "EditPreview" : "Game") && buildId == (isPreview ? null : lease.Identity.GameBuildId) &&
                    child.GetProperty("DeliveryGameLoaded").GetBoolean() == !isPreview &&
                    child.GetProperty("Resources").GetInt32() == 0 && child.GetProperty("Handles").GetInt32() == 0 &&
                    validation.GetProperty("ValidationEnabled").GetBoolean() && validation.GetProperty("Errors").GetInt32() == 0 &&
                    validation.GetProperty("Warnings").GetInt32() == 0, "Child role, game load, exit, validation or resource report failed.");
                var texture = frames.FirstOrDefault(f => f.Runtime == generation)?.Keys?.Texture;
                if (texture != null)
                {
                    var uploads = validation.GetProperty("MeshResources").EnumerateArray().Where(row => row.GetProperty("TextureUploads").GetInt32() != 0).ToArray();
                    Require(uploads.Length > 0 && uploads.All(row =>
                        row.GetProperty("LastTextureRevision").GetString() == texture.Revision &&
                        row.GetProperty("LiveUploads").GetInt32() == 0 &&
                        row.GetProperty("TextureImagesCreated").GetInt32() == row.GetProperty("TextureImagesReleased").GetInt32() &&
                        row.GetProperty("TextureSamplersCreated").GetInt32() == row.GetProperty("TextureSamplersReleased").GetInt32()),
                        "Retired GPU rendering used a different exact texture or retained images, samplers or uploads.");
                }
                children.Add(new(generation, process.Id, role!, buildId, process.ExitCode, lease.Reaped, lease.ResourcesReleased));
            }
        }
        async Task Finish()
        {
            await Doc(DocumentCommand.Close);
            await CheckDrain();
            window.Close();
            await window.Completion.WaitAsync(token);
            await CheckDrain();
        }
        PassProof Proof(Guid mesh, Guid left, Guid right, Guid texture, Guid textured, Guid solid) => new(Environment.ProcessId,
            FileHash(scenePath), FileHash(pinned), mesh, left, right, texture, textured, solid, noBuildBeforePreview, withheldDrained,
            sourceOrder, frames.ToArray(), clicks.ToArray(), children.ToArray());
        Task Write(string file, object proof) => File.WriteAllTextAsync(Path.Combine(output, file),
            JsonSerializer.Serialize(proof, new JsonSerializerOptions { WriteIndented = true }), token);
    }

    private static async Task<SourceOrderProof> ReverseImportedSlots(ModelImportLedgerSession ledger, CancellationToken token)
    {
        // The stock Assimp importer canonicalizes ranges by subasset label. Preserve its actual geometry,
        // UVs, material/texture bytes and capture provenance, but exercise a reordered published head.
        var imported = await ledger.OpenAsync(token) ?? throw new InvalidDataException("Imported replacement ledger is missing.");
        var candidate = imported.Candidate;
        var mesh = candidate.Meshes.Single();
        var geometry = mesh.Geometry;
        Require(geometry.Slots.Length == 2 && mesh.SlotMaterials.Length == 2 && !geometry.Uv0.IsEmpty,
            "The real B import did not yield two UV-capable material ranges.");
        var first = geometry.Slots[0];
        var second = geometry.Slots[1];
        var indices = new uint[geometry.Indices.Length];
        geometry.Indices.AsSpan((int)second.FirstIndex, (int)second.IndexCount).CopyTo(indices);
        geometry.Indices.AsSpan((int)first.FirstIndex, (int)first.IndexCount).CopyTo(indices.AsSpan((int)second.IndexCount));
        MeshSlot[] ranges = [new(first.SlotId, 0, second.IndexCount), new(second.SlotId, second.IndexCount, first.IndexCount)];
        var reordered = new ModelImportMesh(new MeshDefinition(geometry.Vertices, geometry.Uv0, indices, ranges, geometry.Bounds),
            mesh.SlotMaterials[1].MaterialIndex) { SlotMaterials = [mesh.SlotMaterials[1], mesh.SlotMaterials[0]] };
        var fixture = new ModelImportCandidate(candidate.SourceId, candidate.InputRevision, [reordered], candidate.Materials,
            candidate.Instances, candidate.Semantics, candidate.GeometryScale, candidate.Texture);
        var published = await ledger.PublishAsync(fixture, imported.Generation, cancellationToken: token);
        Require(published.Generation == imported.Generation + 1 && published.Candidate.InputRevision == candidate.InputRevision &&
            published.Candidate.Revision != candidate.Revision && published.Meshes.Single().AssetId == imported.Meshes.Single().AssetId &&
            published.Meshes.Single().Slots.SequenceEqual(imported.Meshes.Single().Slots.Reverse()),
            "The real B candidate was not republished with the same published mesh/slot identities in reverse order.");
        return new(candidate.SourceId, imported.Meshes.Single().AssetId, candidate.InputRevision, imported.Generation, published.Generation,
            candidate.Revision, published.Candidate.Revision, mesh.SlotMaterials.Select(slot => slot.Locator).ToArray(),
            reordered.SlotMaterials.Select(slot => slot.Locator).ToArray());
    }

    private static void VerifyArtifact(RenderContentManifest manifest, ExactKeys keys)
    {
        var mesh = manifest.Resolve(keys.Mesh.Id, RenderContentProfile.MeshKindId).Key;
        var texture = manifest.Resolve(keys.Texture.Id, RenderContentProfile.BaseColorTextureKindId).Key;
        Require(manifest.Entries.Length == 7 && Key.From(mesh) == keys.Mesh && Key.From(texture) == keys.Texture,
            "Published Build contains a different mesh/texture or unrelated contents.");
        Check(keys.First, texture);
        Check(keys.Second, null);

        void Check(BindingKeys expected, RenderAssetKey? expectedTexture)
        {
            var entry = manifest.Resolve(expected.Binding.Id, RenderContentProfile.BindingSetKindId);
            var set = (MaterialBindingSetDefinition)entry.Definition;
            Require(Key.From(entry.Key) == expected.Binding && set.Mesh == mesh && set.Bindings.Length == 2,
                "Published Build does not match the materialized binding/mesh closure.");
            var left = set.Bindings.Single(slot => slot.SlotId == keys.LeftSlot).Material;
            var right = set.Bindings.Single(slot => slot.SlotId == keys.RightSlot).Material;
            var leftDef = (OpaqueUnlitMaterialDefinition)manifest.Resolve(left).Definition;
            var rightDef = (OpaqueUnlitMaterialDefinition)manifest.Resolve(right).Definition;
            Require(Key.From(left) == expected.Left && Key.From(right) == expected.Right && leftDef.BaseColorTexture == expectedTexture &&
                (expectedTexture == null || leftDef.Sampler == RenderTextureSampler.NearestClamp) &&
                rightDef.BaseColorSrgb == Vector3.UnitY && rightDef.BaseColorTexture == null, "Published slot assignment lost the exact PNG/green default.");
        }
    }

    private static Region[] Measure(SKBitmap bitmap, CameraProjection projection, float x, float y, float width, float height, float scale,
        bool preview, uint[]? palette, uint solid, bool replaced, bool navigation)
    {
        var regions = new List<Region>();
        var background = Sample("clear", new(0, 1, 0));
        if (preview && !navigation)
        {
            Sample("front-cube", new(-.93f, 1.55f, .68f), 0xFFFF00FF);
            Sample("control-cube", new(0, .27f, .2f), 0x00FFFFFF);
        }
        if (palette != null)
        {
            Sample("first-right", new(.61f, 1.38f, 0), Green);
            Sample("second-right", new(.61f, -.08f, 0), Green);
            Sample("second-left", new(-.64f, .62f, 0), solid);
            if (!navigation)
            {
                Sample("texture-0", new(-1.28f, 2.09f, 0), palette[0]);
                Sample("texture-1", new(-.64f, 2.09f, 0), palette[1]);
                Sample("texture-2", new(-1.28f, 1.35f, 0), palette[2]);
                Sample("texture-3", new(-.64f, 1.35f, 0), palette[3]);
                Sample("first-footprint-left", new(-1.47f, 2.09f, 0), replaced ? background : palette[0]);
                Sample("second-footprint-left", new(-1.47f, .63f, 0), replaced ? background : solid);
                Sample("first-footprint-right", new(1.34f, 1.16f, 0), replaced ? background : Green);
                Sample("second-footprint-right", new(1.34f, -.30f, 0), replaced ? background : Green);
                // This center under the foreground cube is an actual mesh texel in Play, where no legacy cubes are drawn.
                if (!preview) Sample("behind-front-cube", new(-1.05f, 1.62f, 0), palette[2]);
            }
        }
        return regions.ToArray();

        uint Sample(string name, Vector3 point, uint? expected = null)
        {
            Require(projection.TryProject(point, out var ndc), $"Could not project {name}.");
            var pixelX = (int)((x + (ndc.X + 1) * width / 2) * scale);
            var pixelY = (int)((y + (ndc.Y + 1) * height / 2) * scale);
            Require(pixelX > 0 && pixelY > 0 && pixelX < bitmap.Width - 1 && pixelY < bitmap.Height - 1, $"{name} is outside native readback.");
            Span<byte> samples = stackalloc byte[36];
            uint first = 0;
            var offset = 0;
            for (var row = pixelY - 1; row <= pixelY + 1; row++)
                for (var column = pixelX - 1; column <= pixelX + 1; column++)
                {
                    var pixel = bitmap.GetPixel(column, row);
                    var rgba = (uint)pixel.Red << 24 | (uint)pixel.Green << 16 | (uint)pixel.Blue << 8 | pixel.Alpha;
                    if (offset == 0) first = rgba;
                    Require(rgba == (expected ?? first), $"{name} ({column},{row}): expected {(expected ?? first):X8}, drew {rgba:X8}.");
                    samples[offset++] = pixel.Red; samples[offset++] = pixel.Green; samples[offset++] = pixel.Blue; samples[offset++] = pixel.Alpha;
                }
            regions.Add(new(name, pixelX, pixelY, first.ToString("X8"), Convert.ToHexString(SHA256.HashData(samples))));
            return first;
        }
    }

    private static Guid FixtureId(int number, byte kind) => new(number, 0x792e, 0x42b0, 0xab, 0x3f, 0x9a, 0xd2, 0x6c, 0xa7, 0xa3, kind);
    private static bool Revised(Key a, Key b) => a.Id == b.Id && a != b;
    private static void SameKeys(ExactKeys a, ExactKeys b) => Require(a == b, "Exact geometry/material/texture/bindings differ.");
    private static string Color(FrameProof frame, string region) => frame.Regions.Single(item => item.Name == region).Rgba;
    private static void SameColors(FrameProof a, FrameProof b, params string[] names)
    {
        foreach (var name in names.Length == 0 ? a.Regions.Select(r => r.Name) : names)
            Require(a.Regions.Single(r => r.Name == name).Sha256 == b.Regions.Single(r => r.Name == name).Sha256,
                $"{a.Name}/{b.Name}: {name} changed.");
    }
    private static void ChangedIntent(FrameProof a, FrameProof b) => Require(a.PreviewStamp is { } old && b.PreviewStamp is { } current &&
        old.ContentId != current.ContentId && old.SceneId == current.SceneId && old.DocumentEpoch == current.DocumentEpoch &&
        old.SceneRevision == current.SceneRevision && old.CameraRevision == current.CameraRevision && a.Runtime != b.Runtime,
        "A reachable exact asset edit did not advance ContentId/runtime independently of scene and camera.");
    private static void SameChild(FrameProof a, FrameProof b) => Require(a.Pid == b.Pid && a.Runtime == b.Runtime &&
        a.PreviewStamp!.ContentId == b.PreviewStamp!.ContentId && (a.Frame != b.Frame || a.Surface != b.Surface),
        "Camera/resize replaced the child/content or retained the old frame.");
    private static string FileHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed record Key(Guid Id, string Revision)
    {
        public static Key From(RenderAssetKey key) => new(key.AssetId, key.Revision.ToString());
    }
    private sealed record BindingKeys(Key Binding, Key Left, Key Right);
    private sealed record ExactKeys(Key Mesh, Key Texture, Guid LeftSlot, Guid RightSlot, BindingKeys First, BindingKeys Second);
    private sealed record Region(string Name, int X, int Y, string Rgba, string Sha256);
    private sealed record FrameProof(string Name, string? BuildId, int Pid, ulong Runtime, ulong Surface, ulong Frame, ulong Tick, ulong Layout,
        int Width, int Height, float Scale, PreviewFrameStamp? PreviewStamp, RenderSnapshotStamp? GameStamp, string PngSha256, ExactKeys? Keys, Region[] Regions);
    private sealed record ClickProof(string Frame, string Region, Guid? SelectedEntity, Guid ContentId, ulong FrameId);
    private sealed record ChildProof(ulong Generation, int Pid, string Role, string? BuildId, int ExitCode, bool Reaped, bool Released);
    private sealed record SourceOrderProof(Guid SourceId, Guid PublishedMeshId, string InputRevision, long ImportedGeneration, long ReorderedGeneration,
        string ImportedCandidateRevision, string ReorderedCandidateRevision, string[] ImportedSlots, string[] ReorderedSlots);
    private sealed record PassProof(int EditorPid, string SceneSha256, string PinnedSha256, Guid Mesh, Guid Left, Guid Right, Guid Texture,
        Guid Textured, Guid Solid, bool NoBuildBeforePreview, bool WithheldPresentDrained, SourceOrderProof? ReplacementSource,
        FrameProof[] Frames, ClickProof[] Clicks, ChildProof[] Children);
}
