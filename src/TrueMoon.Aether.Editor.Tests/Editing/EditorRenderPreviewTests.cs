using System.Numerics;
using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Menus;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Editor.Tooling;
using TrueMoon.Aether.Editor.Tooling.Editing;
using TrueMoon.Aether.Editor.UI.Documents;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime.Preview;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Tooling.Documents;
using TrueMoon.Aether.Tooling.Preview;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Editing;

public sealed partial class EditorRenderPreviewTests
{
    [Fact]
    public void SameSceneRevisionUsesNewContentIdOnlyWhenReachableExactKeysChange()
    {
        using var assets = new PreviewAssets();
        using var lane = new CapturedLane(assets);
        lane.Inspector.ShowPreview();
        var first = lane.Complete();
        var material = lane.Inspector.Materials;
        material.Open(assets.Paint.Id);
        material.Red = "0.25";
        Assert.True(material.HasDraft);
        lane.Refresh();
        Assert.Same(first.Receipt, lane.Complete().Receipt);

        material.Save();
        assets.Owner.Complete();
        Assert.Same(first.Receipt, lane.Complete().Receipt); // IO receipt is not published until owner refresh.
        lane.Refresh();
        var painted = lane.Complete();
        Assert.NotEqual(first.Source.Digest, painted.Source.Digest);
        Assert.NotEqual(first.Context.ContentId, painted.Context.ContentId);
        material.Undo(); assets.Owner.Complete(); lane.Refresh();
        var undone = lane.Complete();
        Assert.Equal(first.Source.Digest, undone.Source.Digest);
        Assert.NotEqual(painted.Context.ContentId, undone.Context.ContentId);
        Assert.NotEqual(first.Context.ContentId, undone.Context.ContentId);
        material.Redo(); assets.Owner.Complete(); lane.Refresh();
        var redone = lane.Complete();
        Assert.Equal(painted.Source.Digest, redone.Source.Digest);
        Assert.NotEqual(undone.Context.ContentId, redone.Context.ContentId);

        var held = lane.HoldPreparation();
        assets.ReplaceTexture("second.png");
        lane.Refresh();
        try
        {
            DocumentUiFixture.Wait(held.Entered.Task);
            Assert.Null(lane.Desired);
            Assert.Same(redone.Receipt, lane.Preview.Read().Presented);
            Assert.Equal(4, lane.Starts.Count);
        }
        finally { held.Release.TrySetResult(); }
        var textured = lane.Complete();
        Assert.NotEqual(redone.Context.ContentId, textured.Context.ContentId);
        Assert.NotEqual(redone.Source.Digest, textured.Source.Digest);

        assets.RenameTexture(); lane.Refresh();
        Assert.Same(textured.Receipt, lane.Complete().Receipt);
        assets.ReplaceTexture("same.png"); lane.Refresh();
        Assert.Same(textured.Receipt, lane.Complete().Receipt);
        assets.ImportUnused(); lane.Refresh();
        Assert.Same(textured.Receipt, lane.Complete().Receipt);
        assets.WritePng("same.png", PreviewAssets.PaletteA); lane.Refresh();
        Assert.Same(textured.Receipt, lane.Complete().Receipt); // Editing the source file does not commit pixels.
        Ray3 ray = new(new(1.5f, .25f, 6), -Vector3.UnitZ, 10);
        var texturedPicks = ScenePreview.CaptureRenderPicks(textured.Source.Render!);
        Assert.Equal(PreviewPickKind.Miss, texturedPicks.Pick(ray).Kind);

        assets.ReplaceMesh(); lane.Refresh();
        var geometry = lane.Complete();
        Assert.NotEqual(textured.Context.ContentId, geometry.Context.ContentId);
        Assert.NotEqual(textured.Source.Digest, geometry.Source.Digest);
        var geometryPicks = ScenePreview.CaptureRenderPicks(geometry.Source.Render!);
        var hit = geometryPicks.Pick(ray);
        Assert.Equal(PreviewPickKind.Hit, hit.Kind);
        Assert.Equal(new EntityId(DocumentData.Id(200)), hit.Entity);
        Assert.Equal(6d, hit.Distance);
        Assert.Equal(texturedPicks.Revision, geometryPicks.Revision);
        Assert.Equal(PreviewPickKind.Miss, texturedPicks.Pick(ray).Kind);
        Assert.Equal(6, lane.Starts.Count);
        Assert.All(lane.Starts, entry =>
        {
            Assert.Equal(first.Context.SceneRevision, entry.Context.SceneRevision);
            Assert.Equal(first.Context.DocumentEpoch, entry.Context.DocumentEpoch);
            Assert.Equal(first.Context.ViewId, entry.Context.ViewId);
        });
        Assert.Same(first.Source.Source.State, assets.Owner.Document.Read().Content);
    }

