using System.Runtime.InteropServices;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Editor.Tooling;
using TrueMoon.Aether.Editor.Tooling.Documents;
using TrueMoon.Aether.Editor.UI.Documents;
using TrueMoon.Aether.Tooling.Documents;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Rendering;
using TrueMoon.Alloy.Runtime.Semantics;
using TrueMoon.Argentis;
using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Input;
using Xunit;
using RoutedInspector = TrueMoon.Aether.Editor.Tests.Inspector.EditorMaterialAuthoringTests.RoutedInspector;

namespace TrueMoon.Aether.Editor.Tests.Inspector;

public sealed class EditorProjectAssetsTests
{
    private const string Textures = "editor.textures";
    private const string Materials = "editor.materials";
    private const string Meshes = "editor.meshes";
    private const string FirstPng = "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAYAAABytg0kAAAAG0lEQVR4nGO4KsD2/wmn4H+GT2Lc//8wi/0HAEa/CA5sfa9+AAAAAElFTkSuQmCC";
    private const string SecondPng = "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAYAAABytg0kAAAAG0lEQVR4nGNQnHL1f3Uxz38GA98J/3MlVv0HAExSCL160DzLAAAAAElFTkSuQmCC";
    private static bool CanCapture => OperatingSystem.IsWindows() && RuntimeInformation.ProcessArchitecture == Architecture.X64;

    [Fact]
    public void ProjectTexturePickerWorksWithoutSceneAndKeepsReferencesOnReplace()
    {
        if (!CanCapture) return;
        MaterialModelFixture? prepared = null;
        using var owner = new DocumentUiFixture(materialCatalog: true,
            modelImports: project => (prepared = Await(MaterialModelFixture.CreateAsync(project))).CreateImports(),
            createTextureCatalog: project => new(project, AetherEditorConfigurationExtensions.DecodeProjectTexture));
        var models = Assert.IsType<MaterialModelFixture>(prepared);
        var catalog = new ProjectTextureCatalog(owner.Files.Project);
        WritePngs(owner);
        using var inspector = new RoutedInspector(owner, null);
        Assert.Equal(UiSemanticTargetStatus.Hidden, inspector.Target(Textures + ".path"));
        inspector.Click(Textures);
        inspector.Complete();
        Assert.Null(owner.Document.MaterialSnapshot);
        inspector.Click(Textures + ".new");
        inspector.ReplaceText(Textures + ".name", "Ceramic");
        inspector.ReplaceText(Textures + ".path", "../first.png");
        Assert.Equal(UiSemanticTargetStatus.Disabled, inspector.Target(Textures + ".import"));
        inspector.ReplaceText(Textures + ".path", "first.png");
        inspector.Click(Textures + ".import");
        inspector.Complete();
        var original = Assert.Single(Read(catalog).Textures);
        Assert.Equal("Ceramic", original.Name);
        Assert.Equal("first.png", original.SourcePath);
        Assert.Equal((2, 2), (original.Width, original.Height));
        Assert.Contains(original.Id.ToString("D"), inspector.Control<RichTextEditor>(Textures + ".metadata").Text);
        var rowId = $"{Textures}.item.{original.Id:N}";
        var retained = inspector.Control<Button>(rowId);
        Assert.Null(owner.Document.Read().Content);

        inspector.Click(Textures);
        inspector.Click(Materials);
        inspector.Complete();
        inspector.Click(Materials + ".new");
        inspector.Click(Materials + ".texture");
        inspector.Click($"{Materials}.texture.{original.Id:N}");
        inspector.Click(Materials + ".save");
        inspector.Complete();
        var materials = new ProjectMaterialCatalog(owner.Files.Project);
        var finish = Assert.Single(Await(materials.ReadAsync(default)).Materials);
        Assert.Equal(original.Id, finish.BaseColorTextureId);
        Assert.Null(owner.Document.Read().Content);

        var uv = models.UvMesh;
        var plain = models.PlainMesh;
        EditorMaterialAuthoringTests.Open(owner, models, "room.aetherscene", (uv.AssetId, uv.BindingSetId.ToString("D")),
            (plain.AssetId, plain.BindingSetId.ToString("D")));
        inspector.Refresh(DocumentData.Id(200).ToString("D"));
        var importedChoice = $"{Materials}.texture.{models.TextureId:N}";
        var authoredChoice = $"{Materials}.texture.{original.Id:N}";
        inspector.Click(Materials + ".texture");
        Assert.Equal(UiSemanticTargetStatus.Ready, inspector.Target(importedChoice));
        Assert.Equal(UiSemanticTargetStatus.Ready, inspector.Target(authoredChoice));
        owner.Run(DocumentCommand.Close);
        inspector.Refresh(null);
        Assert.Equal(UiSemanticTargetStatus.NotFound, inspector.Target(importedChoice));
        Assert.Equal(UiSemanticTargetStatus.Ready, inspector.Target(authoredChoice));
        inspector.Click(Materials + ".texture");
        EditorMaterialAuthoringTests.Open(owner, models, "room.aetherscene", (uv.AssetId, uv.BindingSetId.ToString("D")),
            (plain.AssetId, plain.BindingSetId.ToString("D")));
        inspector.Refresh(DocumentData.Id(200).ToString("D"));
        var slot = uv.Slots[0].SlotId;
        inspector.Click($"editor.inspector.material.slot.{slot:N}");
        inspector.Click($"editor.inspector.material.choice.{slot:N}.{finish.Id:N}");
        var uncommitted = owner.Document.Read().Content;
        inspector.Click(Textures);
        inspector.Click(Textures + ".name");
        inspector.Send(Textures + ".name", new(InputKind.KeyDown, Key: UiKey.Enter, Control: true), intercepted: true);
        Assert.Same(uncommitted, owner.Document.Read().Content);
        inspector.Click(Textures);
        inspector.Click("editor.inspector.apply");
        var before = owner.Document.Read().Content!;
        Assert.Equal(1, before.UndoCount);
        inspector.Refresh(DocumentData.Id(201).ToString("D"));
        slot = plain.Slots[0].SlotId;
        inspector.Click($"editor.inspector.material.slot.{slot:N}");
        Assert.Equal(UiSemanticTargetStatus.Disabled, inspector.Target($"editor.inspector.material.choice.{slot:N}.{finish.Id:N}"));

        inspector.Click(Materials);
        inspector.Click(Textures);
        inspector.Click(rowId);
        inspector.ReplaceText(Textures + ".name", "Glazed ceramic");
        inspector.Click(Textures + ".rename");
        inspector.Complete();
        var renamed = Assert.Single(Read(catalog).Textures);
        Assert.Equal(original with { Name = "Glazed ceramic" }, renamed);
        inspector.ReplaceText(Textures + ".path", "second.png");
        inspector.Click(Textures + ".replace");
        inspector.Complete();
        var replaced = Assert.Single(Read(catalog).Textures);
        Assert.Equal(renamed with { SourcePath = "second.png", Revision = replaced.Revision }, replaced);
        Assert.NotEqual(original.Revision, replaced.Revision);
        Assert.Equal(finish, Assert.Single(Await(materials.ReadAsync(default)).Materials));
        Assert.Same(retained, inspector.Control<Button>(rowId));
        Assert.Same(before, owner.Document.Read().Content);
    }

