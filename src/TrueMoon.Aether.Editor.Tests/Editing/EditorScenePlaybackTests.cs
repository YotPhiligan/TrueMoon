using System.Collections.Concurrent;
using System.Numerics;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Editor.Tooling;
using TrueMoon.Aether.Editor.Tooling.Documents;
using TrueMoon.Aether.Editor.Tooling.Editing;
using TrueMoon.Aether.Editor.UI;
using TrueMoon.Aether.Editor.UI.Documents;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Runtime.Scenes;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Tooling.Documents;
using TrueMoon.Aether.Tooling.Editing;
using TrueMoon.Aether.Tooling.Preview;
using TrueMoon.Aether.Tooling.Projects;
using TrueMoon.Aether.Tooling.Scenes;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Editing;

public sealed class EditorScenePlaybackTests
{
    [Fact]
    public void RunSimulation_UsesUnsavedSceneCopyAndPreservesHistoryThroughControlsStopAndRestart()
    {
        using var h = new Fixture(); var before = h.Documents.Document.Read();
        Assert.True(h.Playback.RunSimulation()); h.Complete();
        Assert.Null(h.Playback.Error); Assert.Equal(SessionState.Playing, h.State); Assert.True(h.Playback.OwnsRuntime);
        var context = h.Context!;
        Assert.True(context.Build.TrackInputs);
        Assert.True(context.Build.CaptureProjectReferences);
        Assert.True(context.Build.CaptureCompilation);
        Assert.True(context.Build.CapturePreparation);
        Assert.True(context.Build.ReuseArtifact);
        Assert.Equal(h.Documents.Projects.Active!.Project.GameProjectPath, context.Build.ProjectPath);
        Assert.Equal(before.Content!.Document.Id.Value.ToString("D"), context.Scene.Id);
        Assert.Equal(before.Content.Document.Revision, context.Scene.Revision);
        Assert.Equal(11, context.Scene.CopyEntities().Single(e => e.Id == DocumentData.Child.Value.ToString("D")).X);
        Assert.Equal(new[] { "preview-stop", "context", "Build", "Start", "Play" }, h.Events.ToArray());
        Assert.False(h.Documents.Commands.CanExecute(DocumentCommand.Save)); Assert.False(h.Documents.Commands.CanExecute(DocumentCommand.OpenProject));
        Assert.True(h.Playback.CanExecute(EditorCommand.Build)); Assert.True(h.Playback.CanExecute(EditorCommand.Replace));
        Assert.True(h.Playback.Execute(EditorCommand.Pause)); h.Complete(); Assert.Equal(SessionState.Paused, h.State);
        Assert.True(h.Playback.Execute(EditorCommand.Step)); h.Complete(); Assert.Equal(1, h.Steps);
        Assert.True(h.Playback.RunSimulation()); h.Complete(); Assert.Equal(SessionState.Playing, h.State); Assert.Equal(1, h.Builds);
        Assert.True(h.Playback.Execute(EditorCommand.Stop)); h.Complete(); Assert.False(h.Playback.OwnsRuntime);
        Assert.Equal(SessionState.Stopped, h.State); Assert.True(h.Documents.Commands.CanExecute(DocumentCommand.Save));
        Assert.Same(before.Content, h.Documents.Document.Read().Content); Assert.True(h.Documents.Document.Read().IsDirty);
        Assert.Null(h.Documents.Document.Read().Path); Assert.Empty(h.Documents.Store.Writes); Assert.Equal(1, h.Documents.Document.Read().Content!.UndoCount);
        Assert.True(h.Playback.RunSimulation()); h.Complete();
        Assert.Equal(2, h.Builds); Assert.True(h.Context!.Build.SourceRevision > context.Build.SourceRevision);
        Assert.Same(before.Content, h.Documents.Document.Read().Content);
    }

    [Fact]
    public void BuildWhilePlaying_CompilesWithoutChangingTheOwnedRuntimeOrDocument()
    {
        using var h = new Fixture();
        Assert.True(h.Playback.RunSimulation()); h.Complete();
        var identity = h.Identity;
        var document = h.Documents.Document.Read().Content;
        var oldRevision = h.Context!.Build.SourceRevision;

        Assert.True(h.Playback.Execute(EditorCommand.Build)); h.Complete();

        Assert.Null(h.Playback.Error);
        Assert.Equal(new[] { "context", "Build" }, h.Events.Skip(5));
        Assert.True(h.Context!.Build.SourceRevision > oldRevision);
        Assert.Equal(identity, h.Identity);
        Assert.Equal(SessionState.Playing, h.State);
        Assert.Same(document, h.Documents.Document.Read().Content);
        Assert.False(h.Playback.CanExecute(EditorCommand.Start));
        Assert.False(h.Documents.Commands.CanExecute(DocumentCommand.Close));
    }

