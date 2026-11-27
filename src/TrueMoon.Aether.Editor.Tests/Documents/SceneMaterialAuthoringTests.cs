using System.Numerics;
using System.Reflection;
using System.Reflection.Emit;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Runtime.Ecs.Rendering;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Tooling.Builds;
using TrueMoon.Aether.Tooling.Documents;
using TrueMoon.Aether.Tooling.Importing;
using TrueMoon.Aether.Tooling.Projects;
using TrueMoon.Aether.Tooling.Scenes;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Documents;

public sealed class SceneMaterialAuthoringTests
{
    [Fact]
    public async Task MaterialEditChangesExactKeysWithoutChangingSceneBinding()
    {
        const string leftOverride = "e928f180-443b-d773-b59d-1c6106296338";
        using var files = new DocumentFiles();
        var model = new MaterialModelFixture(files.Project);
        var mesh = model.UvMesh;
        var material = Material(400, "Plaster", Vector3.UnitZ);
        var catalog = new ProjectMaterialCatalog(files.Project);
        var snapshot = await catalog.WriteAsync(await catalog.ReadAsync(default), material, default);
        var originalAssets = SceneRenderAssets.Capture(model.ProjectId, model.Assets, snapshot)!;
        Assert.Equal(Guid.Parse(leftOverride), originalAssets.BindingForMaterial(mesh.AssetId, mesh.SlotId, material.Id));
        var document = Scene((mesh.AssetId, leftOverride), (mesh.AssetId, leftOverride));
        var store = new JsonSceneDocumentStore(new(DocumentData.Factory()));
        var stamp = await store.WriteAsync(files.ScenePath, document, null);
        var scene = Resolve(document);
        var original = await Prepare(originalAssets, scene);
        var oldMaterial = Key(original, material.Id);
        var oldArtifact = MaterialModelFixture.PublishBuild(files.Project, 3, original);
        var oldBytes = File.ReadAllBytes(Path.Combine(oldArtifact.DirectoryPath, GameBuildArtifact.RenderContentFile));

        var changed = material with { BaseColorSrgb = new(.25f, .5f, .75f) };
        snapshot = await catalog.WriteAsync(snapshot, changed, default);
        var reopened = await store.ReadAsync(files.ScenePath);
        Assert.Equal(stamp, reopened.Stamp);
        Assert.Equal(document.Revision, reopened.Document.Revision);
        var resolved = Resolve(reopened.Document);
        var changedAssets = SceneRenderAssets.Capture(model.ProjectId, model.Assets, await catalog.ReadAsync(default))!;
        var edited = await Prepare(changedAssets, resolved);
        var editedMaterial = Key(edited, changed.Id);
        Assert.Equal(new OpaqueUnlitMaterialDefinition(changed.BaseColorSrgb).CreateKey(model.ProjectId, changed.Id), editedMaterial);
        var bindingId = Guid.Parse(leftOverride);
        Assert.Equal(original.AssetKeys.Select(key => key.AssetId), edited.AssetKeys.Select(key => key.AssetId));
        Assert.NotEqual(oldMaterial.Revision, editedMaterial.Revision);
        Assert.NotEqual(Key(original, bindingId).Revision, Key(edited, bindingId).Revision);
        Assert.Equal(model.Content.AssetKeys.Length + 2, edited.AssetKeys.Length);
        foreach (var key in model.Content.AssetKeys) Assert.Equal(key, Key(edited, key.AssetId));
        var right = Key(model.Content, mesh.Slots[1].MaterialId);
        foreach (var entity in resolved.Entities.Values)
        {
            var renderer = entity.Components.Values.Single(component => component.TypeId == MeshRenderingComponents.MeshRendererId);
            Assert.Equal(leftOverride, renderer.Properties["bindings"].Value.GetString());
            var binding = SceneRenderAssets.ResolveReferences(changedAssets, renderer.Properties["mesh"].Value.GetString(), leftOverride);
            Assert.Equal(bindingId, binding.Binding);
            Assert.Equal(Key(model.Content, mesh.AssetId), binding.Definition!.Mesh);
            Assert.Equal(new MaterialSlotBinding(mesh.SlotId, editedMaterial), binding.Definition.Bindings[0]);
            Assert.Equal(new MaterialSlotBinding(mesh.Slots[1].SlotId, right), binding.Definition.Bindings[1]);
        }

        var editedBytes = Bytes(edited);
        foreach (var row in new[] { changed with { Name = "Renamed" }, Material(401, "Unused", Vector3.One) })
        {
            snapshot = await catalog.WriteAsync(snapshot, row, default);
            var metadataOnly = await Prepare(SceneRenderAssets.Capture(model.ProjectId, model.Assets, snapshot)!, resolved);
            Assert.Equal(edited.AssetKeys.ToArray(), metadataOnly.AssetKeys.ToArray());
            Assert.Equal(editedBytes, Bytes(metadataOnly));
        }
        var oldBuild = GameBuildArtifact.Open(oldArtifact.DirectoryPath, oldArtifact.HostContractId, oldArtifact.BuildId).RenderContent!;
        Assert.Equal(oldMaterial, oldBuild.Resolve(material.Id, RenderContentProfile.MaterialKindId).Key);
        Assert.Equal(Vector3.UnitZ, Assert.IsType<OpaqueUnlitMaterialDefinition>(oldBuild.Resolve(oldMaterial).Definition).BaseColorSrgb);
        Assert.Equal(oldMaterial, Assert.IsType<MaterialBindingSetDefinition>(oldBuild.Resolve(Key(original, bindingId)).Definition).Bindings[0].Material);
        Assert.Equal(oldBytes, File.ReadAllBytes(Path.Combine(oldArtifact.DirectoryPath, GameBuildArtifact.RenderContentFile)));
    }

