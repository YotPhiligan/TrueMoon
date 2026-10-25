using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.DocumentsUi;

public sealed class CompactDocumentToolsTests
{
    [Fact]
    public void IdleTools_HaveNoDocumentBandAndOpeningOrClosingPromptKeepsSceneAndRetainedControls()
    {
        using var owner = new DocumentUiFixture(); using var model = new EditorDocumentViewModel(owner.Commands);
        var tools = new EditorDocumentView(model, new EditorDocumentViewStyle(), compactHeader: true);
        var scene = new TextBox { Value = "scene input" }; using var layout = Layout(tools, scene); using var ui = DocumentUiFixture.Ui(layout); ui.Update();
        Assert.True(tools.CompactHeader); Assert.False(tools.IsVisible); Assert.Equal(default, tools.Bounds);
        Assert.Equal(new Rect(0, 0, 1280, 800), scene.Bounds); Assert.False(tools.PathInput.IsDisposed);
        var tree = DocumentUiFixture.Tree(tools).ToArray(); var body = scene.Bounds;
        ui.Focus(scene); tools.SynchronizeFocus(ui); model.Request(DocumentCommand.Open); ui.Update(); tools.SynchronizeFocus(ui);
        Assert.True(tools.IsVisible); Assert.True(tools.PathInput.IsFocused); Assert.True(tools.Actions.IsVisible);
        Assert.Equal(body, scene.Bounds); Assert.Equal(44, tools.Bounds.Y); Assert.InRange(tools.Bounds.Height, 1, 300);
        Assert.All(tools.Commands.Values, command => Assert.False(command.IsVisible));
        DocumentUiFixture.Click(ui, tools.CancelPath); ui.Update(); tools.SynchronizeFocus(ui);
        Assert.False(tools.IsVisible); Assert.True(scene.IsFocused); Assert.Equal(body, scene.Bounds);
        Assert.Equal(tree, DocumentUiFixture.Tree(tools).ToArray()); Assert.Empty(owner.Store.Reads);
    }

