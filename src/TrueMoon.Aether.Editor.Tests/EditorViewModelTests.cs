using System.Collections.Concurrent;
using System.Reflection;
using TrueMoon.Aether.Editing;
using TrueMoon.Aether.Sessions;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests;

public sealed class EditorViewModelTests
{
    [Fact]
    public void Selection_PublishesChangedNamesAndRowsWithoutRepeatedNotifications()
    {
        var workspace = new EditorWorkspace(EditorScene.Demo());
        using var model = new EditorViewModel(workspace, Commands());
        var notifications = new List<string?>(); model.PropertyChanged += (_, change) => notifications.Add(change.PropertyName);
        Assert.Same(workspace.Scene, model.Scene); Assert.Equal(7, model.EntityCount); Assert.Equal("red-cube", model.SelectedId);
        model.Select("red-cube"); Assert.Empty(notifications);
        model.Select("green-cube");
        Assert.Equal(new[] { nameof(EditorViewModel.SelectedId), nameof(EditorViewModel.SelectedName), nameof(EditorViewModel.Properties) }, notifications);
        Assert.Equal("green-cube", model.SelectedId); Assert.Equal(EditorScene.Name("green-cube"), model.SelectedName);
        var rows = model.Properties; Assert.Equal("1.5", rows.Single(row => row.Key == "X").Value);
        notifications.Clear(); model.Select("green-cube"); Assert.Empty(notifications); Assert.Same(rows, model.Properties);
        Assert.Throws<ArgumentException>(() => model.Select("unknown")); Assert.Equal("green-cube", model.SelectedId); Assert.Same(rows, model.Properties); Assert.Empty(notifications);
    }

    [Fact]
    public void Refresh_PublishesOnlyChangedStatusErrorAndAvailabilityValues()
    {
        using var model = new EditorViewModel(new(EditorScene.Demo()), Commands());
        var notifications = new List<string?>(); model.PropertyChanged += (_, change) => notifications.Add(change.PropertyName);
        var runtime = new SessionStatus(SessionState.Paused, ProcessId: 1234, Tick: 17);
        var editor = EditorWorkspaceTests.Status(GameEditorOperation.Idle, true);
        var failure = new IOException("command failed");
        model.Refresh(runtime, editor, failure);
        Assert.Equal(new[] { nameof(EditorViewModel.RuntimeState), nameof(EditorViewModel.Tick), nameof(EditorViewModel.ProcessId),
            nameof(EditorViewModel.CanPlay), nameof(EditorViewModel.CanRun), nameof(EditorViewModel.CanStep), nameof(EditorViewModel.CanStop),
            nameof(EditorViewModel.CanReplace), nameof(EditorViewModel.StatusText), nameof(EditorViewModel.ErrorText) }, notifications);
        Assert.Equal(SessionState.Paused, model.RuntimeState); Assert.Equal(17UL, model.Tick); Assert.Equal(1234, model.ProcessId);
        Assert.Equal(GameEditorOperation.Idle, model.Operation);
        Assert.True(model.CanBuild); Assert.False(model.CanStart); Assert.True(model.CanPlay); Assert.True(model.CanRun); Assert.False(model.CanPause);
        Assert.True(model.CanStep); Assert.True(model.CanStop); Assert.True(model.CanReplace); Assert.False(model.CanCancel);
        Assert.Equal("  Пауза   ·   Готово   ·   Tick 17   ·   Runtime PID 1234   ·   7 объектов", model.StatusText);
        Assert.Equal("Ошибка: command failed", model.ErrorText);
        notifications.Clear(); model.Refresh(runtime, editor, failure); Assert.Empty(notifications);
        model.Refresh(runtime, EditorWorkspaceTests.Status(GameEditorOperation.Build, true), new OperationCanceledException());
        Assert.Equal(new[] { nameof(EditorViewModel.Operation), nameof(EditorViewModel.CanBuild), nameof(EditorViewModel.CanPlay),
            nameof(EditorViewModel.CanRun), nameof(EditorViewModel.CanStep), nameof(EditorViewModel.CanReplace), nameof(EditorViewModel.CanCancel),
            nameof(EditorViewModel.StatusText), nameof(EditorViewModel.ErrorText) }, notifications);
        Assert.Equal(GameEditorOperation.Build, model.Operation);
        Assert.False(model.CanBuild); Assert.False(model.CanPlay); Assert.False(model.CanStep); Assert.False(model.CanReplace);
        Assert.True(model.CanStop); Assert.True(model.CanCancel); Assert.Equal("", model.ErrorText);
        Assert.Equal("  Пауза   ·   Сборка   ·   Tick 17   ·   Runtime PID 1234   ·   7 объектов", model.StatusText);
        Assert.DoesNotContain(nameof(EditorViewModel.SelectedId), notifications); Assert.DoesNotContain(nameof(EditorViewModel.Properties), notifications);
        notifications.Clear();
        model.Refresh(runtime, EditorWorkspaceTests.Status(GameEditorOperation.Build, true), new OperationCanceledException());
        Assert.Empty(notifications);
    }

