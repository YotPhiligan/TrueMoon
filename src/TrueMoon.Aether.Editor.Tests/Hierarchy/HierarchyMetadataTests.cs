using System.Text.Json.Nodes;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Hierarchy;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Hierarchy;

public sealed class HierarchyMetadataTests
{
    [Fact]
    public void Rename_EnterCommitsUnicodeAndUndoRedoPreserveSelectionAndStableControls()
    {
        using var test = new HierarchyFixture(); var view = new EditorHierarchyView(test.Model, new EditorHierarchyViewStyle());
        using var ui = DocumentUiFixture.Ui(view); ui.Update(); ui.Focus(view.Tree);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.F2)); ui.Update();
        Assert.True(view.NameEditor.IsFocused); Assert.True(test.Model.IsRenaming); Assert.False(view.Tree.MoveEnabled);
        Assert.Equal(view.NameEditor.Value.Length, view.NameEditor.SelectionLength);
        var row = view.Tree.GetItemBounds(HierarchyFixture.Child)!.Value;
        Assert.Equal(row.Y, view.NameEditor.Bounds.Y); Assert.Equal(row.Height, view.NameEditor.Bounds.Height);
        ui.HandleInput(new(InputKind.Text, Text: "Страж 🛰️"));
        ui.SetTheme(Theme.Light); view.WithViewStyle(new EditorHierarchyViewStyle()); test.Model.Refresh(); ui.Update();
        Assert.Equal("Страж 🛰️", view.NameEditor.Value); Assert.True(view.NameEditor.IsFocused);
        Assert.Equal("Страж 🛰️".Length, view.NameEditor.CaretIndex);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Enter)); ui.Update();
        var content = test.Owner.Document.Read().Content!;
        Assert.Equal(8, content.Document.Revision); Assert.Equal(1, content.UndoCount); Assert.True(test.Owner.Document.Read().IsDirty);
        Assert.Equal("Страж 🛰️", content.Resolved.Entities[DocumentData.Child].Name);
        Assert.Equal(HierarchyFixture.Child, view.Tree.SelectedId); Assert.Equal("Страж 🛰️", view.Tree.Items.Single(item => item.Id == HierarchyFixture.Child).Text);
        Assert.False(view.NameEditor.IsVisible); Assert.True(view.Tree.IsFocused); Assert.Equal(4, DocumentData.X(content));
        test.Transform.Undo(); test.Synchronize(); ui.Update(); Assert.Null(test.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].Name);
        test.Transform.Redo(); test.Synchronize(); ui.Update(); Assert.Equal("Страж 🛰️", view.Tree.Items.Single(item => item.Id == HierarchyFixture.Child).Text);
    }

    [Theory]
    [InlineData("escape")] [InlineData("focus-lost")] [InlineData("other-control")]
    [InlineData("selection")] [InlineData("revision")] [InlineData("replacement")] [InlineData("dispose")]
    public void Rename_CancellationNeverCommitsDraftToChangedOwner(string cause)
    {
        using var test = new HierarchyFixture(); var view = new EditorHierarchyView(test.Model, new EditorHierarchyViewStyle());
        using var ui = DocumentUiFixture.Ui(view); ui.Update(); ui.Focus(view.Tree);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.F2)); ui.Update();
        ui.HandleInput(new(InputKind.Text, Text: "uncommitted"));
        switch (cause)
        {
            case "escape": ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape)); break;
            case "focus-lost": ui.HandleInput(new(InputKind.FocusLost)); break;
            case "other-control": ui.Focus(view.Tree); break;
            case "selection": test.Model.Select(HierarchyFixture.Parent); break;
            case "revision": test.Transform.Fields[0].Value = "12"; test.Transform.Apply(); test.Synchronize(); break;
            case "replacement": DocumentUiFixture.Wait(test.Owner.Document.NewAsync(DocumentData.Scene())); test.Synchronize(); break;
            case "dispose": ui.Dispose(); break;
        }
        Assert.False(test.Model.IsRenaming); Assert.False(test.Model.CommitRename());
        var content = test.Owner.Document.Read().Content!;
        Assert.Null(content.Resolved.Entities[DocumentData.Child].Name); Assert.Equal(cause == "revision" ? 1 : 0, content.UndoCount);
        Assert.Equal(cause == "revision" ? 8 : 7, content.Document.Revision);
    }

    [Fact]
    public void Rename_InvalidDraftRetainsInputAndEmptyDraftRestoresFallbackWithoutChangingId()
    {
        using var test = new HierarchyFixture();
        Assert.True(test.Model.BeginRename()); test.Model.RenameText = new string('x', 129);
        Assert.False(test.Model.CommitRename()); Assert.True(test.Model.IsRenaming); Assert.Contains("InvalidName", test.Model.ErrorText);
        Assert.Equal(7, test.Owner.Document.Read().Content!.Document.Revision);
        test.Model.RenameText = "Named"; Assert.True(test.Model.CommitRename());
        Assert.True(test.Model.BeginRename()); test.Model.RenameText = ""; Assert.True(test.Model.CommitRename());
        Assert.Null(test.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].Name);
        Assert.Equal(HierarchyFixture.Child, test.Model.SelectedId); Assert.Equal(2, test.Owner.Document.Read().Content!.UndoCount);
        Assert.True(test.Model.BeginRename()); Assert.True(test.Model.CommitRename());
        Assert.Equal(2, test.Owner.Document.Read().Content!.UndoCount);
    }

    [Fact]
    public void Reorder_KeyboardAndEdgeDropUseSharedHistoryAndRetainSelection()
    {
        var source = DocumentData.Scene();
        var scene = new SceneDocument(source.Id, source.Revision, source.Entities.Select(entity =>
            new EntityInstance(entity.Id, entity.DefinitionId, entity.Changes, null)), source.Definitions, source.Schemas);
        using var test = new HierarchyFixture(scene); var view = new EditorHierarchyView(test.Model, new EditorHierarchyViewStyle());
        using var ui = DocumentUiFixture.Ui(view); ui.Update(); ui.Focus(view.Tree);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Up, Control: true)); ui.Update();
        Assert.Equal(new[] { HierarchyFixture.Child, HierarchyFixture.Parent }, view.Tree.VisibleItems.Select(item => item.Id));
        Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount); Assert.Equal(HierarchyFixture.Child, test.Model.SelectedId);
        var child = view.Tree.GetItemBounds(HierarchyFixture.Child)!.Value; var parent = view.Tree.GetItemBounds(HierarchyFixture.Parent)!.Value;
        ui.HandleInput(new(InputKind.PointerDown, child.X + 80, child.Y + 12));
        ui.HandleInput(new(InputKind.PointerMove, parent.X + 80, parent.Y + parent.Height - 2));
        ui.HandleInput(new(InputKind.PointerUp, parent.X + 80, parent.Y + parent.Height - 2)); ui.Update();
        Assert.Equal(new[] { HierarchyFixture.Parent, HierarchyFixture.Child }, view.Tree.VisibleItems.Select(item => item.Id));
        Assert.Equal(2, test.Owner.Document.Read().Content!.UndoCount); Assert.All(test.Owner.Document.Read().Content!.Document.Entities, entity => Assert.Null(entity.ParentId));
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Down, Control: true));
        Assert.Equal(2, test.Owner.Document.Read().Content!.UndoCount);
        test.Transform.Undo(); test.Synchronize(); ui.Update();
        Assert.Equal(new[] { HierarchyFixture.Child, HierarchyFixture.Parent }, view.Tree.VisibleItems.Select(item => item.Id));
    }

    [Fact]
    public void Codec_V1RemainsReadableAndV2RoundTripsMetadataAndRejectsDowngrade()
    {
        var codec = new SceneFileCodec(DocumentData.Factory()); var source = DocumentData.Scene();
        var v1 = codec.Encode(source); var legacy = codec.Decode(v1);
        Assert.All(legacy.Entities, entity => { Assert.Null(entity.Name); Assert.Null(entity.SiblingOrder); });
        using var authoring = DocumentData.Factory().Create(legacy);
        Assert.True(authoring.EditHierarchy(new(DocumentData.Operation(7), [new(DocumentData.Child, "Моб 🛰️")],
            [new(DocumentData.Child, null, EntityPlacement.Before, DocumentData.Parent)])).Succeeded);
        var bytes = codec.Encode(authoring.Read().Document); var json = JsonNode.Parse(bytes)!;
        Assert.Equal(2, json["formatVersion"]!.GetValue<int>());
        var restored = codec.Decode(bytes); Assert.Equal(bytes, codec.Encode(restored));
        using var reopened = DocumentData.Factory().Create(restored);
        Assert.Equal("Моб 🛰️", reopened.Read().Resolved.Entities[DocumentData.Child].Name);
        Assert.Equal(new[] { DocumentData.Child, DocumentData.Parent }, reopened.Read().Resolved.Roots);
        Assert.Equal(0, reopened.Read().UndoCount); Assert.Equal(4, DocumentData.X(reopened.Read()));
        json["formatVersion"] = 1; Assert.Throws<InvalidDataException>(() => codec.Decode(DocumentData.Bytes(json)));
    }

    [Fact]
    public void RenameAndReorder_SaveReopenRetainMetadataAndCleanHistory()
    {
        var store = new JsonSceneDocumentStore(new SceneFileCodec(DocumentData.Factory()));
        using var test = new HierarchyFixture(sceneStore: store);
        Assert.True(test.Model.BeginRename()); test.Model.RenameText = "Saved Guardian"; Assert.True(test.Model.CommitRename());
        Assert.True(test.Model.Place(HierarchyFixture.Child, null, HierarchyFixture.Parent, EntityPlacement.Before));
        test.Owner.Run(DocumentCommand.Save, "metadata.aetherscene"); Assert.False(test.Owner.Document.Read().IsDirty);
        test.Owner.Run(DocumentCommand.Close); test.Workspace.SetDocument(null);
        test.Owner.Run(DocumentCommand.Open, "metadata.aetherscene"); test.Synchronize();
        var reopened = test.Owner.Document.Read().Content!;
        Assert.Equal("Saved Guardian", reopened.Resolved.Entities[DocumentData.Child].Name);
        Assert.Equal(new[] { DocumentData.Child, DocumentData.Parent }, reopened.Resolved.Roots);
        Assert.Equal(0, reopened.UndoCount); Assert.Equal(0, reopened.RedoCount); Assert.False(test.Owner.Document.Read().IsDirty);
        Assert.Equal(4, DocumentData.X(reopened));
    }

    [Theory]
    [InlineData("name", "42")] [InlineData("name", "\" \"")] [InlineData("name", "\"bad\\nname\"")]
    [InlineData("siblingOrder", "-1")] [InlineData("siblingOrder", "1.5")] [InlineData("siblingOrder", "2147483648")]
    public void Codec_V2RejectsInvalidAndMissingMetadata(string key, string value)
    {
        var codec = new SceneFileCodec(DocumentData.Factory()); var json = JsonNode.Parse(codec.Encode(DocumentData.Scene()))!;
        json["formatVersion"] = 2;
        foreach (var entity in json["entities"]!.AsArray()) { entity!["name"] = null; entity["siblingOrder"] = null; }
        var changed = json.DeepClone(); changed["entities"]![0]![key] = JsonNode.Parse(value);
        Assert.Throws<InvalidDataException>(() => codec.Decode(DocumentData.Bytes(changed)));
        json["entities"]![0]!.AsObject().Remove(key);
        Assert.Throws<InvalidDataException>(() => codec.Decode(DocumentData.Bytes(json)));
    }
}
