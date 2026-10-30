using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Hierarchy;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Scenes;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Hierarchy;

public sealed class EntityOperationsTests
{
    [Theory]
    [InlineData(EditorEntityKind.Empty)] [InlineData(EditorEntityKind.Cube)]
    public void NewEmptyScene_CreateSelectsEditableEntityAndUndoRestoresEmptyCatalog(EditorEntityKind kind)
    {
        using var test = new HierarchyFixture();
        DocumentUiFixture.Wait(test.Owner.Document.NewAsync(DocumentData.Empty())); test.Synchronize();
        Assert.True(test.Model.CanCreate); Assert.False(test.Model.CanEditSelected);
        Assert.True(test.Model.CreateEntity(kind)); var id = new EntityId(Guid.Parse(test.Model.SelectedId!));
        var content = test.Owner.Document.Read().Content!; var entity = content.Resolved.Entities[id];
        Assert.Equal(kind == EditorEntityKind.Cube ? "Куб" : "Объект", entity.Name);
        Assert.Null(entity.ParentId); Assert.Single(content.Document.Entities); Assert.Equal(1, content.UndoCount);
        Assert.Single(content.Document.Definitions); Assert.Equal(kind == EditorEntityKind.Cube ? 2 : 1, content.Document.Schemas.Length);
        Assert.Contains(entity.Components.Values, component => component.TypeId == BuiltInComponents.TransformId);
        Assert.Equal(kind == EditorEntityKind.Cube, entity.Components.Values.Any(component => component.TypeId == BuiltInComponents.AppearanceId));
        Assert.True(test.Transform.CanEdit); Assert.True(test.Transform.CanUndo);
        test.Transform.Fields.Single(field => field.Key == "x").Value = "2.25"; test.Transform.Apply(); test.Synchronize();
        Assert.Equal(2.25, X(test, id)); Assert.Equal(2, test.Owner.Document.Read().Content!.UndoCount);
        test.Transform.Undo(); test.Synchronize(); Assert.Equal(0, X(test, id));
        test.Transform.Undo(); test.Synchronize(); Assert.Empty(test.Owner.Document.Read().Content!.Document.Entities);
        Assert.Empty(test.Owner.Document.Read().Content!.Document.Definitions); Assert.Empty(test.Owner.Document.Read().Content!.Document.Schemas);
    }

    [Fact]
    public void CreateChildDuplicateDeleteAndHistory_UseOneDocumentAndPreserveInheritedValues()
    {
        using var test = new HierarchyFixture(); test.Model.Select(HierarchyFixture.Parent);
        Assert.True(test.Model.CreateEntity(EditorEntityKind.Empty, true)); var created = test.Model.SelectedId!;
        Assert.Equal(HierarchyFixture.Parent, test.Model.Items.Single(item => item.Id == created).ParentId);
        Assert.Equal(created, test.Model.Items.Last().Id);
        test.Model.Select(HierarchyFixture.Parent); Assert.True(test.Model.DuplicateSelected());
        var copy = new EntityId(Guid.Parse(test.Model.SelectedId!)); var content = test.Owner.Document.Read().Content!;
        Assert.Equal(6, content.Document.Entities.Length); Assert.Equal(2, content.Resolved.Children[copy].Length);
        Assert.Equal(2, X(test, copy)); Assert.Equal(4, DocumentData.X(content)); Assert.Equal(2, content.UndoCount);
        Assert.True(test.Model.DeleteSelected()); Assert.Equal(3, test.Owner.Document.Read().Content!.Document.Entities.Length);
        Assert.Null(test.Model.SelectedId); Assert.False(test.Model.CanEditSelected);
        test.Transform.Undo(); test.Synchronize(); Assert.Equal(6, test.Owner.Document.Read().Content!.Document.Entities.Length);
        test.Transform.Redo(); test.Synchronize(); Assert.Equal(3, test.Owner.Document.Read().Content!.Document.Entities.Length);
        test.Model.Select(created); Assert.True(test.Model.DeleteSelected()); Assert.Equal(HierarchyFixture.Parent, test.Model.SelectedId);
        Assert.Equal(4, DocumentData.X(test.Owner.Document.Read().Content!));
    }