    [Fact]
    public void NamedMeshCreatesBeforeSceneAndAppliesWithSingleSceneHistoryEntry()
    {
        if (!CanCapture) return;
        RegisteredMeshSourceFixture? registered = null;
        using var owner = new DocumentUiFixture(createMeshCatalog: project => new(project, (registered = new(project)).Models));
        using var sources = Use(registered!);
        var source = Await(registered!.AddAsync("models/panel.gltf"));
        var catalog = new ProjectMeshCatalog(owner.Files.Project);
        using var inspector = new RoutedInspector(owner, null);
        Assert.Equal(UiSemanticTargetStatus.Hidden, inspector.Target(Meshes + ".name"));
        inspector.Click(Meshes);
        inspector.Complete();
        inspector.Click(Meshes + ".new");
        inspector.Complete();
        inspector.ReplaceText(Meshes + ".name", "Named panel");
        inspector.Click($"{Meshes}.source.{source.SourceId:N}");
        inspector.Complete();
        Assert.Equal(UiSemanticTargetStatus.Disabled, inspector.Target(Meshes + ".create"));
        inspector.Click($"{Meshes}.geometry.{source.PublishedMeshId:N}");
        inspector.Click(Meshes + ".create");
        inspector.Complete();
        var mesh = Assert.Single(Await(catalog.ReadAsync(default)).Meshes);
        Assert.Equal("Named panel", mesh.Name);
        Assert.NotEqual(source.PublishedMeshId, mesh.Id);
        Assert.Equal("models/panel.gltf", mesh.Source.SourcePath);
        Assert.Equal(new RenderBounds(new(0, 0, 0), new(1, 1, 0)), mesh.Bounds);
        Assert.Equal(new[] { "left", "right" }, mesh.Slots.Select(slot => slot.Name));
        var metadata = inspector.Control<RichTextEditor>(Meshes + ".metadata").Text;
        Assert.Contains("Min: 0, 0, 0", metadata);
        Assert.Contains("Max: 1, 1, 0", metadata);
        Assert.Contains(mesh.Source.MeshLocator, metadata);
        Assert.All(mesh.Slots, slot => Assert.Contains($"{slot.Name} · {slot.Id.ToString("N")[..8]}", metadata));
        Assert.Null(owner.Document.Read().Content);
        Assert.Null(owner.Document.ModelImports);

        var unassigned = DocumentData.Id(590);
        var oldBinding = DocumentData.Id(591).ToString("D");
        var scene = SceneMaterialAuthoringTests.Scene((unassigned, oldBinding));
        owner.Store.File = new(scene, new("mesh panel scene fixture"));
        owner.Run(DocumentCommand.Open, "mesh.aetherscene");
        inspector.Refresh(DocumentData.Id(200).ToString("D"));
        inspector.Click(Meshes);
        inspector.Click($"editor.inspector.mesh.{DocumentData.Id(210):N}");
        inspector.Click($"editor.inspector.mesh.choice.{mesh.Id:N}");
        Assert.Equal(unassigned.ToString("D"), References().Mesh);
        inspector.Click(Meshes);
        inspector.Click(Meshes + ".name");
        inspector.Send(Meshes + ".name", new(InputKind.KeyDown, Key: UiKey.Enter, Control: true), intercepted: true);
        Assert.Equal(unassigned.ToString("D"), References().Mesh);
        inspector.Click(Meshes);
        inspector.Click("editor.inspector.apply");
        var applied = owner.Document.Read();
        Assert.Equal((mesh.Id.ToString("D"), mesh.DefaultBindingId.ToString("D")), References());
        Assert.Equal(1, applied.Content!.UndoCount);
        var assets = owner.Document.ReadRenderAssets(applied.OpeningId);
        var payload = Assert.IsType<GameBuildRenderContent>(Await(SceneRenderAssets.PrepareBuildAsync(assets, applied.Content.Resolved, true, default)));
        Assert.Equal(mesh.MeshRevision, Assert.Single(payload.AssetKeys, key => key.AssetId == mesh.Id).Revision);
        Assert.Equal(mesh.DefaultBindingRevision, Assert.Single(payload.AssetKeys, key => key.AssetId == mesh.DefaultBindingId).Revision);
        inspector.Click("editor.inspector.undo");
        Assert.Equal((unassigned.ToString("D"), oldBinding), References());
        Assert.Equal(1, owner.Document.Read().Content!.RedoCount);
        inspector.Click("editor.inspector.redo");
        Assert.Equal((mesh.Id.ToString("D"), mesh.DefaultBindingId.ToString("D")), References());
        Assert.Equal(1, owner.Document.Read().Content!.UndoCount);

        (string? Mesh, string? Binding) References()
        {
            var component = owner.Document.Read().Content!.Resolved.Entities[new(DocumentData.Id(200))].Components[new(DocumentData.Id(210))];
            return (component.Properties["mesh"].Value.GetString(), component.Properties["bindings"].Value.GetString());
        }
    }

