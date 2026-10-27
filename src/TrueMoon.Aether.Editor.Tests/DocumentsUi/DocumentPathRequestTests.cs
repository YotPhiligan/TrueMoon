using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Tests.Documents;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.DocumentsUi;

public sealed class DocumentPathRequestTests
{
    [Fact]
    public void RequestOpen_PresentsRetainedDraftWithoutReadingUntilExplicitConfirmation()
    {
        using var owner = new DocumentUiFixture(); using var model = new EditorDocumentViewModel(owner.Commands);
        model.Refresh(); model.PathDraft = "Моя сцена.aetherscene";
        var notifications = new List<string?>(); model.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        model.Request(DocumentCommand.Open);
        Assert.Equal(DocumentCommand.Open, model.PathRequest); Assert.True(model.IsToolsVisible);
        Assert.Empty(owner.Store.Reads); Assert.False(owner.Commands.Read().Busy); Assert.Null(model.Document!.Content);
        Assert.Contains(nameof(model.PathRequest), notifications); Assert.Contains(nameof(model.IsToolsVisible), notifications);
        model.ConfirmPath(); owner.Complete(); model.Refresh();
        Assert.EndsWith("Моя сцена.aetherscene", Assert.Single(owner.Store.Reads));
        Assert.Null(model.PathRequest); Assert.False(model.IsToolsVisible); Assert.NotNull(model.Document!.Content);
        Assert.Equal("Моя сцена.aetherscene", model.PathDraft);
        model.ConfirmPath(); Assert.Single(owner.Store.Reads);
    }

    [Theory]
    [InlineData(DocumentCommand.Open)]
    [InlineData(DocumentCommand.Save)]
    [InlineData(DocumentCommand.SaveAs)]
    public void CancelPath_PreservesDirtyDocumentDraftAndHistoryWithoutRunningThePendingAction(DocumentCommand command)
    {
        using var owner = new DocumentUiFixture(); owner.Run(DocumentCommand.New);
        using var model = new EditorDocumentViewModel(owner.Commands); model.Refresh();
        var original = model.Document!.Content; model.PathDraft = "keep-draft.aetherscene";
        model.Request(command);
        Assert.Equal(command, model.PathRequest); Assert.True(model.IsToolsVisible);
        model.CancelPath();
        Assert.Null(model.PathRequest); Assert.False(model.IsToolsVisible);
        Assert.Same(original, model.Document!.Content); Assert.True(model.Document.IsDirty);
        Assert.Equal("keep-draft.aetherscene", model.PathDraft); Assert.Empty(owner.Store.Reads); Assert.Empty(owner.Store.Writes);
        Assert.False(model.AwaitingDecision); Assert.False(owner.Commands.Read().Busy);
    }

    [Theory]
    [InlineData(DocumentCommand.Save)]
    [InlineData(DocumentCommand.SaveAs)]
    public void UntitledSaveAndSaveAs_RequirePathThenSaveExactlyTheAcceptedDocument(DocumentCommand command)
    {
        using var owner = new DocumentUiFixture(); owner.Run(DocumentCommand.New);
        using var model = new EditorDocumentViewModel(owner.Commands); model.Refresh();
        var original = model.Document!.Content!.Document; model.PathDraft = "first-scene.aetherscene";
        model.Request(command);
        Assert.Equal(command, model.PathRequest); Assert.Empty(owner.Store.Writes);
        model.ConfirmPath(); owner.Complete(); model.Refresh();
        var write = Assert.Single(owner.Store.Writes);
        Assert.Same(original, write.Document); Assert.EndsWith("first-scene.aetherscene", write.Path);
        Assert.Null(write.Expected); Assert.False(model.Document!.IsDirty); Assert.Null(model.PathRequest);
        Assert.False(model.IsToolsVisible); Assert.EndsWith("first-scene.aetherscene", model.Document.Path);
    }

    [Fact]
    public void NamedSave_IsImmediateButSaveAsStillRequiresAnExplicitDestination()
    {
        using var owner = new DocumentUiFixture(); owner.Run(DocumentCommand.Open, "room.aetherscene");
        using var model = new EditorDocumentViewModel(owner.Commands); model.Refresh();
        model.PathDraft = "other.aetherscene"; model.Request(DocumentCommand.Save); owner.Complete(); model.Refresh();
        var originalSave = Assert.Single(owner.Store.Writes);
        Assert.Equal(owner.Files.ScenePath, originalSave.Path); Assert.Equal(new SceneFileStamp("observed-file"), originalSave.Expected);
        Assert.Null(model.PathRequest); Assert.False(model.IsToolsVisible);
        model.Request(DocumentCommand.SaveAs); Assert.Equal(DocumentCommand.SaveAs, model.PathRequest);
        Assert.Single(owner.Store.Writes); model.ConfirmPath(); owner.Complete(); model.Refresh();
        Assert.Equal(2, owner.Store.Writes.Count); Assert.EndsWith("other.aetherscene", owner.Store.Writes[1].Path);
        Assert.Null(owner.Store.Writes[1].Expected); Assert.False(model.IsToolsVisible);
    }

