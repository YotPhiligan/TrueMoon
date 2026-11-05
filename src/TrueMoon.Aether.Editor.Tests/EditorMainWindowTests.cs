using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Editor.Tests.Editing;
using TrueMoon.Aether.Editor.Tests.Inspector;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests;

public sealed class EditorMainWindowTests
{
    [Fact]
    public void Factory_CreatesOneTypedEditorPerRootAndOnlyRootOwnsItsIndependentProduct()
    {
        using var h = new EditorMainWindowFixture();
        var first = h.Create(); var second = h.Create("Second", new(1400, 900));
        var firstOwned = new EditorWindowResource(); var secondOwned = new EditorWindowResource();
        first.EditorView.Own(firstOwned); second.EditorView.Own(secondOwned);
        Assert.Equal(2, h.ViewCreations); Assert.Equal(2, h.RootStyle.Configurations); Assert.Equal(2, h.ViewStyle.Configurations);
        Assert.Same(h.Model, first.DataContext); Assert.Same(h.Model, first.EditorView.DataContext);
        Assert.Same(first.EditorView, first.Child); Assert.Same(first, first.EditorView.Parent);
        Assert.NotSame(first.EditorView, second.EditorView); Assert.NotSame(first.EditorView.Canvas, second.EditorView.Canvas);
        Assert.Equal(new Size(1280, 800), first.ClientSize); Assert.Equal(new Size(1400, 900), second.ClientSize);
        Assert.Equal("Editor", first.Title); Assert.Equal("Second", second.Title);
        Assert.Empty(DocumentUiFixture.Tree(first).OfType<WindowFrame>());
        Assert.Null(first.EditorView.Canvas.DrawnFrame); Assert.False(first.IsHosted);
        first.Dispose(); first.Dispose();
        Assert.True(first.EditorView.IsDisposed); Assert.Equal(1, firstOwned.Disposals);
        Assert.False(second.IsDisposed); Assert.Equal(0, secondOwned.Disposals);
        h.Model.Select("green-cube"); Assert.Equal("green-cube", h.Model.SelectedId);
        second.Dispose(); Assert.Equal(1, secondOwned.Disposals); Assert.Equal(0, h.RootStyle.Disposals); Assert.Equal(0, h.ViewStyle.Disposals);
        using var third = h.Create(); Assert.Equal(3, h.ViewCreations); Assert.Same(h.Model, third.EditorView.DataContext);
    }

    [Theory]
    [InlineData("model")]
    [InlineData("documents")]
    [InlineData("factory")]
    [InlineData("style")]
    [InlineData("arguments")]
    public void Constructor_RejectsMissingDependencyBeforeRequestingAnyFactoryProduct(string missing)
    {
        using var h = new EditorMainWindowFixture();
        Assert.Throws<ArgumentNullException>(() => new EditorMainWindow(missing == "model" ? null! : h.Model,
            missing == "documents" ? null! : h.Documents, missing == "factory" ? null! : h.Views,
            missing == "style" ? null! : h.RootStyle, missing == "arguments" ? null! : new("Editor", new(1280, 800))));
        Assert.Equal(0, h.ViewCreations); Assert.Equal(0, h.RootStyle.Configurations);
        h.Model.Select("green-cube"); Assert.Equal("green-cube", h.Model.SelectedId); Assert.False(h.Owner.Commands.Read().Closed);
    }