    [Fact]
    public void MeshDraftKeepsIndependentGeometryAndNameThroughReceiptsAndConflict()
    {
        if (!CanCapture) return;
        RegisteredMeshSourceFixture? registered = null;
        using var owner = new DocumentUiFixture(materialCatalog: true, createTextureCatalog: project => new(project),
            createMeshCatalog: project => new(project, (registered = new(project)).Models));
        using var sources = Use(registered!);
        var source = Await(registered!.AddAsync("original.gltf"));
        var replacement = Await(registered.AddAsync("replacement.gltf", reverseSlots: true));
        var catalog = new ProjectMeshCatalog(owner.Files.Project, registered.Models);
        var seed = Await(catalog.CreateAsync(Await(catalog.ReadAsync(default)), source, "Original", default));
        var original = Assert.Single(seed.Meshes);
        using var inspector = new RoutedInspector(owner, null);
        inspector.Click(Meshes);
        inspector.Complete();
        Assert.Null(owner.Document.MaterialSnapshot);
        Assert.Null(owner.Document.TextureSnapshot);
        inspector.Click($"{Meshes}.item.{original.Id:N}");
        inspector.Complete();
        var row = inspector.Control<Button>($"{Meshes}.item.{original.Id:N}");
        inspector.ReplaceText(Meshes + ".name", "Renamed");
        ChooseReplacement();
        inspector.Click(Meshes + ".rename");
        inspector.Complete();
        var renamed = Await(catalog.ReadAsync(default));
        var renamedMesh = Assert.Single(renamed.Meshes);
        Assert.Equal("Renamed", renamedMesh.Name);
        Assert.Equal(original.MeshRevision, renamedMesh.MeshRevision);
        Assert.Equal(UiSemanticTargetStatus.Ready, inspector.Target(Meshes + ".replace"));
        Assert.Equal(UiSemanticTargetStatus.Disabled, inspector.Target(Meshes + ".reload"));

        inspector.Click(Meshes);
        inspector.Click(Materials);
        inspector.Complete(); // Material receipt starts Texture.
        inspector.Complete(); // Texture receipt permits Material editing.
        inspector.Click(Materials + ".new");
        Assert.False(Await(owner.Commands.RequestExitAsync()));
        Assert.Equal("Сохраните или отмените правки ресурсов проекта.", owner.Commands.Read().Error?.Message);
        inspector.Click(Materials + ".cancel");
        inspector.Click(Materials);
        Assert.Equal(UiSemanticTargetStatus.Ready, inspector.Target(Meshes + ".draft"));
        Assert.False(Await(owner.Commands.RequestExitAsync()));
        Assert.Equal(EditorDocumentCommands.MeshDraftWarning, owner.Commands.Read().Error?.Message);
        Assert.Equal(0, owner.RuntimeStops);

        inspector.Click(Meshes);
        inspector.ReplaceText(Meshes + ".name", "Not yet saved");
        inspector.Click(Meshes + ".replace");
        inspector.Complete();
        var replaced = Await(catalog.ReadAsync(default));
        var replacedMesh = Assert.Single(replaced.Meshes);
        Assert.Equal("Renamed", replacedMesh.Name);
        Assert.Equal("replacement.gltf", replacedMesh.Source.SourcePath);
        Assert.Equal(original.Slots.Reverse().Select(slot => slot.Id), replacedMesh.Slots.Select(slot => slot.Id));
        Assert.Equal("Not yet saved", inspector.Control<RichTextEditor>(Meshes + ".name").Text);
        Assert.Same(row, inspector.Control<Button>($"{Meshes}.item.{original.Id:N}"));

        Await(catalog.RenameAsync(replaced, original.Id, "External", default));
        inspector.Click(Meshes + ".rename");
        inspector.Complete();
        Assert.Equal("External", Assert.Single(Await(catalog.ReadAsync(default)).Meshes).Name);
        Assert.Equal("Not yet saved", inspector.Control<RichTextEditor>(Meshes + ".name").Text);
        Assert.NotEmpty(inspector.Control<RichTextEditor>(Meshes + ".status").Text);
        Assert.Equal(UiSemanticTargetStatus.Disabled, inspector.Target(Meshes + ".reload"));
        inspector.Click(Meshes + ".cancel");
        inspector.Click(Meshes + ".reload");
        inspector.Complete();
        inspector.Complete(); // Reload also refreshes registered sources.
        Assert.Equal("External", inspector.Control<RichTextEditor>(Meshes + ".name").Text);
        Assert.Equal(UiSemanticTargetStatus.Hidden, inspector.Target(Meshes + ".draft"));
        Assert.Null(owner.Document.Read().Content);

        void ChooseReplacement()
        {
            inspector.Click($"{Meshes}.source.{replacement.SourceId:N}");
            inspector.Complete();
            inspector.Click($"{Meshes}.geometry.{replacement.PublishedMeshId:N}");
        }
    }