    [Fact]
    public async Task ReopenedSingleAndCombinedBindingsResolveAuthoredAndImportedSlots()
    {
        var model = new MaterialModelFixture(DocumentData.Id(350));
        var mesh = model.UvMesh;
        var single = model.PlainMesh;
        var authored = Material(400, "Plaster", Vector3.UnitZ);
        var assets = With(model, authored);
        var singleBinding = assets.BindingForMaterial(single.AssetId, single.SlotId, authored.Id);
        Assert.Equal(Guid.Parse("c3ccd1d3-e388-2e01-c75d-8c1b2fbcca25"), singleBinding);
        var left = assets.BindingForMaterial(mesh.AssetId, mesh.SlotId, authored.Id)!.Value.ToString("D");
        var rightSlot = mesh.Slots[1].SlotId;
        var combined = assets.BindingWithSlotMaterial(mesh.AssetId, left, rightSlot, single.MaterialId);
        Assert.Equal($"slots/1:{mesh.SlotId:N}={authored.Id:N};{rightSlot:N}={single.MaterialId:N}", combined);
        var scene = Scene((single.AssetId, singleBinding!.Value.ToString("D")), (mesh.AssetId, combined!));
        var codec = new SceneFileCodec(DocumentData.Factory());
        var reopened = Resolve(codec.Decode(codec.Encode(scene)));
        var content = await Prepare(With(model, authored), reopened);
        var authoredKey = Key(content, authored.Id);
        var singleSet = Bindings(content, singleBinding.Value);
        Assert.Equal(new MaterialSlotBinding(single.SlotId, authoredKey), Assert.Single(singleSet.Bindings));
        var combinedSet = Bindings(content, Guid.Parse("55d012fa-19c4-a792-9741-6fc467dd65b7"));
        Assert.Equal(new[] { new MaterialSlotBinding(mesh.SlotId, authoredKey), new(rightSlot, Key(model.Content, single.MaterialId)) }, combinedSet.Bindings.ToArray());
        Assert.Equal(model.Content.AssetKeys.Length + 3, content.AssetKeys.Length);
    }