    [Fact]
    public void CreateDeleteUndoSaveReopen_PreserveNewIdsCatalogAndValuesWithCleanHistory()
    {
        var store = new JsonSceneDocumentStore(new SceneFileCodec(DocumentData.Factory()));
        using var test = new HierarchyFixture(DocumentData.Empty(), sceneStore: store);
        Assert.True(test.Model.CreateEntity(EditorEntityKind.Cube)); var root = test.Model.SelectedId!;
        Assert.True(test.Model.CreateEntity(EditorEntityKind.Empty, true)); var child = test.Model.SelectedId!;
        test.Transform.Fields.Single(field => field.Key == "x").Value = "3.5"; test.Transform.Apply(); test.Synchronize();
        test.Model.Select(root); Assert.True(test.Model.DeleteSelected()); Assert.Empty(test.Model.Items);
        test.Transform.Undo(); test.Synchronize();
        test.Owner.Run(DocumentCommand.Save, "created.aetherscene"); test.Owner.Run(DocumentCommand.Close); test.Workspace.SetDocument(null);
        test.Owner.Run(DocumentCommand.Open, "created.aetherscene"); test.Synchronize();
        var reopened = test.Owner.Document.Read().Content!;
        Assert.Equal(new[] { root, child }, test.Model.Items.Select(item => item.Id)); Assert.Equal(root, test.Model.Items[1].ParentId);
        Assert.Equal(3.5, X(test, new(Guid.Parse(child)))); Assert.Equal(2, reopened.Document.Definitions.Length);
        Assert.Equal(2, reopened.Document.Schemas.Length); Assert.Equal(0, reopened.UndoCount); Assert.Equal(0, reopened.RedoCount);
        Assert.False(test.Owner.Document.Read().IsDirty);
    }

    [Theory]
    [InlineData("runtime")] [InlineData("draft")] [InlineData("rename")] [InlineData("stale")]
    [InlineData("replacement")] [InlineData("save")]
    public void StructuralAdmission_RejectsUnavailableOrStaleOwnerWithoutChangingContent(string reason)
    {
        using var test = new HierarchyFixture(); TaskCompletionSource<SceneFileStamp>? write = null;
        switch (reason)
        {
            case "runtime": test.Owner.RuntimeIdle = false; break;
            case "draft": test.Transform.Fields[0].Value = "18"; break;
            case "rename": Assert.True(test.Model.BeginRename()); break;
            case "stale": Assert.True(test.Owner.Document.Apply(DocumentData.Edit(7, 12)).Succeeded); break;
            case "replacement": DocumentUiFixture.Wait(test.Owner.Document.NewAsync(DocumentData.Scene())); break;
            case "save":
                write = new(TaskCreationOptions.RunContinuationsAsynchronously); test.Owner.Store.Write = (_, _, _, _) => write.Task;
                Assert.True(test.Owner.Commands.Execute(DocumentCommand.Save)); break;
        }
        var before = test.Owner.Document.Read().Content;
        try
        {
            Assert.False(test.Model.CreateEntity(EditorEntityKind.Cube));
            if (reason is "stale" or "replacement")
            {
                if (reason == "stale") Assert.True(test.Owner.Document.Apply(DocumentData.Edit(8, 13)).Succeeded);
                else { test.Owner.Run(DocumentCommand.Save, "replacement.aetherscene"); DocumentUiFixture.Wait(test.Owner.Document.NewAsync(DocumentData.Scene())); }
                before = test.Owner.Document.Read().Content;
            }
            Assert.False(test.Model.DeleteSelected()); Assert.Same(before, test.Owner.Document.Read().Content);
        }
        finally { write?.SetResult(new("saved")); if (write != null) test.Owner.Complete(); }
    }

