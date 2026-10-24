using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Scenes;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Editing;

public sealed class EditorEditingAdmissionTests
{
    [Fact]
    public void GuardedEdits_RejectCapturedDocumentAfterReplacementEvenWhenIdentityAndRevisionAreEqual()
    {
        using var owner = new DocumentUiFixture(); owner.Run(DocumentCommand.Open, "room.aetherscene"); var captured = owner.Document.Read().Content!.Document;
        owner.Store.File = new(DocumentData.Scene(), new("replacement")); owner.Run(DocumentCommand.Open, "replacement.aetherscene");
        var replacement = owner.Document.Read().Content!; Assert.NotSame(captured, replacement.Document); Assert.Equal(captured.Id, replacement.Document.Id); Assert.Equal(captured.Revision, replacement.Document.Revision);
        Assert.Throws<InvalidOperationException>(() => owner.Commands.Apply(captured, DocumentData.Edit(7, 51)));
        Assert.Throws<InvalidOperationException>(() => owner.Commands.Undo(captured, DocumentData.Operation(7)));
        Assert.Throws<InvalidOperationException>(() => owner.Commands.Redo(captured, DocumentData.Operation(7)));
        Assert.Same(replacement, owner.Document.Read().Content); Assert.False(owner.Document.Read().IsDirty);
        Assert.True(owner.Commands.Apply(replacement.Document, DocumentData.Edit(7, 52)).Succeeded);
        Assert.Equal(52, DocumentData.X(owner.Document.Read().Content!)); Assert.Equal(1, owner.Document.Read().Content!.UndoCount);
    }

    [Fact]
    public void GuardedMutationAndHistory_RejectLiveGameAndCancelledApplicationLifetime()
    {
        using var lifetime = new CancellationTokenSource(); using var owner = new DocumentUiFixture(lifetime: lifetime.Token); owner.Run(DocumentCommand.Open, "room.aetherscene");
        var captured = owner.Document.Read().Content!.Document; owner.RuntimeIdle = false;
        Assert.Throws<InvalidOperationException>(() => owner.Commands.Apply(captured, DocumentData.Edit(7, 61)));
        Assert.Throws<InvalidOperationException>(() => owner.Commands.Undo(captured, DocumentData.Operation(7)));
        Assert.Throws<InvalidOperationException>(() => owner.Commands.Redo(captured, DocumentData.Operation(7)));
        owner.RuntimeIdle = true; lifetime.Cancel(); Assert.Throws<InvalidOperationException>(() => owner.Commands.Apply(captured, DocumentData.Edit(7, 62)));
        Assert.Same(captured, owner.Document.Read().Content!.Document); Assert.Equal(4, DocumentData.X(owner.Document.Read().Content!)); Assert.False(owner.Document.Read().IsDirty);
    }

