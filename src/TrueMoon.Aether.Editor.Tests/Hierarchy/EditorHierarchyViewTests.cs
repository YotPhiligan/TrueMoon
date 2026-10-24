using TrueMoon.Aether.Editor.Hierarchy;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Hierarchy;

public sealed class EditorHierarchyViewTests
{
    [Fact]
    public void View_MapsDomainIconsSelectionAndRootActionToSharedTransformHistory()
    {
        using var test = new HierarchyFixture();
        var view = new EditorHierarchyView(test.Model, new EditorHierarchyViewStyle()); using var ui = DocumentUiFixture.Ui(view); ui.Update();
        Assert.Same(test.Model, view.DataContext);
        Assert.Equal(UiSymbol.Entity, view.Tree.Items.Single(item => item.Id == HierarchyFixture.Parent).Icon);
        Assert.Equal(UiSymbol.Interaction, view.Tree.Items.Single(item => item.Id == HierarchyFixture.Child).Icon);
        Assert.Equal(HierarchyFixture.Parent, view.Tree.Items.Single(item => item.Id == HierarchyFixture.Child).ParentId);
        Assert.Equal(HierarchyFixture.Child, view.Tree.SelectedId); Assert.True(view.Tree.IsExpanded(HierarchyFixture.Parent));
        Assert.Equal(24, view.Tree.RowHeight); Assert.Equal(12, view.Tree.FontSize); Assert.True(view.RootButton.IsEnabled);
        view.Tree.Select(HierarchyFixture.Parent); ui.Update();
        Assert.Equal(HierarchyFixture.Parent, test.Workspace.SelectedId); Assert.Equal("2", test.Transform.Fields[0].Value);
        Assert.False(view.RootButton.IsEnabled);
        view.Tree.Select(HierarchyFixture.Child); ui.Update(); DocumentUiFixture.Click(ui, view.RootButton); ui.Update();
        Assert.Null(test.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].ParentId);
        Assert.Null(view.Tree.Items.Single(item => item.Id == HierarchyFixture.Child).ParentId);
        Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount); Assert.Equal("4", test.Transform.Fields[0].Value);
        Assert.Equal(HierarchyFixture.Child, view.Tree.SelectedId); Assert.False(view.RootButton.IsEnabled);
    }

    [Fact]
    public void View_ThemeStyleAndRepeatedRefreshPreserveRetainedPartsExpansionSelectionAndFocus()
    {
        using var test = new HierarchyFixture();
        var view = new EditorHierarchyView(test.Model, new EditorHierarchyViewStyle()); using var ui = DocumentUiFixture.Ui(view); ui.Update();
        ui.Focus(view.Tree); var tree = DocumentUiFixture.Tree(view).ToArray(); var selected = view.Tree.SelectedId;
        var items = view.Tree.Items; var modelItems = test.Model.Items;
        ui.SetTheme(Theme.Light); view.WithViewStyle(new EditorHierarchyViewStyle()); ui.Update();
        Assert.Equal(tree, DocumentUiFixture.Tree(view)); Assert.Same(items, view.Tree.Items); Assert.Same(modelItems, test.Model.Items);
        Assert.Equal(selected, view.Tree.SelectedId); Assert.True(view.Tree.IsExpanded(HierarchyFixture.Parent)); Assert.True(view.Tree.IsFocused);
        Assert.Equal(Theme.Light.Error, view.Error.Foreground); Assert.Equal(Theme.Light.Foreground, view.Tree.Foreground);
        test.Model.Refresh(); Assert.False(ui.Update()); Assert.All(tree, element => Assert.False(element.IsDisposed));
        test.Transform.Fields[0].Value = "44"; test.Model.Refresh(); ui.Update();
        Assert.False(view.Tree.MoveEnabled); Assert.False(view.RootButton.IsEnabled); Assert.Equal("Завершите правку свойств перед переносом", view.Hint.Value);
        test.Transform.Cancel(); test.Model.Refresh(); ui.Update(); Assert.True(view.Tree.MoveEnabled); Assert.True(view.RootButton.IsEnabled);
    }

    [Fact]
    public void View_EmptyGraphAndCycleDiagnosticArePresentedWithoutReplacingTree()
    {
        using var test = new HierarchyFixture();
        var view = new EditorHierarchyView(test.Model, new EditorHierarchyViewStyle());
        using var ui = UiSession.Create(view, new SkiaRasterRenderBackend(), new RasterUiTarget(), new(1280, 800)); ui.Update();
        var tree = view.Tree;
        Assert.False(test.Model.Move(HierarchyFixture.Parent, HierarchyFixture.Child)); ui.Update();
        Assert.Same(tree, view.Tree); Assert.True(view.Error.IsVisible); Assert.Contains("InvalidScene", view.Error.Text);
        ui.Focus(view.Error); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
        Assert.Equal(test.Model.ErrorText, view.Error.SelectedText);
        ui.HandleInput(new(InputKind.Text, Text: "x")); Assert.Equal(test.Model.ErrorText, view.Error.Text);
        Assert.Equal(2, view.Tree.Items.Count); Assert.Equal(7, test.Owner.Document.Read().Content!.Document.Revision);
        DocumentUiFixture.Wait(test.Owner.Document.NewAsync(DocumentData.Empty())); test.Synchronize(); ui.Update();
        Assert.Empty(view.Tree.Items); Assert.Empty(view.Tree.VisibleItems); Assert.Null(view.Tree.SelectedId);
        Assert.False(view.RootButton.IsEnabled); Assert.Empty(view.Error.Text);
        test.Owner.Run(DocumentCommand.Save, "empty.aetherscene");
        test.Owner.Run(DocumentCommand.Close); test.Workspace.SetDocument(null); test.Model.Refresh(); ui.Update();
        Assert.False(view.Tree.MoveEnabled); Assert.False(view.RootButton.IsEnabled);
        Assert.Equal(test.Model.Items.Select(item => item.Id), view.Tree.Items.Select(item => item.Id));
        Assert.Equal("Иерархия · только просмотр", view.Hint.Value); Assert.Empty(view.Error.Text);
    }

    [Fact]
    public void SnapshotFence_CancelsCapturedMoveWhenTransformCommitsWithoutChangingHierarchyItems()
    {
        using var test = new HierarchyFixture();
        var view = new EditorHierarchyView(test.Model, new EditorHierarchyViewStyle()); using var ui = DocumentUiFixture.Ui(view); ui.Update();
        var row = view.Tree.GetItemBounds(HierarchyFixture.Child)!.Value;
        var blankY = view.Scroll.ChildClipBounds.Y + view.Scroll.ChildClipBounds.Height - 4;
        var x = row.X + 80; var requests = 0; view.Tree.MoveRequested += _ => requests++;
        ui.HandleInput(new(InputKind.PointerDown, x, row.Y + row.Height / 2));
        Assert.True(ui.HandleInput(new(InputKind.PointerMove, x, blankY)).PointerCaptured);
        test.Transform.Fields[0].Value = "18"; test.Transform.Apply(); test.Synchronize(); ui.Update();
        Assert.False(ui.HandleInput(new(InputKind.PointerUp, x, blankY)).PointerCaptured);
        Assert.Equal(0, requests); Assert.Equal(8, test.Owner.Document.Read().Content!.Document.Revision);
        Assert.Equal(18, DocumentData.X(test.Owner.Document.Read().Content!));
        Assert.Equal(DocumentData.Parent, test.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].ParentId);
        Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
    }

    [Fact]
    public void ViewDisposal_UnsubscribesUiAndPreservesBorrowedModelTransformAndDocument()
    {
        using var test = new HierarchyFixture();
        var view = new EditorHierarchyView(test.Model, new EditorHierarchyViewStyle()); var ui = DocumentUiFixture.Ui(view); ui.Update();
        var initial = view.Tree.Items.ToArray(); ui.Dispose();
        Assert.True(view.IsDisposed); Assert.True(view.Tree.IsDisposed); Assert.True(view.Scroll.IsDisposed);
        Assert.True(test.Model.Move(HierarchyFixture.Child, null));
        Assert.Equal(initial, view.Tree.Items); Assert.Null(test.Model.Items.Single(item => item.Id == HierarchyFixture.Child).ParentId);
        Assert.False(test.Owner.Commands.Read().Closed); Assert.Equal(0, test.Owner.Authoring.Products[0].Disposals);
        test.Transform.Fields[0].Value = "52"; test.Transform.Apply(); Assert.Equal(52, DocumentData.X(test.Owner.Document.Read().Content!));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StyleFailure_DisposesUnpublishedTreeAndPreservesBorrowedModelsAndPrimaryException(bool cleanupFailure)
    {
        using var test = new HierarchyFixture(); var failure = new IOException("hierarchy style failed"); var cleanup = new IOException("hierarchy cleanup failed");
        EditorHierarchyView unpublished = null!; var resource = new Resource(cleanupFailure ? cleanup : null);
        var error = Record.Exception(() => new EditorHierarchyView(test.Model, new FailingStyle(view => { unpublished = view; view.Own(resource); }, failure)));
        if (cleanupFailure) Assert.Collection(Assert.IsType<AggregateException>(error).Flatten().InnerExceptions, cause => Assert.Same(failure, cause), cause => Assert.Same(cleanup, cause));
        else Assert.Same(failure, error);
        Assert.Equal(1, resource.Disposals); Assert.True(unpublished.IsDisposed); Assert.True(unpublished.Tree.IsDisposed); Assert.True(unpublished.Scroll.IsDisposed);
        test.Model.Select(HierarchyFixture.Parent); Assert.Equal(HierarchyFixture.Parent, test.Workspace.SelectedId); Assert.False(test.Owner.Commands.Read().Closed);
        Assert.True(test.Model.Move(HierarchyFixture.Child, null)); Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
    }

    private sealed class FailingStyle(Action<EditorHierarchyView> capture, Exception error) : IViewStyle<EditorHierarchyView>
    {
        public void Configure(IViewStyleBuilder<EditorHierarchyView> styles)
        { styles.Part(view => { capture(view); return view.Tree; }).FontSize(13); throw error; }
    }
    private sealed class Resource(Exception? failure) : IDisposable
    { public int Disposals; public void Dispose() { Disposals++; if (failure != null) throw failure; } }
}
