using System.Numerics;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Editor.Tests.Editing;
using TrueMoon.Aether.Editor.Tooling.Documents;
using TrueMoon.Aether.Editor.Tooling.Editing;
using TrueMoon.Aether.Editor.UI.Documents;
using TrueMoon.Aether.Editor.UI.Editing;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Tooling.Documents;
using TrueMoon.Aether.Tooling.Preview;
using TrueMoon.Aether.Tooling.Projects;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Alloy.Runtime.Semantics;
using TrueMoon.Argentis;
using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Layout;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Inspector;

public sealed class EditorMaterialAuthoringTests
{
    private const string Panel = "editor.materials";
    private static string Entity(int index) => DocumentData.Id(200 + index).ToString("D");
    private static string Slot(Guid id) => $"editor.inspector.material.slot.{id:N}";
    private static string Edit(Guid id) => $"editor.inspector.material.edit.{id:N}";
    private static string Copy(Guid id) => $"editor.inspector.material.copy.{id:N}";
    private static string Choice(Guid slot, Guid material) => $"editor.inspector.material.choice.{slot:N}.{material:N}";

    [Fact]
    public void SaveAndUndoMaterialDoNotChangeSceneHistory()
    {
        using var owner = new DocumentUiFixture(materialCatalog: true);
        using var inspector = new RoutedInspector(owner, null);
        var catalog = new ProjectMaterialCatalog(owner.Files.Project);
        Assert.Null(owner.Document.Read().Content);
        Assert.Equal(UiSemanticTargetStatus.Hidden, inspector.Target(Panel + ".name"));
        inspector.Click(Panel);
        inspector.Complete();
        Assert.Empty(Read(catalog).Materials);
        inspector.Click(Panel + ".new");
        Assert.Equal(Panel + ".name", inspector.Ui.FocusedElement?.SemanticId);
        Assert.Equal("Новый материал", inspector.Control<RichTextEditor>(Panel + ".name").Text);
        inspector.ReplaceText(Panel + ".name", "Plaster");
        inspector.Click(Panel + ".save");
        inspector.Complete();
        var original = Assert.Single(Read(catalog).Materials);
        Assert.Equal("Plaster", original.Name);
        Assert.Equal(Vector3.One, original.BaseColorSrgb);
        Assert.Null(original.BaseColorTextureId);
        Assert.Null(owner.Document.Read().Content);
        Assert.Equal(UiSemanticTargetStatus.Disabled, inspector.Target(Panel + ".undo"));

        owner.Run(DocumentCommand.Open, "room.aetherscene");
        inspector.Refresh(InspectorFixture.Child);
        var scene = owner.Document.Read().Content!.Document;
        var itemId = $"{Panel}.item.{original.Id:N}";
        var retained = inspector.Control<Button>(itemId);
        inspector.Click(itemId);
        inspector.ReplaceText(Panel + ".r", "1.5");
        Assert.Equal(UiSemanticTargetStatus.Disabled, inspector.Target(Panel + ".save"));
        Assert.Equal("1.5", inspector.Control<TextBox>(Panel + ".r").Value);
        inspector.Send(Panel + ".r", new(InputKind.KeyDown, Key: UiKey.Z, Control: true), intercepted: true);
        Assert.Equal("1", inspector.Control<TextBox>(Panel + ".r").Value);
        inspector.Send(Panel + ".r", new(InputKind.KeyDown, Key: UiKey.Y, Control: true), intercepted: true);
        Assert.Equal("1.5", inspector.Control<TextBox>(Panel + ".r").Value);
        inspector.ReplaceText(Panel + ".r", "0.25");
        inspector.Click(Panel);
        Assert.Equal(UiSemanticTargetStatus.Hidden, inspector.Target(Panel + ".r"));
        Assert.Equal(UiSemanticTargetStatus.Ready, inspector.Target(Panel + ".status"));
        var close = owner.Commands.RequestExitAsync();
        DocumentUiFixture.Wait(close);
        Assert.False(close.GetAwaiter().GetResult());
        Assert.Equal(EditorDocumentCommands.MaterialDraftWarning, owner.Commands.Read().Error?.Message);
        Assert.Equal(0, owner.RuntimeStops);
        Assert.Equal(original, Read(catalog).Find(original.Id));
        AssertUnchangedScene(owner, scene);
        owner.Run(DocumentCommand.Close);
        inspector.Refresh(null);
        Assert.Null(owner.Document.Read().Content);
        owner.Run(DocumentCommand.Open, "room.aetherscene");
        inspector.Refresh(InspectorFixture.Child);
        scene = owner.Document.Read().Content!.Document;
        inspector.Click(Panel);
        Assert.Equal("0.25", inspector.Control<TextBox>(Panel + ".r").Value);
        inspector.Click(Panel + ".save");
        inspector.Complete();
        var edited = original with { BaseColorSrgb = new(.25f, 1, 1) };
        Assert.Equal(edited, Read(catalog).Find(original.Id));
        Assert.Same(retained, inspector.Control<Button>(itemId));

        inspector.Click(Panel + ".undo");
        inspector.Complete();
        Assert.Equal(original, Read(catalog).Find(original.Id));
        Assert.Equal("1", inspector.Control<TextBox>(Panel + ".r").Value);
        inspector.Click(Panel + ".redo");
        inspector.Complete();
        Assert.Equal(edited, Read(catalog).Find(original.Id));
        Assert.Equal("0.25", inspector.Control<TextBox>(Panel + ".r").Value);
        AssertUnchangedScene(owner, scene);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CopyAssignUndoRedoKeepsTheSameAsset(bool namedSlots)
    {
        using var owner = ModelProject(out var models);
        var mesh = namedSlots ? models.UvMesh : models.PlainMesh;
        var slot = mesh.Slots[0].SlotId;
        var imported = mesh.Slots[0].MaterialId;
        var defaults = mesh.Slots.Select(value => value.MaterialId).ToArray();
        Open(owner, models, "first.aetherscene", (mesh.AssetId, mesh.BindingSetId.ToString("D")));
        using var first = new RoutedInspector(owner, Entity(0));
        first.Complete();
        first.Click(Slot(slot));
        first.Click(Edit(slot));
        Assert.Equal(Panel, first.Ui.FocusedElement?.SemanticId);
        Assert.True(first.Control<RichTextEditor>(Panel + ".name").Options.IsReadOnly);
        Assert.Equal(UiSemanticTargetStatus.Disabled, first.Target(Panel + ".save"));
        Assert.Empty(Read(new(owner.Files.Project)).Materials);
        first.Click(Slot(slot));
        first.Click(Copy(slot));
        first.Complete();
        var copy = Assert.Single(Read(new(owner.Files.Project)).Materials);
        Assert.NotEqual(imported, copy.Id);
        var source = models.Assets.ReadMaterial(imported)!;
        Assert.Equal(source.Name + " — копия", copy.Name);
        Assert.Equal(source.BaseColorSrgb, copy.BaseColorSrgb);
        Assert.Equal(source.BaseColorTextureId, copy.BaseColorTextureId);
        Assert.Equal(1, owner.Document.Read().Content!.UndoCount);
        AssertSlots(owner, 0, [copy.Id, .. defaults.Skip(1)]);

        using var otherOwner = new DocumentUiFixture(materialCatalog: true, modelImports: _ => models.CreateImports(), sharedProject: owner.Files.Project);
        Open(otherOwner, models, "second.aetherscene", (mesh.AssetId, mesh.BindingSetId.ToString("D")));
        using var second = new RoutedInspector(otherOwner, Entity(0));
        second.Complete();
        second.Click(Slot(slot));
        second.Click(Choice(slot, copy.Id));
        second.Click("editor.inspector.apply");
        second.Refresh(Entity(0));
        Assert.NotEqual(owner.Document.Read().Path, otherOwner.Document.Read().Path);
        Assert.NotEqual(owner.Document.Read().Content!.Document.Id, otherOwner.Document.Read().Content!.Document.Id);
        AssertSlots(otherOwner, 0, [copy.Id, .. defaults.Skip(1)]);

        first.Click("editor.inspector.undo");
        first.Refresh(Entity(0));
        AssertSlots(owner, 0, defaults);
        AssertSlots(otherOwner, 0, [copy.Id, .. defaults.Skip(1)]);
        Assert.Equal(copy, Assert.Single(Read(new(owner.Files.Project)).Materials));
        first.Click("editor.inspector.redo");
        first.Refresh(Entity(0));
        AssertSlots(owner, 0, [copy.Id, .. defaults.Skip(1)]);
        AssertSlots(otherOwner, 0, [copy.Id, .. defaults.Skip(1)]);
        Assert.Equal(copy, Assert.Single(Read(new(owner.Files.Project)).Materials));

        first.Click(Slot(slot));
        first.Click(Edit(slot));
        Assert.Equal(Panel + ".name", first.Ui.FocusedElement?.SemanticId);
        Assert.False(first.Control<RichTextEditor>(Panel + ".name").Options.IsReadOnly);
        Assert.Equal(copy.Name, first.Control<RichTextEditor>(Panel + ".name").Text);
    }

    [Fact]
    public void CopyFailureAndStaleSelectionNeverRetarget()
    {
        var entered = DocumentData.Signal();
        var release = DocumentData.Signal();
        var holdWrite = false;
        using var owner = ModelProject(out var models, project => new(project, token =>
        {
            if (!Volatile.Read(ref holdWrite)) return Task.CompletedTask;
            entered.TrySetResult();
            return release.Task.WaitAsync(token);
        }));
        var mesh = models.UvMesh;
        var slot = mesh.Slots[0].SlotId;
        var defaults = mesh.Slots.Select(value => value.MaterialId).ToArray();
        var renderer = (mesh.AssetId, mesh.BindingSetId.ToString("D"));
        Open(owner, models, "room.aetherscene", renderer, renderer);
        using var inspector = new RoutedInspector(owner, Entity(0));
        inspector.Complete();
        var scene = owner.Document.Read().Content!.Document;
        var catalog = new ProjectMaterialCatalog(owner.Files.Project);
        var external = new ProjectMaterial(Guid.NewGuid(), "External", Vector3.One, null);
        Await(catalog.WriteAsync(Read(catalog), external, default));
        inspector.Click(Slot(slot));
        inspector.Click(Copy(slot));
        inspector.Complete();
        AssertUnchangedScene(owner, scene);
        Assert.Equal(external, Assert.Single(Read(catalog).Materials));
        Assert.NotEmpty(inspector.Control<RichTextEditor>(Panel + ".status").Text);
        Assert.Equal(UiSemanticTargetStatus.NotFound, inspector.Target($"{Panel}.item.{external.Id:N}"));

        inspector.Click(Panel + ".reload");
        inspector.Complete();
        Volatile.Write(ref holdWrite, true);
        try
        {
            inspector.Click(Slot(slot));
            inspector.Click(Copy(slot));
            DocumentUiFixture.Wait(entered.Task);
            var pending = owner.Commands.Pending;
            Assert.False(pending.IsCompleted);
            inspector.Refresh(Entity(1));
            Assert.False(pending.IsCompleted);
            Assert.Equal(external, Assert.Single(Read(catalog).Materials));
        }
        finally
        {
            release.TrySetResult();
        }
        inspector.Complete();
        var copy = Assert.Single(Read(catalog).Materials, row => row.Id != external.Id);
        Assert.Contains("копия", copy.Name);
        AssertUnchangedScene(owner, scene);
        AssertSlots(owner, 0, defaults);
        AssertSlots(owner, 1, defaults);
        Assert.Equal("Копия создана, слот не изменён.", inspector.Control<RichTextEditor>(Panel + ".status").Text);
    }

    [Fact]
    public void MissingTextureRemainsVisibleUntilExplicitlyRemoved()
    {
        using var owner = new DocumentUiFixture(materialCatalog: true);
        var catalog = new ProjectMaterialCatalog(owner.Files.Project);
        var missing = Guid.NewGuid();
        var material = new ProjectMaterial(Guid.NewGuid(), "With\r\nmissing texture", Vector3.One, missing);
        Await(catalog.WriteAsync(Read(catalog), material, default));
        using var inspector = new RoutedInspector(owner, null);
        inspector.Click(Panel);
        inspector.Complete();
        inspector.Click($"{Panel}.item.{material.Id:N}");
        Assert.Equal("With\nmissing texture", inspector.Control<RichTextEditor>(Panel + ".name").Text);
        Assert.Contains(missing.ToString("D"), inspector.Control<Button>(Panel + ".texture").Value);
        inspector.ReplaceText(Panel + ".name", "Still missing");
        Assert.Equal(UiSemanticTargetStatus.Disabled, inspector.Target(Panel + ".save"));
        Assert.Equal(material, Read(catalog).Find(material.Id));
        inspector.Click(Panel + ".texture");
        inspector.Click(Panel + ".texture.none");
        inspector.Click(Panel + ".save");
        inspector.Complete();
        Assert.Equal(material with { Name = "Still missing", BaseColorTextureId = null }, Read(catalog).Find(material.Id));
    }

    [Fact]
    public void ImportedTextureIsAssignableToUvSlotButNotToMeshWithoutUv()
    {
        using var owner = ModelProject(out var models);
        var uv = models.UvMesh;
        var plain = models.PlainMesh;
        Open(owner, models, "room.aetherscene", (uv.AssetId, uv.BindingSetId.ToString("D")),
            (plain.AssetId, plain.BindingSetId.ToString("D")));
        using var inspector = new RoutedInspector(owner, Entity(0));
        inspector.Complete();
        inspector.Click(Panel);
        inspector.Click(Panel + ".new");
        inspector.Click(Panel + ".texture");
        inspector.Click($"{Panel}.texture.{models.TextureId:N}");
        inspector.Click(Panel + ".save");
        inspector.Complete();
        var authored = Assert.Single(Read(new(owner.Files.Project)).Materials);
        Assert.Equal(models.TextureId, authored.BaseColorTextureId);

        var slot = uv.Slots[0].SlotId;
        inspector.Click(Slot(slot));
        inspector.Click(Choice(slot, authored.Id));
        inspector.Click("editor.inspector.apply");
        AssertSlots(owner, 0, [authored.Id, uv.Slots[1].MaterialId]);
        inspector.Refresh(Entity(1));
        var before = owner.Document.Read().Content!;
        slot = plain.Slots[0].SlotId;
        inspector.Click(Slot(slot));
        var choice = Choice(slot, authored.Id);
        Assert.Equal(UiSemanticTargetStatus.Disabled, inspector.Target(choice));
        Assert.Contains("недоступен", inspector.Control<Button>(choice).Value);
        Assert.Equal(UiSemanticTargetStatus.Disabled,
            inspector.Ui.HandleSemanticInput(inspector.Ui.CaptureSemantics(), choice, new(InputKind.KeyDown, Key: UiKey.Enter)).Status);
        Assert.Same(before.Document, owner.Document.Read().Content!.Document);
        Assert.Equal(1, before.UndoCount);
        AssertSlots(owner, 1, [plain.Slots[0].MaterialId]);
    }

    [Fact]
    public void InspectorWithoutMaterialCatalogKeepsSceneActionsUsable()
    {
        using var owner = new DocumentUiFixture();
        owner.Run(DocumentCommand.Open, "room.aetherscene");
        using var inspector = new RoutedInspector(owner, InspectorFixture.Child);
        Assert.Equal(UiSemanticTargetStatus.Hidden, inspector.Target(Panel));
        var x = $"editor.inspector.transform.{DocumentData.Transform.Value:N}.x";
        inspector.ReplaceText(x, "9");
        inspector.Click("editor.inspector.apply");
        var content = owner.Document.Read().Content!;
        Assert.Equal(1, content.UndoCount);
        Assert.Equal(9, DocumentData.X(content));
        Assert.False(File.Exists(Path.Combine(owner.Files.Project.ContentDirectory, ".aether-material-catalog.json")));
    }

    private static DocumentUiFixture ModelProject(out MaterialModelFixture models,
        Func<ResolvedProject, ProjectMaterialCatalog>? createMaterialCatalog = null)
    {
        MaterialModelFixture? prepared = null;
        var owner = new DocumentUiFixture(materialCatalog: true,
            modelImports: project => (prepared = Await(MaterialModelFixture.CreateAsync(project))).CreateImports(),
            createMaterialCatalog: createMaterialCatalog);
        models = prepared ?? throw new InvalidOperationException("The project did not create model imports.");
        return owner;
    }

    internal static void Open(DocumentUiFixture owner, MaterialModelFixture models, string path, params (Guid Mesh, string Binding)[] renderers)
    {
        var template = SceneMaterialAuthoringTests.Scene(renderers);
        var scene = new SceneDocument(new(Guid.NewGuid()), template.Revision, template.Entities, template.Definitions, template.Schemas);
        owner.Store.File = new(scene, new("material scene fixture"));
        owner.Run(DocumentCommand.Open, path);
        var opened = owner.Document.Read();
        owner.Document.ModelImports!.Open(scene.Id.Value, opened.OpeningId, new([models.Selection], models.Content, new("import fixture")));
    }

    private static void AssertUnchangedScene(DocumentUiFixture owner, SceneDocument original)
    {
        var current = owner.Document.Read();
        Assert.Same(original, current.Content!.Document);
        Assert.False(current.IsDirty);
        Assert.Equal(0, current.Content.UndoCount);
        Assert.Equal(0, current.Content.RedoCount);
    }

    private static void AssertSlots(DocumentUiFixture owner, int index, Guid[] materials)
    {
        var state = owner.Document.Read();
        var component = state.Content!.Resolved.Entities[new(DocumentData.Id(200 + index))].Components[new(DocumentData.Id(210 + index))];
        var references = SceneRenderAssets.ResolveReferences(owner.Document.ReadRenderAssets(state.OpeningId),
            component.Properties["mesh"].Value.GetString(), component.Properties["bindings"].Value.GetString());
        Assert.Null(references.Diagnostic);
        var bindings = references.Definition!.Bindings;
        Assert.Equal(materials.Length, bindings.Length);
        for (var i = 0; i < bindings.Length; i++) Assert.Equal(materials[i], bindings[i].Material.AssetId);
    }

    private static ProjectMaterialCatalogSnapshot Read(ProjectMaterialCatalog catalog) => Await(catalog.ReadAsync(default));
    private static T Await<T>(Task<T> task) => task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();

    internal sealed class RoutedInspector : IDisposable
    {
        private readonly DocumentUiFixture _owner;
        private readonly EditorPreview _preview;
        private readonly EditorInspectorViewModel _model;
        private readonly EditorInspectorView _view;
        private string? _selection;
        public UiSession Ui { get; }

        public RoutedInspector(DocumentUiFixture owner, string? selected)
        {
            _owner = owner;
            _preview = new((snapshot, _) => Task.FromResult(EditingFixture.Receipt(snapshot)), () => Task.CompletedTask);
            _model = new(owner.Commands, _preview);
            _view = new(_model, new EditorInspectorViewStyle());
            Ui = InspectorFixture.Ui(_view, width: 540, height: 650);
            Refresh(selected);
        }

        public void Refresh(string? selected)
        {
            _selection = selected;
            _model.Refresh(selected);
            Drain();
        }

        public void Complete() { _owner.Complete(); Refresh(_selection); }
        public T Control<T>(string id) where T : Element => Assert.IsType<T>(Assert.Single(DocumentUiFixture.Tree(_view), element => element.SemanticId == id));
        public UiSemanticTargetStatus Target(string id) => Ui.FindSemanticTarget(Ui.CaptureSemantics(), id).Status;

        public void ReplaceText(string id, string value)
        {
            Click(id);
            Send(id, new(InputKind.KeyDown, Key: UiKey.A, Control: true));
            Send(id, new(InputKind.Text, Text: value));
        }

        public void Click(string id)
        {
            var element = Assert.Single(DocumentUiFixture.Tree(_view), candidate => candidate.SemanticId == id);
            for (var parent = element.Parent; parent != null; parent = parent.Parent)
            {
                if (parent is not ScrollViewer scroll) continue;
                scroll.BringIntoView(element.Bounds);
                Drain();
            }
            var snapshot = Ui.CaptureSemantics();
            var target = Ui.FindSemanticTarget(snapshot, id);
            Assert.Equal(UiSemanticTargetStatus.Ready, target.Status);
            var rect = target.Node!.VisibleBounds;
            Assert.True(rect.Width > 0 && rect.Height > 0, $"{id} has no visible bounds.");
            var x = rect.X + rect.Width / 2;
            var y = rect.Y + rect.Height / 2;
            Send(id, new(InputKind.PointerDown, x, y));
            Send(id, new(InputKind.PointerUp, x, y), handled: element is Button or RichTextEditor);
        }

        public void Send(string id, UiInput input, bool intercepted = false, bool handled = true)
        {
            var result = Ui.HandleSemanticInput(Ui.CaptureSemantics(), id, input);
            Assert.Equal(intercepted ? UiSemanticTargetStatus.Intercepted : UiSemanticTargetStatus.Routed, result.Status);
            Assert.Equal(intercepted ? "editor.inspector" : id, result.HitTargetId);
            Assert.Equal(handled, result.Input.Handled);
            Drain();
        }

        private void Drain()
        {
            for (var i = 0; i < 5 && Ui.NeedsUpdate; i++) Ui.Update();
        }

        public void Dispose() { Ui.Dispose(); _model.Dispose(); _preview.Dispose(); }
    }
}