    [Theory]
    [InlineData(null, 1280, 800)]
    [InlineData("Editor", 0, 800)]
    [InlineData("Editor", 1280, -1)]
    [InlineData("Editor", 1100.5f, 800)]
    [InlineData("Editor", 1280, float.NaN)]
    [InlineData("Editor", float.PositiveInfinity, 800)]
    [InlineData("Editor", 32769, 800)]
    [InlineData("Editor", 1280, 32770)]
    public void InvalidInitialProperties_RejectBeforeCreatingEditorOrApplyingRootStyle(string? title, float width, float height)
    {
        using var h = new EditorMainWindowFixture();
        Assert.Throws<ArgumentOutOfRangeException>(() => h.Create(title!, new(width, height)));
        Assert.Equal(0, h.ViewCreations); Assert.Equal(0, h.RootStyle.Configurations); Assert.Equal(0, h.ViewStyle.Configurations);
        Assert.False(h.Owner.Commands.Read().Closed); Assert.Empty(h.Owner.Store.Writes);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("throw")]
    [InlineData("disposed")]
    [InlineData("parented")]
    [InlineData("attached")]
    [InlineData("wrong-model")]
    public void RejectedFactoryProducts_PreserveForeignOwnershipAndCleanAcceptedWrongModelOnly(string kind)
    {
        using var h = new EditorMainWindowFixture(); using var other = new EditorMainWindowFixture();
        var resource = new EditorWindowResource(); using var parent = new Panel();
        EditorView? product = null; TrueMoon.Alloy.Runtime.UiSession? ui = null;
        var failure = new IOException("factory failed");
        if (kind is not ("null" or "throw"))
        {
            product = new(kind == "wrong-model" ? other.Model : h.Model, new EditorViewStyle()); product.Own(resource);
            if (kind == "disposed") product.Dispose();
            else if (kind == "parented") parent.Add(product);
            else if (kind == "attached") ui = DocumentUiFixture.Ui(product);
        }
        h.ViewFactory = () => kind == "throw" ? throw failure : product;
        try
        {
            var error = Record.Exception(() => h.Create());
            if (kind == "throw") Assert.Same(failure, error);
            else if (kind == "wrong-model") Assert.IsType<ArgumentException>(error);
            else Assert.IsType<InvalidOperationException>(error);
            Assert.Equal(1, h.ViewCreations); Assert.Equal(0, h.RootStyle.Configurations);
            Assert.Equal(kind is "disposed" or "wrong-model" ? 1 : 0, resource.Disposals);
            if (kind == "parented") Assert.Same(parent, product!.Parent);
            if (kind == "attached") { Assert.True(product!.IsAttached); Assert.False(product.IsDisposed); }
            if (kind == "wrong-model") Assert.True(product!.IsDisposed);
            h.Model.Select("green-cube"); other.Model.Select("switch");
            Assert.Equal("green-cube", h.Model.SelectedId); Assert.Equal("switch", other.Model.SelectedId);
            Assert.False(h.Owner.Commands.Read().Closed); Assert.False(other.Owner.Commands.Read().Closed);
        }
        finally { ui?.Dispose(); if (product?.Parent == null) product?.Dispose(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RootStyleFailure_DisposesAcceptedTreeAndPreservesBorrowedModelsAndBothErrors(bool cleanupFails)
    {
        using var h = new EditorMainWindowFixture(); var failure = new IOException("root style"); var cleanup = new IOException("view cleanup");
        EditorView product = null!; var resource = new EditorWindowResource(cleanupFails ? cleanup : null);
        h.ViewFactory = () => { product = new(h.Model, new EditorViewStyle()); product.Own(resource); return product; };
        h.RootStyle.ConfigureAction = _ => throw failure;
        var error = Record.Exception(() => h.Create());
        if (cleanupFails) Assert.Collection(Assert.IsType<AggregateException>(error).Flatten().InnerExceptions,
            item => Assert.Same(failure, item), item => Assert.Same(cleanup, item));
        else Assert.Same(failure, error);
        Assert.True(product.IsDisposed); Assert.True(product.Canvas.IsDisposed); Assert.True(product.Overlays.IsDisposed);
        Assert.Equal(1, resource.Disposals); Assert.Equal(1, h.ViewCreations); Assert.Equal(1, h.RootStyle.Configurations);
        h.Model.Select("green-cube"); h.Documents.PathDraft = "still borrowed";
        Assert.Equal("green-cube", h.Model.SelectedId); Assert.Equal("still borrowed", h.Documents.PathDraft); Assert.False(h.Owner.Commands.Read().Closed);
        Assert.Equal(0, h.RootStyle.Disposals);
    }

    [Fact]
    public void Disposal_ReleasesCaptionSubscriptionSoBorrowedDocumentModelCannotRetainDisposedRoot()
    {
        using var h = new EditorMainWindowFixture();
        var disposed = CreateDisposedRoot(h);
        for (var attempt = 0; attempt < 3 && disposed.IsAlive; attempt++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        }
        Assert.False(disposed.IsAlive); Assert.Equal(1, h.ViewCreations); Assert.Equal(0, h.RootStyle.Disposals);
        h.Documents.PathDraft = "borrowed model stays live"; h.Model.Select("green-cube");
        Assert.Equal("borrowed model stays live", h.Documents.PathDraft); Assert.Equal("green-cube", h.Model.SelectedId);
        Assert.False(h.Owner.Commands.Read().Closed);
        GC.KeepAlive(h.Documents); GC.KeepAlive(h.Model);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference CreateDisposedRoot(EditorMainWindowFixture fixture)
    {
        var root = fixture.Create("Released"); var reference = new WeakReference(root);
        root.Dispose(); return reference;
    }

    [Fact]
    public void FromRoot_AcceptsOnlyDirectWindowOrItsOneFrameAndChecksOwnerLifetime()
    {
        using var h = new EditorMainWindowFixture(); var root = h.Create();
        Assert.Same(root, EditorMainWindow.FromRoot(root));
        var frame = WindowFrame.CreateDefault(root.Title, root);
        using var ui = DocumentUiFixture.Ui(frame); ui.Update();
        Assert.Same(root, EditorMainWindow.FromRoot(frame)); Assert.Same(root.EditorView, EditorMainWindow.FromRoot(frame).EditorView);
        Exception? error = null;
        var worker = new Thread(() => error = Record.Exception(() => EditorMainWindow.FromRoot(frame)));
        worker.Start(); Assert.True(worker.Join(TimeSpan.FromSeconds(5)));
        Assert.IsType<InvalidOperationException>(error); Assert.Same(frame, root.Parent);
        Assert.Throws<ArgumentNullException>(() => EditorMainWindow.FromRoot(null!));
        ui.Dispose(); Assert.Throws<ObjectDisposedException>(() => EditorMainWindow.FromRoot(root));
    }

    [Theory]
    [InlineData("raw-editor")]
    [InlineData("panel")]
    [InlineData("nested-frame")]
    public void FromRoot_DoesNotRecursivelyDiscoverWindowOrTreatEmbeddedEditorAsStandalone(string kind)
    {
        using var h = new EditorMainWindowFixture(); Element root; EditorMainWindow? component = null;
        if (kind == "raw-editor") root = new EditorView(h.Model, new EditorViewStyle());
        else
        {
            component = h.Create();
            root = kind == "panel" ? new Panel().WithChildren(component)
                : WindowFrame.CreateDefault("outer", WindowFrame.CreateDefault("inner", component));
        }
        using (root)
        {
            Assert.Throws<ArgumentException>(() => EditorMainWindow.FromRoot(root));
            Assert.False(root.IsDisposed); if (component != null) { Assert.False(component.IsDisposed); Assert.NotNull(component.Parent); }
        }
    }

    [Fact]
    public void RootStyleBoundaryAndCaptionChanges_RetainNestedEditorAndUnsubscribeOnDisposal()
    {
        using var h = new EditorMainWindowFixture();
        h.RootStyle.ConfigureAction = styles => styles.Root().Padding(17).FontSize(41).Background(Theme.Dark.Error);
        var root = h.Create("Custom"); var editor = root.EditorView; var canvas = editor.Canvas; var body = editor.Child;
        using var ui = DocumentUiFixture.Ui(root); ui.Update();
        Assert.Equal(new Thickness(17), root.Padding); Assert.Equal(41, root.FontSize);
        Assert.NotEqual(41, editor.FontSize); Assert.NotEqual(41, editor.SceneTitle.FontSize);
        var editorColor = editor.Background; var changes = 0; root.PropertyChanged += (_, property) => { if (ReferenceEquals(property, Window.TitleProperty)) changes++; };
        h.Owner.Run(DocumentCommand.New); h.Documents.Refresh(); ui.Update();
        Assert.Equal("Custom · Новая сцена *", root.Title); Assert.Equal(root.Title + "\n" + h.Owner.Files.Project.ScenesDirectory, root.AccessibleName);
        Assert.Equal(1, changes); ui.SetTheme(Theme.Light); ui.Update();
        Assert.Same(editor, root.EditorView); Assert.Same(body, editor.Child); Assert.Same(canvas, editor.Canvas);
        Assert.Equal(1, h.ViewCreations); Assert.Equal(1, h.RootStyle.Configurations); Assert.Equal(1, h.ViewStyle.Configurations);
        Assert.NotEqual(root.Background, editor.Background); Assert.Equal(editorColor, editor.Background);
        ui.Dispose();
        h.Owner.Run(DocumentCommand.SaveAs, "saved.aetherscene"); h.Documents.Refresh();
        Assert.Equal(1, changes); Assert.Equal("Custom · Новая сцена *", root.Title);
        h.Model.Select("green-cube"); Assert.Equal("green-cube", h.Model.SelectedId);
        Assert.Equal(0, h.RootStyle.Disposals); Assert.Equal(0, h.ViewStyle.Disposals);
    }

    [Fact]
    public void CaptionDetachReattach_DiscardsOldQueuedWorkAndCoalescesLatestStateWithoutRebuilding()
    {
        using var h = new EditorMainWindowFixture(); var root = h.Create("Transfer"); var editor = root.EditorView;
        var first = new ContentControl(); first.SetContent(root);
        using var firstUi = DocumentUiFixture.Ui(first); firstUi.Update();
        var changes = 0; root.PropertyChanged += (_, property) => { if (ReferenceEquals(property, Window.TitleProperty)) changes++; };
        h.Owner.Run(DocumentCommand.New); h.Documents.Refresh();
        Assert.Equal("Transfer", root.Title);
        first.SetContent(null); Assert.False(root.IsAttached); Assert.False(root.IsDisposed);
        var second = new ContentControl(); second.SetContent(root);
        using var secondUi = DocumentUiFixture.Ui(second);
        h.Owner.Run(DocumentCommand.SaveAs, "latest.aetherscene"); h.Documents.Refresh();
        firstUi.Update(); Assert.Equal("Transfer", root.Title); Assert.Equal(0, changes);
        secondUi.Update();
        Assert.Equal("Transfer · latest.aetherscene", root.Title); Assert.Equal(1, changes);
        Assert.Same(editor, root.EditorView); Assert.Same(root, editor.Parent); Assert.Equal(1, h.ViewCreations);
        Assert.Equal(root.Title + "\n" + Path.Combine(h.Owner.Files.Project.ScenesDirectory, "latest.aetherscene"), root.AccessibleName);
        firstUi.Dispose(); Assert.False(root.IsDisposed); secondUi.Dispose(); Assert.True(editor.IsDisposed);
    }

    [Fact]
    public void WrappedSourceUpdatesAndProjectReopen_PreserveDraftHistoryControlsAndRejectOldOwner()
    {
        using var h = new SourcePresentationFixture(); var creations = 0;
        h.Model.Select(DocumentData.Child.Value.ToString("D"));
        var views = new Factory<EditorView>(() =>
        {
            creations++;
            return new(h.Model, new EditorViewStyle(), new Factory<EditorDocumentView>(() => new(h.Documents, new EditorDocumentViewStyle())),
                new Factory<EditorTransformView>(() => new(h.Transform, new EditorTransformViewStyle())), true,
                new Factory<EditorStatusBarView, EditorStatusBarArguments>(arguments => new(h.Status, new EditorStatusBarViewStyle(),
                    new Factory<EditorStatusPopupView, EditorStatusPopupArguments>(popup => new(h.Status, new EditorStatusPopupViewStyle(), popup)), arguments)));
        });
        var root = new EditorMainWindow(h.Model, h.Documents, views, new EditorMainWindowStyle(), new("Source Editor", new(1280, 800)));
        using var ui = InspectorFixture.Ui(root); ui.Update();
        var editor = root.EditorView; var path = editor.Documents!.PathInput; var canvas = editor.Canvas;
        path.Value = "uncommitted path draft"; ui.Focus(path); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
        Assert.True(path.IsFocused); var caret = path.CaretIndex; var selection = path.SelectionLength;
        h.Transform.Fields[0].Value = "unfinished transform";
        var content = h.Active.Documents!.Read().Content; var selected = h.Model.SelectedId;
        var sourceButton = editor.StatusBar!.SourceButton; var old = h.Active;
        h.Bind(); var scan = SourcePresentationFixture.Wait(h.Control.Scanner.Next());
        var oldHints = h.Control.Hints.Latest;
        scan.Complete(EditorSourceScan.Difference(h.Control.Files.Source)); h.Until(snapshot => snapshot.State == EditorSourceState.Changed);
        h.Refresh(); ui.SetTheme(Theme.Light); ui.Update();
        Assert.Equal(old.OwnerKey, h.Model.Source!.OwnerKey); Assert.Equal(EditorSourceState.Changed, h.Model.Source.State);
        Assert.Same(content, h.Active.Documents.Read().Content); Assert.Equal(selected, h.Model.SelectedId);
        Assert.Same(editor, root.EditorView); Assert.Same(canvas, editor.Canvas); Assert.Same(sourceButton, editor.StatusBar.SourceButton);
        Assert.True(path.IsFocused); Assert.Equal("uncommitted path draft", path.Value); Assert.Equal(caret, path.CaretIndex); Assert.Equal(selection, path.SelectionLength);
        Assert.Equal("unfinished transform", h.Transform.Fields[0].Value); Assert.Equal(1, creations);
        SourcePresentationFixture.Wait(h.Projects.CloseAsync(DocumentCloseChoice.Discard)); h.Refresh(); ui.Update();
        SourcePresentationFixture.Wait(h.Projects.OpenAsync(h.Control.Files.Project.ManifestPath)); h.Refresh(); ui.Update();
        Assert.NotEqual(old.OwnerKey, h.Active.OwnerKey); Assert.Equal(h.Active.OwnerKey, h.Model.Source!.OwnerKey);
        Assert.Equal(EditorSourceState.NoBuild, h.Model.Source.State); var current = h.Model.Source;
        oldHints.Signal(true); h.Refresh(); ui.Update(); Assert.Same(current, h.Model.Source);
        Assert.Same(editor, root.EditorView); Assert.Same(sourceButton, editor.StatusBar.SourceButton); Assert.Equal(1, creations);
        ui.Dispose(); Assert.True(root.IsDisposed); Assert.True(editor.IsDisposed); Assert.False(h.Active.IsClosed);
    }
}

internal sealed class EditorMainWindowFixture : IDisposable
{
    internal DocumentUiFixture Owner { get; } = new();
    internal EditorDocumentViewModel Documents { get; }
    internal EditorViewModel Model { get; }
    internal EditorWindowStyleCounter RootStyle { get; } = new();
    internal EditorViewStyleCounter ViewStyle { get; } = new();
    internal IFactory<EditorView> Views { get; }
    internal Func<EditorView?>? ViewFactory;
    internal int ViewCreations;
    internal EditorMainWindowFixture()
    {
        Documents = new(Owner.Commands); Documents.Refresh();
        Model = new(new(EditorScene.Demo()), new EditorCommands((_, _) => Task.CompletedTask, default), Documents);
        Views = new Factory<EditorView>(() => { ViewCreations++; return ViewFactory == null ? new(Model, ViewStyle) : ViewFactory()!; });
    }
    internal EditorMainWindow Create(string title = "Editor", Size? size = null) => new(Model, Documents, Views, RootStyle, new(title, size ?? new(1280, 800)));
    public void Dispose() { Model.Dispose(); Documents.Dispose(); Owner.Dispose(); }
}
internal sealed class EditorWindowStyleCounter : IViewStyle<EditorMainWindow>, IDisposable
{
    internal int Configurations, Disposals;
    internal Action<IViewStyleBuilder<EditorMainWindow>>? ConfigureAction;
    public void Configure(IViewStyleBuilder<EditorMainWindow> styles) { Configurations++; if (ConfigureAction == null) new EditorMainWindowStyle().Configure(styles); else ConfigureAction(styles); }
    public void Dispose() => Disposals++;
}
internal sealed class EditorViewStyleCounter : IViewStyle<EditorView>, IDisposable
{
    internal int Configurations, Disposals;
    public void Configure(IViewStyleBuilder<EditorView> styles) { Configurations++; new EditorViewStyle().Configure(styles); }
    public void Dispose() => Disposals++;
}
internal sealed class EditorWindowResource(Exception? failure = null) : IDisposable
{
    internal int Disposals;
    public void Dispose() { Disposals++; if (failure != null) throw failure; }
}
