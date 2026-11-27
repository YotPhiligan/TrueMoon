using System.Numerics;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Runtime.Ecs.Rendering;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Tooling.Documents;
using TrueMoon.Aether.Tooling.Projects;
using TrueMoon.Aether.Tooling.Scenes;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Documents;

public sealed class ProjectMeshCatalogTests
{
    [Fact]
    public async Task NamedMeshReopensWithoutSelectedImportOrLiveSource()
    {
        using var files = new DocumentFiles();
        var project = files.Project;
        ProjectMesh saved;
        {
            await using var sources = new RegisteredMeshSourceFixture(project);
            var selector = await sources.AddAsync("models/panel.gltf");
            var writer = new ProjectMeshCatalog(project, sources.Models);
            var choice = Assert.Single(await writer.ReadSourceAsync(selector.SourceId, default));
            Assert.Equal(selector, choice.Selector);
            Assert.Equal(new RenderBounds(Vector3.Zero, new(1, 1, 0)), choice.Bounds);
            Assert.True(choice.HasUv0);
            Assert.Equal(new[] { "left", "right" }, choice.Slots.ToArray());
            saved = Assert.Single((await writer.CreateAsync(await writer.ReadAsync(default), selector, " Panel ", default)).Meshes);
            Assert.NotEqual(selector.PublishedMeshId, saved.Id);
            Assert.Equal(selector.SourceId, saved.Source.SourceId);
            Assert.Equal(selector.PublishedMeshId, saved.Source.PublishedMeshId);
            Assert.Equal(selector.Generation, saved.Source.Generation);
            Assert.Equal("models/panel.gltf", saved.Source.SourcePath);
            Assert.Equal(selector.CandidateRevision, saved.Source.CandidateRevision);
            Assert.Equal(choice.Key.Revision, saved.Source.MeshRevision);
        }
        File.Delete(Path.Combine(project.RootDirectory, "models", "panel.gltf"));
        File.Delete(Path.Combine(project.ContentDirectory, ".aether-model-catalog.json"));
        var reader = new ProjectMeshCatalog(project);
        Assert.False(reader.CanEdit);
        var reopened = Assert.Single((await reader.ReadAsync(default)).Meshes);
        Assert.Equal(saved.Id, reopened.Id);
        Assert.Equal("Panel", reopened.Name);
        Assert.Equal(saved.DefaultBindingId, reopened.DefaultBindingId);
        Assert.Equal(saved.MeshRevision, reopened.MeshRevision);
        Assert.Equal(saved.DefaultBindingRevision, reopened.DefaultBindingRevision);
        Assert.Equal(new RenderBounds(Vector3.Zero, new(1, 1, 0)), reopened.Bounds);
        Assert.Equal(saved.Slots.ToArray(), reopened.Slots.ToArray());
        Assert.Equal(saved.Source, reopened.Source);
        var document = SceneMaterialAuthoringTests.Scene((saved.Id, saved.DefaultBindingId.ToString("D")));
        await using var session = Session(project, reader);
        Assert.True(await session.NewAsync(document));
        Assert.Empty(session.ReadModelImports(session.Read().OpeningId));
        var build = await Capture(session);
        var mesh = Assert.IsType<MeshDefinition>(Entry(build, saved.Id).Definition);
        Assert.Equal(reopened.Bounds, mesh.Bounds);
        Assert.False(mesh.Uv0.IsEmpty);
        Assert.Equal(saved.MeshRevision, Key(build, saved.Id).Revision);
        Assert.Equal(saved.DefaultBindingRevision, Key(build, saved.DefaultBindingId).Revision);
        Assert.Equal(saved.Slots.Select(slot => slot.Id), mesh.Slots.Select(slot => slot.SlotId));
        Assert.Equal(saved.Slots.Select(slot => new MaterialSlotBinding(slot.Id, Key(build, slot.DefaultMaterialId))), Bindings(build, saved.DefaultBindingId).Bindings.ToArray());
        var texture = Assert.Single(saved.DefaultTextures);
        Assert.Equal(new byte[] { 255, 128, 64, 255 }, Assert.IsType<BaseColorTextureDefinition>(Entry(build, texture.Id).Definition).Pixels.ToArray());
        Assert.Equal(5, build.AssetKeys.Length);
    }