    [Theory]
    [InlineData(DocumentCommand.New)] [InlineData(DocumentCommand.Open)] [InlineData(DocumentCommand.Close)]
    public async Task DocumentTransition_DrainsPreviewBeforeChangingDocumentAndGuardsMutationsWhileWaiting(DocumentCommand transition)
    {
        await using var owner = new DocumentUiFixture(); owner.Run(DocumentCommand.Open, "room.aetherscene"); var captured = owner.Document.Read().Content!;
        var entered = DocumentData.Signal(); var release = DocumentData.Signal(); var stops = 0;
        await using var commands = new EditorDocumentCommands(owner.Projects, () => owner.RuntimeIdle, () => Task.CompletedTask,
            stopPreview: async () => { stops++; entered.TrySetResult(); await release.Task; });
        try
        {
            Assert.True(commands.Execute(transition, "replacement.aetherscene")); await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(commands.Pending.IsCompleted); Assert.Same(captured, owner.Document.Read().Content); Assert.Equal(0, owner.Authoring.Products[0].Disposals);
            Assert.Throws<InvalidOperationException>(() => commands.Apply(captured.Document, DocumentData.Edit(7, 71)));
            Assert.Throws<InvalidOperationException>(() => commands.Undo(captured.Document, DocumentData.Operation(7)));
            Assert.Throws<InvalidOperationException>(() => commands.Redo(captured.Document, DocumentData.Operation(7)));
            release.SetResult(); await commands.Pending.WaitAsync(TimeSpan.FromSeconds(10)); Assert.Equal(1, stops); Assert.Equal(1, owner.Authoring.Products[0].Disposals);
            Assert.Null(commands.Read().Error); Assert.False(commands.Read().Busy);
            if (transition == DocumentCommand.Close) Assert.Null(owner.Document.Read().Content);
            else Assert.NotSame(captured, owner.Document.Read().Content);
        }
        finally { release.TrySetResult(); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DirtyTransitionCancelOrSave_DoesNotStopWorkingPreview(bool save)
    {
        await using var owner = new DocumentUiFixture(); owner.Run(DocumentCommand.Open, "room.aetherscene"); Assert.True(owner.Document.Apply(DocumentData.Edit(7, 81)).Succeeded);
        var before = owner.Document.Read().Content; var stops = 0;
        await using var commands = new EditorDocumentCommands(owner.Projects, () => true, () => Task.CompletedTask, stopPreview: () => { stops++; return Task.CompletedTask; });
        if (save) { Assert.True(commands.Execute(DocumentCommand.Save)); await commands.Pending; Assert.False(owner.Document.Read().IsDirty); }
        else
        {
            Assert.True(commands.Execute(DocumentCommand.Close));
            Assert.True(SpinWait.SpinUntil(() => commands.Read().AwaitingDecision, TimeSpan.FromSeconds(10)));
            Assert.True(commands.Choose(DocumentCloseChoice.Cancel)); await commands.Pending; Assert.True(owner.Document.Read().IsDirty);
        }
        Assert.Equal(0, stops); Assert.Same(before, owner.Document.Read().Content); Assert.Equal(0, owner.Authoring.Products[0].Disposals);
    }

    [Fact]
    public async Task PreviewCleanupFailure_VetoesExitAndPreservesOwnedDocumentHistoryForRetry()
    {
        await using var owner = new DocumentUiFixture(); owner.Run(DocumentCommand.Open, "room.aetherscene"); Assert.True(owner.Document.Apply(DocumentData.Edit(7, 91)).Succeeded);
        var before = owner.Document.Read().Content; var failure = new IOException("preview reap failed"); var stops = 0; var runtimeStops = 0;
        await using var commands = new EditorDocumentCommands(owner.Projects, () => true, () => { runtimeStops++; return Task.CompletedTask; },
            stopPreview: () => ++stops == 1 ? Task.FromException(failure) : Task.CompletedTask);
        var first = commands.RequestExitAsync(); Assert.True(SpinWait.SpinUntil(() => commands.Read().AwaitingDecision, TimeSpan.FromSeconds(10)));
        Assert.True(commands.Choose(DocumentCloseChoice.Discard)); Assert.False(await first);
        Assert.Same(failure, commands.Read().Error); Assert.False(commands.Read().Closed); Assert.Same(before, owner.Document.Read().Content);
        Assert.Equal(1, before!.UndoCount); Assert.True(owner.Document.Read().IsDirty); Assert.Equal(0, owner.Authoring.Products[0].Disposals); Assert.Equal(0, runtimeStops);
        var retry = commands.RequestExitAsync(); Assert.True(SpinWait.SpinUntil(() => commands.Read().AwaitingDecision, TimeSpan.FromSeconds(10)));
        Assert.True(commands.Choose(DocumentCloseChoice.Discard)); Assert.True(await retry);
        Assert.Equal(2, stops); Assert.Equal(1, runtimeStops); Assert.Equal(1, owner.Authoring.Products[0].Disposals); Assert.True(commands.Read().Closed); Assert.Null(commands.Read().Error);
    }

    [Fact]
    public void PreviewFailureAndRetry_PreserveCommittedDocumentHistoryAndUncommittedDraft()
    {
        using var owner = new DocumentUiFixture(); owner.Run(DocumentCommand.Open, "room.aetherscene"); var attempts = 0; var failure = new IOException("GPU child unavailable");
        using var preview = new EditorPreview((scene, _) => ++attempts == 1 ? Task.FromException<ScenePreviewDocument>(failure) : Task.FromResult(EditingFixture.Receipt(scene)), () => Task.CompletedTask);
        using var model = new EditorTransformViewModel(owner.Commands, preview); model.Refresh(DocumentData.Child.Value.ToString()); model.Fields[0].Value = "101"; model.Apply();
        var committed = owner.Document.Read().Content; model.Fields[0].Value = "102"; model.ShowPreview(); DocumentUiFixture.Wait(preview.Pending); model.Refresh(DocumentData.Child.Value.ToString());
        Assert.Same(committed, owner.Document.Read().Content); Assert.Equal(1, committed!.UndoCount); Assert.True(owner.Document.Read().IsDirty); Assert.Equal("102", model.Fields[0].Value);
        Assert.Contains(failure.Message, model.PreviewText); Assert.True(model.HasDraft); Assert.True(model.CanApply); Assert.True(model.CanPreview);
        model.Refresh(DocumentData.Child.Value.ToString()); Assert.Equal(1, attempts); model.ShowPreview(); DocumentUiFixture.Wait(preview.Pending); model.Refresh(DocumentData.Child.Value.ToString());
        Assert.Equal(2, attempts); Assert.Equal(8, preview.Read().Presented!.Revision); Assert.Same(committed, owner.Document.Read().Content); Assert.Equal("102", model.Fields[0].Value); Assert.Null(preview.Read().Error);
    }
}
