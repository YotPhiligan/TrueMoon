using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.DocumentsUi;

public sealed class DocumentViewTests
{
    [Fact]
    public void RetainedParts_BindDraftAvailabilityActionsAndVisibleErrors()
    {
        using var owner = new DocumentUiFixture(); using var model = new EditorDocumentViewModel(owner.Commands);
        var view = new EditorDocumentView(model, new EditorDocumentViewStyle()); using var ui = DocumentUiFixture.Ui(view); ui.Update();
        Assert.Same(model, view.DataContext); Assert.Equal("Main.aetherscene", view.PathInput.Value);
        Assert.True(view.Commands[DocumentCommand.New].IsEnabled); Assert.True(view.Commands[DocumentCommand.Open].IsEnabled);
        Assert.False(view.Commands[DocumentCommand.Save].IsEnabled); Assert.False(view.Decision.IsVisible);
        Assert.Equal(260, view.PathInput.Width); Assert.Equal(360, view.SavePathInput.Width);
        Assert.Equal(ControlTone.Accent, view.SaveChoice.Tone); Assert.Equal(ControlTone.Danger, view.DiscardChoice.Tone);
        ui.Focus(view.PathInput); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true)); ui.HandleInput(new(InputKind.Text, Text: "new-scene.aetherscene"));
        Assert.Equal("new-scene.aetherscene", model.PathDraft);
        DocumentUiFixture.Click(ui, view.Commands[DocumentCommand.Save]); Assert.Empty(owner.Store.Writes);
        DocumentUiFixture.Click(ui, view.Commands[DocumentCommand.New]); owner.Complete(); model.Refresh(); ui.Update();
        Assert.True(view.Commands[DocumentCommand.Save].IsEnabled); Assert.Contains("*", view.Title.Value);
        DocumentUiFixture.Click(ui, view.Commands[DocumentCommand.Save]); owner.Complete(); model.Refresh(); ui.Update();
        Assert.EndsWith("new-scene.aetherscene", Assert.Single(owner.Store.Writes).Path); Assert.DoesNotContain("*", view.Title.Value);
        owner.Store.Write = (_, _, _, _) => Task.FromException<SceneFileStamp>(new IOException("destination unavailable"));
        DocumentUiFixture.Click(ui, view.Commands[DocumentCommand.Save]); owner.Complete(); model.Refresh(); ui.Update();
        Assert.Contains("destination unavailable", view.Error.Value); Assert.Equal(view.Theme.Error, view.Error.Foreground);
        Assert.Equal("new-scene.aetherscene", view.PathInput.Value); Assert.True(view.Commands[DocumentCommand.Save].IsEnabled);
    }

    [Theory]
    [InlineData(DocumentCloseChoice.Cancel)] [InlineData(DocumentCloseChoice.Discard)] [InlineData(DocumentCloseChoice.Save)]
    public void InlineDecision_UsesSeparateSaveDraftAndSafeCancelFocusThenRestoresPriorInput(DocumentCloseChoice choice)
    {
        using var owner = new DocumentUiFixture(); owner.Run(DocumentCommand.New);
        using var model = new EditorDocumentViewModel(owner.Commands) { PathDraft = "open-target.aetherscene", SavePathDraft = "saved-original.aetherscene" };
        var view = new EditorDocumentView(model, new EditorDocumentViewStyle()); using var ui = DocumentUiFixture.Ui(view); ui.Update();
        ui.Focus(view.PathInput); view.SynchronizeFocus(ui);
        DocumentUiFixture.Click(ui, view.Commands[DocumentCommand.Close]); owner.Decision(); model.Refresh(); ui.Update(); view.SynchronizeFocus(ui);
        Assert.True(view.Decision.IsVisible); Assert.True(view.CancelChoice.IsFocused); Assert.True(model.NeedsSavePath);
        Assert.Equal("saved-original.aetherscene", view.SavePathInput.Value); Assert.False(view.PathInput.IsEnabled);
        DocumentUiFixture.Click(ui, choice switch { DocumentCloseChoice.Save => view.SaveChoice, DocumentCloseChoice.Discard => view.DiscardChoice, _ => view.CancelChoice });
        owner.Complete(); model.Refresh(); ui.Update(); view.SynchronizeFocus(ui);
        Assert.False(view.Decision.IsVisible); Assert.True(view.PathInput.IsFocused); Assert.Equal("open-target.aetherscene", view.PathInput.Value);
        Assert.Equal("saved-original.aetherscene", view.SavePathInput.Value); Assert.Empty(model.ErrorText);
        if (choice == DocumentCloseChoice.Save)
        { Assert.EndsWith("saved-original.aetherscene", Assert.Single(owner.Store.Writes).Path); Assert.Null(owner.Document.Read().Content); }
        else
        { Assert.Empty(owner.Store.Writes); Assert.Equal(choice == DocumentCloseChoice.Cancel, owner.Document.Read().Content != null); }
    }

    [Fact]
    public void ThemeStyleAndRefresh_PreserveTreePathDraftFocusAndSelection()
    {
        using var owner = new DocumentUiFixture(); using var model = new EditorDocumentViewModel(owner.Commands);
        var view = new EditorDocumentView(model, new EditorDocumentViewStyle()); using var ui = DocumentUiFixture.Ui(view); ui.Update();
        model.PathDraft = "draft with spaces.aetherscene"; ui.Update(); ui.Focus(view.PathInput);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
        var selection = view.PathInput.SelectionLength; var tree = DocumentUiFixture.Tree(view).ToArray(); var path = view.PathInput;
        owner.Run(DocumentCommand.New); model.Refresh(); ui.Update(); ui.SetTheme(Theme.Light); ui.Update();
        view.WithViewStyle(new EditorDocumentViewStyle()); ui.Update();
        Assert.Equal(tree, DocumentUiFixture.Tree(view).ToArray()); Assert.Same(path, view.PathInput);
        Assert.True(path.IsFocused); Assert.Equal(selection, path.SelectionLength); Assert.Equal("draft with spaces.aetherscene", path.Value);
        Assert.Equal("draft with spaces.aetherscene", model.PathDraft); Assert.Equal(Theme.Light.Error, view.Error.Foreground);
        model.Refresh(); Assert.False(ui.Update()); Assert.All(tree, element => Assert.False(element.IsDisposed));
    }

    [Fact]
    public void Keyboard_SaveSaveAsCloseAndEscapeUseSameTrackedCommandsAsButtons()
    {
        using var owner = new DocumentUiFixture(); using var model = new EditorDocumentViewModel(owner.Commands);
        var view = new EditorDocumentView(model, new EditorDocumentViewStyle()); using var ui = DocumentUiFixture.Ui(view); ui.Update();
        owner.Run(DocumentCommand.New); model.Refresh(); ui.Update(); model.PathDraft = "first.aetherscene"; ui.Update();
        ui.Focus(view.PathInput); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.S, Control: true)); owner.Complete(); model.Refresh(); ui.Update();
        Assert.EndsWith("first.aetherscene", Assert.Single(owner.Store.Writes).Path);
        model.PathDraft = "copy.aetherscene"; ui.Update();
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.S, Control: true, Shift: true)); owner.Complete(); model.Refresh(); ui.Update();
        Assert.EndsWith("copy.aetherscene", owner.Store.Writes[1].Path); Assert.Null(owner.Store.Writes[1].Expected);
        Assert.False(view.HandleShortcut(new(InputKind.KeyUp, Key: UiKey.S, Control: true)));
        Assert.False(view.HandleShortcut(new(InputKind.KeyDown, Key: UiKey.S)));
        owner.Run(DocumentCommand.New); Assert.True(owner.Commands.Execute(DocumentCommand.Close)); owner.Decision(); model.Refresh(); ui.Update();
        Assert.True(view.HandleShortcut(new(InputKind.KeyDown, Key: UiKey.Escape))); owner.Complete(); model.Refresh(); ui.Update();
        Assert.False(model.AwaitingDecision);
        ui.Focus(view.PathInput); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.W, Control: true)); owner.Decision(); model.Refresh(); ui.Update();
        Assert.True(owner.Commands.Choose(DocumentCloseChoice.Discard)); owner.Complete(); model.Refresh(); ui.Update();
        Assert.Null(owner.Document.Read().Content); Assert.Equal(0, owner.RuntimeStops);
    }

    [Fact]
    public void KeyboardDuringDecision_SaveUsesPromptPathAndCloseCannotReplaceAcceptedIntent()
    {
        using var owner = new DocumentUiFixture(); owner.Run(DocumentCommand.New); var original = owner.Document.Read().Content!.Document;
        using var model = new EditorDocumentViewModel(owner.Commands) { PathDraft = "ordinary-draft.aetherscene", SavePathDraft = "prompt-save.aetherscene" };
        var view = new EditorDocumentView(model, new EditorDocumentViewStyle()); using var ui = DocumentUiFixture.Ui(view); ui.Update();
        Assert.True(owner.Commands.Execute(DocumentCommand.New)); owner.Decision(); model.Refresh(); ui.Update();
        Assert.True(view.HandleShortcut(new(InputKind.KeyDown, Key: UiKey.W, Control: true)));
        Assert.True(owner.Commands.Read().AwaitingDecision); Assert.Equal(DocumentCommand.New, owner.Commands.Read().Operation); Assert.Empty(owner.Store.Writes);
        Assert.True(view.HandleShortcut(new(InputKind.KeyDown, Key: UiKey.S, Control: true, Shift: true))); owner.Complete(); model.Refresh(); ui.Update();
        Assert.False(model.AwaitingDecision); Assert.Same(original, Assert.Single(owner.Store.Writes).Document);
        Assert.EndsWith("prompt-save.aetherscene", owner.Store.Writes[0].Path); Assert.NotEqual(original.Id, model.Document!.Content!.Document.Id);
        Assert.True(model.Document.IsDirty); Assert.Null(model.Document.Path); Assert.Equal("ordinary-draft.aetherscene", model.PathDraft);
    }

    [Fact]
    public void ViewDisposal_DetachesBindingsAndDoesNotDisposeBorrowedModelOrCommands()
    {
        using var owner = new DocumentUiFixture(); using var model = new EditorDocumentViewModel(owner.Commands);
        var view = new EditorDocumentView(model, new EditorDocumentViewStyle()); var ui = DocumentUiFixture.Ui(view); ui.Update();
        var path = view.PathInput; var value = path.Value; ui.Dispose();
        Assert.True(view.IsDisposed); Assert.True(path.IsDisposed); Assert.True(view.Decision.IsDisposed);
        model.PathDraft = "borrowed-model-still-alive.aetherscene"; owner.Run(DocumentCommand.New); model.Refresh();
        Assert.Equal(value, path.Value); Assert.Equal("borrowed-model-still-alive.aetherscene", model.PathDraft);
        Assert.True(model.CanSave); Assert.False(owner.Commands.Read().Closed); Assert.Equal(0, owner.RuntimeStops);
        Assert.Equal(0, owner.Store.Disposals); Assert.Equal(0, owner.Authoring.Disposals);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CreationFailure_DisposesUnpublishedTreeAndOwnedResourcesWithoutOwningModel(bool cleanupFailure)
    {
        using var owner = new DocumentUiFixture(); using var model = new EditorDocumentViewModel(owner.Commands);
        var original = new IOException("document style failed"); var cleanup = new IOException("unpublished document tree cleanup failed");
        EditorDocumentView unpublished = null!; var resource = new Resource(cleanupFailure ? cleanup : null);
        var error = Record.Exception(() => new EditorDocumentView(model, new FailingStyle(view => { unpublished = view; view.Own(resource); }, original)));
        if (cleanupFailure) Assert.Collection(Assert.IsType<AggregateException>(error).Flatten().InnerExceptions, e => Assert.Same(original, e), e => Assert.Same(cleanup, e));
        else Assert.Same(original, error);
        Assert.True(unpublished.IsDisposed); Assert.True(unpublished.PathInput.IsDisposed); Assert.True(unpublished.Decision.IsDisposed);
        Assert.Equal(1, resource.Disposals); model.PathDraft = "retry.aetherscene"; model.Refresh(); Assert.True(model.CanNew);
        Assert.False(owner.Commands.Read().Closed); Assert.Equal(0, owner.RuntimeStops);
    }

    private sealed class FailingStyle(Action<EditorDocumentView> capture, Exception error) : IViewStyle<EditorDocumentView>
    { public void Configure(IViewStyleBuilder<EditorDocumentView> styles) { styles.Part(view => { capture(view); return view.Error; }).FontSize(13); throw error; } }
    private sealed class Resource(Exception? error) : IDisposable
    { public int Disposals; public void Dispose() { Disposals++; if (error != null) throw error; } }
}