    [Fact]
    public async Task GeometryReplacementPreservesSlotsAcrossSourceOrderChange()
    {
        using var files = new DocumentFiles();
        var project = files.Project;
        await using var sources = new RegisteredMeshSourceFixture(project);
        var first = await sources.AddAsync("first.obj");
        var second = await sources.AddAsync("reversed.obj", reverseSlots: true);
        var catalog = new ProjectMeshCatalog(project, sources.Models);
        var before = await catalog.CreateAsync(await catalog.ReadAsync(default), first, "Panel", default);
        var original = Assert.Single(before.Meshes);
        before = await catalog.CreateAsync(before, first, "Neighbour", default);
        var neighbour = Assert.Single(before.Meshes, mesh => mesh.Id != original.Id);
        var materials = new ProjectMaterialCatalog(project);
        var leftPaint = new ProjectMaterial(DocumentData.Id(560), "Left paint", Vector3.UnitX, null);
        var rightPaint = new ProjectMaterial(DocumentData.Id(561), "Right paint", Vector3.One, null);
        var paints = await materials.WriteAsync(await materials.ReadAsync(default), leftPaint, default);
        paints = await materials.WriteAsync(paints, rightPaint, default);
        var left = original.Slots[0];
        var right = original.Slots[1];
        var picker = SceneRenderAssets.Capture(project.Description.Id, null, paints, meshes: before, meshCatalog: catalog)!;
        var single = Assert.IsType<Guid>(picker.BindingForMaterial(original.Id, left.Id, leftPaint.Id));
        var combined = picker.BindingWithSlotMaterial(original.Id, single.ToString("D"), right.Id, rightPaint.Id);
        Assert.NotNull(combined);
        Assert.StartsWith("slots/1:", combined);
        Assert.True(SceneMaterialBindingReference.TryParse(original.Id, combined, out var combinedId, out _));
        var document = SceneMaterialAuthoringTests.Scene((original.Id, single.ToString("D")), (original.Id, combined),
            (original.Id, original.DefaultBindingId.ToString("D")), (neighbour.Id, neighbour.DefaultBindingId.ToString("D")));
        await using var session = Session(project, catalog, materials);
        Assert.True(await session.NewAsync(document));
        var scene = session.Read().Content;
        var oldBuild = await Capture(session);
        var pinned = MaterialModelFixture.PublishBuild(project, document.Revision, oldBuild);

        var after = await catalog.ReplaceGeometryAsync(before, original.Id, second, default);
        var changed = Assert.Single(after.Meshes, mesh => mesh.Id == original.Id);
        Assert.Equal(original.Name, changed.Name);
        Assert.Equal(original.DefaultBindingId, changed.DefaultBindingId);
        Assert.Equal(new[] { right, left }, changed.Slots.ToArray());
        Assert.Equal(original.DefaultMaterials.OrderBy(row => row.Id), changed.DefaultMaterials.OrderBy(row => row.Id));
        Assert.Equal(original.DefaultTextures.ToArray(), changed.DefaultTextures.ToArray());
        Assert.Equal(second.SourceId, changed.Source.SourceId);
        Assert.Equal(new RenderBounds(Vector3.Zero, new(3, 1, 0)), changed.Bounds);
        Assert.NotEqual(original.MeshRevision, changed.MeshRevision);
        Assert.NotEqual(original.DefaultBindingRevision, changed.DefaultBindingRevision);
        Assert.Same(scene, session.Read().Content);
        var newBuild = await Capture(session);
        Assert.Equal(oldBuild.AssetKeys.Select(key => key.AssetId), newBuild.AssetKeys.Select(key => key.AssetId));
        var changes = new[] { original.Id, original.DefaultBindingId, single, combinedId };
        Assert.Equal(changes.Order(), newBuild.AssetKeys.Where(key => Key(oldBuild, key.AssetId) != key).Select(key => key.AssetId).Order());
        var geometry = Assert.IsType<MeshDefinition>(Entry(newBuild, original.Id).Definition);
        Assert.Equal(new[] { right.Id, left.Id }, geometry.Slots.Select(slot => slot.SlotId));
        Assert.Equal(new Vector3[] { Vector3.Zero, new(3, 0, 0), Vector3.UnitY }, geometry.Vertices.ToArray());
        Assert.Equal(new uint[] { 2, 1, 0, 0, 1, 2 }, geometry.Indices.ToArray());
        AssertBinding(oldBuild, single, (left.Id, leftPaint.Id), (right.Id, right.DefaultMaterialId));
        AssertBinding(newBuild, single, (right.Id, right.DefaultMaterialId), (left.Id, leftPaint.Id));
        AssertBinding(newBuild, combinedId, (right.Id, rightPaint.Id), (left.Id, leftPaint.Id));
        AssertBinding(newBuild, original.DefaultBindingId, (right.Id, right.DefaultMaterialId), (left.Id, left.DefaultMaterialId));
        foreach (var row in original.DefaultTextures)
            Assert.Equal(new byte[] { 255, 128, 64, 255 }, Assert.IsType<BaseColorTextureDefinition>(Entry(newBuild, row.Id).Definition).Pixels.ToArray());
        File.Delete(Blob(project, original));
        var pinnedContent = GameBuildArtifact.Open(pinned.DirectoryPath, pinned.HostContractId, pinned.BuildId).RenderContent!;
        Assert.Equal(oldBuild.AssetKeys.ToArray(), pinnedContent.Entries.Select(entry => entry.Key).ToArray());
        Assert.Equal(Bytes(oldBuild), await File.ReadAllBytesAsync(Path.Combine(pinned.DirectoryPath, GameBuildArtifact.RenderContentFile)));
    }