    [Fact]
    public async Task Execute_RejectsUnavailableCommandsAndForwardsAllowedActions()
    {
        var calls = new ConcurrentQueue<EditorCommand>();
        var commands = new EditorCommands((command, _) => { calls.Enqueue(command); return Task.CompletedTask; }, CancellationToken.None);
        using var model = new EditorViewModel(new(EditorScene.Demo()), commands);
        model.Execute(EditorCommand.Start); Assert.Empty(calls);
        model.Execute(EditorCommand.Build); await commands.Pending; Assert.Equal(new[] { EditorCommand.Build }, calls.ToArray());
        model.Refresh(new(SessionState.Paused), EditorWorkspaceTests.Status(GameEditorOperation.Idle, false));
        model.Execute(EditorCommand.Play); await commands.Pending;
        model.Refresh(new(SessionState.Paused), EditorWorkspaceTests.Status(GameEditorOperation.Build, false));
        model.Execute(EditorCommand.Play); model.Execute(EditorCommand.Cancel); await commands.Pending;
        Assert.Equal(new[] { EditorCommand.Build, EditorCommand.Play, EditorCommand.Cancel }, calls.ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => model.Execute((EditorCommand)99));
        Assert.Equal(3, calls.Count);
    }

    [Fact]
    public async Task StopAsync_ForwardsShutdownAndWaitsForAcceptedCommand()
    {
        var entered = Signal(); var release = Signal(); var stopped = Signal(); var calls = new ConcurrentQueue<EditorCommand>();
        var commands = new EditorCommands(async (command, token) =>
        {
            calls.Enqueue(command);
            if (command == EditorCommand.Build) { entered.SetResult(); await release.Task; }
            else { Assert.Equal(EditorCommand.Stop, command); Assert.False(token.CanBeCanceled); stopped.SetResult(); }
        }, CancellationToken.None);
        using var model = new EditorViewModel(new(EditorScene.Demo()), commands);
        model.Execute(EditorCommand.Build); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var stopping = model.StopAsync(); await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(stopping.IsCompleted); release.SetResult(); await stopping.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { EditorCommand.Build, EditorCommand.Stop }, calls.ToArray());
    }

    [Fact]
    public async Task Dispose_DisconnectsModelAndPreservesBorrowedWorkspaceAndCommands()
    {
        var workspace = new EditorWorkspace(EditorScene.Demo()); var calls = new ConcurrentQueue<EditorCommand>();
        var commands = new EditorCommands((command, _) => { calls.Enqueue(command); return Task.CompletedTask; }, CancellationToken.None);
        var model = new EditorViewModel(workspace, commands); var notifications = 0; model.PropertyChanged += (_, _) => notifications++;
        model.Dispose(); model.Dispose(); workspace.Select("green-cube");
        Assert.Equal("green-cube", workspace.SelectedId); Assert.Equal("red-cube", model.SelectedId); Assert.Equal(0, notifications); Assert.Empty(calls);
        commands.Execute(EditorCommand.Build); await commands.Pending; Assert.Equal(new[] { EditorCommand.Build }, calls.ToArray());
        Assert.Throws<ObjectDisposedException>(() => model.Select("red-cube"));
        Assert.Throws<ObjectDisposedException>(() => model.Execute(EditorCommand.Play));
        Assert.Throws<ObjectDisposedException>(() => model.Refresh(new(SessionState.Stopped), EditorWorkspaceTests.Status(GameEditorOperation.Idle, false)));
        Assert.Throws<ObjectDisposedException>(() => { _ = model.StopAsync(); });
    }

    [Fact]
    public void ModelSurface_HasNoControlThemeOrContainerDependencies()
    {
        var model = typeof(EditorViewModel);
        var types = model.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Select(field => field.FieldType)
            .Concat(model.GetConstructors().SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType)))
            .Concat(model.GetProperties().Select(property => property.PropertyType));
        Assert.All(types, type =>
        {
            Assert.False(typeof(Element).IsAssignableFrom(type), type.FullName);
            Assert.NotEqual(typeof(Theme), type);
            Assert.NotEqual("TrueMoon.Services.IServiceResolver", type.FullName);
        });
    }

    private static EditorCommands Commands() => new((_, _) => Task.CompletedTask, CancellationToken.None);
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