    [Theory]
    [InlineData("paused")]
    [InlineData("runtime changed")]
    [InlineData("document changed")]
    public void BuildWhilePlaying_RequiresTheOwnedPlayingScene(string change)
    {
        using var h = new Fixture();
        Assert.True(h.Playback.RunSimulation()); h.Complete();
        if (change == "paused") { Assert.True(h.Playback.Execute(EditorCommand.Pause)); h.Complete(); }
        else if (change == "runtime changed") h.Identity = Fixture.NewIdentity();
        else DocumentUiFixture.Wait(h.Documents.Document.NewAsync(DocumentData.Scene(), DocumentCloseChoice.Discard));

        Assert.False(h.Playback.CanExecute(EditorCommand.Build));
        Assert.False(h.Playback.Execute(EditorCommand.Build));
        Assert.Equal(1, h.Builds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuildWhilePlaying_FailureOrCancelKeepsTheGameAndAllowsRetry(bool cancel)
    {
        using var h = new Fixture();
        Assert.True(h.Playback.RunSimulation()); h.Complete();
        var identity = h.Identity;
        var entered = DocumentData.Signal();
        var failure = new IOException("compiler failed");
        h.Build = async token =>
        {
            entered.TrySetResult();
            if (cancel) await Task.Delay(Timeout.Infinite, token);
            else throw failure;
        };

        Assert.True(h.Playback.Execute(EditorCommand.Build));
        DocumentUiFixture.Wait(entered.Task);
        if (cancel)
        {
            Assert.False(h.Playback.CanExecute(EditorCommand.Build));
            Assert.True(h.Playback.Execute(EditorCommand.Cancel));
        }
        h.Complete();

        Assert.Same(cancel ? null : failure, h.Playback.Error);
        Assert.Equal(identity, h.Identity);
        Assert.Equal(SessionState.Playing, h.State);
        Assert.Equal(0, h.Stops);
        Assert.Equal(0, h.Replaces);
        h.Build = null;
        Assert.True(h.Playback.Execute(EditorCommand.Build)); h.Complete();
        Assert.Null(h.Playback.Error);
        Assert.Equal(3, h.Builds);
        Assert.Equal(identity, h.Identity);
    }

    [Theory] [InlineData("build")] [InlineData("preview")]
    public void PreparationFailure_PreservesDocumentAndCanRetryWithoutStartingGame(string stage)
    {
        using var h = new Fixture(); var before = h.Documents.Document.Read(); var failure = new IOException(stage);
        if (stage == "build") h.Build = _ => Task.FromException(failure); else h.StopPreview = () => Task.FromException(failure);
        Assert.True(h.Playback.RunSimulation()); h.Complete();
        Assert.Same(failure, h.Playback.Error); Assert.Equal(0, h.Starts); Assert.False(h.Playback.OwnsRuntime);
        Assert.Same(before.Content, h.Documents.Document.Read().Content); Assert.True(h.Documents.Document.Read().IsDirty);
        Assert.True(h.Playback.CanExecute(EditorCommand.Start)); h.Build = null; h.StopPreview = null;
        Assert.True(h.Playback.RunSimulation()); h.Complete(); Assert.Null(h.Playback.Error); Assert.Equal(1, h.Starts);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void Replace_BuildsCapturedDocumentAndPreservesModeAndHistory(bool playing)
    {
        using var h = new Fixture(); var before = h.Documents.Document.Read();
        Assert.True(playing ? h.Playback.RunSimulation() : h.Playback.Execute(EditorCommand.Start)); h.Complete();
        var origin = h.Identity; var revision = h.Context!.Build.SourceRevision;
        Assert.True(h.Playback.Execute(EditorCommand.Replace)); h.Complete();
        Assert.Null(h.Playback.Error); Assert.Equal(1, h.Starts); Assert.Equal(2, h.Builds); Assert.Equal(1, h.Replaces); Assert.Equal(0, h.Stops);
        Assert.Equal(playing ? SessionState.Playing : SessionState.Paused, h.State); Assert.NotEqual(origin, h.Identity);
        Assert.True(h.Context!.Build.SourceRevision > revision); Assert.False(h.Context.Build.ReuseArtifact);
        Assert.Equal(new[] { "context", "Build", "Replace" }, h.Events.Skip(playing ? 5 : 4));
        Assert.Same(before.Content, h.Documents.Document.Read().Content); Assert.Equal(1, before.Content!.UndoCount);
        Assert.True(h.Documents.Document.Read().IsDirty); Assert.True(h.Playback.OwnsRuntime); Assert.True(h.Playback.CanExecute(EditorCommand.Replace));
    }

    [Theory] [InlineData("document")] [InlineData("runtime")]
    public void Replace_RejectsDifferentDocumentOrRuntimeBeforeBuild(string change)
    {
        using var h = new Fixture(); Assert.True(h.Playback.RunSimulation()); h.Complete();
        if (change == "runtime") h.Identity = Fixture.NewIdentity();
        else { var state = h.Documents.Document.Read().Content!; h.Documents.Document.Apply(DocumentData.Edit(state.Document.Revision, 17)); }
        Assert.False(h.Playback.CanExecute(EditorCommand.Replace)); Assert.False(h.Playback.Execute(EditorCommand.Replace));
        Assert.Equal(1, h.Builds); Assert.Equal(0, h.Replaces); Assert.Equal(0, h.Stops);
    }

    [Theory] [InlineData("document")] [InlineData("runtime")] [InlineData("context")]
    public void Replace_RejectsStaleCompletionBeforeTransaction(string change)
    {
        using var h = new Fixture(); Assert.True(h.Playback.RunSimulation()); h.Complete();
        var entered = DocumentData.Signal(); var release = DocumentData.Signal();
        h.Build = async _ => { entered.TrySetResult(); await release.Task; };
        Assert.True(h.Playback.Execute(EditorCommand.Replace)); DocumentUiFixture.Wait(entered.Task);
        try
        {
            if (change == "runtime") h.Identity = Fixture.NewIdentity();
            else if (change == "context") h.Version++;
            else { var state = h.Documents.Document.Read().Content!; h.Documents.Document.Apply(DocumentData.Edit(state.Document.Revision, 17)); }
        }
        finally { release.TrySetResult(); }
        h.Complete(); Assert.Null(h.Playback.Error); Assert.Equal(0, h.Replaces); Assert.Equal(0, h.Stops);
        Assert.Equal(SessionState.Playing, h.State); Assert.True(h.Playback.OwnsRuntime);
    }

    [Theory] [InlineData("build")] [InlineData("transaction")] [InlineData("rollback")]
    public void Replace_FailureKeepsOldOrRecoveredRuntimeOwned(string stage)
    {
        using var h = new Fixture(); var before = h.Documents.Document.Read(); Assert.True(h.Playback.RunSimulation()); h.Complete();
        Exception failure = stage == "build" ? new IOException("compiler") : new GameReplacementException("Candidate", new IOException("restore"), stage == "rollback", stage == "rollback", null);
        if (stage == "build") h.Build = _ => Task.FromException(failure);
        else h.Replace = _ => { if (stage == "rollback") h.Identity = Fixture.NewIdentity(); return Task.FromException(failure); };
        Assert.True(h.Playback.Execute(EditorCommand.Replace)); h.Complete();
        Assert.Same(failure, h.Playback.Error); Assert.Equal(stage == "build" ? 0 : 1, h.Replaces); Assert.Equal(0, h.Stops);
        Assert.Equal(SessionState.Playing, h.State); Assert.True(h.Playback.OwnsRuntime); Assert.True(h.Playback.CanExecute(EditorCommand.Replace));
        Assert.Same(before.Content, h.Documents.Document.Read().Content); Assert.True(h.Documents.Document.Read().IsDirty);
    }

    [Theory] [InlineData(EditorCommand.Cancel)] [InlineData(EditorCommand.Stop)]
    public void Replace_InterruptFencesStopBeforeCancellingTransaction(EditorCommand interrupt)
    {
        using var h = new Fixture(); Assert.True(h.Playback.RunSimulation()); h.Complete();
        var entered = DocumentData.Signal(); var release = DocumentData.Signal(); var cancelled = DocumentData.Signal();
        var stopWasFenced = false;
        h.Replace = async token =>
        {
            using var registration = token.Register(() => { stopWasFenced = h.Events.Contains("Stop"); cancelled.TrySetResult(); });
            entered.TrySetResult(); await release.Task; token.ThrowIfCancellationRequested();
        };
        Assert.True(h.Playback.Execute(EditorCommand.Replace)); DocumentUiFixture.Wait(entered.Task);
        try
        {
            Assert.True(h.Playback.Execute(interrupt)); DocumentUiFixture.Wait(cancelled.Task);
            Assert.Equal(interrupt == EditorCommand.Stop, stopWasFenced);
            Assert.False(h.Playback.Pending.IsCompleted); Assert.False(h.Playback.Execute(EditorCommand.Replace));
            Assert.False(h.Documents.Commands.Execute(DocumentCommand.New));
        }
        finally { release.TrySetResult(); h.Complete(); }
        Assert.Equal(interrupt == EditorCommand.Stop ? SessionState.Stopped : SessionState.Playing, h.State);
    }

    [Theory] [InlineData(EditorCommand.Cancel)] [InlineData(EditorCommand.Stop)]
    public void Replace_InterruptDuringBuildDrainsBeforeAdmission(EditorCommand interrupt)
    {
        using var h = new Fixture(); Assert.True(h.Playback.RunSimulation()); h.Complete();
        var entered = DocumentData.Signal(); var release = DocumentData.Signal(); h.Build = async _ => { entered.TrySetResult(); await release.Task; };
        Assert.True(h.Playback.Execute(EditorCommand.Replace)); DocumentUiFixture.Wait(entered.Task);
        try { Assert.True(h.Playback.Execute(interrupt)); Assert.False(h.Playback.Pending.IsCompleted); Assert.False(h.Playback.Execute(EditorCommand.Replace)); }
        finally { release.TrySetResult(); }
        h.Complete(); Assert.Equal(0, h.Replaces); Assert.Equal(interrupt == EditorCommand.Stop ? 1 : 0, h.Stops);
        Assert.Equal(interrupt == EditorCommand.Stop ? SessionState.Stopped : SessionState.Playing, h.State);
        Assert.Equal(interrupt != EditorCommand.Stop, h.Playback.OwnsRuntime); Assert.Equal(1, h.Documents.Document.Read().Content!.UndoCount);
    }

    [Theory] [InlineData(EditorCommand.Cancel)] [InlineData(EditorCommand.Stop)]
    public void InterruptDuringUncooperativeBuild_DrainsBeforeAdmissionAndCannotStartLate(EditorCommand interrupt)
    {
        using var h = new Fixture(); var entered = DocumentData.Signal(); var release = DocumentData.Signal();
        h.Build = async _ => { entered.TrySetResult(); await release.Task; };
        Assert.True(h.Playback.RunSimulation()); DocumentUiFixture.Wait(entered.Task);
        try
        {
            Assert.False(h.Playback.RunSimulation()); Assert.True(h.Documents.Commands.Read().Busy);
            Assert.False(h.Documents.Commands.Execute(DocumentCommand.New)); Assert.True(h.Playback.Execute(interrupt));
            Assert.False(h.Playback.Pending.IsCompleted); Assert.False(h.Documents.Commands.CanExecute(DocumentCommand.Save));
        }
        finally { release.TrySetResult(); }
        h.Complete(); Assert.Equal(0, h.Starts); Assert.False(h.Documents.Commands.Read().Busy);
        Assert.Null(h.Playback.Error); Assert.Equal(1, h.Documents.Document.Read().Content!.UndoCount);
    }

    [Fact]
    public void DirectDocumentChangeDuringBuild_RejectsCapturedRevisionBeforeStartup()
    {
        using var h = new Fixture(); var entered = DocumentData.Signal(); var release = DocumentData.Signal();
        h.Build = async _ => { entered.TrySetResult(); await release.Task; };
        Assert.True(h.Playback.RunSimulation()); DocumentUiFixture.Wait(entered.Task);
        try { var state = h.Documents.Document.Read().Content!; h.Documents.Document.Apply(DocumentData.Edit(state.Document.Revision, 17)); }
        finally { release.TrySetResult(); }
        h.Complete(); Assert.Equal(0, h.Starts); Assert.Equal(17, DocumentData.X(h.Documents.Document.Read().Content!));
        Assert.Equal(2, h.Documents.Document.Read().Content!.UndoCount); Assert.Null(h.Playback.Error);
    }

    [Fact]
    public void PreviewStop_IsDrainedBeforeContextAndBuild()
    {
        using var h = new Fixture(); var entered = DocumentData.Signal(); var release = DocumentData.Signal();
        h.StopPreview = async () => { entered.TrySetResult(); await release.Task; };
        Assert.True(h.Playback.RunSimulation()); DocumentUiFixture.Wait(entered.Task);
        try { Assert.Null(h.Context); Assert.Equal(0, h.Builds); Assert.Equal(0, h.Starts); h.Documents.Commands.Cancel(); Assert.False(h.Playback.Pending.IsCompleted); }
        finally { release.TrySetResult(); }
        h.Complete(); Assert.Null(h.Context); Assert.Equal(0, h.Builds); Assert.Equal(0, h.Starts);
        Assert.True(h.Documents.Commands.CanExecute(DocumentCommand.Save));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void StartupFailure_DrainsOwnedGameAndRetainsCleanupFailureForRetry(bool cleanupFails)
    {
        using var h = new Fixture(); var startFailure = new IOException("startup"); var stopFailure = new IOException("cleanup");
        h.Start = _ => Task.FromException(startFailure); h.StopFailure = cleanupFails ? stopFailure : null;
        Assert.True(h.Playback.RunSimulation()); h.Complete(); Assert.Equal(1, h.Starts); Assert.Equal(1, h.Stops);
        if (cleanupFails)
        {
            var aggregate = Assert.IsType<AggregateException>(h.Playback.Error);
            Assert.Equal(new Exception[] { startFailure, stopFailure }, aggregate.InnerExceptions);
            Assert.True(h.Playback.OwnsRuntime); Assert.False(h.Playback.CanExecute(EditorCommand.Start));
            h.StopFailure = null; Assert.True(h.Playback.Execute(EditorCommand.Stop)); h.Complete();
            Assert.False(h.Playback.OwnsRuntime); Assert.Equal(2, h.Stops);
        }
        else { Assert.Same(startFailure, h.Playback.Error); Assert.False(h.Playback.OwnsRuntime); }
        Assert.Equal(1, h.Documents.Document.Read().Content!.UndoCount); Assert.True(h.Documents.Document.Read().IsDirty);
        h.Start = null; Assert.True(h.Playback.RunSimulation()); h.Complete(); Assert.Null(h.Playback.Error);
    }

    [Fact]
    public void RejectedExpectedContext_DoesNotAcquireOrStopAnUnrelatedRuntime()
    {
        using var h = new Fixture(); h.BeforeStart = () => h.Version++;
        Assert.True(h.Playback.RunSimulation()); h.Complete();
        Assert.Equal(0, h.Starts); Assert.Equal(0, h.Stops); Assert.False(h.Playback.OwnsRuntime);
        Assert.Null(h.Playback.Error); Assert.Equal(1, h.Documents.Document.Read().Content!.UndoCount);
    }

    [Fact]
    public void ForcedExit_CancelsAndDrainsSceneBuildBeforeDocumentAndRuntimeCleanup()
    {
        using var h = new Fixture(); var entered = DocumentData.Signal(); var release = DocumentData.Signal();
        h.Build = async _ => { entered.TrySetResult(); await release.Task; };
        Assert.True(h.Playback.RunSimulation()); DocumentUiFixture.Wait(entered.Task); var close = h.Documents.Commands.StopAsync();
        try { Assert.False(close.IsCompleted); Assert.NotNull(h.Documents.Document.Read().Content); }
        finally { release.TrySetResult(); }
        DocumentUiFixture.Wait(close); h.Complete(); Assert.True(h.Documents.Commands.Read().Closed);
        Assert.Null(h.Documents.Document.Read().Content); Assert.Equal(0, h.Starts); Assert.Equal(1, h.Documents.RuntimeStops);
    }

    [Fact]
    public void UserExitCancel_KeepsPlayingGameAndUnsavedAuthoringHistory()
    {
        using var h = new Fixture(); var before = h.Documents.Document.Read();
        Assert.True(h.Playback.RunSimulation()); h.Complete();
        var close = h.Documents.Commands.RequestExitAsync(); h.Documents.Decision();
        Assert.True(h.Documents.Commands.Choose(DocumentCloseChoice.Cancel));
        DocumentUiFixture.Wait(h.Documents.CompleteTransition(close, expectedExit: false)); Assert.Equal(SessionState.Playing, h.State);
        Assert.Same(before.Content, h.Documents.Document.Read().Content); Assert.Equal(0, h.Stops);
        Assert.True(h.Playback.Execute(EditorCommand.Stop)); h.Complete();
    }

    [Fact]
    public void ProjectionLimit_FailsBeforePreviewOrCompilerAndKeepsFullAuthoringScene()
    {
        using var h = new Fixture(); var source = DocumentData.Scene();
        var scene = new SceneDocument(source.Id, 0, Enumerable.Range(0, 65).Select(_ =>
            new EntityInstance(new(Guid.NewGuid()), DocumentData.Base, new())).ToArray(), source.Definitions, source.Schemas);
        DocumentUiFixture.Wait(h.Documents.Document.NewAsync(scene, DocumentCloseChoice.Discard));
        Assert.True(h.Playback.RunSimulation()); h.Complete(); Assert.IsType<SceneResolutionException>(h.Playback.Error);
        Assert.Empty(h.Events); Assert.Equal(0, h.Builds); Assert.Equal(0, h.Starts);
        Assert.Equal(65, h.Documents.Document.Read().Content!.Document.Entities.Length);
    }

    [Fact]
    public void ViewModel_RoutesRunStopThroughSceneQueueAndRejectsUnappliedInspectorDraft()
    {
        using var h = new Fixture(); var workspace = new EditorWorkspace(new("empty", 0, []), false);
        workspace.SetProject(h.Documents.Projects.Active);
        using var documents = new EditorDocumentViewModel(h.Documents.Commands);
        using var inspector = new EditorInspectorViewModel(h.Documents.Commands, h.Preview);
        var legacy = 0; var commands = new EditorCommands((_, _) => { legacy++; return Task.CompletedTask; }, default);
        using var model = new EditorViewModel(workspace, commands, documents, inspector, null, h.Playback);
        model.Refresh(h.Runtime(), h.Editor()); model.Select(DocumentData.Child.Value.ToString("D"));
        model.Refresh(h.Runtime(), h.Editor()); var selection = model.SelectedId;
        Assert.True(model.CanRun); Assert.True(model.CanBuild);
        inspector.Fields[0].Value = "24"; model.Refresh(h.Runtime(), h.Editor());
        Assert.False(model.CanRun); Assert.False(model.CanBuild); model.RunSimulation(); Assert.Equal(0, h.Starts);
        inspector.Cancel(); model.Refresh(h.Runtime(), h.Editor()); model.RunSimulation(); h.Complete();
        model.Refresh(h.Runtime(), h.Editor()); Assert.True(model.CanStop); Assert.True(model.CanPause); Assert.True(model.CanBuild);
        Assert.True(model.CanReplace); model.Execute(EditorCommand.Replace); h.Complete(); Assert.Equal(1, h.Replaces);
        model.Execute(EditorCommand.Stop); h.Complete(); model.Refresh(h.Runtime(), h.Editor());
        Assert.Equal(selection, model.SelectedId); Assert.True(model.CanRun); Assert.Equal(0, legacy);
        Assert.Equal(1, h.Documents.Document.Read().Content!.UndoCount);
    }

    [Fact]
    public void ContextSupersededBeforeBuild_DoesNotCompileOrLaunchAnotherScene()
    {
        using var h = new Fixture(); h.AfterContext = () => { h.Context = h.Context! with { Scene = new("other", 0, []) }; h.Version++; };
        Assert.True(h.Playback.RunSimulation()); h.Complete();
        Assert.Equal(0, h.Builds); Assert.Equal(0, h.Starts); Assert.False(h.Playback.OwnsRuntime);
        Assert.Null(h.Playback.Error); Assert.Equal(1, h.Documents.Document.Read().Content!.UndoCount);
    }

    [Fact]
    public void FailedStopDuringBuild_RetainsRetryAdmissionEvenBeforeRuntimeWasAcquired()
    {
        using var h = new Fixture(); var entered = DocumentData.Signal(); var release = DocumentData.Signal();
        var failure = new IOException("stop"); h.Build = async _ => { entered.TrySetResult(); await release.Task; };
        Assert.True(h.Playback.RunSimulation()); DocumentUiFixture.Wait(entered.Task);
        try { h.StopFailure = failure; Assert.True(h.Playback.Execute(EditorCommand.Stop)); }
        finally { release.TrySetResult(); }
        h.Complete(); Assert.Same(failure, h.Playback.Error); Assert.Equal(0, h.Starts);
        Assert.True(h.Playback.CanExecute(EditorCommand.Stop)); Assert.False(h.Playback.CanExecute(EditorCommand.Start));
        h.StopFailure = null; Assert.True(h.Playback.Execute(EditorCommand.Stop)); h.Complete();
        Assert.False(h.Playback.OwnsRuntime); Assert.True(h.Playback.CanExecute(EditorCommand.Start));
    }

    [Fact]
    public void DirectSaveDuringBuild_VetoesStartupUntilActualDocumentIoDrains()
    {
        using var h = new Fixture(); var entered = DocumentData.Signal(); var release = DocumentData.Signal();
        var saved = new TaskCompletionSource<SceneFileStamp>(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Build = async _ => { entered.TrySetResult(); await release.Task; };
        h.Documents.Store.Write = (_, _, _, _) => saved.Task;
        Assert.True(h.Playback.RunSimulation()); DocumentUiFixture.Wait(entered.Task);
        var saving = h.Documents.Document.SaveAsAsync("held.aetherscene");
        try
        {
            release.TrySetResult(); h.Complete(); Assert.Equal(0, h.Starts); Assert.False(h.Playback.CanExecute(EditorCommand.Start));
            Assert.False(saving.IsCompleted); Assert.Equal(1, h.Documents.Document.Read().Content!.UndoCount);
        }
        finally { saved.TrySetResult(new("saved")); release.TrySetResult(); DocumentUiFixture.Wait(saving); }
        Assert.True(h.Playback.CanExecute(EditorCommand.Start)); Assert.False(h.Documents.Document.Read().IsDirty);
    }

    [Fact]
    public void CancelBeforeViewportReady_DoesNotAcquireRuntimeOrStartAfterReadinessArrives()
    {
        var ready = DocumentData.Signal(); using var h = new Fixture(ready.Task); var built = DocumentData.Signal();
        h.Build = _ => { built.TrySetResult(); return Task.CompletedTask; };
        Assert.True(h.Playback.RunSimulation()); DocumentUiFixture.Wait(built.Task);
        Assert.Equal(0, h.Starts); Assert.False(h.Playback.OwnsRuntime);
        Assert.True(h.Playback.Execute(EditorCommand.Cancel)); h.Complete(); ready.TrySetResult();
        Assert.Equal(0, h.Starts); Assert.False(h.Playback.OwnsRuntime); Assert.Null(h.Playback.Error);
        Assert.True(h.Playback.CanExecute(EditorCommand.Start)); Assert.Equal(1, h.Documents.Document.Read().Content!.UndoCount);
    }

    [Fact]
    public async Task FreshPlaybackUsesAuthoredMaterialButSelectedBuildStaysPinned()
    {
        if (!OperatingSystem.IsWindows()) return; // Handle-verified model source capture is a Windows profile.
        MaterialModelFixture model = null!;
        using var h = new Fixture(materialCatalog: true,
            modelImports: project => (model = MaterialModelFixture.CreateAsync(project).GetAwaiter().GetResult()).CreateImports());
        var project = h.Documents.Projects.Active!.Project;
        var documents = h.Documents.Document;
        var authored = new ProjectMaterial(Guid.NewGuid(), "Plaster", new(0, 0, 1), null);
        var snapshot = await documents.WriteMaterialAsync(await documents.ReadMaterialCatalogAsync(default), authored, default);
        var mesh = model.PlainMesh;
        var assets = SceneRenderAssets.WithMaterials(project.Description.Id, model.Assets, snapshot)!;
        var binding = Assert.IsType<Guid>(assets.BindingForMaterial(mesh.AssetId, mesh.SlotId, authored.Id));
        var scene = SceneMaterialAuthoringTests.Scene((mesh.AssetId, binding.ToString("D")));
        Assert.True(await documents.NewAsync(scene, DocumentCloseChoice.Discard));
        var approved = documents.Read();
        documents.ModelImports!.Open(scene.Id.Value, approved.OpeningId, new([model.Selection], model.Content, new("test-selection")));
        Assert.Null(documents.MaterialSnapshot);

        Assert.True(h.Playback.RunSimulation()); h.Complete();
        Assert.Null(h.Playback.Error);
        Assert.Equal(SessionState.Playing, h.State);
        var first = h.Context!;
        Assert.Same(approved.Content!.Document, first.World!.Scene);
        var a = Assert.IsType<GameBuildRenderContent>(first.Build.RenderContent);
        var firstMaterial = a.AssetKeys.Single(key => key.AssetId == authored.Id);
        Assert.Equal(new OpaqueUnlitMaterialDefinition(authored.BaseColorSrgb).CreateKey(project.Description.Id, authored.Id), firstMaterial);
        var firstBinding = a.AssetKeys.Single(key => key.AssetId == binding);
        var artifact = MaterialModelFixture.PublishBuild(project, first.Build.SourceRevision, a);
        Assert.True(h.Playback.Execute(EditorCommand.Stop)); h.Complete();

        authored = authored with { BaseColorSrgb = Vector3.One };
        await documents.WriteMaterialAsync(snapshot, authored, default);
        Assert.True(h.Playback.Execute(EditorCommand.Build)); h.Complete();
        Assert.Null(h.Playback.Error);
        Assert.Same(approved.Content.Document, h.Context!.World!.Scene);
        var b = Assert.IsType<GameBuildRenderContent>(h.Context.Build.RenderContent);
        Assert.Equal(new OpaqueUnlitMaterialDefinition(Vector3.One).CreateKey(project.Description.Id, authored.Id),
            b.AssetKeys.Single(key => key.AssetId == authored.Id));
        Assert.NotEqual(firstBinding, b.AssetKeys.Single(key => key.AssetId == binding));
        Assert.Same(approved.Content, documents.Read().Content);
        var builds = h.Builds;

        await File.WriteAllTextAsync(Path.Combine(project.ContentDirectory, ".aether-material-catalog.json"), "{}");
        var pinned = GameBuildArtifact.Open(artifact.DirectoryPath, artifact.HostContractId, artifact.BuildId);
        Assert.True(h.Playback.RunSelectedBuild(pinned)); h.Complete();
        Assert.Null(h.Playback.Error);
        Assert.Equal(SessionState.Playing, h.State);
        Assert.Equal(builds, h.Builds);
        Assert.Same(pinned, h.SelectedBuild);
        Assert.Equal(pinned.SourceRevision, h.Context!.Build.SourceRevision);
        Assert.Null(h.Context.Build.RenderContent);
        Assert.Same(approved.Content.Document, h.Context.World!.Scene);
        Assert.Contains(pinned.RenderContent!.Entries, entry => entry.Key == firstMaterial);
        Assert.Contains(pinned.RenderContent.Entries, entry => entry.Key == firstBinding);
    }

    private sealed class Fixture : IDisposable
    {
        public DocumentUiFixture Documents { get; }
        public EditorPreview Preview { get; }
        public EditorScenePlayback Playback { get; }
        public ConcurrentQueue<string> Events { get; } = new();
        public GameEditorContext? Context; public long Version;
        public SessionState State = SessionState.Stopped;
        public int Builds, Starts, Stops, Steps, Replaces;
        public SessionIdentity? Identity;
        public Func<CancellationToken, Task>? Build, Start, Replace;
        public Func<Task>? StopPreview; public Exception? StopFailure; public Action? BeforeStart, AfterContext;
        public GameBuildArtifact? SelectedBuild { get; private set; }
        public Fixture(Task? ready = null, bool materialCatalog = false, Func<ResolvedProject, SceneModelImports>? modelImports = null)
        {
            Documents = new(materialCatalog: materialCatalog, modelImports: modelImports);
            DocumentUiFixture.Wait(Documents.Document.NewAsync(DocumentData.Scene()));
            var state = Documents.Document.Read().Content!;
            Documents.Commands.Apply(state.Document, DocumentData.Edit(state.Document.Revision, 9));
            Preview = new((_, _) => throw new NotSupportedException(), () => { Events.Enqueue("preview-stop"); return StopPreview?.Invoke() ?? Task.CompletedTask; });
            Playback = new(Documents.Commands, Preview, Runtime, Editor,
                context => { Events.Enqueue("context"); Context = context; Version++; AfterContext?.Invoke(); }, Dispatch, StartVersionAsync,
                ready: ready, replace: ReplaceVersionAsync, selectBuild: (build, version, token) =>
                {
                    if (version != Version) throw new OperationCanceledException(token);
                    SelectedBuild = build;
                    return Task.CompletedTask;
                });
            Documents.StopRuntime = () => Dispatch(EditorCommand.Stop, default);
        }
        private Task StartVersionAsync(long version, CancellationToken token)
        {
            BeforeStart?.Invoke(); if (version != Version) throw new OperationCanceledException(token);
            Events.Enqueue("Start"); Starts++; Identity = NewIdentity(); SetState(SessionState.Paused); return Start?.Invoke(token) ?? Task.CompletedTask;
        }
        private async Task ReplaceVersionAsync(long version, CancellationToken token)
        {
            if (version != Version) throw new OperationCanceledException(token);
            Events.Enqueue("Replace"); Replaces++;
            if (Replace != null) await Replace(token); else Identity = NewIdentity();
        }
        public static SessionIdentity NewIdentity() => new(Guid.NewGuid(), 1, new('a', 64), new('b', 64), new('c', 64));
        public SessionStatus Runtime() => new(State) { Identity = Identity };
        public GameEditorStatus Editor() => new(Version, Context, GameEditorOperation.Idle, null, null, false);
        private async Task Dispatch(EditorCommand command, CancellationToken token)
        {
            Events.Enqueue(command.ToString());
            switch (command)
            {
                case EditorCommand.Build: Builds++; if (Build != null) await Build(token); break;
                case EditorCommand.Play: SetState(SessionState.Playing); break;
                case EditorCommand.Pause: SetState(SessionState.Paused); break;
                case EditorCommand.Step: Steps++; break;
                case EditorCommand.Stop: Stops++; if (StopFailure != null) throw StopFailure; Version++; Identity = null; SetState(SessionState.Stopped); break;
            }
        }
        private void SetState(SessionState state) { State = state; Documents.RuntimeIdle = state == SessionState.Stopped; }
        public void Complete() => DocumentUiFixture.Wait(Playback.Pending);
        public void Dispose()
        {
            StopFailure = null;
            if (Playback.CanExecute(EditorCommand.Stop)) { Playback.Execute(EditorCommand.Stop); Complete(); }
            Preview.Dispose(); Documents.Dispose();
        }
    }
}