    [Fact]
    public async Task StaleSourceConflictingCatalogAndIncompatibleGeometryRejectWithoutPublication()
    {
        using var files = new DocumentFiles();
        var project = files.Project;
        await using var sources = new RegisteredMeshSourceFixture(project);
        var initial = await sources.AddAsync("initial.obj");
        var wrongLabel = await sources.AddAsync("different.obj", rightLabel: "middle");
        var wrongCount = await sources.AddSourceAsync("single.obj", rightLabel: null);
        var noUv = await sources.AddAsync("plain.obj", uv: false);
        var catalog = new ProjectMeshCatalog(project, sources.Models);
        var empty = await catalog.ReadAsync(default);
        var saved = await catalog.CreateAsync(empty, initial, "Original", default);
        var mesh = Assert.Single(saved.Meshes);
        var materials = new ProjectMaterialCatalog(project);
        await using var session = Session(project, catalog, materials);
        Assert.True(await session.NewAsync(SceneMaterialAuthoringTests.Scene((mesh.Id, mesh.DefaultBindingId.ToString("D")))));
        var scene = session.Read().Content;
        var before = Bytes(await Capture(session));
        var catalogPath = Path.Combine(project.ContentDirectory, ".aether-mesh-catalog.json");
        var blobPath = Blob(project, mesh);
        var catalogBytes = await File.ReadAllBytesAsync(catalogPath);
        var blobBytes = await File.ReadAllBytesAsync(blobPath);
        var live = await sources.RepublishAsync(initial);
        Assert.Equal(initial.PublishedMeshId, live.PublishedMeshId);
        Assert.NotEqual(initial.Generation, live.Generation);
        Assert.NotEqual(initial.CandidateRevision, live.CandidateRevision);
        await Assert.ThrowsAsync<InvalidDataException>(() => catalog.CreateAsync(saved, initial, "Stale head", default));
        await Assert.ThrowsAsync<InvalidDataException>(() => catalog.ReplaceGeometryAsync(saved, mesh.Id, initial, default));
        await Assert.ThrowsAsync<SceneFileConflictException>(() => catalog.CreateAsync(empty, live, "Stale catalog", default));
        foreach (var incompatible in new[] { wrongLabel, wrongCount, noUv })
            await Assert.ThrowsAsync<InvalidDataException>(() => catalog.ReplaceGeometryAsync(saved, mesh.Id, incompatible, default));
        Assert.Equal(catalogBytes, await File.ReadAllBytesAsync(catalogPath));
        Assert.Equal(blobBytes, await File.ReadAllBytesAsync(blobPath));
        Assert.Equal(before, Bytes(await Capture(session)));

        var collision = new ProjectMaterial(mesh.Id, "Conflicting material", Vector3.One, null);
        var colliding = await materials.WriteAsync(await materials.ReadAsync(default), collision, default);
        var assets = SceneRenderAssets.Capture(project.Description.Id, null, colliding, meshes: saved, meshCatalog: catalog)!;
        Assert.Equal(mesh.Id, assets.Collision?.Id);
        var collisionError = await Assert.ThrowsAsync<InvalidOperationException>(() => Capture(session));
        Assert.Contains(mesh.Id.ToString("D"), collisionError.Message);
        File.Delete(Path.Combine(project.ContentDirectory, ".aether-material-catalog.json"));

        var corrupted = (byte[])blobBytes.Clone();
        corrupted[^1] ^= 1;
        await File.WriteAllBytesAsync(blobPath, corrupted);
        Assert.Equal(mesh.PayloadHash, Assert.Single((await catalog.ReadAsync(default)).Meshes).PayloadHash);
        var blobError = await Assert.ThrowsAsync<InvalidOperationException>(() => Capture(session));
        Assert.Contains(mesh.Id.ToString("D"), blobError.Message);
        Assert.Equal(catalogBytes, await File.ReadAllBytesAsync(catalogPath));
        Assert.Equal(corrupted, await File.ReadAllBytesAsync(blobPath));
        Assert.Same(scene, session.Read().Content);
        await File.WriteAllBytesAsync(blobPath, blobBytes);
        Assert.Equal(before, Bytes(await Capture(session)));
    }