    [Fact]
    public void Search_CyclesNamesAndIdsWithoutFilteringOrMutatingDocument()
    {
        using var test = new HierarchyFixture(); Assert.True(test.Model.BeginRename()); test.Model.RenameText = "Guardian"; Assert.True(test.Model.CommitRename());
        var before = test.Owner.Document.Read().Content; var items = test.Model.Items;
        test.Model.SearchText = "GUARD"; Assert.Equal(1, test.Model.MatchCount); test.Model.FindNext(); Assert.Equal(HierarchyFixture.Child, test.Model.SelectedId);
        test.Model.SearchText = HierarchyFixture.Parent; test.Model.FindNext(); Assert.Equal(HierarchyFixture.Parent, test.Model.SelectedId);
        test.Model.SearchText = "a846e4a2"; Assert.Equal(2, test.Model.MatchCount); test.Model.FindNext(); Assert.Equal(HierarchyFixture.Child, test.Model.SelectedId);
        test.Model.FindNext(); Assert.Equal(HierarchyFixture.Parent, test.Model.SelectedId);
        test.Model.SearchText = "missing"; Assert.False(test.Model.CanFindNext); test.Model.FindNext(); Assert.Equal(HierarchyFixture.Parent, test.Model.SelectedId);
        Assert.Same(items, test.Model.Items); Assert.Same(before, test.Owner.Document.Read().Content);
    }