    [Fact]
    public void AssetDraftVetoesProjectExitAndFailedOperationKeepsDraft()
    {
        if (!CanCapture) return;
        using var owner = new DocumentUiFixture(materialCatalog: true,
            createTextureCatalog: project => new(project, AetherEditorConfigurationExtensions.DecodeProjectTexture));
        var catalog = new ProjectTextureCatalog(owner.Files.Project, AetherEditorConfigurationExtensions.DecodeProjectTexture);
        WritePngs(owner);
        var original = Assert.Single(Await(catalog.ImportAsync(Read(catalog), "Original", "first.png", default)).Textures);
        using var inspector = new RoutedInspector(owner, null);
        inspector.Click(Textures);
        inspector.Complete();
        var rowId = $"{Textures}.item.{original.Id:N}";
        inspector.Click(rowId);
        inspector.Click(Materials);
        inspector.Complete();
        inspector.Click(Materials + ".new");
        inspector.ReplaceText(Textures + ".name", "Renamed");
        inspector.ReplaceText(Textures + ".path", "second.png");
        inspector.Click(Textures + ".rename");
        inspector.Complete();
        var renamed = Read(catalog);
        Assert.Equal(original with { Name = "Renamed" }, Assert.Single(renamed.Textures));
        Assert.Equal("second.png", inspector.Control<RichTextEditor>(Textures + ".path").Text);
        Assert.Equal(UiSemanticTargetStatus.Disabled, inspector.Target(Textures + ".reload"));
        Assert.Equal(UiSemanticTargetStatus.Disabled, inspector.Target(rowId));

        inspector.Click(Materials + ".cancel");
        inspector.Click(Textures);
        Assert.Equal(UiSemanticTargetStatus.Ready, inspector.Target(Textures + ".draft"));
        Assert.Equal(UiSemanticTargetStatus.Hidden, inspector.Target(Textures + ".path"));
        var denied = owner.Commands.RequestExitAsync();
        Assert.False(Await(denied));
        Assert.Equal(EditorDocumentCommands.TextureDraftWarning, owner.Commands.Read().Error?.Message);
        Assert.Equal(0, owner.RuntimeStops);
        inspector.Click(Textures);
        Assert.Equal("second.png", inspector.Control<RichTextEditor>(Textures + ".path").Text);

        var external = Assert.Single(Await(catalog.RenameAsync(renamed, original.Id, "External", default)).Textures);
        inspector.ReplaceText(Textures + ".name", "Keep my draft");
        inspector.Click(Textures + ".replace");
        inspector.Complete();
        Assert.Equal(external, Assert.Single(Read(catalog).Textures));
        Assert.NotEmpty(inspector.Control<RichTextEditor>(Textures + ".status").Text);
        inspector.Click(Textures);
        Assert.Equal(UiSemanticTargetStatus.Ready, inspector.Target(Textures + ".status"));
        Assert.Equal(UiSemanticTargetStatus.Ready, inspector.Target(Textures + ".draft"));
        inspector.Click(Textures);
        Assert.Equal("Keep my draft", inspector.Control<RichTextEditor>(Textures + ".name").Text);
        Assert.Equal("second.png", inspector.Control<RichTextEditor>(Textures + ".path").Text);
        Assert.Equal(UiSemanticTargetStatus.Disabled, inspector.Target(Textures + ".reload"));
        inspector.Click(Textures + ".cancel");
        inspector.Click(Textures + ".reload");
        inspector.Complete();
        Assert.Equal("External", inspector.Control<RichTextEditor>(Textures + ".name").Text);
        Assert.Equal("first.png", inspector.Control<RichTextEditor>(Textures + ".path").Text);
        Assert.Equal(UiSemanticTargetStatus.Hidden, inspector.Target(Textures + ".draft"));

        inspector.ReplaceText(Textures + ".name", "Uncommitted name");
        inspector.ReplaceText(Textures + ".path", "second.png");
        inspector.Click(Textures + ".replace");
        inspector.Complete();
        var replaced = Assert.Single(Read(catalog).Textures);
        Assert.Equal(external with { SourcePath = "second.png", Revision = replaced.Revision }, replaced);
        Assert.NotEqual(external.Revision, replaced.Revision);
        Assert.Equal("Uncommitted name", inspector.Control<RichTextEditor>(Textures + ".name").Text);
        Assert.Equal(UiSemanticTargetStatus.Ready, inspector.Target(Textures + ".draft"));
        inspector.Click(Textures + ".cancel");
        Assert.Equal("External", inspector.Control<RichTextEditor>(Textures + ".name").Text);
        Assert.Equal("second.png", inspector.Control<RichTextEditor>(Textures + ".path").Text);
        Assert.Null(owner.Document.Read().Content);
    }

