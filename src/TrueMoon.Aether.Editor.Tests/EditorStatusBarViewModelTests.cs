using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Editor.Tests.Editing;
using TrueMoon.Aether.Editing;
using TrueMoon.Aether.Sessions;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests;

public sealed class EditorStatusBarViewModelTests
{
    [Theory]
    [InlineData(SessionState.Stopped, EditorStatusKind.Ready, "Готово")]
    [InlineData(SessionState.Starting, EditorStatusKind.Busy, "Запуск…")]
    [InlineData(SessionState.Playing, EditorStatusKind.Running, "Симуляция")]
    [InlineData(SessionState.Paused, EditorStatusKind.Paused, "Симуляция на паузе")]
    [InlineData(SessionState.Stopping, EditorStatusKind.Busy, "Остановка…")]
    [InlineData(SessionState.Faulted, EditorStatusKind.Error, "Ошибка runtime / команды")]
    [InlineData(SessionState.CleanupFailed, EditorStatusKind.Error, "Ошибка runtime / команды")]
    public void RuntimeSnapshot_MapsSemanticStatusAndRetainsExactTickPidAndEntityCount(SessionState state, EditorStatusKind kind, string summary)
    {
        using var source = new HeaderFixture();
        using var model = new EditorStatusBarViewModel(source.Model, source.Documents, source.Transform);
        source.Model.Refresh(new(state, ProcessId: 1732, Tick: 345UL), EditorWorkspaceTests.Status(GameEditorOperation.Idle, false));
        Assert.Equal(kind, model.Kind); Assert.Equal(summary, model.Summary);
        Assert.Contains("Tick 345", model.RuntimeDetails); Assert.Contains("PID 1732", model.RuntimeDetails);
        Assert.Equal("7 объектов", model.EntityText); Assert.False(model.CanCancel);
        Assert.False(model.HasError); Assert.Empty(model.ErrorDetails);
        source.Refresh(SessionState.Stopped);
        Assert.Contains("PID —", model.RuntimeDetails); Assert.Contains("Tick 0", model.RuntimeDetails);
    }

    [Fact]
    public void EmptyProject_ReportsExplicitEmptyStateAndNoUnavailableCommands()
    {
        using var source = new HeaderFixture(false);
        using var editor = new EditorViewModel(new(new("empty", 0, []), false), source.Commands, source.Documents, source.Transform);
        using var model = new EditorStatusBarViewModel(editor, source.Documents, source.Transform);
        Assert.Equal(EditorStatusKind.Ready, model.Kind); Assert.Equal("Проект не открыт", model.Summary);
        Assert.Equal("0 объектов", model.EntityText); Assert.Equal("Edit Preview: выкл.", model.PreviewSummary);
        Assert.False(model.CanCancel); Assert.False(model.CanPreview); Assert.False(model.CanStopPreview);
        model.ShowPreview(); DocumentUiFixture.Wait(model.CancelAsync());
        Assert.False(source.Preview.Read().Active); Assert.Empty(source.RuntimeCalls);
    }

    [Theory]
    [InlineData(GameEditorOperation.Build, "Сборка проекта…")]
    [InlineData(GameEditorOperation.Replace, "Применение сборки…")]
    [InlineData(GameEditorOperation.Stop, "Остановка…")]
    [InlineData(GameEditorOperation.Start, "Выполняется команда…")]
    public void ActiveGameOperation_OverridesRuntimeAndKeepsDiagnostics(GameEditorOperation operation, string expected)
    {
        using var source = new HeaderFixture();
        using var model = new EditorStatusBarViewModel(source.Model, source.Documents, source.Transform);
        source.Model.Refresh(new(SessionState.Playing, ProcessId: 42, Tick: 99), EditorWorkspaceTests.Status(operation, true),
            new IOException("complete diagnostic: first line\nsecond line"));
        Assert.Equal(EditorStatusKind.Busy, model.Kind); Assert.Equal(expected, model.Summary);
        Assert.Equal("Операция: " + expected, model.OperationDetails);
        Assert.Contains("complete diagnostic: first line\nsecond line", model.ErrorDetails);
        Assert.True(model.HasError); Assert.Contains("Tick 99", model.RuntimeDetails);
    }