    [Fact]
    public async Task UnusedMeshChangesDoNotChangeBuildPayload()
    {
        using var files = new DocumentFiles();
        var project = files.Project;
        await using var sources = new RegisteredMeshSourceFixture(project);
        var first = await sources.AddAsync("first.obj");
        var reversed = await sources.AddAsync("reversed.obj", reverseSlots: true);
        var equivalent = await sources.AddAsync("equivalent.obj");
        var catalog = new ProjectMeshCatalog(project, sources.Models);
        var snapshot = await catalog.CreateAsync(await catalog.ReadAsync(default), first, "Used", default);
        var used = Assert.Single(snapshot.Meshes);
        snapshot = await catalog.CreateAsync(snapshot, first, "Unused", default);
        var unused = Assert.Single(snapshot.Meshes, mesh => mesh.Id != used.Id);
        await using var session = Session(project, catalog);
        Assert.True(await session.NewAsync(SceneMaterialAuthoringTests.Scene((used.Id, used.DefaultBindingId.ToString("D")))));
        var original = await Capture(session);
        Assert.DoesNotContain(original.AssetKeys, key => key.AssetId == unused.Id);
        snapshot = await catalog.RenameAsync(snapshot, used.Id, "New name", default);
        Assert.Equal(Bytes(original), Bytes(await Capture(session)));
        snapshot = await catalog.ReplaceGeometryAsync(snapshot, used.Id, equivalent, default);
        var provenanceOnly = Assert.Single(snapshot.Meshes, mesh => mesh.Id == used.Id);
        Assert.Equal(equivalent.SourceId, provenanceOnly.Source.SourceId);
        Assert.Equal(used.PayloadHash, provenanceOnly.PayloadHash);
        Assert.Equal(Bytes(original), Bytes(await Capture(session)));
        snapshot = await catalog.ReplaceGeometryAsync(snapshot, unused.Id, reversed, default);
        var replaced = Assert.Single(snapshot.Meshes, mesh => mesh.Id == unused.Id);
        Assert.NotEqual(unused.MeshRevision, replaced.MeshRevision);
        Assert.Equal(Bytes(original), Bytes(await Capture(session)));
        File.Delete(Blob(project, replaced));
        Assert.Equal(Bytes(original), Bytes(await Capture(session)));
    }