    [Fact]
    public void TextureMetadataAndRenameRemainAvailableWithoutDecoder()
    {
        if (!CanCapture) return;
        using var owner = new DocumentUiFixture(createTextureCatalog: project => new(project));
        var catalog = new ProjectTextureCatalog(owner.Files.Project, AetherEditorConfigurationExtensions.DecodeProjectTexture);
        WritePngs(owner);
        var original = Assert.Single(Await(catalog.ImportAsync(Read(catalog), "Original", "first.png", default)).Textures);
        File.Delete(Path.Combine(owner.Files.Project.RootDirectory, "first.png"));
        using var inspector = new RoutedInspector(owner, null);
        inspector.Click(Textures);
        inspector.Complete();
        Assert.Equal(UiSemanticTargetStatus.Disabled, inspector.Target(Textures + ".new"));
        inspector.Click($"{Textures}.item.{original.Id:N}");
        Assert.Equal("first.png", inspector.Control<RichTextEditor>(Textures + ".path").Text);
        Assert.True(inspector.Control<RichTextEditor>(Textures + ".path").Options.IsReadOnly);
        Assert.Equal(UiSemanticTargetStatus.Disabled, inspector.Target(Textures + ".replace"));
        inspector.ReplaceText(Textures + ".name", "Metadata only");
        inspector.Click(Textures + ".rename");
        inspector.Complete();
        Assert.Equal(original with { Name = "Metadata only" }, Assert.Single(Read(catalog).Textures));
    }