    [Fact]
    public void PreviewUsesCommittedPayloadWithoutSourceReadsAndDrainsOnStop()
    {
        using var assets = new PreviewAssets();
        using var lane = new CapturedLane(assets);
        File.Delete(Path.Combine(assets.Owner.Files.Root, "first.obj"));
        File.Delete(Path.Combine(assets.Owner.Files.Root, "first.png"));
        lane.Inspector.ShowPreview();
        var first = lane.Complete();
        var held = lane.HoldPreparation();
        assets.ReplaceTexture("second.png"); lane.Refresh();
        try
        {
            DocumentUiFixture.Wait(held.Entered.Task);
            assets.ReplaceMesh(); lane.Refresh();
            assets.RenameTexture(); lane.Refresh();
            Assert.Null(lane.Desired);
            Assert.Same(first.Receipt, lane.Preview.Read().Presented);
            Assert.Single(lane.Starts);
        }
        finally { held.Release.TrySetResult(); }
        var latest = lane.Complete();
        Assert.Equal(2, lane.Starts.Count);
        Assert.Equal(3, lane.Preparations);
        Assert.Equal(assets.Textures.Textures.Single(row => row.Id == assets.TextureId).Revision,
            Assert.Single(latest.Source.Render!.Content.AssetKeys, key => key.AssetId == assets.TextureId).Revision);
        Assert.NotEqual(first.Context.ContentId, latest.Context.ContentId);

        var blob = assets.UsedTextureBlob;
        var bytes = File.ReadAllBytes(blob);
        var malformed = (byte[])bytes.Clone(); malformed[^1] ^= 1;
        File.WriteAllBytes(blob, malformed);
        assets.RenameTexture(); lane.Refresh();
        DocumentUiFixture.Wait(lane.Preview.Pending);
        Assert.NotNull(lane.Preview.Read().Error);
        Assert.Contains(assets.TextureId.ToString("D"), lane.Preview.Read().Error!.Message);
        Assert.Same(latest.Receipt, lane.Preview.Read().Presented);
        Assert.Null(lane.Desired);
        lane.Refresh(); DocumentUiFixture.Wait(lane.Preview.Pending);
        Assert.Equal(4, lane.Preparations); // Bad committed input does not retry at frame cadence.
        File.WriteAllBytes(blob, bytes);
        lane.Inspector.ShowPreview();
        Assert.Same(latest.Receipt, lane.Complete().Receipt); // Explicit retry proves the same live payload.

        assets.ReplaceTexture("third.png");
        var transfer = lane.HoldPresentation();
        lane.Refresh();
        try
        {
            DocumentUiFixture.Wait(transfer.Entered.Task);
            Assert.Equal(3, lane.Starts.Count);
            Assert.Same(latest.Receipt, lane.Preview.Read().Presented);
            Assert.True(assets.Owner.Commands.Execute(DocumentCommand.Close));
            DocumentUiFixture.Wait(transfer.Cancelled.Task);
            Assert.Null(lane.Desired);
            Assert.Null(lane.Preview.Read().Presented);
            Assert.False(assets.Owner.Commands.Pending.IsCompleted);
            Assert.Equal(0, lane.Stops);
        }
        finally { transfer.Release.TrySetResult(); }
        assets.Owner.Complete();
        Assert.Null(assets.Owner.Document.Read().Content);
        Assert.False(lane.Preview.Read().Active);
        Assert.Null(lane.Preview.Read().Presented); // Late acknowledgement did not restore the closed opening.
        Assert.Equal(1, lane.Stops);
    }

