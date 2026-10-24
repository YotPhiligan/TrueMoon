using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Hierarchy;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Editing;
using TrueMoon.Aether.Sessions;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Hierarchy;

public sealed class EditorHierarchyShellTests
{
    [Fact]
    public void Shell_TreeSelectionTransformCommitParentMoveAndKeyboardHistoryShareOneRetainedDocument()
    {
        using var test = new HierarchyFixture(); using var documents = new EditorDocumentViewModel(test.Owner.Commands);
        using var model = new EditorViewModel(test.Workspace, new((_, _) => Task.CompletedTask, CancellationToken.None), documents, test.Transform, test.Model);
        var products = 0;
        var view = new EditorView(model, new EditorViewStyle(),
            new Factory<EditorDocumentView>(() => new(documents, new EditorDocumentViewStyle())),
            new Factory<EditorTransformView>(() => new(test.Transform, new EditorTransformViewStyle())), false, null,
            new Factory<EditorHierarchyView>(() => { products++; return new(test.Model, new EditorHierarchyViewStyle()); }));
        using var ui = DocumentUiFixture.Ui(view); Refresh();
        Assert.Equal(1, products); Assert.Empty(view.Entities); Assert.True(view.EntityList.IsDisposed);
        var hierarchy = view.Hierarchy!; var tree = hierarchy.Tree; var transform = view.Transform!; var input = transform.Fields["x"];
        hierarchy.Tree.Select(HierarchyFixture.Parent); ui.Update();
        Assert.Equal(HierarchyFixture.Parent, model.SelectedId); Assert.Equal("2", input.Value);
        hierarchy.Tree.Select(HierarchyFixture.Child); ui.Update(); ui.Focus(input);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true)); ui.HandleInput(new(InputKind.Text, Text: "45"));
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Enter, Control: true)); Refresh();
        Assert.Equal(45, DocumentData.X(test.Owner.Document.Read().Content!)); Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
        Assert.Contains(model.Properties, property => property.Key == "x" && property.Value == "45");
        ui.Focus(tree); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Left, Control: true)); Refresh();
        Assert.Null(test.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].ParentId);
        Assert.Equal(2, test.Owner.Document.Read().Content!.UndoCount); Assert.Equal("45", input.Value);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Z, Control: true)); Refresh();
        Assert.Equal(DocumentData.Parent, test.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].ParentId); Assert.Equal("45", input.Value);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Z, Control: true)); Refresh();
        Assert.Equal("4", input.Value); Assert.False(test.Owner.Document.Read().IsDirty); Assert.Equal(0, test.Owner.Document.Read().Content!.UndoCount);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Y, Control: true)); Refresh();
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Y, Control: true)); Refresh();
        Assert.Null(test.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].ParentId); Assert.Equal("45", input.Value);
        ui.SetTheme(Theme.Light); ui.Update();
        Assert.Same(hierarchy, view.Hierarchy); Assert.Same(tree, hierarchy.Tree); Assert.Same(transform, view.Transform); Assert.Same(input, transform.Fields["x"]);
        Assert.Equal(HierarchyFixture.Child, tree.SelectedId); Assert.True(tree.IsFocused);
        ui.Dispose(); Assert.True(hierarchy.IsDisposed); Assert.True(tree.IsDisposed); Assert.True(transform.IsDisposed);
        Assert.False(test.Owner.Commands.Read().Closed); test.Model.Select(HierarchyFixture.Parent); Assert.Equal(HierarchyFixture.Parent, test.Workspace.SelectedId);

        void Refresh() { model.Refresh(new(SessionState.Stopped), EditorWorkspaceTests.Status(GameEditorOperation.Idle, false)); ui.Update(); }
    }

    [Fact]
    public void NullHierarchyFactoryProduct_CleansAlreadyCreatedViewsAndPreservesBorrowedOwners()
    {
        using var test = new HierarchyFixture(); using var documents = new EditorDocumentViewModel(test.Owner.Commands);
        using var model = new EditorViewModel(test.Workspace, new((_, _) => Task.CompletedTask, CancellationToken.None), documents, test.Transform, test.Model);
        EditorDocumentView createdDocument = null!; EditorTransformView createdTransform = null!;
        var error = Assert.Throws<InvalidOperationException>(() => new EditorView(model, new EditorViewStyle(),
            new Factory<EditorDocumentView>(() => createdDocument = new(documents, new EditorDocumentViewStyle())),
            new Factory<EditorTransformView>(() => createdTransform = new(test.Transform, new EditorTransformViewStyle())), false, null,
            new Factory<EditorHierarchyView>(() => null!)));
        Assert.Contains("Hierarchy view factory returned null", error.Message);
        Assert.True(createdDocument.IsDisposed); Assert.True(createdTransform.IsDisposed); Assert.True(createdTransform.Fields["x"].IsDisposed);
        Assert.False(test.Owner.Commands.Read().Closed); Assert.Equal(0, test.Owner.Authoring.Products[0].Disposals);
        Assert.True(test.Model.Move(HierarchyFixture.Child, null)); Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
    }

    [Fact]
    public void RootStyleFailure_DisposesFactoryCreatedHierarchyOnceWithoutOwningItsModel()
    {
        using var test = new HierarchyFixture();
        using var model = new EditorViewModel(test.Workspace, new((_, _) => Task.CompletedTask, CancellationToken.None));
        EditorHierarchyView created = null!; var cleanup = new Cleanup(); var failure = new IOException("shell style failed");
        var error = Assert.Throws<IOException>(() => new EditorView(model, new FailingStyle(failure), null, null, false, null,
            new Factory<EditorHierarchyView>(() => { created = new(test.Model, new EditorHierarchyViewStyle()); created.Own(cleanup); return created; })));
        Assert.Same(failure, error); Assert.True(created.IsDisposed); Assert.True(created.Tree.IsDisposed); Assert.Equal(1, cleanup.Count);
        Assert.True(test.Model.Move(HierarchyFixture.Child, null)); Assert.False(test.Owner.Commands.Read().Closed);
    }

    private sealed class Cleanup : IDisposable { public int Count; public void Dispose() => Count++; }
    private sealed class FailingStyle(Exception error) : IViewStyle<EditorView>
    { public void Configure(IViewStyleBuilder<EditorView> styles) { styles.Root().FontSize(13); throw error; } }
}