    [Fact]
    public void OpenPrompt_ConfirmsEditedUnicodePathOnceAndThemeChangePreservesSelectionAndFocus()
    {
        using var owner = new DocumentUiFixture(); using var model = new EditorDocumentViewModel(owner.Commands);
        var tools = new EditorDocumentView(model, new EditorDocumentViewStyle(), compactHeader: true);
        var scene = new TextBox { Value = "scene" }; using var layout = Layout(tools, scene); using var ui = DocumentUiFixture.Ui(layout); ui.Update();
        ui.Focus(scene); model.Request(DocumentCommand.Open); ui.Update(); tools.SynchronizeFocus(ui);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
        ui.HandleInput(new(InputKind.Text, Text: "Сцена с пробелом.aetherscene"));
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true)); var selection = tools.PathInput.SelectionLength;
        var path = tools.PathInput; var viewport = scene.Bounds; var tree = DocumentUiFixture.Tree(tools).ToArray();
        ui.SetTheme(Theme.Light); tools.WithViewStyle(new EditorDocumentViewStyle()); ui.Update();
        Assert.True(path.IsFocused); Assert.Equal(selection, path.SelectionLength);
        Assert.Equal(tree, DocumentUiFixture.Tree(tools).ToArray()); Assert.Same(path, tools.PathInput);
        Assert.Equal("Сцена с пробелом.aetherscene", model.PathDraft); Assert.Equal(Theme.Light.PanelBackdrop, tools.Backdrop);
        DocumentUiFixture.Click(ui, tools.ConfirmPath); owner.Complete(); model.Refresh(); ui.Update(); tools.SynchronizeFocus(ui);
        Assert.EndsWith("Сцена с пробелом.aetherscene", Assert.Single(owner.Store.Reads));
        Assert.False(tools.IsVisible); Assert.True(scene.IsFocused); Assert.Equal(viewport, scene.Bounds);
        Assert.Equal("Сцена с пробелом.aetherscene", path.Value); Assert.False(path.IsDisposed);
    }

    [Fact]
    public void OpenPrompt_RefreshUpdatesConfirmationAvailabilityWhenRuntimeAdmissionChanges()
    {
        using var owner = new DocumentUiFixture(); using var model = new EditorDocumentViewModel(owner.Commands);
        var tools = new EditorDocumentView(model, new EditorDocumentViewStyle(), compactHeader: true);
        using var layout = Layout(tools, new Panel()); using var ui = DocumentUiFixture.Ui(layout); ui.Update();
        model.Request(DocumentCommand.Open); ui.Update(); Assert.True(tools.ConfirmPath.IsEnabled);
        owner.RuntimeIdle = false; model.Refresh(); ui.Update();
        Assert.False(tools.ConfirmPath.IsEnabled); Assert.False(tools.PathInput.IsEnabled);
        DocumentUiFixture.Click(ui, tools.ConfirmPath); Assert.Empty(owner.Store.Reads); Assert.Equal(DocumentCommand.Open, model.PathRequest);
        owner.RuntimeIdle = true; model.Refresh(); ui.Update();
        Assert.True(tools.ConfirmPath.IsEnabled); Assert.True(tools.PathInput.IsEnabled);
        DocumentUiFixture.Click(ui, tools.ConfirmPath); owner.Complete(); model.Refresh(); ui.Update();
        Assert.Single(owner.Store.Reads); Assert.False(tools.IsVisible);
    }

    [Fact]
    public void UnsavedDecision_StaysVisibleWithSafeCancelFocusAndCannotBeDismissedAsAnOrdinaryPrompt()
    {
        using var owner = new DocumentUiFixture(); owner.Run(DocumentCommand.New);
        using var model = new EditorDocumentViewModel(owner.Commands);
        var tools = new EditorDocumentView(model, new EditorDocumentViewStyle(), compactHeader: true);
        var scene = new TextBox { Value = "scene" }; using var layout = Layout(tools, scene); using var ui = DocumentUiFixture.Ui(layout); ui.Update();
        ui.Focus(scene); tools.SynchronizeFocus(ui); model.Request(DocumentCommand.Close); owner.Decision(); model.Refresh(); ui.Update(); tools.SynchronizeFocus(ui);
        var original = model.Document!.Content;
        Assert.True(tools.IsVisible); Assert.True(tools.Decision.IsVisible); Assert.True(tools.CancelChoice.IsFocused);
        Assert.False(tools.Actions.IsVisible); Assert.False(tools.DismissTools.IsVisible); Assert.True(model.NeedsSavePath);
        model.CancelPath(); ui.Update(); Assert.True(tools.IsVisible); Assert.True(model.AwaitingDecision); Assert.Same(original, model.Document!.Content);
        DocumentUiFixture.Click(ui, tools.CancelChoice); owner.Complete(); model.Refresh(); ui.Update(); tools.SynchronizeFocus(ui);
        Assert.False(tools.IsVisible); Assert.True(scene.IsFocused); Assert.Same(original, model.Document!.Content);
        Assert.True(model.Document.IsDirty); Assert.Empty(owner.Store.Writes);
    }

    [Fact]
    public void CompactShortcuts_RequestSaveDestinationAndEscapeCancelsWithoutWriting()
    {
        using var owner = new DocumentUiFixture(); owner.Run(DocumentCommand.New);
        using var model = new EditorDocumentViewModel(owner.Commands);
        var tools = new EditorDocumentView(model, new EditorDocumentViewStyle(), compactHeader: true);
        using var ui = DocumentUiFixture.Ui(tools); ui.Update();
        Assert.True(tools.HandleShortcut(new(InputKind.KeyDown, Key: UiKey.S, Control: true))); ui.Update();
        Assert.Equal(DocumentCommand.Save, model.PathRequest); Assert.True(tools.IsVisible); Assert.Empty(owner.Store.Writes);
        Assert.True(tools.HandleShortcut(new(InputKind.KeyDown, Key: UiKey.Escape))); ui.Update();
        Assert.False(tools.IsVisible); Assert.Null(model.PathRequest); Assert.True(model.Document!.IsDirty); Assert.Empty(owner.Store.Writes);
        Assert.False(tools.HandleShortcut(new(InputKind.KeyUp, Key: UiKey.Escape)));
    }

    [Fact]
    public void EditorRoot_ForwardsSaveAndEscapeWhileItsRetainedDocumentToolsAreHidden()
    {
        using var owner = new DocumentUiFixture(); owner.Run(DocumentCommand.New);
        using var documents = new EditorDocumentViewModel(owner.Commands);
        using var model = new EditorViewModel(new(new("empty", 0, [])), new((_, _) => Task.CompletedTask, CancellationToken.None), documents);
        var view = new EditorView(model, new EditorViewStyle(),
            new Factory<EditorDocumentView>(() => new(documents, new EditorDocumentViewStyle(), compactHeader: true)), compactHeader: true);
        using var ui = DocumentUiFixture.Ui(view); ui.Update();
        var tools = view.Documents!; var viewport = view.Canvas.Bounds;
        Assert.False(tools.IsVisible); Assert.False(view.Toolbar.IsVisible);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.S, Control: true)); ui.Update();
        Assert.Equal(DocumentCommand.Save, documents.PathRequest); Assert.True(tools.IsVisible); Assert.Empty(owner.Store.Writes);
        tools.SynchronizeFocus(ui); Assert.True(tools.PathInput.IsFocused);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape)); ui.Update(); tools.SynchronizeFocus(ui);
        Assert.Null(documents.PathRequest); Assert.False(tools.IsVisible); Assert.Equal(viewport, view.Canvas.Bounds);
        Assert.True(documents.Document!.IsDirty); Assert.Empty(owner.Store.Writes);
    }

    [Fact]
    public void DisposingHiddenCompactTools_DetachesBindingsAndKeepsBorrowedModelAndDocumentUsable()
    {
        using var owner = new DocumentUiFixture(); using var model = new EditorDocumentViewModel(owner.Commands);
        var tools = new EditorDocumentView(model, new EditorDocumentViewStyle(), compactHeader: true);
        var ui = DocumentUiFixture.Ui(tools); ui.Update(); Assert.False(tools.IsVisible);
        var path = tools.PathInput; var draft = path.Value; ui.Dispose();
        Assert.True(tools.IsDisposed); Assert.True(path.IsDisposed); Assert.True(tools.ConfirmPath.IsDisposed); Assert.True(tools.CancelPath.IsDisposed);
        Assert.True(tools.DismissTools.IsDisposed); Assert.All(DocumentUiFixture.Tree(tools), element => Assert.True(element.IsDisposed));
        model.PathDraft = "borrowed.aetherscene"; model.Request(DocumentCommand.Open); model.ConfirmPath(); owner.Complete(); model.Refresh();
        Assert.Equal(draft, path.Value); Assert.NotNull(model.Document!.Content); Assert.Equal(0, owner.Store.Disposals); Assert.Equal(0, owner.RuntimeStops);
    }

    private static EditorLayout Layout(EditorDocumentView tools, Element scene) =>
        new(tools, new Panel { IsVisible = false }, scene, new Panel { IsVisible = false }, new Panel(), overlayChrome: true);
}
