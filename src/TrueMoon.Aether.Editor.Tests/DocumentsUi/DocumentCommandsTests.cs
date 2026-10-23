using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Scenes;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.DocumentsUi;

public sealed class DocumentCommandsTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EmptyOrMetadataOnlyProject_RejectsDocumentActionsAndExitsWithoutImplicitScene(bool metadataOnly)
    {
        await using var owner = new DocumentUiFixture(openProject: metadataOnly, metadataOnly: metadataOnly);
        if (metadataOnly) Assert.Null(owner.Projects.Active!.Documents);
        foreach (var command in new[] { DocumentCommand.New, DocumentCommand.Open, DocumentCommand.Save, DocumentCommand.SaveAs, DocumentCommand.Close })
        { Assert.False(owner.Commands.CanExecute(command)); Assert.False(owner.Commands.Execute(command, "room.aetherscene")); }
        Assert.Null(owner.Commands.Read().Document); Assert.Equal(metadataOnly ? owner.Files.Project.ScenesDirectory : null, owner.Commands.Read().ScenesDirectory);
        Assert.False(owner.Commands.Read().Busy); Assert.Null(owner.Commands.Read().Error);
        Assert.True(await owner.Commands.RequestExitAsync()); Assert.True(owner.Commands.Read().Closed);
        Assert.Equal(1, owner.RuntimeStops); Assert.False(owner.Projects.IsClosed); Assert.Empty(owner.Authoring.Products);
        Assert.Empty(owner.Store.Reads); Assert.Empty(owner.Store.Writes);
    }

    [Fact]
    public async Task NewSaveAsOpenAndSave_UseCurrentPathStampAndPreservePersistedRawScene()
    {
        await using var owner = new DocumentUiFixture();
        owner.Run(DocumentCommand.New);
        var created = owner.Document.Read(); Assert.True(created.IsDirty); Assert.Null(created.Path);
        Assert.Equal(0, created.Content!.Document.Revision); Assert.Empty(created.Content.Document.Entities);
        Assert.Empty(created.Content.Document.Definitions); Assert.Empty(created.Content.Document.Schemas); Assert.Empty(owner.Store.Writes);
        owner.Run(DocumentCommand.Save, "first.aetherscene");
        var first = Assert.Single(owner.Store.Writes); Assert.Null(first.Expected); Assert.Same(created.Content.Document, first.Document);
        Assert.Equal(Path.Combine(owner.Files.Project.ScenesDirectory, "first.aetherscene"), first.Path);
        Assert.False(owner.Document.Read().IsDirty); Assert.Equal(first.Path, owner.Document.Read().Path);
        owner.Run(DocumentCommand.Save, "ignored-draft.aetherscene");
        Assert.Equal(first.Path, owner.Store.Writes[1].Path); Assert.Equal(new SceneFileStamp("written-1"), owner.Store.Writes[1].Expected);
        owner.Run(DocumentCommand.SaveAs, "copy.aetherscene");
        Assert.Null(owner.Store.Writes[2].Expected); Assert.EndsWith("copy.aetherscene", owner.Document.Read().Path);
        owner.Run(DocumentCommand.Open, "room.aetherscene");
        DocumentData.AssertScene(owner.Document.Read().Content!.Document);
        Assert.Equal(owner.Store.File.Stamp, owner.Document.Read().Stamp); Assert.False(owner.Document.Read().IsDirty);
        Assert.True(owner.Document.Apply(DocumentData.Edit(7, 34)).Succeeded);
        owner.Run(DocumentCommand.Save, "still-ignored.aetherscene");
        Assert.Equal(owner.Files.ScenePath, owner.Store.Writes[3].Path); Assert.Equal(owner.Store.File.Stamp, owner.Store.Writes[3].Expected);
        Assert.Equal(8, owner.Store.Writes[3].Document.Revision); Assert.Equal(34d, DocumentData.X(owner.Document.Read().Content!));
        Assert.False(owner.Document.Read().IsDirty); Assert.Null(owner.Commands.Read().Error);
        Assert.Equal(0, owner.Store.Disposals); Assert.Equal(0, owner.Authoring.Disposals);
    }

    [Theory]
    [InlineData("new")] [InlineData("open")] [InlineData("close")] [InlineData("exit")]
    public async Task DirtyTransition_CancelRetainsIdentityContentAndAllowsRetry(string transition)
    {
        await using var owner = new DocumentUiFixture(); Assert.True(await owner.Document.NewAsync(DocumentData.Scene()));
        Assert.True(owner.Document.Apply(DocumentData.Edit(7, 21)).Succeeded); var before = owner.Document.Read();
        var exiting = owner.BeginTransition(transition); owner.Decision();
        Assert.True(owner.Commands.Read().Busy); Assert.False(owner.Commands.CanExecute(DocumentCommand.Save));
        Assert.False(owner.Commands.Execute(DocumentCommand.Open, "rejected.aetherscene"));
        Assert.True(owner.Commands.Choose(DocumentCloseChoice.Cancel)); Assert.False(owner.Commands.Choose(DocumentCloseChoice.Discard));
        await owner.CompleteTransition(exiting, false);
        Assert.Same(before.Content, owner.Document.Read().Content); Assert.Equal(before.Path, owner.Document.Read().Path);
        Assert.True(owner.Document.Read().IsDirty); Assert.Equal(21d, DocumentData.X(owner.Document.Read().Content!));
        Assert.Equal(0, owner.Authoring.Products[0].Disposals); Assert.Empty(owner.Store.Writes); Assert.Empty(owner.Store.Reads);
        Assert.Equal(0, owner.RuntimeStops); Assert.False(owner.Commands.Read().Busy); Assert.False(owner.Commands.Read().Closed);
        Assert.Null(owner.Commands.Read().Error); Assert.True(owner.Commands.CanExecute(DocumentCommand.Save));
    }

    [Theory]
    [InlineData("new")] [InlineData("open")] [InlineData("close")] [InlineData("exit")]
    public async Task DirtyTransition_DiscardReleasesOriginalWithoutSavingAndCommitsIntendedTarget(string transition)
    {
        await using var owner = new DocumentUiFixture(); Assert.True(await owner.Document.NewAsync(DocumentData.Scene()));
        var before = owner.Document.Read().Content!;
        var exiting = owner.BeginTransition(transition); owner.Decision(); Assert.True(owner.Commands.Choose(DocumentCloseChoice.Discard));
        await owner.CompleteTransition(exiting);
        Assert.Empty(owner.Store.Writes); Assert.Equal(1, owner.Authoring.Products[0].Disposals);
        Assert.Equal(4d, DocumentData.X(before)); Assert.False(owner.Commands.Read().Busy); Assert.Null(owner.Commands.Read().Error);
        if (transition == "open") { Assert.Equal(owner.Files.ScenePath.Replace("room.aetherscene", "other.aetherscene"), Assert.Single(owner.Store.Reads)); DocumentData.AssertScene(owner.Document.Read().Content!.Document); }
        else if (transition == "new") { Assert.Empty(owner.Document.Read().Content!.Document.Entities); Assert.NotEqual(before.Document.Id, owner.Document.Read().Content!.Document.Id); Assert.True(owner.Document.Read().IsDirty); }
        else { Assert.Null(owner.Document.Read().Content); Assert.False(owner.Document.Read().IsDirty); }
        Assert.Equal(transition == "exit" ? 1 : 0, owner.RuntimeStops); Assert.Equal(transition == "exit", owner.Commands.Read().Closed);
    }

    [Theory]
    [InlineData("new", false)] [InlineData("open", false)] [InlineData("close", false)] [InlineData("exit", false)]
    [InlineData("new", true)] [InlineData("open", true)] [InlineData("close", true)] [InlineData("exit", true)]
    public async Task DirtyTransition_SavePersistsOriginalBeforeReplacementOrExitUsingCorrectTarget(string transition, bool titled)
    {
        await using var owner = new DocumentUiFixture();
        if (titled) Assert.True(await owner.Document.LoadAsync("existing.aetherscene"));
        else Assert.True(await owner.Document.NewAsync(DocumentData.Scene()));
        Assert.True(owner.Document.Apply(DocumentData.Edit(7, 47)).Succeeded); var original = owner.Document.Read();
        owner.Store.Write = (_, document, _, _) =>
        {
            Assert.Equal(0, owner.Authoring.Products[0].Disposals); Assert.Equal(0, owner.RuntimeStops);
            Assert.Same(original.Content!.Document, document); return Task.FromResult(new SceneFileStamp("saved-before-transition"));
        };
        var exiting = owner.BeginTransition(transition); owner.Decision();
        Assert.True(owner.Commands.Choose(DocumentCloseChoice.Save, "first-save.aetherscene"));
        await owner.CompleteTransition(exiting);
        var saved = Assert.Single(owner.Store.Writes);
        Assert.Equal(Path.Combine(owner.Files.Project.ScenesDirectory, titled ? "existing.aetherscene" : "first-save.aetherscene"), saved.Path);
        Assert.Equal(titled ? owner.Store.File.Stamp : null, saved.Expected); Assert.Equal(8, saved.Document.Revision);
        Assert.Equal(1, owner.Authoring.Products[0].Disposals); Assert.Equal(transition == "exit" ? 1 : 0, owner.RuntimeStops);
        Assert.Null(owner.Commands.Read().Error); Assert.False(owner.Commands.Read().Busy);
    }

    [Theory]
    [InlineData("new")] [InlineData("open")] [InlineData("close")] [InlineData("exit")]
    public async Task FailedDecisionSave_VetoesTransitionKeepsDirtyOriginalAndCanRetryIntent(string transition)
    {
        await using var owner = new DocumentUiFixture(); Assert.True(await owner.Document.NewAsync(DocumentData.Scene()));
        var before = owner.Document.Read(); var error = new IOException("document destination is read-only");
        owner.Store.Write = (_, _, _, _) => Task.FromException<SceneFileStamp>(error);
        var exiting = owner.BeginTransition(transition); owner.Decision(); Assert.True(owner.Commands.Choose(DocumentCloseChoice.Save, "bad.aetherscene"));
        await owner.CompleteTransition(exiting, false);
        Assert.Same(error, owner.Commands.Read().Error); Assert.Same(before.Content, owner.Document.Read().Content);
        Assert.True(owner.Document.Read().IsDirty); Assert.Null(owner.Document.Read().Path);
        Assert.False(owner.Commands.Read().Busy); Assert.False(owner.Commands.Read().AwaitingDecision); Assert.False(owner.Commands.Read().Closed);
        Assert.Equal(0, owner.RuntimeStops); Assert.Equal(0, owner.Authoring.Products[0].Disposals);
        owner.Store.Write = null;
        exiting = owner.BeginTransition(transition); owner.Decision(); Assert.True(owner.Commands.Choose(DocumentCloseChoice.Discard));
        await owner.CompleteTransition(exiting); Assert.Null(owner.Commands.Read().Error);
    }

    [Theory]
    [InlineData("read")] [InlineData("conflict")] [InlineData("path")]
    public async Task FailedOpenOrSave_PreservesCurrentDocumentAndClearsErrorOnSuccessfulRetry(string failure)
    {
        await using var owner = new DocumentUiFixture(); owner.Run(DocumentCommand.Open, "room.aetherscene");
        var before = owner.Document.Read(); Exception? error = failure switch
        { "read" => new InvalidDataException("unsupported scene"), "conflict" => new SceneFileConflictException(owner.Files.ScenePath), _ => null };
        if (failure == "read") owner.Store.Read = (_, _) => Task.FromException<SceneFile>(error!);
        if (failure == "conflict") owner.Store.Write = (_, _, _, _) => Task.FromException<SceneFileStamp>(error!);
        owner.Run(failure == "conflict" ? DocumentCommand.Save : DocumentCommand.Open, failure == "path" ? "../escape.aetherscene" : "next.aetherscene");
        Assert.NotNull(owner.Commands.Read().Error); if (error != null) Assert.Same(error, owner.Commands.Read().Error);
        else Assert.IsType<ArgumentException>(owner.Commands.Read().Error);
        Assert.Same(before.Content, owner.Document.Read().Content); Assert.Equal(before.Path, owner.Document.Read().Path);
        Assert.Equal(before.SavedContentRevision, owner.Document.Read().SavedContentRevision); Assert.False(owner.Document.Read().IsDirty);
        owner.Store.Read = null; owner.Store.Write = null; owner.Run(DocumentCommand.Save);
        Assert.Null(owner.Commands.Read().Error); Assert.False(owner.Commands.Read().Busy); Assert.False(owner.Commands.Read().AwaitingDecision);
    }

    [Fact]
    public async Task DirtyOpenDiscard_InvalidCandidateDoesNotDiscardCurrentContentOrSaveIt()
    {
        await using var owner = new DocumentUiFixture(); Assert.True(await owner.Document.NewAsync(DocumentData.Scene()));
        var original = owner.Document.Read().Content; var error = new InvalidDataException("candidate cannot load");
        owner.Store.Read = (_, _) => Task.FromException<SceneFile>(error);
        Assert.True(owner.Commands.Execute(DocumentCommand.Open, "invalid.aetherscene")); owner.Decision();
        Assert.True(owner.Commands.Choose(DocumentCloseChoice.Discard)); await owner.Commands.Pending;
        Assert.Same(original, owner.Document.Read().Content); Assert.True(owner.Document.Read().IsDirty);
        Assert.Same(error, owner.Commands.Read().Error); Assert.Empty(owner.Store.Writes); Assert.Equal(0, owner.Authoring.Products[0].Disposals);
    }

    [Fact]
    public async Task BusyCancel_RejectsConcurrentActionsAndDrainsIgnoredReadWithoutPublishingLateContent()
    {
        await using var owner = new DocumentUiFixture(); owner.Run(DocumentCommand.Open, "room.aetherscene"); var before = owner.Document.Read();
        var entered = DocumentData.Signal(); var finish = DocumentData.Signal(); var cancelled = DocumentData.Signal();
        owner.Store.Read = async (_, token) => { using var registration = token.Register(() => cancelled.TrySetResult()); entered.SetResult(); await finish.Task.WaitAsync(TimeSpan.FromSeconds(10)); return new(DocumentData.Empty(22), new("late")); };
        Assert.True(owner.Commands.Execute(DocumentCommand.Open, "late.aetherscene")); var pending = owner.Commands.Pending;
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); Assert.True(owner.Commands.Read().Busy);
        Assert.False(owner.Commands.Execute(DocumentCommand.New)); Assert.False(owner.Commands.Execute(DocumentCommand.Save)); Assert.Same(pending, owner.Commands.Pending);
        owner.Commands.Cancel(); await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5)); Assert.False(pending.IsCompleted); Assert.True(owner.Commands.Read().Busy);
        Assert.Same(before.Content, owner.Document.Read().Content); finish.SetResult(); await pending.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Same(before.Content, owner.Document.Read().Content); Assert.Equal(before.Path, owner.Document.Read().Path);
        Assert.False(owner.Commands.Read().Busy); Assert.Null(owner.Commands.Read().Error); Assert.Single(owner.Authoring.Products);
        owner.Store.Read = null; owner.Run(DocumentCommand.Open, "room.aetherscene"); Assert.Null(owner.Commands.Read().Error);
    }

    [Fact]
    public async Task RequestExit_DrainsIgnoredSaveBeforeStoppingRuntimeAndConcurrentRequestsShareCompletion()
    {
        await using var owner = new DocumentUiFixture(); owner.Run(DocumentCommand.Open, "room.aetherscene");
        var entered = DocumentData.Signal(); var finish = DocumentData.Signal(); var cancelled = DocumentData.Signal();
        owner.Store.Write = async (_, _, _, token) => { using var registration = token.Register(() => cancelled.TrySetResult()); entered.SetResult(); await finish.Task.WaitAsync(TimeSpan.FromSeconds(10)); return new("committed"); };
        Assert.True(owner.Commands.Execute(DocumentCommand.Save)); var pending = owner.Commands.Pending;
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); var exiting = owner.Commands.RequestExitAsync();
        Assert.Same(exiting, owner.Commands.RequestExitAsync()); await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(exiting.IsCompleted); Assert.False(pending.IsCompleted); Assert.Equal(0, owner.RuntimeStops);
        Assert.True(owner.Commands.Read().Exiting); Assert.False(owner.Commands.Execute(DocumentCommand.New));
        Assert.Equal(0, owner.Authoring.Products[0].Disposals); finish.SetResult();
        Assert.True(await exiting.WaitAsync(TimeSpan.FromSeconds(10))); await pending;
        Assert.Equal(1, owner.RuntimeStops); Assert.Equal(1, owner.Authoring.Products[0].Disposals);
        Assert.Null(owner.Document.Read().Content); Assert.True(owner.Commands.Read().Closed); Assert.False(owner.Commands.Read().Busy);
        Assert.False(owner.Projects.IsClosed); Assert.Equal(0, owner.Store.Disposals); Assert.Equal(0, owner.Authoring.Disposals);
    }

    [Fact]
    public async Task ForcedStop_ResolvesPendingDecisionDrainsWorkAndLeavesBorrowedProjectOwnerUsable()
    {
        await using var owner = new DocumentUiFixture(); owner.Run(DocumentCommand.New);
        Assert.True(owner.Commands.Execute(DocumentCommand.Close)); owner.Decision();
        var stopping = owner.Commands.StopAsync(); await stopping.WaitAsync(TimeSpan.FromSeconds(10)); await owner.Commands.Pending;
        Assert.True(owner.Commands.Read().Closed); Assert.False(owner.Commands.Read().AwaitingDecision); Assert.Null(owner.Document.Read().Content);
        Assert.False(owner.Commands.Execute(DocumentCommand.New)); Assert.Equal(1, owner.RuntimeStops); Assert.Equal(1, owner.Authoring.Products[0].Disposals);
        Assert.False(owner.Projects.IsClosed); Assert.False(owner.Projects.Active!.IsClosed); Assert.False(owner.Document.Read().IsClosed);
        Assert.True(await owner.Document.NewAsync(DocumentData.Empty())); Assert.True(owner.Document.Read().IsDirty);
        Assert.Equal(0, owner.Store.Disposals); Assert.Equal(0, owner.Authoring.Disposals);
        await owner.Commands.StopAsync(); Assert.Equal(1, owner.RuntimeStops);
    }

    [Fact]
    public async Task RuntimeAndLifetimeGates_RejectAllActionsWithoutInvokingStoreAndRecoverWhenIdle()
    {
        using var lifetime = new CancellationTokenSource(); await using var owner = new DocumentUiFixture(lifetime: lifetime.Token);
        owner.Run(DocumentCommand.Open, "room.aetherscene"); owner.RuntimeIdle = false;
        foreach (var command in new[] { DocumentCommand.New, DocumentCommand.Open, DocumentCommand.Save, DocumentCommand.SaveAs, DocumentCommand.Close })
        { Assert.False(owner.Commands.CanExecute(command)); Assert.False(owner.Commands.Execute(command, "target.aetherscene")); }
        Assert.False(owner.Commands.Read().RuntimeIdle); Assert.Single(owner.Store.Reads); Assert.Empty(owner.Store.Writes);
        owner.RuntimeIdle = true; Assert.True(owner.Commands.CanExecute(DocumentCommand.Save)); lifetime.Cancel();
        Assert.False(owner.Commands.CanExecute(DocumentCommand.New)); Assert.False(owner.Commands.Execute(DocumentCommand.Save));
        Assert.Empty(owner.Store.Writes); Assert.Null(owner.Commands.Read().Error);
    }

    [Fact]
    public async Task Cancel_CallbackFailureIsObservedWhileIgnoredIoDrainsAndCanRecover()
    {
        await using var owner = new DocumentUiFixture(); owner.Run(DocumentCommand.Open, "room.aetherscene"); var original = owner.Document.Read().Content;
        var entered = DocumentData.Signal(); var finish = DocumentData.Signal(); var expected = new IOException("cancellation observer failed");
        owner.Store.Read = async (_, token) => { using var registration = token.Register(() => throw expected); entered.SetResult(); await finish.Task.WaitAsync(TimeSpan.FromSeconds(10)); return owner.Store.File; };
        Assert.True(owner.Commands.Execute(DocumentCommand.Open, "late.aetherscene")); var pending = owner.Commands.Pending;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); Assert.Null(Record.Exception(owner.Commands.Cancel));
            Assert.False(pending.IsCompleted); Assert.True(owner.Commands.Read().Busy); Assert.Same(original, owner.Document.Read().Content);
        }
        finally { finish.TrySetResult(); await pending.WaitAsync(TimeSpan.FromSeconds(10)); }
        Assert.Same(expected, Assert.Single(Assert.IsType<AggregateException>(owner.Commands.Read().Error).Flatten().InnerExceptions));
        Assert.Same(original, owner.Document.Read().Content); Assert.False(owner.Commands.Read().Busy); Assert.Single(owner.Authoring.Products);
        owner.Store.Read = null; owner.Run(DocumentCommand.Open, "room.aetherscene"); Assert.Null(owner.Commands.Read().Error);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Exit_CallbackFailureReturnsTrackedDrainRetainsOwnerAndSupportsExplicitRetry(bool force)
    {
        await using var owner = new DocumentUiFixture(); owner.Run(DocumentCommand.Open, "room.aetherscene"); var original = owner.Document.Read().Content;
        var entered = DocumentData.Signal(); var finish = DocumentData.Signal(); var expected = new IOException("exit cancellation observer failed");
        owner.Store.Read = async (_, token) => { using var registration = token.Register(() => throw expected); entered.SetResult(); await finish.Task.WaitAsync(TimeSpan.FromSeconds(10)); return owner.Store.File; };
        Assert.True(owner.Commands.Execute(DocumentCommand.Open, "late.aetherscene")); var pending = owner.Commands.Pending; Task? stopping = null; Task<bool>? exiting = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Null(Record.Exception(() => { if (force) stopping = owner.Commands.StopAsync(); else exiting = owner.Commands.RequestExitAsync(); }));
            var drain = stopping ?? exiting!; Assert.False(drain.IsCompleted); Assert.True(owner.Commands.Read().Busy); Assert.True(owner.Commands.Read().Exiting);
            if (!force) Assert.Same(exiting, owner.Commands.Pending);
            Assert.Equal(0, owner.RuntimeStops); Assert.Equal(0, owner.Authoring.Products[0].Disposals); finish.SetResult();
            if (force) Assert.Same(expected, Assert.Single((await Assert.ThrowsAsync<AggregateException>(() => stopping!)).Flatten().InnerExceptions));
            else Assert.False(await exiting!);
        }
        finally { finish.TrySetResult(); await pending.WaitAsync(TimeSpan.FromSeconds(10)); }
        Assert.Same(expected, Assert.Single(Assert.IsType<AggregateException>(owner.Commands.Read().Error).Flatten().InnerExceptions));
        Assert.Same(original, owner.Document.Read().Content); Assert.False(owner.Commands.Read().Busy); Assert.Equal(force, owner.Commands.Read().Closed);
        owner.Store.Read = null; await owner.Commands.StopAsync(); Assert.Null(owner.Document.Read().Content); Assert.Equal(1, owner.RuntimeStops);
        Assert.Equal(1, owner.Authoring.Products[0].Disposals); Assert.Null(owner.Commands.Read().Error);
    }

    [Fact]
    public async Task ExitCancelAndForcedUpgrade_SharePendingCloseAndNeverWaitForAnotherUserDecision()
    {
        await using var owner = new DocumentUiFixture(); owner.Run(DocumentCommand.New);
        var exiting = owner.Commands.RequestExitAsync(); owner.Decision();
        Assert.True(owner.Commands.Read().Exiting); Assert.False(exiting.IsCompleted); Assert.Equal(0, owner.RuntimeStops);
        var stopping = owner.Commands.StopAsync(); await stopping.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(await exiting); Assert.True(owner.Commands.Read().Closed); Assert.False(owner.Commands.Read().AwaitingDecision);
        Assert.Null(owner.Document.Read().Content); Assert.Equal(1, owner.RuntimeStops); Assert.Empty(owner.Store.Writes);
        Assert.Equal(1, owner.Authoring.Products[0].Disposals); Assert.False(owner.Projects.IsClosed);
    }

    [Fact]
    public async Task Exit_RuntimeDrainFailureIsObservableAndRetryKeepsOwnedSessionAvailableUntilStopSucceeds()
    {
        await using var owner = new DocumentUiFixture(); owner.Run(DocumentCommand.Open, "room.aetherscene");
        var failure = new IOException("runtime drain failed"); owner.StopRuntime = () => Task.FromException(failure);
        Assert.False(await owner.Commands.RequestExitAsync()); Assert.Same(failure, owner.Commands.Read().Error);
        Assert.False(owner.Commands.Read().Closed); Assert.False(owner.Commands.Read().Busy); Assert.Null(owner.Document.Read().Content);
        Assert.False(owner.Projects.Active!.IsClosed); Assert.Equal(1, owner.Authoring.Products[0].Disposals);
        owner.StopRuntime = null; Assert.True(await owner.Commands.RequestExitAsync()); Assert.Equal(2, owner.RuntimeStops);
        Assert.True(owner.Commands.Read().Closed); Assert.Null(owner.Commands.Read().Error); Assert.Equal(1, owner.Authoring.Products[0].Disposals);
    }

    [Fact]
    public async Task ForcedStop_RuntimeFailureRemainsObservedAndRetainsRetryWithoutReopeningAdmission()
    {
        await using var owner = new DocumentUiFixture(); owner.Run(DocumentCommand.New);
        var failure = new IOException("forced stop retry"); owner.StopRuntime = () => Task.FromException(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(owner.Commands.StopAsync));
        Assert.True(owner.Commands.Read().Closed); Assert.Same(failure, owner.Commands.Read().Error);
        Assert.False(owner.Commands.Execute(DocumentCommand.New)); Assert.Null(owner.Document.Read().Content);
        owner.StopRuntime = null; await owner.Commands.StopAsync(); Assert.Equal(2, owner.RuntimeStops);
        Assert.Null(owner.Commands.Read().Error); Assert.Equal(1, owner.Authoring.Products[0].Disposals);
    }

    [Fact]
    public async Task Exit_AfterIgnoredSaveOfOlderSnapshotStillRequiresDecisionForNewerEdits()
    {
        await using var owner = new DocumentUiFixture(); owner.Run(DocumentCommand.Open, "room.aetherscene");
        Assert.True(owner.Document.Apply(DocumentData.Edit(7, 45)).Succeeded);
        var entered = DocumentData.Signal(); var finish = DocumentData.Signal();
        owner.Store.Write = async (_, _, _, _) => { entered.SetResult(); await finish.Task.WaitAsync(TimeSpan.FromSeconds(10)); return new("older-saved"); };
        Assert.True(owner.Commands.Execute(DocumentCommand.Save)); var saving = owner.Commands.Pending;
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); Assert.True(owner.Document.Apply(DocumentData.Edit(8, 67)).Succeeded);
        var exit = owner.Commands.RequestExitAsync(); Assert.False(exit.IsCompleted); Assert.Equal(0, owner.RuntimeStops);
        finish.SetResult(); await saving.WaitAsync(TimeSpan.FromSeconds(10)); owner.Decision();
        Assert.True(owner.Document.Read().IsDirty); Assert.Equal(67d, DocumentData.X(owner.Document.Read().Content!));
        Assert.Equal(8, Assert.Single(owner.Store.Writes).Document.Revision); Assert.Equal(8, owner.Document.Read().SavedContentRevision);
        Assert.Equal(0, owner.RuntimeStops); Assert.True(owner.Commands.Choose(DocumentCloseChoice.Cancel)); Assert.False(await exit);
        Assert.True(owner.Document.Read().IsDirty); Assert.False(owner.Commands.Read().Closed); Assert.Equal(0, owner.Authoring.Products[0].Disposals);
        owner.Store.Write = null; owner.Run(DocumentCommand.Save); Assert.False(owner.Document.Read().IsDirty);
        Assert.Equal(new SceneFileStamp("older-saved"), owner.Store.Writes[1].Expected); Assert.Equal(9, owner.Store.Writes[1].Document.Revision);
    }

    [Fact]
    public async Task DocumentIoAcceptedOutsideCoordinator_RejectsUiAdmissionWithoutReplacingTrackedWork()
    {
        await using var owner = new DocumentUiFixture(); owner.Run(DocumentCommand.Open, "room.aetherscene");
        var entered = DocumentData.Signal(); var finish = DocumentData.Signal();
        owner.Store.Write = async (_, _, _, _) => { entered.SetResult(); await finish.Task.WaitAsync(TimeSpan.FromSeconds(10)); return new("external-save"); };
        var originalPending = owner.Commands.Pending; var saving = owner.Document.SaveAsync(); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            foreach (var command in new[] { DocumentCommand.New, DocumentCommand.Open, DocumentCommand.Save, DocumentCommand.SaveAs, DocumentCommand.Close })
            { Assert.False(owner.Commands.CanExecute(command)); Assert.False(owner.Commands.Execute(command)); }
            Assert.Same(originalPending, owner.Commands.Pending); Assert.False(owner.Commands.Read().Busy); Assert.Single(owner.Store.Writes);
        }
        finally { finish.TrySetResult(); await saving.WaitAsync(TimeSpan.FromSeconds(10)); }
        Assert.True(owner.Commands.CanExecute(DocumentCommand.Open)); Assert.Null(owner.Commands.Read().Error);
    }

    [Theory]
    [InlineData("projects")] [InlineData("transition")] [InlineData("stop")]
    public void Constructor_RejectsMissingBorrowedBoundariesBeforeStartingWork(string dependency)
    {
        using var owner = new DocumentUiFixture();
        var error = Assert.Throws<ArgumentNullException>(() => new EditorDocumentCommands(dependency == "projects" ? null! : owner.Projects,
            dependency == "transition" ? null! : () => true, dependency == "stop" ? null! : () => Task.CompletedTask));
        Assert.Equal(dependency switch { "transition" => "canTransition", "stop" => "stopRuntime", _ => "projects" }, error.ParamName);
        Assert.Empty(owner.Authoring.Products); Assert.False(owner.Commands.Read().Busy); Assert.Equal(0, owner.RuntimeStops);
    }

    [Fact]
    public async Task InvalidArguments_AreRejectedWithoutChangingSnapshotOrStartingWork()
    {
        await using var owner = new DocumentUiFixture(); var pending = owner.Commands.Pending;
        foreach (var command in new[] { DocumentCommand.None, DocumentCommand.Exit, (DocumentCommand)99 })
        { Assert.Throws<ArgumentOutOfRangeException>(() => owner.Commands.CanExecute(command)); Assert.Throws<ArgumentOutOfRangeException>(() => owner.Commands.Execute(command)); }
        Assert.Throws<ArgumentNullException>(() => owner.Commands.Execute(DocumentCommand.New, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => owner.Commands.Choose((DocumentCloseChoice)99));
        Assert.False(owner.Commands.Choose(DocumentCloseChoice.Save)); Assert.Same(pending, owner.Commands.Pending);
        Assert.Null(owner.Document.Read().Content); Assert.Empty(owner.Authoring.Products); Assert.Null(owner.Commands.Read().Error);
    }
}