    [Fact]
    public async Task MissingTextureOnlyRejectsUsedMaterial()
    {
        var model = new MaterialModelFixture(DocumentData.Id(350));
        var uv = model.UvMesh;
        var plain = model.PlainMesh;
        var solid = Material(400, "Plaster", Vector3.One);
        var missing = solid with { BaseColorTextureId = DocumentData.Id(450) };
        var textured = solid with { BaseColorTextureId = model.TextureId };
        var solidAssets = With(model, solid);
        var missingAssets = With(model, missing);
        var texturedAssets = With(model, textured);
        var leftBinding = solidAssets.BindingForMaterial(uv.AssetId, uv.SlotId, solid.Id)!.Value;
        var plainBinding = solidAssets.BindingForMaterial(plain.AssetId, plain.SlotId, solid.Id)!.Value;
        Assert.Same(model.Content, await Prepare(missingAssets, Resolve(Scene((uv.AssetId, uv.BindingSetId.ToString("D"))))));
        Assert.Null(missingAssets.BindingForMaterial(uv.AssetId, uv.SlotId, solid.Id));
        var missingError = await Assert.ThrowsAsync<InvalidOperationException>(() => Prepare(missingAssets, Resolve(Scene((uv.AssetId, leftBinding.ToString("D"))))));
        Assert.Contains(solid.Name, missingError.Message);
        Assert.Contains(solid.Id.ToString("D"), missingError.Message);
        Assert.Contains(missing.BaseColorTextureId!.Value.ToString("D"), missingError.Message);
        var combined = solidAssets.BindingWithSlotMaterial(uv.AssetId, leftBinding.ToString("D"), uv.Slots[1].SlotId, plain.MaterialId)!;
        Assert.Contains(missing.BaseColorTextureId.Value.ToString("D"), SceneRenderAssets.ResolveReferences(missingAssets, uv.AssetId.ToString("D"), combined).Diagnostic);

        Assert.Equal(leftBinding, texturedAssets.BindingForMaterial(uv.AssetId, uv.SlotId, solid.Id));
        var content = await Prepare(texturedAssets, Resolve(Scene((uv.AssetId, leftBinding.ToString("D")))));
        var definition = Assert.IsType<OpaqueUnlitMaterialDefinition>(content.Entries.Single(entry => entry.Key.AssetId == solid.Id).Definition);
        Assert.Equal(Key(model.Content, model.TextureId), definition.BaseColorTexture);
        Assert.Equal(RenderTextureSampler.NearestClamp, definition.Sampler);
        Assert.Equal(new SceneRenderAssets.Choice(model.TextureId, "Fixture · albedo.png"), Assert.Single(texturedAssets.Textures));
        Assert.Equal(new ProjectMaterial(uv.MaterialId, "Fixture · blue", Vector3.UnitZ, model.TextureId), texturedAssets.ReadMaterial(uv.MaterialId));
        Assert.Equal(textured, texturedAssets.ReadMaterial(textured.Id));

        Assert.Null(texturedAssets.BindingForMaterial(plain.AssetId, plain.SlotId, textured.Id));
        Assert.Null(texturedAssets.BindingWithSlotMaterial(plain.AssetId, plain.BindingSetId.ToString("D"), plain.SlotId, textured.Id));
        var uvError = await Assert.ThrowsAsync<InvalidOperationException>(() => Prepare(texturedAssets, Resolve(Scene((plain.AssetId, plainBinding.ToString("D"))))));
        Assert.Contains("UV0", uvError.Message);
        Assert.Contains(textured.Id.ToString("D"), uvError.Message);
        Assert.Contains(plain.AssetId.ToString("D"), uvError.Message);
        var wrongSlot = plain.SlotId;
        Assert.Null(texturedAssets.BindingWithSlotMaterial(uv.AssetId, uv.BindingSetId.ToString("D"), wrongSlot, textured.Id));
        var invalid = SceneMaterialBindingReference.Format([(wrongSlot, textured.Id), (uv.Slots[1].SlotId, plain.MaterialId)]);
        Assert.Contains(wrongSlot.ToString("D"), SceneRenderAssets.ResolveReferences(texturedAssets, uv.AssetId.ToString("D"), invalid).Diagnostic);
        Assert.Equal(uv.BindingSetId, texturedAssets.DefaultBinding(uv.AssetId));
    }