    [Fact]
    public void ConfirmedOpenOfDirtyUntitledScene_KeepsSeparateSaveDecisionAndAcceptedOpenTarget()
    {
        using var owner = new DocumentUiFixture(); owner.Run(DocumentCommand.New);
        using var model = new EditorDocumentViewModel(owner.Commands); model.Refresh();
        var original = model.Document!.Content!.Document;
        model.PathDraft = "next.aetherscene"; model.SavePathDraft = "preserved-original.aetherscene";
        model.Request(DocumentCommand.Open); model.ConfirmPath(); owner.Decision(); model.Refresh();
        Assert.Null(model.PathRequest); Assert.True(model.IsToolsVisible); Assert.True(model.AwaitingDecision); Assert.True(model.NeedsSavePath);
        Assert.Equal(DocumentCommand.Open, model.Operation); Assert.Empty(owner.Store.Reads); Assert.Empty(owner.Store.Writes);
        model.Choose(DocumentCloseChoice.Save); owner.Complete(); model.Refresh();
        Assert.Same(original, Assert.Single(owner.Store.Writes).Document);
        Assert.EndsWith("preserved-original.aetherscene", owner.Store.Writes[0].Path);
        Assert.EndsWith("next.aetherscene", Assert.Single(owner.Store.Reads));
        Assert.False(model.IsToolsVisible); Assert.False(model.AwaitingDecision); Assert.False(model.Document!.IsDirty);
        Assert.Equal("next.aetherscene", model.PathDraft); Assert.Equal("preserved-original.aetherscene", model.SavePathDraft);
    }

    [Fact]
    public void RejectedRequests_DoNotOpenToolsOrBypassRuntimeAdmission()
    {
        using var owner = new DocumentUiFixture(); using var model = new EditorDocumentViewModel(owner.Commands);
        model.Refresh(); model.Request(DocumentCommand.Save); model.Request(DocumentCommand.SaveAs);
        Assert.Null(model.PathRequest); Assert.False(model.IsToolsVisible);
        owner.RuntimeIdle = false; model.Refresh(); model.Request(DocumentCommand.Open); model.Request(DocumentCommand.New);
        Assert.Null(model.PathRequest); Assert.False(model.IsToolsVisible);
        Assert.Empty(owner.Store.Reads); Assert.Empty(owner.Store.Writes); Assert.Null(model.Document!.Content);
    }

    [Fact]
    public void PathConfirmation_RechecksAdmissionAndRetainsUnsubmittedDraftUntilRuntimeIsIdle()
    {
        using var owner = new DocumentUiFixture(); using var model = new EditorDocumentViewModel(owner.Commands); model.Refresh();
        model.PathDraft = "deferred.aetherscene"; model.Request(DocumentCommand.Open);
        Assert.True(model.CanConfirmPath);
        owner.RuntimeIdle = false; model.Refresh(); model.ConfirmPath();
        Assert.False(model.CanConfirmPath); Assert.Equal(DocumentCommand.Open, model.PathRequest); Assert.Empty(owner.Store.Reads);
        owner.RuntimeIdle = true; model.Refresh(); model.ConfirmPath(); owner.Complete(); model.Refresh();
        Assert.Null(model.PathRequest); Assert.EndsWith("deferred.aetherscene", Assert.Single(owner.Store.Reads));
        Assert.False(model.IsToolsVisible); Assert.Equal("deferred.aetherscene", model.PathDraft);
    }

    [Theory]
    [InlineData(DocumentCommand.Exit)]
    [InlineData((DocumentCommand)999)]
    public void InvalidRequest_RejectsBeforeChangingPromptOrDocument(DocumentCommand invalid)
    {
        using var owner = new DocumentUiFixture(); using var model = new EditorDocumentViewModel(owner.Commands); model.Refresh();
        model.Request(DocumentCommand.Open);
        Assert.Throws<ArgumentOutOfRangeException>(() => model.Request(invalid));
        Assert.Equal(DocumentCommand.Open, model.PathRequest); Assert.True(model.IsToolsVisible);
        Assert.Empty(owner.Store.Reads); Assert.Empty(owner.Store.Writes); Assert.Null(model.Document!.Content);
    }

    [Fact]
    public void DismissedError_DoesNotHideARepeatedFailureFromANewAcceptedAttempt()
    {
        using var owner = new DocumentUiFixture(); owner.Run(DocumentCommand.Open, "room.aetherscene");
        using var model = new EditorDocumentViewModel(owner.Commands); model.Refresh();
        owner.Store.Write = (_, _, _, _) => Task.FromException<SceneFileStamp>(new IOException("same destination unavailable"));
        model.Request(DocumentCommand.Save); owner.Complete(); model.Refresh();
        Assert.True(model.IsToolsVisible); Assert.Contains("same destination unavailable", model.ErrorText);
        var document = model.Document!.Content; model.CancelPath(); model.Refresh();
        Assert.False(model.IsToolsVisible); Assert.Same(document, model.Document!.Content);
        model.Request(DocumentCommand.Save); owner.Complete(); model.Refresh();
        Assert.True(model.IsToolsVisible); Assert.Contains("same destination unavailable", model.ErrorText);
        Assert.Equal(2, owner.Store.Writes.Count); Assert.Same(document, model.Document!.Content);
    }

    [Fact]
    public void PathActions_KeepOwnerThreadAndDisposedModelBoundaryWithoutOwningCoordinator()
    {
        using var owner = new DocumentUiFixture(); var model = new EditorDocumentViewModel(owner.Commands); model.Refresh();
        foreach (var action in new Action[] { () => model.Request(DocumentCommand.Open), model.ConfirmPath, model.CancelPath })
        {
            Exception? failure = null; var worker = new Thread(() => failure = Record.Exception(action));
            worker.Start(); Assert.True(worker.Join(TimeSpan.FromSeconds(5))); Assert.IsType<InvalidOperationException>(failure);
        }
        Assert.Null(model.PathRequest); Assert.Empty(owner.Store.Reads);
        model.Dispose();
        Assert.Throws<ObjectDisposedException>(() => model.Request(DocumentCommand.Open));
        Assert.Throws<ObjectDisposedException>(model.ConfirmPath); Assert.Throws<ObjectDisposedException>(model.CancelPath);
        owner.Run(DocumentCommand.New); Assert.True(owner.Document.Read().IsDirty); Assert.Equal(0, owner.RuntimeStops);
    }
}
