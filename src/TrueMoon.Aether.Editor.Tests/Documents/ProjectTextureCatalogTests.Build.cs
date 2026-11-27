using System.Numerics;
using TrueMoon.Aether.Editor.Tooling;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Tooling.Documents;
using TrueMoon.Aether.Tooling.Scenes;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Documents;

public sealed partial class ProjectTextureCatalogTests
{
    [Fact]
    public async Task TextureReplacementChangesOnlyReachableExactKeys()
    {
        using var files = new DocumentFiles();
        var project = files.Project;
        var model = await MaterialModelFixture.CreateAsync(project);
        var textureCatalog = new ProjectTextureCatalog(project, AetherEditorConfigurationExtensions.DecodeProjectTexture);
        await WritePng("first.png", TexturePaletteA);
        await WritePng("second.png", TexturePaletteB);
        await WritePng("same.png", TexturePaletteB);
        byte[] unusedPixels = (byte[])TexturePaletteA.Clone();
        unusedPixels[0] ^= 1;
        await WritePng("unused.png", unusedPixels);

        var textures = await textureCatalog.ImportAsync(await textureCatalog.ReadAsync(default), "Tiles", "first.png", default);
        var texture = Assert.Single(textures.Textures);
        var material = new ProjectMaterial(DocumentData.Id(401), "Tile finish", Vector3.One, texture.Id);
        var materials = new ProjectMaterialCatalog(project);
        var catalog = await materials.WriteAsync(await materials.ReadAsync(default), material, default);
        var assets = SceneRenderAssets.Capture(model.ProjectId, model.Assets, catalog,
            textures: textures, textureCatalog: textureCatalog)!;
        var mesh = model.UvMesh;
        var bindingId = Assert.IsType<Guid>(assets.BindingForMaterial(mesh.AssetId, mesh.SlotId, material.Id));
        var document = SceneMaterialAuthoringTests.Scene((mesh.AssetId, bindingId.ToString("D")),
            (mesh.AssetId, mesh.BindingSetId.ToString("D")));
        var factory = DocumentData.Factory();
        var store = new JsonSceneDocumentStore(new(factory));
        await using var session = new SceneDocumentSession(project, store, factory)
        {
            ModelImports = model.CreateImports(), MaterialCatalog = materials, TextureCatalog = new(project)
        };
        Assert.True(await session.NewAsync(document));
        var opening = session.Read().OpeningId;
        await session.SelectModelImportsAsync(opening, [model.Selection], model.Content);
        var scene = session.Read().Content;
        var original = await Capture();
        var pinned = MaterialModelFixture.PublishBuild(project, document.Revision, original);
        var originalKey = Key(original, texture.Id);
        Assert.Equal(TexturePaletteA, Assert.IsType<BaseColorTextureDefinition>(Entry(original, texture.Id).Definition).Pixels.ToArray());

        textures = await textureCatalog.ReplaceSourceAsync(textures, texture.Id, "second.png", default);
        Assert.Same(scene, session.Read().Content);
        var edited = await Capture();
        Assert.Equal(original.AssetKeys.Select(key => key.AssetId), edited.AssetKeys.Select(key => key.AssetId));
        var changedIds = new HashSet<Guid> { texture.Id, material.Id, bindingId };
        Assert.Equal(changedIds.Order(), edited.AssetKeys.Where(key => Key(original, key.AssetId) != key).Select(key => key.AssetId).Order());
        var materialKey = Key(edited, material.Id);
        var changedTexture = Key(edited, texture.Id);
        var editedBinding = Assert.IsType<MaterialBindingSetDefinition>(Entry(edited, bindingId).Definition);
        Assert.Equal(new MaterialSlotBinding(mesh.SlotId, materialKey), editedBinding.Bindings[0]);
        Assert.Equal(new MaterialSlotBinding(mesh.Slots[1].SlotId, Key(original, mesh.Slots[1].MaterialId)), editedBinding.Bindings[1]);
        var editedMaterial = Assert.IsType<OpaqueUnlitMaterialDefinition>(Entry(edited, material.Id).Definition);
        Assert.Equal(changedTexture, editedMaterial.BaseColorTexture);
        Assert.Equal(RenderTextureSampler.NearestClamp, editedMaterial.Sampler);
        Assert.Equal(TexturePaletteB, Assert.IsType<BaseColorTextureDefinition>(Entry(edited, texture.Id).Definition).Pixels.ToArray());

        var editedBytes = Bytes(edited);
        textures = await textureCatalog.RenameAsync(textures, texture.Id, "Renamed", default);
        Assert.Equal(editedBytes, Bytes(await Capture()));
        textures = await textureCatalog.ReplaceSourceAsync(textures, texture.Id, "same.png", default);
        Assert.Equal(editedBytes, Bytes(await Capture()));
        textures = await textureCatalog.ImportAsync(textures, "Unused", "unused.png", default);
        var unused = Assert.Single(textures.Textures, row => row.Id != texture.Id);
        Assert.True(File.Exists(Blob(unused.Revision)));
        File.Delete(Blob(unused.Revision));
        Assert.Equal(editedBytes, Bytes(await Capture()));

        File.Delete(Blob(changedTexture.Revision));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Capture());
        Assert.Contains("Renamed", error.Message);
        Assert.Contains(texture.Id.ToString("D"), error.Message);
        File.Delete(Blob(originalKey.Revision));
        var exact = GameBuildArtifact.Open(pinned.DirectoryPath, pinned.HostContractId, pinned.BuildId).RenderContent!;
        Assert.Equal(TexturePaletteA, Assert.IsType<BaseColorTextureDefinition>(exact.Resolve(originalKey).Definition).Pixels.ToArray());
        Assert.Equal(Key(original, material.Id), Assert.IsType<MaterialBindingSetDefinition>(exact.Resolve(Key(original, bindingId)).Definition).Bindings[0].Material);
        Assert.Equal(Bytes(original), await File.ReadAllBytesAsync(Path.Combine(pinned.DirectoryPath, GameBuildArtifact.RenderContentFile)));

        Task WritePng(string path, byte[] rgba) => File.WriteAllBytesAsync(Path.Combine(project.RootDirectory, path), TexturePng(rgba));
        string Blob(RenderContentHash revision) => Path.Combine(project.ContentDirectory, ".aether-texture-data", Convert.ToHexString(revision.ToArray()) + ".tmra");
        async Task<GameBuildRenderContent> Capture() => Assert.IsType<GameBuildRenderContent>(await session.CheckModelImportsAsync(opening));
    }

    private static RenderAssetEntry Entry(GameBuildRenderContent content, Guid id) => Assert.Single(content.Entries, item => item.Key.AssetId == id);
    private static RenderAssetKey Key(GameBuildRenderContent content, Guid id) => Entry(content, id).Key;
    private static byte[] Bytes(GameBuildRenderContent content)
    {
        using var stream = new MemoryStream();
        content.WriteTo(stream);
        return stream.ToArray();
    }
}