    [Fact]
    public async Task ImportSlotOrTextureCollisionBlocksPickerAndBuildWithoutHidingCatalog()
    {
        var model = new MaterialModelFixture(DocumentData.Id(350));
        var mesh = model.UvMesh;
        var scene = Resolve(Scene((mesh.AssetId, mesh.BindingSetId.ToString("D"))));
        foreach (var id in new[] { mesh.SlotId, model.TextureId })
        {
            var row = new ProjectMaterial(id, "Ambiguous", Vector3.One, null);
            var assets = With(model, row);
            Assert.Equal(id, assets.Collision!.Id);
            Assert.Equal(row, assets.ReadMaterial(id));
            Assert.Null(assets.BindingForMaterial(mesh.AssetId, mesh.SlotId, mesh.MaterialId));
            Assert.Contains(id.ToString("D"), SceneRenderAssets.ResolveReferences(assets, mesh.AssetId.ToString("D"), mesh.BindingSetId.ToString("D")).Diagnostic);
            Assert.Contains(id.ToString("D"), (await Assert.ThrowsAsync<InvalidOperationException>(() => Prepare(assets, scene))).Message);
        }
        Assert.Same(model.Content, await Prepare(With(model, Material(401, "Independent", Vector3.One)), scene));
    }

    [Fact]
    public async Task ExplicitFullPayloadRetainsRawBindingsAndDeduplicatesOnlyExactImportKeys()
    {
        var model = new MaterialModelFixture(DocumentData.Id(350));
        var plain = model.PlainMesh;
        var plainGeometry = model.Content.Entries.Single(entry => entry.Key.AssetId == plain.AssetId);
        var rawMaterial = new OpaqueUnlitMaterialDefinition(new(.25f, .5f, .75f));
        var rawKey = rawMaterial.CreateKey(model.ProjectId, DocumentData.Id(402));
        var rawBinding = new MaterialBindingSetDefinition(plainGeometry.Key, [new(plain.SlotId, rawKey)]);
        var rawBindingKey = rawBinding.CreateKey(model.ProjectId, DocumentData.Id(403));
        var rawTexture = new BaseColorTextureDefinition(1, 1, [36, 48, 60, 255]);
        var rawTextureKey = rawTexture.CreateKey(model.ProjectId, DocumentData.Id(404));
        var raw = new GameBuildRenderContent(model.ProjectId,
            [plainGeometry, new(rawKey, rawMaterial), new(rawBindingKey, rawBinding), new(rawTextureKey, rawTexture)]);
        var authored = Material(400, "Catalog", Vector3.One) with { BaseColorTextureId = rawTextureKey.AssetId };
        var snapshot = new ProjectMaterialCatalogSnapshot(model.ProjectId, [authored], null);
        var resolved = Resolve(Scene((plain.AssetId, rawBindingKey.AssetId.ToString("D"))));
        var rawOnly = SceneRenderAssets.Capture(model.ProjectId, null, snapshot, raw)!;
        Assert.Empty(rawOnly.Meshes);
        Assert.Empty(rawOnly.Textures);
        Assert.Same(raw, await Prepare(rawOnly, resolved));
        var unknownBinding = Resolve(Scene((plain.AssetId, DocumentData.Id(450).ToString("D"))));
        Assert.Same(raw, await SceneRenderAssets.PrepareBuildAsync(rawOnly, unknownBinding, requireAll: false, default));
        var merged = SceneRenderAssets.Capture(model.ProjectId, model.Assets, snapshot, raw)!;
        var content = await Prepare(merged, resolved);
        Assert.Equal(model.Content.AssetKeys.Append(rawKey).Append(rawBindingKey).Append(rawTextureKey).OrderBy(key => key.AssetId), content.AssetKeys);
        Assert.Equal(rawKey, Assert.Single(Bindings(content, rawBindingKey.AssetId).Bindings).Material);
        Assert.Equal(model.TextureId, Assert.Single(merged.Textures).Id);
        Assert.Null(merged.BindingForMaterial(model.UvMesh.AssetId, model.UvMesh.SlotId, authored.Id));
        var different = new OpaqueUnlitMaterialDefinition(new(.1f, .2f, .3f));
        var conflictKey = different.CreateKey(model.ProjectId, plain.MaterialId);
        var conflict = new GameBuildRenderContent(model.ProjectId, [new(conflictKey, different)]);
        var rejected = SceneRenderAssets.Capture(model.ProjectId, model.Assets, snapshot, conflict)!;
        Assert.Equal(plain.MaterialId, rejected.Collision!.Id);
        Assert.Contains(plain.MaterialId.ToString("D"), (await Assert.ThrowsAsync<InvalidOperationException>(() => Prepare(rejected, resolved))).Message);
    }