    [Theory]
    [InlineData(true, 1u)]
    [InlineData(false, 1u)]
    [InlineData(true, 0u)]
    public void Click_SelectsVisibleMeshAndPreservesCubeAndBackgroundRouting(bool enabled, uint layers)
    {
        using var t = new ViewportInputFixture(wrap: true, meshEnabled: enabled, meshLayers: layers);
        t.Start();
        var receipt = t.Preview.PresentedView!;
        Assert.NotEqual(t.Workspace.ContentToken, receipt.State.Context.ContentId);
        t.Model.Select(DocumentData.Parent.Value.ToString("D"));
        ClickAndCheck(t.Point(new Vector3(0, 1.2f, 0)), enabled && layers != 0 ? ViewportInputFixture.MeshEntity : null);
        ClickAndCheck(t.Point(DocumentData.Child), DocumentData.Child);
        ClickAndCheck(t.Point(new Vector3(0, 2, 0)), null);
        Assert.Equal(0, t.Content.UndoCount);
        Assert.False(t.Owner.Document.Read().IsDirty);

        var foreign = new PreviewViewState(receipt.State.Context with { ContentId = Guid.NewGuid() }, receipt.State.Camera);
        Assert.False(t.Controller.MatchesPreview(foreign.Stamp));
        Assert.False(t.DrawAndAck(t.Issue(foreign)));
        Assert.Same(receipt, t.Preview.PresentedView);
        ((OverlayHost)t.Ui.Root).Items.Remove(t.View);
        DocumentUiFixture.Wait(t.Coordinator.Pending);
        Assert.False(t.Controller.IsActive);
        Assert.Null(t.Coordinator.Read().Presented);
        Assert.True(Assert.Single(t.Runtime.Leases).ResourcesReleased);

        void ClickAndCheck((float X, float Y) point, EntityId? expected)
        {
            Assert.True(t.Input(InputKind.PointerDown, point.X, point.Y).PointerCaptured);
            var release = t.Input(InputKind.PointerUp, point.X, point.Y);
            Assert.True(release.Handled);
            Assert.False(release.PointerCaptured);
            Assert.Equal(expected?.Value.ToString("D"), t.Workspace.SelectedId);
            Assert.Equal(expected, t.Inspector.Selection.EntityId);
        }
    }

    [Fact]
    public void Click_MeshCannotPublishAfterPresentedContentBecomesStale()
    {
        using var t = new ViewportInputFixture(wrap: true, meshEnabled: true);
        t.Start();
        var receipt = t.Preview.PresentedView!;
        var point = t.Point(new Vector3(0, 1, 0));
        var pick = receipt.Picks.Pick(receipt.Projection, receipt.ImageBounds, point.X, point.Y);
        Assert.Equal(PreviewPickKind.Hit, pick.Kind);
        Assert.Equal(ViewportInputFixture.MeshEntity, pick.Entity);
        var parent = DocumentData.Parent.Value.ToString("D");
        t.Model.Select(parent);
        var selection = t.Workspace.SelectionRevision;

        Assert.True(t.Input(InputKind.PointerDown, point.X, point.Y).PointerCaptured);
        Assert.True(t.Controller.IsGesturing);
        Assert.True(t.State.SetDesiredSource(receipt.State.Context with { ContentId = Guid.NewGuid() }));
        Assert.False(t.Input(InputKind.PointerUp, point.X, point.Y).PointerCaptured);
        Assert.False(t.Controller.IsGesturing);
        Assert.Same(receipt, t.Preview.PresentedView);
        Assert.Equal(parent, t.Workspace.SelectedId);
        Assert.Equal(DocumentData.Parent, t.Inspector.Selection.EntityId);
        Assert.Equal(selection, t.Workspace.SelectionRevision);
    }

    [Fact]
    public void SameRevisionReceiptWithForeignContentIdIsRejected()
    {
        using var assets = new PreviewAssets();
        using var lane = new CapturedLane(assets);
        lane.Inspector.ShowPreview();
        var first = lane.Complete();
        lane.ForeignReceipt = true;
        assets.ReplaceTexture("second.png");
        lane.Refresh();
        DocumentUiFixture.Wait(lane.Preview.Pending);
        Assert.IsType<InvalidOperationException>(lane.Preview.Read().Error);
        Assert.Same(first.Receipt, lane.Preview.Read().Presented);
        Assert.Null(lane.Desired);
        Assert.Equal(2, lane.Starts.Count);
        Assert.Equal(first.Context.SceneRevision, lane.Starts[1].Receipt.Revision);
        Assert.NotEqual(lane.Starts[1].Context.ContentId, lane.Starts[1].Receipt.ViewContext!.ContentId);
    }

