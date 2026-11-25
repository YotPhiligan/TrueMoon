using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Views;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Editor.UI.Documents;
using TrueMoon.Aether.Editor.UI.Hierarchy;
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
    public void NamedHierarchyParts_PreserveStylesAndRoutedFocusInsideAnotherView()
    {
        const string searchName = "Search", treeName = "Tree", nameEditorName = "NameEditor";
        using var test = new HierarchyFixture();
        var hierarchy = new EditorHierarchyView(test.Model, new EditorHierarchyViewStyle());
        var host = new PartHost();
        var outerSearch = new TextBox().Named(searchName);
        var outerTree = new TreeView().Named(treeName);
        var outerNameEditor = new TextBox().Named(nameEditorName);
        host.SetContent(new Panel().WithChildren(outerSearch, outerTree, outerNameEditor, hierarchy));
        using var ui = DocumentUiFixture.Ui(host);
        ui.Update();

        var search = hierarchy.GetRequiredPart<TextBox>(searchName);
        var tree = hierarchy.GetRequiredPart<TreeView>(treeName);
        var nameEditor = hierarchy.GetRequiredPart<TextBox>(nameEditorName);
        var title = hierarchy.GetRequiredPart<Text>("Title");
        var actions = hierarchy.GetRequiredPart<HStack>("Actions");
        var searchRow = hierarchy.GetRequiredPart<HStack>("SearchRow");
        Assert.Same(title, hierarchy.Title);
        Assert.Same(actions, hierarchy.Actions);
        Assert.Same(searchRow, hierarchy.SearchRow);
        Assert.Same(hierarchy.Search, search);
        Assert.Same(hierarchy.Tree, tree);
        Assert.Same(hierarchy.NameEditor, nameEditor);
        Assert.Same(outerSearch, host.GetRequiredPart<TextBox>(searchName));
        Assert.Same(outerTree, host.GetRequiredPart<TreeView>(treeName));
        Assert.Same(outerNameEditor, host.GetRequiredPart<TextBox>(nameEditorName));
        Assert.Equal("Поиск по имени или ID · Enter следующее совпадение", search.AccessibleName);
        Assert.Equal("Обозреватель сущностей", tree.AccessibleName);
        Assert.Equal("Имя сущности", nameEditor.AccessibleName);
        Assert.Equal(0, search.MinWidth);
        Assert.Equal(0, nameEditor.MinWidth);
        Assert.True(tree.ReorderEnabled);
        Assert.Equal(156, search.Width);
        Assert.Equal(24, tree.RowHeight);
        Assert.Equal(new Thickness(2, 0, 2, 0), nameEditor.Padding);
        Assert.True(float.IsNaN(outerSearch.Width));
        Assert.Equal(13, title.FontSize);
        Assert.Equal(4, actions.Spacing);
        Assert.Equal(4, searchRow.Spacing);

        ui.Focus(tree);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.F, Control: true));
        Assert.True(search.IsFocused);
        Assert.False(outerSearch.IsFocused);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape));
        Assert.True(tree.IsFocused);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.F2));
        ui.Update();
        Assert.True(nameEditor.IsFocused);
        Assert.False(outerNameEditor.IsFocused);

        ui.SetTheme(Theme.Light);
        hierarchy.WithViewStyle(new EditorHierarchyViewStyle());
        ui.Update();
        Assert.True(nameEditor.IsFocused);
        Assert.Same(nameEditor, hierarchy.NameEditor);
        Assert.Equal(new Thickness(2, 0, 2, 0), nameEditor.Padding);
        Assert.Same(title, hierarchy.Title);
        Assert.Same(actions, hierarchy.Actions);
        Assert.Same(searchRow, hierarchy.SearchRow);
        Assert.Equal(13, title.FontSize);
        Assert.Equal(4, actions.Spacing);
        Assert.Equal(4, searchRow.Spacing);
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

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void DataContextSwap_RetainsFocusedEditorAndDiscardsPreviousModelUpdates(bool rename, bool deferred)
    {
        using var previous = new HierarchyFixture();
        using var next = new HierarchyFixture();
        previous.Model.SearchText = "previous search";
        next.Model.SearchText = "next search";
        if (rename)
        {
            next.Model.Select(HierarchyFixture.Parent);
            Assert.True(previous.Model.BeginRename());
            Assert.True(next.Model.BeginRename());
            previous.Model.RenameText = "previous draft";
            next.Model.RenameText = "next draft";
        }

        var style = new EditorHierarchyViewStyle();
        var view = deferred ? ViewCreation.Prepare(new EditorHierarchyView(previous.Model), style)
            : EditorHierarchyView.Create(previous.Model, style);
        Assert.Null(view.Child);
        using var ui = DocumentUiFixture.Ui(view);
        ui.Update();
        var editor = rename ? view.NameEditor : view.Search;
        ui.Focus(editor);
        Assert.True(editor.IsFocused);
        Assert.Equal(rename ? "previous draft" : "previous search", editor.Value);
        previous.Model.SearchText = "queued search";
        if (rename) previous.Model.RenameText = "queued draft";
        Assert.Equal(rename ? "previous draft" : "previous search", editor.Value);

        view.DataContext = next.Model;
        Assert.Same(editor, rename ? view.NameEditor : view.Search);
        Assert.True(editor.IsFocused);
        Assert.Equal(rename ? "next draft" : "next search", editor.Value);
        ui.Update();
        Assert.True(editor.IsFocused);
        Assert.Equal(rename ? "next draft" : "next search", editor.Value);

        ui.SetTheme(Theme.Light);
        view.WithViewStyle(style);
        ui.Update();
        Assert.Same(editor, rename ? view.NameEditor : view.Search);
        Assert.True(editor.IsFocused);
        Assert.Equal(rename ? "next draft" : "next search", editor.Value);
        Assert.Equal(156, view.Search.Width);
        Assert.Equal(new Thickness(2, 0, 2, 0), view.NameEditor.Padding);

        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
        ui.HandleInput(new(InputKind.Text, Text: "edited in next"));
        Assert.Equal("edited in next", rename ? next.Model.RenameText : next.Model.SearchText);
        Assert.Equal("queued search", previous.Model.SearchText);
        if (rename)
        {
            Assert.Equal("queued draft", previous.Model.RenameText);
            Assert.NotNull(view.Tree.GetItemBounds(HierarchyFixture.Parent));
            Assert.Equal(view.Tree.GetItemBounds(HierarchyFixture.Parent)!.Value.Y, editor.Bounds.Y);
            ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Enter));
            Assert.Equal(1, next.Owner.Document.Read().Content!.UndoCount);
            Assert.Equal(0, previous.Owner.Document.Read().Content!.UndoCount);
            Assert.True(view.Tree.IsFocused);
        }
    }

    [Fact]
    public void ContextObserver_ResumesWithCurrentSelectionAndCommandAfterDetach()
    {
        using var previous = new HierarchyFixture();
        using var next = new HierarchyFixture();
        var view = EditorHierarchyView.Create(previous.Model, new EditorHierarchyViewStyle());
        var root = new Panel().WithChildren(view);
        using var ui = DocumentUiFixture.Ui(root);
        ui.Update();
        Assert.Equal(HierarchyFixture.Child, view.Tree.SelectedId);

        previous.Model.Select(HierarchyFixture.Parent);
        Assert.True(root.Items.Remove(view));
        view.DataContext = next.Model;
        root.Items.Add(view);
        ui.Update();
        Assert.Equal(HierarchyFixture.Child, view.Tree.SelectedId);
        Assert.True(view.RootButton.IsEnabled);

        DocumentUiFixture.Click(ui, view.RootButton);
        ui.Update();
        Assert.Null(next.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].ParentId);
        Assert.Equal(DocumentData.Parent, previous.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].ParentId);
    }

    [Fact]
    public void Search_RevealsCollapsedOffscreenMatchAndRetainsScrollAcrossThemeAndStyleChanges()
    {
        var scene = DocumentData.Scene();
        var parent = scene.Entities.Single(entity => entity.Id == DocumentData.Parent);
        var child = scene.Entities.Single(entity => entity.Id == DocumentData.Child);
        var siblings = Enumerable.Range(1, 12)
            .Select(index => new EntityInstance(new(DocumentData.Id(100 + index)), DocumentData.Hidden))
            .ToArray();
        using var test = new HierarchyFixture(new SceneDocument(scene.Id, scene.Revision,
            [.. siblings, new EntityInstance(parent.Id, parent.DefinitionId, parent.Changes, null, parent.Name, siblings.Length), child],
            scene.Definitions, scene.Schemas));
        test.Model.Select(siblings[0].Id.Value.ToString("D"));

        var view = new EditorHierarchyView(test.Model, new EditorHierarchyViewStyle());
        using var ui = UiSession.Create(view, new SkiaRasterRenderBackend(), new RasterUiTarget(), new(250, 200));
        ui.Update();
        ui.Focus(view.Tree);
        Assert.Null(view.Tree.GetItemBounds(HierarchyFixture.Child));
        Assert.False(view.Tree.IsExpanded(HierarchyFixture.Parent));
        Assert.Equal(0, view.Scroll.Offset);
        var clip = view.Scroll.ChildClipBounds;
        Assert.True(clip.Height >= view.Tree.RowHeight);
        var parentRow = view.Tree.GetItemBounds(HierarchyFixture.Parent)!.Value;
        Assert.True(parentRow.Y >= clip.Y + clip.Height);

        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.F, Control: true));
        Assert.True(view.Search.IsFocused);
        ui.HandleInput(new(InputKind.Text, Text: HierarchyFixture.Child));
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Enter));
        ui.Update();
        ui.Update();
        Assert.Equal(HierarchyFixture.Child, view.Tree.SelectedId);
        Assert.True(view.Tree.IsExpanded(HierarchyFixture.Parent));
        Assert.True(view.Search.IsFocused);
        var revealedOffset = view.Scroll.Offset;
        Assert.True(revealedOffset > 0);
        var row = view.Tree.GetItemBounds(HierarchyFixture.Child)!.Value;
        Assert.InRange(row.Y, clip.Y, clip.Y + clip.Height - row.Height);

        var x = clip.X + clip.Width / 2;
        var y = clip.Y + clip.Height / 2;
        Assert.True(ui.HandleInput(new(InputKind.Wheel, x, y, WheelDelta: 1)).Handled);
        ui.Update();
        Assert.True(view.Scroll.Offset < revealedOffset);
        Assert.True(ui.HandleInput(new(InputKind.Wheel, x, y, WheelDelta: -1)).Handled);
        ui.Update();
        Assert.Equal(revealedOffset, view.Scroll.Offset);

        ui.SetTheme(Theme.Light);
        view.WithViewStyle(new EditorHierarchyViewStyle());
        ui.Update();
        Assert.Equal(revealedOffset, view.Scroll.Offset);
        Assert.True(view.Tree.IsExpanded(HierarchyFixture.Parent));
        Assert.True(view.Search.IsFocused);

        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape));
        ui.Update();
        Assert.Empty(view.Search.Value);
        Assert.True(view.Tree.IsFocused);
        Assert.Equal(HierarchyFixture.Child, view.Tree.SelectedId);
    }

    [Fact]
    public void View_HeaderAndScrollKeepTheirSlotsWhenViewportShrinks()
    {
        using var test = new HierarchyFixture();
        var view = new EditorHierarchyView(test.Model, new EditorHierarchyViewStyle());
        using var ui = DocumentUiFixture.Ui(view);
        AssertGeometry(250, 200);
        var headerWithoutFullGap = (int)MathF.Ceiling(view.RootButton.DesiredSize.Width + view.Padding.Horizontal + 2);
        AssertGeometry(headerWithoutFullGap, 160);
        AssertGeometry(160, 115);
        AssertGeometry(40, 100);

        void AssertGeometry(int width, int height)
        {
            ui.Resize(new(width, height));
            ui.Update();
            var area = view.Layout.Bounds;
            var headerHeight = Math.Min(area.Height, Math.Max(view.Title.DesiredSize.Height, view.RootButton.DesiredSize.Height));
            var buttonWidth = Math.Min(area.Width, view.RootButton.DesiredSize.Width);
            Assert.Equal(new Rect(area.X, area.Y, Math.Max(0, area.Width - buttonWidth - 4), headerHeight), view.Title.Bounds);
            Assert.Equal(new Rect(area.X + area.Width - buttonWidth, area.Y, buttonWidth, headerHeight), view.RootButton.Bounds);

            var spacing = view.Layout.Get(UiProperties.Spacing);
            var bottom = area.Y + area.Height;
            var actionsY = Math.Min(bottom, view.Caption.Bounds.Y + view.Caption.Bounds.Height + spacing);
            Assert.Equal(new Rect(area.X, actionsY, area.Width, Math.Min(bottom - actionsY, view.Actions.DesiredSize.Height)), view.Actions.Bounds);
            var searchY = Math.Min(bottom, view.Actions.Bounds.Y + view.Actions.Bounds.Height + spacing);
            Assert.Equal(new Rect(area.X, searchY, area.Width, Math.Min(bottom - searchY, view.SearchRow.DesiredSize.Height)), view.SearchRow.Bounds);
            var rowsHeight = view.Caption.DesiredSize.Height + view.Actions.DesiredSize.Height + view.SearchRow.DesiredSize.Height
                + view.Hint.DesiredSize.Height + 5 * spacing;
            var scrollY = area.Y + Math.Min(area.Height, headerHeight + rowsHeight);
            Assert.Equal(new Rect(area.X, scrollY, area.Width, area.Y + area.Height - scrollY), view.Scroll.Bounds);
        }
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
        var tree = view.Tree; var initial = tree.Items.ToArray(); ui.Dispose();
        Assert.True(view.IsDisposed); Assert.True(tree.IsDisposed); Assert.True(view.Scroll.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => { _ = view.Tree; });
        Assert.True(test.Model.Move(HierarchyFixture.Child, null));
        Assert.Equal(initial, tree.Items); Assert.Null(test.Model.Items.Single(item => item.Id == HierarchyFixture.Child).ParentId);
        Assert.False(test.Owner.Commands.Read().Closed); Assert.Equal(0, test.Owner.Authoring.Products[0].Disposals);
        test.Transform.Fields[0].Value = "52"; test.Transform.Apply(); Assert.Equal(52, DocumentData.X(test.Owner.Document.Read().Content!));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StyleFailure_DisposesUnpublishedTreeAndPreservesBorrowedModelsAndPrimaryException(bool cleanupFailure)
    {
        using var test = new HierarchyFixture(); var failure = new IOException("hierarchy style failed"); var cleanup = new IOException("hierarchy cleanup failed");
        TreeView unpublishedTree = null!;
        ScrollViewer unpublishedScroll = null!;
        var resource = new Resource(cleanupFailure ? cleanup : null);
        using var draft = new EditorHierarchyView(test.Model, new FailingStyle(view =>
        {
            unpublishedTree = view.Tree;
            unpublishedScroll = view.Scroll;
            unpublishedTree.Own(resource);
        }, failure));
        var error = Record.Exception(() => DocumentUiFixture.Ui(draft));
        if (cleanupFailure) Assert.Collection(Assert.IsType<AggregateException>(error).Flatten().InnerExceptions, cause => Assert.Same(failure, cause), cause => Assert.Same(cleanup, cause));
        else Assert.Same(failure, error);
        Assert.Equal(1, resource.Disposals);
        Assert.Null(draft.Child);
        Assert.False(draft.IsAttached);
        Assert.False(draft.IsDisposed);
        Assert.True(unpublishedTree.IsDisposed);
        Assert.True(unpublishedScroll.IsDisposed);
        test.Model.Select(HierarchyFixture.Parent); Assert.Equal(HierarchyFixture.Parent, test.Workspace.SelectedId); Assert.False(test.Owner.Commands.Read().Closed);
        Assert.True(test.Model.Move(HierarchyFixture.Child, null)); Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
    }

    private sealed class PartHost : View { }

    private sealed class FailingStyle(Action<EditorHierarchyView> capture, Exception error) : IViewStyle<EditorHierarchyView>
    {
        public void Configure(IViewStyleBuilder<EditorHierarchyView> styles)
        { styles.Part(view => { capture(view); return view.Tree; }).FontSize(13); throw error; }
    }
    private sealed class Resource(Exception? failure) : IDisposable
    { public int Disposals; public void Dispose() { Disposals++; if (failure != null) throw failure; } }
}