    internal static SceneDocument Scene(params (Guid Mesh, string Binding)[] renderers) => new(new(DocumentData.Id(230)), 3,
        renderers.Select((item, index) => new EntityInstance(new(DocumentData.Id(200 + index)), new(DocumentData.Id(231)), new(addedComponents:
        [
            new(new(DocumentData.Id(220 + index)), BuiltInComponents.TransformId, 1),
            new(new(DocumentData.Id(210 + index)), MeshRenderingComponents.MeshRendererId, 1,
                [KeyValuePair.Create("mesh", PropertyValue.From(item.Mesh.ToString("D"))), KeyValuePair.Create("bindings", PropertyValue.From(item.Binding))])
        ]))), [new(new(DocumentData.Id(231)), EntityPatch.Empty)], [.. BuiltInComponents.Schemas, MeshRenderingComponents.MeshRendererSchema]);

    private static ProjectMaterial Material(int id, string name, Vector3 color) => new(DocumentData.Id(id), name, color, null);

    private static SceneRenderAssets With(MaterialModelFixture model, params ProjectMaterial[] materials) =>
        SceneRenderAssets.Capture(model.ProjectId, model.Assets, new(model.ProjectId, materials, null))!;

    private static ResolvedSceneSnapshot Resolve(SceneDocument scene)
    {
        using var authoring = DocumentData.Factory().Create(scene);
        return authoring.Read().Resolved;
    }

    private static async Task<GameBuildRenderContent> Prepare(SceneRenderAssets assets, ResolvedSceneSnapshot scene) =>
        Assert.IsType<GameBuildRenderContent>(await SceneRenderAssets.PrepareBuildAsync(assets, scene, requireAll: true, default));

    private static RenderAssetKey Key(GameBuildRenderContent content, Guid id) => Assert.Single(content.AssetKeys, key => key.AssetId == id);
    private static MaterialBindingSetDefinition Bindings(GameBuildRenderContent content, Guid id) =>
        Assert.IsType<MaterialBindingSetDefinition>(Assert.Single(content.Entries, entry => entry.Key.AssetId == id).Definition);

    private static byte[] Bytes(GameBuildRenderContent content)
    {
        using var stream = new MemoryStream();
        content.WriteTo(stream);
        return stream.ToArray();
    }

}

internal sealed class MaterialModelFixture
{
    private static readonly Guid SourceId = DocumentData.Id(300);
    private const string RootName = "material-model.obj";
    internal Guid ProjectId { get; }
    internal SceneModelImportSelection Selection { get; }
    internal GameBuildRenderContent Content { get; }
    internal SceneRenderAssets Assets { get; }
    internal PublishedModelMesh UvMesh => Selection.Watch.Published.Meshes[0];
    internal PublishedModelMesh PlainMesh => Selection.Watch.Published.Meshes[1];
    internal Guid TextureId => Selection.Watch.Published.Texture!.AssetId;

    internal MaterialModelFixture(ResolvedProject project) : this(project.Description.Id, project.ContentDirectory, new('A', 64)) { }
    internal MaterialModelFixture(Guid projectId) : this(projectId, Path.GetTempPath(), new('A', 64)) { }

    internal static async Task<MaterialModelFixture> CreateAsync(ResolvedProject project)
    {
        await File.WriteAllTextAsync(Path.Combine(project.ContentDirectory, RootName), "v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n");
        await using var capture = new ModelSourceCapture(new(SourceId, project.ContentDirectory, RootName));
        var source = await capture.CaptureAsync();
        return new(project.Description.Id, project.ContentDirectory, source.InputRevision);
    }

    internal SceneModelImports CreateImports()
    {
        var factories = new SourceFactories();
        return new(ProjectId, factories, factories, null, null, null);
    }