    [Fact]
    public void View_ButtonsAndKeyboardShareCreateDuplicateDeleteCommandsAndRetainedSearch()
    {
        using var test = new HierarchyFixture(); DocumentUiFixture.Wait(test.Owner.Document.NewAsync(DocumentData.Empty())); test.Synchronize();
        var view = new EditorHierarchyView(test.Model, new EditorHierarchyViewStyle()); using var ui = DocumentUiFixture.Ui(view); ui.Update();
        Assert.True(view.CreateButton.IsEnabled); Assert.False(view.DeleteButton.IsEnabled); Assert.Equal(26, view.CreateButton.Width);
        DocumentUiFixture.Click(ui, view.CubeButton); ui.Update(); var root = test.Model.SelectedId!;
        Assert.Equal("Куб", Assert.Single(view.Tree.Items).Text);
        ui.Focus(view.Tree); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Insert, Control: true)); ui.Update();
        var child = test.Model.SelectedId!; Assert.Equal(root, view.Tree.Items.Single(item => item.Id == child).ParentId);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.D, Control: true)); ui.Update(); Assert.Equal(3, view.Tree.Items.Count);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Delete)); ui.Update(); Assert.Equal(2, view.Tree.Items.Count); Assert.Equal(root, view.Tree.SelectedId);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.F, Control: true)); Assert.True(view.Search.IsFocused);
        ui.HandleInput(new(InputKind.Text, Text: child)); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Enter)); ui.Update();
        Assert.Equal(child, view.Tree.SelectedId); Assert.True(view.Tree.IsExpanded(root)); Assert.True(view.Search.IsFocused);
        var tree = view.Tree; var search = view.Search; ui.SetTheme(Theme.Light); view.WithViewStyle(new EditorHierarchyViewStyle()); ui.Update();
        Assert.Same(tree, view.Tree); Assert.Same(search, view.Search); Assert.Equal(child, view.Search.Value); Assert.True(view.Search.IsFocused);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape)); ui.Update(); Assert.Equal("", view.Search.Value); Assert.True(view.Tree.IsFocused);
        Assert.Equal(4, test.Owner.Document.Read().Content!.UndoCount);
    }

    [Fact]
    public void QueuedSelectionAndMultipleStructuralCommits_ProjectLatestItemsBeforeSelectingNewIdentity()
    {
        using var test = new HierarchyFixture(); var view = new EditorHierarchyView(test.Model, new EditorHierarchyViewStyle());
        using var ui = DocumentUiFixture.Ui(view); ui.Update();
        test.Model.Select(HierarchyFixture.Parent);
        Assert.True(test.Model.CreateEntity(EditorEntityKind.Cube)); Assert.True(test.Model.CreateEntity(EditorEntityKind.Empty, true));
        Assert.True(test.Model.DuplicateSelected()); var selected = test.Model.SelectedId;
        ui.Update();
        Assert.Equal(selected, view.Tree.SelectedId); Assert.Contains(view.Tree.Items, item => item.Id == selected);
        Assert.Equal(test.Model.Items.Select(item => item.Id), view.Tree.Items.Select(item => item.Id));
        Assert.Equal(5, view.Tree.Items.Count); Assert.Equal(3, test.Owner.Document.Read().Content!.UndoCount);
    }

    [Fact]
    public void ContextMenu_RightClickSelectsTargetAndSharesCommandsWithKeyboardAndButtons()
    {
        using var test = new HierarchyFixture(); var view = new EditorHierarchyView(test.Model, new EditorHierarchyViewStyle());
        using var ui = DocumentUiFixture.Ui(view); ui.Update(); var row = view.Tree.GetItemBounds(HierarchyFixture.Parent)!.Value;
        ui.HandleInput(new(InputKind.PointerDown, row.X + 80, row.Y + 12, Button: 1)); ui.Update();
        Assert.Equal(HierarchyFixture.Parent, test.Model.SelectedId); var popup = Assert.IsType<MenuPopup>(view.Overlays.Popup);
        var duplicate = popup.Children.OfType<MenuItem>().Single(item => item.ActionId == "duplicate");
        DocumentUiFixture.Click(ui, duplicate); ui.Update(); Assert.Null(view.Overlays.Popup);
        Assert.Equal(4, view.Tree.Items.Count); Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
        Assert.NotEqual(HierarchyFixture.Parent, test.Model.SelectedId);
        ui.Focus(view.Tree); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.F10, Shift: true)); ui.Update(); Assert.IsType<MenuPopup>(view.Overlays.Popup);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape)); ui.Update(); Assert.Null(view.Overlays.Popup); Assert.True(view.Tree.IsFocused);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ContextMenu_StaleSnapshotOrRuntimeAdmissionFencesLateActivation(bool runtime)
    {
        using var test = new HierarchyFixture(); var view = new EditorHierarchyView(test.Model, new EditorHierarchyViewStyle());
        using var ui = DocumentUiFixture.Ui(view); ui.Update(); ui.Focus(view.Tree);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.F10, Shift: true)); ui.Update();
        var popup = Assert.IsType<MenuPopup>(view.Overlays.Popup); var remove = popup.Children.OfType<MenuItem>().Single(item => item.ActionId == "delete");
        if (runtime) { test.Owner.RuntimeIdle = false; test.Model.Refresh(); }
        else { Assert.True(test.Owner.Document.Apply(DocumentData.Edit(7, 19)).Succeeded); test.Synchronize(); }
        var before = test.Owner.Document.Read().Content; remove.Command.Execute(); Assert.Same(before, test.Owner.Document.Read().Content);
        ui.Update(); Assert.Null(view.Overlays.Popup); Assert.True(popup.IsDisposed); Assert.Equal(2, view.Tree.Items.Count);
    }

    [Fact]
    public void ContextRenameAndDispose_CancelDraftClosePopupAndKeepBorrowedModelUsable()
    {
        using var test = new HierarchyFixture(); var view = new EditorHierarchyView(test.Model, new EditorHierarchyViewStyle());
        var ui = DocumentUiFixture.Ui(view); ui.Update(); ui.Focus(view.Tree); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.F10, Shift: true)); ui.Update();
        var popup = Assert.IsType<MenuPopup>(view.Overlays.Popup);
        DocumentUiFixture.Click(ui, popup.Children.OfType<MenuItem>().Single(item => item.ActionId == "rename")); ui.Update();
        Assert.True(view.NameEditor.IsFocused); Assert.True(test.Model.IsRenaming); ui.Dispose();
        Assert.False(test.Model.IsRenaming); Assert.True(view.IsDisposed); Assert.True(popup.IsDisposed);
        Assert.True(test.Model.CreateEntity(EditorEntityKind.Empty)); Assert.False(test.Owner.Commands.Read().Closed);
    }

    private static double X(HierarchyFixture test, EntityId id) => test.Owner.Document.Read().Content!.Resolved.Entities[id].Components.Values
        .Single(component => component.TypeId == BuiltInComponents.TransformId).Properties["x"].Value.GetNumber();
}