    [Fact]
    public void GameCancellation_InvokesExistingCancelExactlyOnceAndIdleCancellationIsInert()
    {
        using var source = new HeaderFixture();
        using var model = new EditorStatusBarViewModel(source.Model, source.Documents, source.Transform);
        source.Model.Refresh(new(SessionState.Stopped), EditorWorkspaceTests.Status(GameEditorOperation.Build, false));
        Assert.True(model.CanCancel); DocumentUiFixture.Wait(model.CancelAsync()); DocumentUiFixture.Wait(source.Commands.Pending);
        Assert.Equal(new[] { EditorCommand.Cancel }, source.RuntimeCalls.ToArray());
        source.Refresh(SessionState.Stopped); Assert.False(model.CanCancel);
        DocumentUiFixture.Wait(model.CancelAsync()); Assert.Single(source.RuntimeCalls);
    }

    [Fact]
    public void PendingDocumentSave_TakesStatusAndCancellationPriorityOverGame()
    {
        using var source = new HeaderFixture(openDocument: true);
        using var model = new EditorStatusBarViewModel(source.Model, source.Documents, source.Transform);
        source.Transform.Fields[0].Value = "52"; source.Transform.Apply();
        var started = DocumentData.Signal(); var release = DocumentData.Signal(); var cancelled = false;
        source.Owner.Store.Write = async (_, _, _, token) =>
        {
            started.TrySetResult();
            try { await release.Task.WaitAsync(token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { cancelled = true; throw; }
            return new("saved");
        };
        try
        {
            Assert.True(source.Owner.Commands.Execute(DocumentCommand.Save)); DocumentUiFixture.Wait(started.Task);
            source.Documents.Refresh();
            source.Model.Refresh(new(SessionState.Stopped), EditorWorkspaceTests.Status(GameEditorOperation.Build, false));
            Assert.Equal(EditorStatusKind.Busy, model.Kind); Assert.Equal("Сохранение сцены…", model.Summary);
            Assert.Equal("Операция: Сохранение сцены…", model.OperationDetails);
            Assert.True(model.CanCancel); DocumentUiFixture.Wait(model.CancelAsync()); source.Owner.Complete();
            source.Documents.Refresh(); Assert.True(cancelled); Assert.Empty(source.RuntimeCalls);
            Assert.True(source.Owner.Document.Read().IsDirty); Assert.Equal(52, DocumentData.X(source.Owner.Document.Read().Content!));
        }
        finally { release.TrySetResult(); }
    }

    [Fact]
    public void DirtyDocumentDecision_UsesWaitingStatusAndCancelKeepsDocumentAndDraft()
    {
        using var source = new HeaderFixture(openDocument: true);
        using var model = new EditorStatusBarViewModel(source.Model, source.Documents, source.Transform);
        source.Transform.Fields[0].Value = "71"; source.Transform.Apply();
        var document = source.Owner.Document.Read().Content;
        Assert.True(source.Owner.Commands.Execute(DocumentCommand.New)); source.Owner.Decision(); source.Documents.Refresh();
        Assert.Equal(EditorStatusKind.Waiting, model.Kind); Assert.Equal("Выберите: сохранить или отменить", model.Summary);
        Assert.True(model.CanCancel); DocumentUiFixture.Wait(model.CancelAsync()); source.Owner.Complete(); source.Documents.Refresh();
        Assert.Same(document, source.Owner.Document.Read().Content); Assert.False(source.Documents.AwaitingDecision);
        Assert.Equal(EditorStatusKind.Ready, model.Kind); Assert.Empty(source.RuntimeCalls);
    }

    [Fact]
    public void DocumentError_PreservesFullRuntimeAndDocumentDiagnosticsAndClearsOnRecovery()
    {
        using var source = new HeaderFixture(openDocument: true);
        using var model = new EditorStatusBarViewModel(source.Model, source.Documents, source.Transform);
        source.Owner.Store.Write = (_, _, _, _) => Task.FromException<SceneFileStamp>(new IOException("document failure\nkeep this complete"));
        source.Owner.Run(DocumentCommand.Save); source.Documents.Refresh();
        source.Model.Refresh(new(SessionState.Faulted, ProcessId: 81, Tick: 12), EditorWorkspaceTests.Status(GameEditorOperation.Idle, false), new IOException("runtime failure"));
        Assert.Equal(EditorStatusKind.Error, model.Kind); Assert.Equal("Ошибка документа", model.Summary);
        Assert.Equal("Ошибка: runtime failure\n\nОшибка: document failure\nkeep this complete", model.ErrorDetails);
        Assert.Contains("PID 81", model.RuntimeDetails); Assert.True(model.HasError);
        source.Owner.Store.Write = null; source.Owner.Run(DocumentCommand.Save);
        source.Refresh(SessionState.Stopped);
        Assert.False(model.HasError); Assert.Empty(model.ErrorDetails); Assert.Equal(EditorStatusKind.Ready, model.Kind);
    }

    [Fact]
    public void PreviewActions_UseCommittedRevisionAndStopWithoutDiscardingDraftOrHistory()
    {
        using var source = new HeaderFixture(openDocument: true);
        using var model = new EditorStatusBarViewModel(source.Model, source.Documents, source.Transform);
        var before = source.Owner.Document.Read().Content;
        source.Transform.Fields[0].Value = "88";
        Assert.True(model.CanPreview); model.ShowPreview(); DocumentUiFixture.Wait(source.Preview.Pending);
        source.Transform.Refresh(DocumentData.Child.Value.ToString());
        Assert.Equal("Edit Preview: вкл.", model.PreviewSummary); Assert.True(model.CanStopPreview);
        Assert.False(model.CanCancel);
        Assert.Equal(7, source.Preview.Read().Presented!.Revision); Assert.Contains("кадр r7", model.PreviewDetails);
        Assert.Equal("88", source.Transform.Fields[0].Value); Assert.Same(before, source.Owner.Document.Read().Content);
        DocumentUiFixture.Wait(model.StopPreviewAsync()); source.Transform.Refresh(DocumentData.Child.Value.ToString());
        Assert.Equal("Edit Preview: выкл.", model.PreviewSummary); Assert.False(model.CanStopPreview);
        Assert.Null(source.Preview.Read().Presented); Assert.Same(before, source.Owner.Document.Read().Content);
        Assert.Equal("88", source.Transform.Fields[0].Value); Assert.Equal(0, before!.UndoCount); Assert.Empty(source.RuntimeCalls);
    }

    [Theory]
    [InlineData(SessionState.Paused, false, EditorStatusKind.Paused, "Симуляция на паузе")]
    [InlineData(SessionState.Playing, false, EditorStatusKind.Running, "Симуляция")]
    [InlineData(SessionState.Faulted, false, EditorStatusKind.Error, "Ошибка runtime / команды")]
    [InlineData(SessionState.Paused, true, EditorStatusKind.Error, "Ошибка runtime / команды")]
    public void ActiveEditPreview_ReportsEditingRatherThanGameSimulationAndRetainsErrorPriorityAndRuntimeDetails(
        SessionState runtimeState, bool commandFailure, EditorStatusKind withoutPreviewKind, string withoutPreviewSummary)
    {
        using var source = new HeaderFixture(openDocument: true);
        using var model = new EditorStatusBarViewModel(source.Model, source.Documents, source.Transform);
        var runtime = new SessionStatus(runtimeState, ProcessId: 1234, Tick: 17);
        var editor = EditorWorkspaceTests.Status(GameEditorOperation.Idle, false);
        var failure = commandFailure ? new IOException("preview runtime diagnostic") : null;
        source.Model.Refresh(runtime, editor, failure);
        Assert.Equal(withoutPreviewKind, model.Kind); Assert.Equal(withoutPreviewSummary, model.Summary);
        Assert.StartsWith("Runtime: ", model.RuntimeDetails);

        model.ShowPreview(); DocumentUiFixture.Wait(source.Preview.Pending);
        source.Transform.Refresh(DocumentData.Child.Value.ToString());
        Assert.True(source.Preview.Read().Active); Assert.False(source.Preview.Read().Busy);
        var errorTakesPriority = commandFailure || runtimeState == SessionState.Faulted;
        Assert.Equal(errorTakesPriority ? EditorStatusKind.Error : EditorStatusKind.Ready, model.Kind);
        Assert.Equal(errorTakesPriority ? "Ошибка runtime / команды" : "Редактирование сцены", model.Summary);
        Assert.StartsWith("Runtime Edit Preview: ", model.RuntimeDetails);
        Assert.Contains("Tick 17", model.RuntimeDetails); Assert.Contains("PID 1234", model.RuntimeDetails);
        Assert.Equal(commandFailure ? "Ошибка: preview runtime diagnostic" : "", model.ErrorDetails);
        Assert.Equal(commandFailure, model.HasError);
        Assert.Equal("Edit Preview: вкл.", model.PreviewSummary); Assert.Empty(source.RuntimeCalls);

        var notifications = new List<string?>(); model.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        source.Model.Refresh(runtime, editor, failure);
        Assert.Empty(notifications);
        DocumentUiFixture.Wait(model.StopPreviewAsync()); source.Transform.Refresh(DocumentData.Child.Value.ToString());
        Assert.Equal(withoutPreviewKind, model.Kind); Assert.Equal(withoutPreviewSummary, model.Summary);
        Assert.StartsWith("Runtime: ", model.RuntimeDetails);
        Assert.Contains("Tick 17", model.RuntimeDetails); Assert.Contains("PID 1234", model.RuntimeDetails);
    }

    [Fact]
    public void BusyPreview_CancelDrainsItsOwnerAndDoesNotCancelRuntime()
    {
        using var source = new HeaderFixture(openDocument: true);
        var entered = DocumentData.Signal(); var cancelled = false; var stops = 0;
        using var preview = new TrueMoon.Aether.Editor.Editing.EditorPreview(async (scene, token) =>
        {
            entered.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { cancelled = true; throw; }
            return EditingFixture.Receipt(scene);
        }, () => { stops++; return Task.CompletedTask; });
        using var transform = new TrueMoon.Aether.Editor.Editing.EditorTransformViewModel(source.Owner.Commands, preview);
        transform.Refresh(DocumentData.Child.Value.ToString());
        using var model = new EditorStatusBarViewModel(source.Model, source.Documents, transform);
        model.ShowPreview(); DocumentUiFixture.Wait(entered.Task); transform.Refresh(DocumentData.Child.Value.ToString());
        Assert.Equal(EditorStatusKind.Busy, model.Kind); Assert.Equal("Обновление Edit Preview…", model.Summary);
        Assert.Equal("Операция: Обновление Edit Preview…", model.OperationDetails);
        Assert.True(model.CanCancel); DocumentUiFixture.Wait(model.CancelAsync()); transform.Refresh(DocumentData.Child.Value.ToString());
        Assert.True(cancelled); Assert.Equal(1, stops); Assert.False(model.CanCancel);
        Assert.False(preview.Read().Active); Assert.False(preview.Read().Busy); Assert.Empty(source.RuntimeCalls);
    }

    [Fact]
    public void PreviewFailure_IsActionableAndRetryClearsErrorAfterActualPresentation()
    {
        using var source = new HeaderFixture(openDocument: true); var attempts = 0;
        using var preview = new TrueMoon.Aether.Editor.Editing.EditorPreview((scene, _) => ++attempts == 1 ?
            Task.FromException<TrueMoon.Aether.Preview.ScenePreviewDocument>(new IOException("preview complete failure\nline two")) :
            Task.FromResult(EditingFixture.Receipt(scene)), () => Task.CompletedTask);
        using var transform = new TrueMoon.Aether.Editor.Editing.EditorTransformViewModel(source.Owner.Commands, preview);
        transform.Refresh(DocumentData.Child.Value.ToString());
        using var model = new EditorStatusBarViewModel(source.Model, source.Documents, transform);
        model.ShowPreview(); DocumentUiFixture.Wait(preview.Pending); transform.Refresh(DocumentData.Child.Value.ToString());
        Assert.Equal(EditorStatusKind.Error, model.Kind); Assert.Equal("Ошибка Edit Preview", model.Summary);
        Assert.Equal("preview complete failure\nline two", model.ErrorDetails); Assert.Equal("Edit Preview: ошибка", model.PreviewSummary);
        Assert.True(model.CanPreview); Assert.True(model.CanStopPreview);
        model.ShowPreview(); DocumentUiFixture.Wait(preview.Pending); transform.Refresh(DocumentData.Child.Value.ToString());
        Assert.Equal(2, attempts); Assert.False(model.HasError); Assert.Equal("Edit Preview: вкл.", model.PreviewSummary);
        Assert.NotNull(preview.Read().Presented);
    }

    [Fact]
    public void Disposal_UnsubscribesAndRejectsActionsWhileBorrowedModelsStayUsable()
    {
        using var source = new HeaderFixture();
        var model = new EditorStatusBarViewModel(source.Model, source.Documents, source.Transform);
        var changed = new List<string?>(); model.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        source.Refresh(SessionState.Playing); Assert.Contains(nameof(model.Summary), changed); Assert.Contains(nameof(model.Kind), changed);
        changed.Clear(); var snapshot = model.Summary; model.Dispose(); model.Dispose();
        source.Refresh(SessionState.Paused); source.Documents.PathDraft = "still usable"; source.Transform.Refresh(null);
        Assert.Empty(changed); Assert.Equal(snapshot, model.Summary); Assert.Equal("still usable", source.Documents.PathDraft);
        Assert.Throws<ObjectDisposedException>(() => model.ShowPreview());
        Assert.Throws<ObjectDisposedException>(() => { _ = model.CancelAsync(); });
        Assert.Throws<ObjectDisposedException>(() => { _ = model.StopPreviewAsync(); });
        Assert.False(source.Owner.Commands.Read().Closed); Assert.Equal(SessionState.Paused, source.Model.RuntimeState);
    }
}