    [Fact]
    public void MeshMetadataAndRenameRemainAvailableWithoutModelCatalog()
    {
        if (!CanCapture) return;
        using var owner = new DocumentUiFixture(createMeshCatalog: project => new(project));
        var registered = new RegisteredMeshSourceFixture(owner.Files.Project);
        using var sources = Use(registered);
        var source = Await(registered.AddAsync("panel.gltf"));
        var catalog = new ProjectMeshCatalog(owner.Files.Project, registered.Models);
        var original = Assert.Single(Await(catalog.CreateAsync(Await(catalog.ReadAsync(default)), source, "Panel", default)).Meshes);
        File.Delete(Path.Combine(owner.Files.Project.RootDirectory, "panel.gltf"));
        using var inspector = new RoutedInspector(owner, null);
        inspector.Click(Meshes);
        inspector.Complete();
        Assert.Equal(UiSemanticTargetStatus.Disabled, inspector.Target(Meshes + ".new"));
        inspector.Click($"{Meshes}.item.{original.Id:N}");
        Assert.Equal(UiSemanticTargetStatus.Disabled, inspector.Target(Meshes + ".replace"));
        Assert.Contains("panel.gltf", inspector.Control<RichTextEditor>(Meshes + ".metadata").Text);
        inspector.ReplaceText(Meshes + ".name", "Metadata only");
        inspector.Click(Meshes + ".rename");
        inspector.Complete();
        var renamed = Assert.Single(Await(catalog.ReadAsync(default)).Meshes);
        Assert.Equal("Metadata only", renamed.Name);
        Assert.Equal(original.MeshRevision, renamed.MeshRevision);
        Assert.Equal(original.DefaultBindingRevision, renamed.DefaultBindingRevision);
        Assert.Equal(original.PayloadHash, renamed.PayloadHash);
        Assert.Null(owner.Document.Read().Content);
    }

    [Fact]
    public void TextureAndMeshPanelsAreHiddenWhenNoCatalogIsConfigured()
    {
        using var owner = new DocumentUiFixture(materialCatalog: true);
        using var inspector = new RoutedInspector(owner, null);
        Assert.Equal(UiSemanticTargetStatus.Ready, inspector.Target(Materials));
        Assert.Equal(UiSemanticTargetStatus.Hidden, inspector.Target(Textures));
        Assert.Equal(UiSemanticTargetStatus.Hidden, inspector.Target(Meshes));
    }

    private static void WritePngs(DocumentUiFixture owner)
    {
        var root = owner.Files.Project.RootDirectory;
        File.WriteAllBytes(Path.Combine(root, "first.png"), Convert.FromBase64String(FirstPng));
        File.WriteAllBytes(Path.Combine(root, "second.png"), Convert.FromBase64String(SecondPng));
    }

    private static ProjectTextureCatalogSnapshot Read(ProjectTextureCatalog catalog) => Await(catalog.ReadAsync(default));
    private static IDisposable Use(RegisteredMeshSourceFixture fixture) => new SourceLifetime(fixture);
    private sealed class SourceLifetime(RegisteredMeshSourceFixture fixture) : IDisposable
    {
        public void Dispose() => DocumentUiFixture.Wait(fixture.DisposeAsync().AsTask());
    }
    private static T Await<T>(Task<T> task) => task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
}