    [Fact]
    public async Task PrivateDefaultTextureCanBeUsedWithoutPublishingOwningMesh()
    {
        using var files = new DocumentFiles();
        var project = files.Project;
        await using var sources = new RegisteredMeshSourceFixture(project);
        var source = await sources.AddAsync("painted.obj");
        var meshes = new ProjectMeshCatalog(project, sources.Models);
        var snapshot = await meshes.CreateAsync(await meshes.ReadAsync(default), source, "Visible", default);
        var visible = Assert.Single(snapshot.Meshes);
        snapshot = await meshes.CreateAsync(snapshot, source, "Library", default);
        var library = Assert.Single(snapshot.Meshes, row => row.Id != visible.Id);
        var texture = Assert.Single(library.DefaultTextures);
        var defaultSlot = visible.Slots[0];
        var originalMaterial = Assert.Single(library.DefaultMaterials, row => row.BaseColorTextureId == texture.Id);
        var authored = new ProjectMaterial(DocumentData.Id(451), "Copied finish", Vector3.One, texture.Id);
        var materials = new ProjectMaterialCatalog(project);
        var materialSnapshot = await materials.WriteAsync(await materials.ReadAsync(default), authored, default);
        var assets = SceneRenderAssets.Capture(project.Description.Id, null, materialSnapshot, meshes: snapshot, meshCatalog: meshes)!;
        Assert.Contains(assets.Textures, choice => choice.Id == texture.Id && choice.Name == "Из меша · Library");
        Assert.Equal(texture.Id, assets.ReadMaterial(originalMaterial.Id)?.BaseColorTextureId);
        Assert.DoesNotContain(assets.Materials, choice => choice.Id == originalMaterial.Id);
        Assert.NotNull(assets.MaterialDiagnostic(visible.Id, defaultSlot.Id, originalMaterial.Id));
        var binding = Assert.IsType<Guid>(assets.BindingForMaterial(visible.Id, defaultSlot.Id, authored.Id));

        await using var session = Session(project, meshes, materials);
        Assert.True(await session.NewAsync(SceneMaterialAuthoringTests.Scene((visible.Id, binding.ToString("D")))));
        var payload = await Capture(session);
        Assert.Equal(new[] { visible.Id, binding, authored.Id, visible.Slots[1].DefaultMaterialId, texture.Id }.Order(),
            payload.AssetKeys.Select(key => key.AssetId).Order());
        Assert.Equal(texture.Key(project.Description.Id),
            Assert.IsType<OpaqueUnlitMaterialDefinition>(Entry(payload, authored.Id).Definition).BaseColorTexture);
        Assert.Equal(new byte[] { 255, 128, 64, 255 }, Assert.IsType<BaseColorTextureDefinition>(Entry(payload, texture.Id).Definition).Pixels.ToArray());
    }

    private static SceneDocumentSession Session(ResolvedProject project, ProjectMeshCatalog meshes, ProjectMaterialCatalog? materials = null)
    {
        var factory = DocumentData.Factory();
        return new(project, new JsonSceneDocumentStore(new(factory)), factory) { MeshCatalog = meshes, MaterialCatalog = materials };
    }

    private static async Task<GameBuildRenderContent> Capture(SceneDocumentSession session) =>
        Assert.IsType<GameBuildRenderContent>(await session.CheckModelImportsAsync(session.Read().OpeningId));

    private static string Blob(ResolvedProject project, ProjectMesh row) =>
        Path.Combine(project.ContentDirectory, ".aether-mesh-data", Convert.ToHexString(row.PayloadHash.ToArray()) + ".trc");

    private static RenderAssetEntry Entry(GameBuildRenderContent content, Guid id) => Assert.Single(content.Entries, row => row.Key.AssetId == id);
    private static RenderAssetKey Key(GameBuildRenderContent content, Guid id) => Entry(content, id).Key;
    private static MaterialBindingSetDefinition Bindings(GameBuildRenderContent content, Guid id) =>
        Assert.IsType<MaterialBindingSetDefinition>(Entry(content, id).Definition);

    private static void AssertBinding(GameBuildRenderContent content, Guid id, params (Guid Slot, Guid Material)[] rows) =>
        Assert.Equal(rows.Select(row => new MaterialSlotBinding(row.Slot, Key(content, row.Material))), Bindings(content, id).Bindings.ToArray());

    private static byte[] Bytes(GameBuildRenderContent content)
    {
        using var output = new MemoryStream();
        content.WriteTo(output);
        return output.ToArray();
    }
}