    private static T WaitFor<T>(Task<T> task) => task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();

    private sealed class PreviewAssets : IDisposable
    {
        internal static readonly byte[] PaletteA = Convert.FromHexString("D51006FFE40911FFF2160BFFFC0316FF");
        private static readonly byte[] PaletteB = Convert.FromHexString("2194D5FF7B730CFF304D90FF6D18AAFF");
        internal readonly DocumentUiFixture Owner;
        internal readonly ProjectMaterial Paint;
        internal readonly Guid TextureId;
        internal ProjectTextureCatalogSnapshot Textures;
        private readonly RegisteredMeshSourceFixture _sources;
        private readonly ProjectTextureCatalog _textureCatalog;
        private readonly ProjectMeshCatalog _meshCatalog;
        private readonly MeshSourceChoice _replacement;
        private readonly Guid _meshId;
        private ProjectMeshCatalogSnapshot _meshes;
        private int _renames;
        internal Func<Task>? StopPreview;

        internal PreviewAssets()
        {
            Owner = new(materialCatalog: true, createTextureCatalog: project => new(project, AetherEditorConfigurationExtensions.DecodeProjectTexture),
                createMeshCatalog: project => new(project), stopPreview: () => StopPreview?.Invoke() ?? Task.CompletedTask);
            var project = Owner.Files.Project;
            _sources = new(project);
            var source = WaitFor(_sources.AddAsync("first.obj"));
            _replacement = WaitFor(_sources.AddAsync("reversed.obj", reverseSlots: true));
            _meshCatalog = new(project, _sources.Models);
            _meshes = WaitFor(_meshCatalog.CreateAsync(WaitFor(_meshCatalog.ReadAsync(default)), source, "Panel", default));
            var mesh = Assert.Single(_meshes.Meshes);
            _meshId = mesh.Id;
            _textureCatalog = Owner.Document.Textures;
            WritePng("first.png", PaletteA);
            WritePng("second.png", PaletteB);
            WritePng("same.png", PaletteB);
            WritePng("third.png", PaletteA);
            Textures = WaitFor(_textureCatalog.ImportAsync(WaitFor(_textureCatalog.ReadAsync(default)), "Tiles", "first.png", default));
            TextureId = Assert.Single(Textures.Textures).Id;
            var materials = new ProjectMaterialCatalog(project);
            Paint = new(DocumentData.Id(401), "Paint", Vector3.One, TextureId);
            var materialSnapshot = WaitFor(materials.WriteAsync(WaitFor(materials.ReadAsync(default)), Paint, default));
            var picker = SceneRenderAssets.Capture(project.Description.Id, null, materialSnapshot,
                textures: Textures, textureCatalog: _textureCatalog, meshes: _meshes, meshCatalog: _meshCatalog)!;
            var binding = Assert.IsType<Guid>(picker.BindingForMaterial(mesh.Id, mesh.Slots[0].Id, Paint.Id));
            Owner.Store.File = new(SceneMaterialAuthoringTests.Scene((mesh.Id, binding.ToString("D"))), new("render preview"));
            Owner.Run(DocumentCommand.Open, "preview.aetherscene");
            Owner.Document.PublishMaterialCatalog(materialSnapshot);
            Owner.Document.PublishTextureCatalog(Textures);
            Owner.Document.PublishMeshCatalog(_meshes);
        }