    private MaterialModelFixture(Guid projectId, string directory, string inputRevision)
    {
        ProjectId = projectId;
        Vector3[] vertices = [Vector3.Zero, Vector3.UnitX, Vector3.UnitY];
        var bounds = new RenderBounds(Vector3.Zero, new(1, 1, 0));
        var uv = new MeshDefinition(vertices, [Vector2.Zero, Vector2.UnitX, Vector2.UnitY], [0, 1, 2, 2, 1, 0],
            [new(RenderContentProfile.DefaultSlotId, 0, 3), new(DocumentData.Id(351), 3, 3)], bounds);
        var plain = new MeshDefinition(vertices, [0, 1, 2], [new(RenderContentProfile.DefaultSlotId, 0, 3)], bounds);
        var candidate = new ModelImportCandidate(SourceId, inputRevision,
            [new(uv, 0) { SlotMaterials = [new("left", 0), new("right", 1)] }, new(plain, 2)],
            [new(Vector3.UnitZ), new(Vector3.UnitY), new(Vector3.UnitX)],
            [new(0, Matrix4x4.Identity), new(1, Matrix4x4.Identity)],
            new(["uv", "plain"], ["blue", "green", "red"], ["first", "second"]),
            texture: new("albedo.png", 0, new(1, 1, [255, 128, 64, 255])));
        var bindings = new ModelSubassetBinding[]
        {
            Bind(ModelSubassetKind.Mesh, 0, "uv", 301), Bind(ModelSubassetKind.Mesh, 1, "plain", 302),
            Bind(ModelSubassetKind.Material, 0, "blue", 303), Bind(ModelSubassetKind.Material, 1, "green", 304), Bind(ModelSubassetKind.Material, 2, "red", 305),
            Bind(ModelSubassetKind.Slot, 0, "slot/uv/left", 306), Bind(ModelSubassetKind.Slot, 1, "slot/plain", 307), Bind(ModelSubassetKind.Slot, 2, "slot/uv/right", 308),
            Bind(ModelSubassetKind.BindingSet, 0, "binding/uv", 309), Bind(ModelSubassetKind.BindingSet, 1, "binding/plain", 310),
            Bind(ModelSubassetKind.Instance, 0, "first", 311), Bind(ModelSubassetKind.Instance, 1, "second", 312),
            Bind(ModelSubassetKind.Texture, 0, "albedo.png", 313)
        };
        var published = new PublishedModelImport(1, candidate,
            new(SourceId, bindings.Select(binding => new ModelSubassetEntry(binding.Key, binding.Id, true))), bindings);
        Selection = new("Fixture", new(new(SourceId, directory, RootName), published) { UseFileSystemNotifications = false });
        Content = new(projectId, published.CreateRenderEntries(projectId).ToArray());
        Assets = new(Content, [Selection]);
    }

    internal static GameBuildArtifact PublishBuild(ResolvedProject project, long revision, GameBuildRenderContent content)
    {
        var output = Directory.CreateDirectory(Path.Combine(project.RootDirectory, "material-logic")).FullName;
        var assembly = new PersistedAssemblyBuilder(new AssemblyName("Room.Logic"), typeof(object).Assembly);
        var type = assembly.DefineDynamicModule("Room.Logic").DefineType("Room.Logic.Game", TypeAttributes.Public);
        type.DefineNestedType("Nested", TypeAttributes.NestedPublic).CreateType();
        type.CreateType();
        assembly.Save(Path.Combine(output, project.Description.EntryAssembly));
        return GameArtifactPublisher.Publish(output, Path.Combine(project.RootDirectory, "artifacts"), new('A', 64),
            project.Description.EntryAssembly, project.Description.EntryType, revision, null, default, false, content);
    }

    private static ModelSubassetBinding Bind(ModelSubassetKind kind, int index, string locator, int id) =>
        new(kind, index, new(kind, locator), DocumentData.Id(id));

    private sealed class SourceFactories : IFactory<ModelSourceCapture, ModelSourceCaptureArguments>, IFactory<ModelSourceWatch, ModelSourceWatchArguments>
    {
        private readonly ModelSourceWatchHost _watches;
        internal SourceFactories() => _watches = new(this, TimeProvider.System);
        public ModelSourceCapture Create(ModelSourceCaptureArguments arguments) => new(arguments);
        public ModelSourceWatch Create(ModelSourceWatchArguments arguments) => _watches.Create(arguments);
    }
}
