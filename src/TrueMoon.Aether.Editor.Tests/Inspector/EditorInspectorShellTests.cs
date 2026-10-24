using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Hierarchy;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Editing;
using TrueMoon.Aether.Sessions;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Inspector;

public sealed class EditorInspectorShellTests
{
    [Fact]
    public void Shell_SelectionGenericDraftAndHierarchyMovesShareHistoryAndUseOnlyOneInspector()
    {
        using var test = new InspectorFixture(); var workspace = new EditorWorkspace(EditorScene.Demo());
        workspace.SetDocument(test.Owner.Document.Read().Content); workspace.Select(InspectorFixture.Child);
        using var documents = new EditorDocumentViewModel(test.Owner.Commands);
        using var hierarchy = new EditorHierarchyViewModel(workspace, test.Owner.Commands, test.Model);
        using var model = new EditorViewModel(workspace, new((_, _) => Task.CompletedTask, CancellationToken.None), documents, test.Model, hierarchy);
        var legacyCalls = 0; var inspectorCalls = 0;
        var view = new EditorView(model, new EditorViewStyle(),
            new Factory<EditorDocumentView>(() => new(documents, new EditorDocumentViewStyle())),
            new Factory<EditorTransformView>(() => { legacyCalls++; return new(test.Model, new EditorTransformViewStyle()); }), false, null,
            new Factory<EditorHierarchyView>(() => new(hierarchy, new EditorHierarchyViewStyle())),
            new Factory<EditorInspectorView>(() => { inspectorCalls++; return new(test.Model, new EditorInspectorViewStyle()); }));
        using var ui = InspectorFixture.Ui(view); Refresh();
        Assert.Equal(1, inspectorCalls); Assert.Equal(0, legacyCalls); Assert.Null(view.Transform);
        Assert.NotNull(view.Inspector); Assert.False(view.PropertiesPanel.IsVisible); Assert.False(view.InspectorHint.IsVisible);
        Assert.Empty(DocumentUiFixture.Tree(view).OfType<PropertyRow>());
        var inspector = view.Inspector!; var tree = view.Hierarchy!.Tree;
        tree.Select(InspectorFixture.Parent); ui.Update(); Assert.Equal(2, inspector.Components.Count);
        Assert.Equal(InspectorFixture.Parent, model.SelectedId);
        tree.Select(InspectorFixture.Child); ui.Update(); Assert.Equal(5, inspector.Components.Count);
        test.Property(DocumentData.Custom, "text").Value = "pending ECS"; hierarchy.Refresh();
        Assert.False(hierarchy.CanMove); Assert.False(hierarchy.Move(InspectorFixture.Child, null));
        Assert.Equal(DocumentData.Parent, test.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].ParentId);
        ui.Update(); ui.Focus(inspector.Apply);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Enter, Control: true)); Refresh();
        Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
        Assert.Equal("pending ECS", test.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].Components[DocumentData.Custom].Properties["text"].Value.GetString());
        Assert.True(hierarchy.CanMove); Assert.True(tree.MoveEnabled);
        ui.Focus(tree); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Left, Control: true)); Refresh();
        Assert.Null(test.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].ParentId);
        Assert.Equal(2, test.Owner.Document.Read().Content!.UndoCount);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Z, Control: true)); Refresh();
        Assert.Equal(DocumentData.Parent, test.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].ParentId);
        Assert.Equal("pending ECS", Assert.IsType<RichTextEditor>(inspector.Properties[(DocumentData.Custom, "text")].Input).Text);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Z, Control: true)); Refresh();
        Assert.False(test.Owner.Document.Read().IsDirty); Assert.Equal(DocumentData.Text, test.Property(DocumentData.Custom, "text").Value);
        ui.SetTheme(Theme.Light); ui.Update(); Assert.Same(inspector, view.Inspector); Assert.Same(tree, view.Hierarchy.Tree);
        ui.Dispose(); Assert.True(inspector.IsDisposed); Assert.False(test.Owner.Commands.Read().Closed);
        Assert.True(test.Model.CanChangeComponents);

        void Refresh() { model.Refresh(new(SessionState.Stopped), EditorWorkspaceTests.Status(GameEditorOperation.Idle, false)); ui.Update(); ui.Update(); }
    }

    [Fact]
    public void NullInspectorFactory_CleansPreviouslyCreatedDocumentViewWithoutTakingBorrowedOwners()
    {
        using var test = new InspectorFixture(); using var documents = new EditorDocumentViewModel(test.Owner.Commands);
        var workspace = new EditorWorkspace(EditorScene.Demo()); workspace.SetDocument(test.Owner.Document.Read().Content);
        using var model = new EditorViewModel(workspace, new((_, _) => Task.CompletedTask, CancellationToken.None), documents, test.Model);
        EditorDocumentView product = null!;
        var error = Assert.Throws<InvalidOperationException>(() => new EditorView(model, new EditorViewStyle(),
            new Factory<EditorDocumentView>(() => product = new(documents, new EditorDocumentViewStyle())), null, false, null, null,
            new Factory<EditorInspectorView>(() => null!)));
        Assert.Contains("Inspector factory returned null", error.Message); Assert.True(product.IsDisposed);
        Assert.False(test.Owner.Commands.Read().Closed); Assert.Equal(0, test.Owner.Authoring.Products[0].Disposals);
        test.Refresh(); test.Property(DocumentData.Custom, "text").Value = "owner survives"; test.Model.Apply();
        Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
    }

    [Fact]
    public void RootStyleFailure_DisposesCreatedInspectorExactlyOnceAndLeavesItsBorrowedModelAlive()
    {
        using var test = new InspectorFixture();
        var workspace = new EditorWorkspace(EditorScene.Demo()); workspace.SetDocument(test.Owner.Document.Read().Content);
        using var model = new EditorViewModel(workspace, new((_, _) => Task.CompletedTask, CancellationToken.None), transform: test.Model);
        EditorInspectorView product = null!; var resource = new Cleanup(); var failure = new IOException("root style failed");
        var error = Assert.Throws<IOException>(() => new EditorView(model, new FailingStyle(failure), null, null, false, null, null,
            new Factory<EditorInspectorView>(() => { product = new(test.Model, new EditorInspectorViewStyle()); product.Own(resource); return product; })));
        Assert.Same(failure, error); Assert.Equal(1, resource.Disposals); Assert.True(product.IsDisposed); Assert.True(product.Scroll.IsDisposed);
        Assert.All(product.Properties.Values, row => Assert.True(row.Input.IsDisposed));
        test.Refresh(); test.Property(DocumentData.Custom, "flag").BooleanValue = true; test.Model.Apply();
        Assert.True(test.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].Components[DocumentData.Custom].Properties["flag"].Value.GetBoolean());
        Assert.False(test.Owner.Commands.Read().Closed);
    }

    private sealed class Cleanup : IDisposable { public int Disposals; public void Dispose() => Disposals++; }
    private sealed class FailingStyle(Exception failure) : IViewStyle<EditorView>
    { public void Configure(IViewStyleBuilder<EditorView> styles) { styles.Root().FontSize(13); throw failure; } }
}