        internal string UsedTextureBlob => Path.Combine(Owner.Files.Project.ContentDirectory, ".aether-texture-data",
            Convert.ToHexString(Textures.Textures.Single(row => row.Id == TextureId).Revision.ToArray()) + ".tmra");
        internal void WritePng(string path, byte[] rgba) => File.WriteAllBytes(Path.Combine(Owner.Files.Root, path), ProjectTextureCatalogTests.TexturePng(rgba));
        internal void ReplaceTexture(string path) => Owner.Document.PublishTextureCatalog(Textures =
            WaitFor(_textureCatalog.ReplaceSourceAsync(Textures, TextureId, path, default)));
        internal void RenameTexture() => Owner.Document.PublishTextureCatalog(Textures =
            WaitFor(_textureCatalog.RenameAsync(Textures, TextureId, $"Tiles {(++_renames)}", default)));
        internal void ImportUnused() => Owner.Document.PublishTextureCatalog(Textures =
            WaitFor(_textureCatalog.ImportAsync(Textures, "Unused", "third.png", default)));
        internal void ReplaceMesh() => Owner.Document.PublishMeshCatalog(_meshes =
            WaitFor(_meshCatalog.ReplaceGeometryAsync(_meshes, _meshId, _replacement, default)));
        public void Dispose() { DocumentUiFixture.Wait(_sources.DisposeAsync().AsTask()); Owner.Dispose(); }
    }

    private sealed class CapturedLane : IDisposable
    {
        internal sealed record Start(PreparedScenePreview Source, PreviewViewContext Context, ScenePreviewDocument Receipt);
        internal sealed class Barrier
        {
            internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal readonly TaskCompletionSource Cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal async Task WaitAsync(CancellationToken token)
            {
                using var registration = token.Register(() => Cancelled.TrySetResult());
                Entered.TrySetResult(); await Release.Task.ConfigureAwait(false);
            }
        }

        private volatile Barrier? _prepare, _present;
        private Start? _live;
        internal readonly EditorInspectorViewModel Inspector;
        internal readonly EditorPreview Preview;
        internal readonly List<Start> Starts = [];
        internal volatile PreviewViewContext? Desired;
        internal int Stops, Preparations;
        internal bool ForeignReceipt;

        internal CapturedLane(PreviewAssets assets) : this(assets.Owner) => assets.StopPreview = Preview.StopAsync;

        internal CapturedLane(DocumentUiFixture owner)
        {
            var viewId = Guid.NewGuid(); var workspace = new EditorWorkspace(new("preview", 0, []));
            workspace.SetProject(owner.Projects.Active);
            var thread = Environment.CurrentManagedThreadId;
            Preview = new(scene =>
            {
                Assert.Equal(thread, Environment.CurrentManagedThreadId);
                return new(new(workspace.DocumentEpoch, viewId, workspace.ContentToken, scene.Id.Value, scene.Revision), PreviewCamera.Default);
            }, context => { Desired = context; return true; },
                (receipt, context) => _live is { } live && ReferenceEquals(live.Receipt, receipt) && live.Context == context,
                async (source, view, token) =>
                {
                    var returnedContext = ForeignReceipt ? view.Context with { ContentId = Guid.NewGuid() } : view.Context;
                    var receipt = EditingFixture.Receipt(source.Source.Scene) with { ViewContext = returnedContext };
                    var start = new Start(source, view.Context, receipt);
                    Starts.Add(start);
                    if (Interlocked.Exchange(ref _present, null) is { } barrier) await barrier.WaitAsync(token).ConfigureAwait(false);
                    _live = start;
                    return receipt;
                }, () => { Stops++; _live = null; return Task.CompletedTask; },
                async (source, token) =>
                {
                    Preparations++;
                    var prepared = await source.PrepareAsync(token).ConfigureAwait(false);
                    if (Interlocked.Exchange(ref _prepare, null) is { } barrier) await barrier.WaitAsync(token).ConfigureAwait(false);
                    return prepared;
                });
            Inspector = new(owner.Commands, Preview, workspace);
            Refresh();
        }

        internal Barrier HoldPreparation() => _prepare = new();
        internal Barrier HoldPresentation() => _present = new();
        internal void Refresh() => Inspector.Refresh(DocumentData.Id(200).ToString("D"));
        internal Start Complete()
        {
            DocumentUiFixture.Wait(Preview.Pending);
            var state = Preview.Read();
            Assert.Null(state.Error);
            Assert.NotNull(state.Presented);
            var start = Assert.Single(Starts, entry => ReferenceEquals(entry.Receipt, state.Presented));
            Assert.Equal(start.Context, Desired);
            Assert.NotNull(start.Source.Render);
            return start;
        }
        public void Dispose() { Inspector.Dispose(); Preview.Dispose(); }
    }
}
