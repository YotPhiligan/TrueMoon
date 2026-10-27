using TrueMoon.Aether.Scenes;
using System.Reflection;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.DocumentsUi;

public sealed class DocumentViewModelTests
{
    [Fact]
    public void Refresh_PublishesChangesOnOwnerOnlyAndWorkerCompletionCannotNotifyBindings()
    {
        using var owner = new DocumentUiFixture(); using var model = new EditorDocumentViewModel(owner.Commands);
        var notifications = new List<(string? Property, int Thread)>(); var uiThread = Environment.CurrentManagedThreadId;
        model.PropertyChanged += (_, change) => notifications.Add((change.PropertyName, Environment.CurrentManagedThreadId));
        model.Refresh();
        Assert.True(model.CanNew); Assert.True(model.CanOpen); Assert.True(model.CanEditPath);
        Assert.False(model.CanSave); Assert.False(model.CanSaveAs); Assert.False(model.CanClose); Assert.False(model.BlocksRuntime);
        Assert.Equal(owner.Files.Project.ScenesDirectory, model.ScenesRoot); Assert.Empty(model.ErrorText);
        notifications.Clear(); model.Refresh(); Assert.Empty(notifications);
        owner.Run(DocumentCommand.New); Assert.Empty(notifications); Assert.Null(model.Document!.Content);
        model.Refresh();
        Assert.Same(owner.Document.Read().Content, model.Document!.Content); Assert.True(model.Document.IsDirty);
        Assert.True(model.CanSave); Assert.True(model.CanSaveAs); Assert.True(model.CanClose); Assert.True(model.BlocksRuntime);
        Assert.Contains(notifications, e => e.Property == nameof(model.Document)); Assert.Contains(notifications, e => e.Property == nameof(model.Title));
        Assert.All(notifications, change => Assert.Equal(uiThread, change.Thread)); Assert.Contains("*", model.Title);
        notifications.Clear(); model.Refresh(); Assert.Empty(notifications);
        Exception? rejected = null; var worker = new Thread(() => rejected = Record.Exception(model.Refresh)); worker.Start(); Assert.True(worker.Join(TimeSpan.FromSeconds(5)));
        Assert.IsType<InvalidOperationException>(rejected); Assert.Empty(notifications);
        Assert.True(model.CanSave); Assert.Same(owner.Document.Read().Content, model.Document.Content);
    }

    [Fact]
    public void PathDrafts_SurviveErrorsBusyDecisionAndCompletionWithoutCrossingTargets()
    {
        using var owner = new DocumentUiFixture(); using var model = new EditorDocumentViewModel(owner.Commands); model.Refresh();
        model.PathDraft = "requested-open.aetherscene"; model.SavePathDraft = "keep-original.aetherscene";
        model.Execute(DocumentCommand.New); owner.Complete(); model.Refresh(); var original = model.Document!.Content!.Document;
        model.Execute(DocumentCommand.Open); owner.Decision(); model.Refresh();
        Assert.True(model.AwaitingDecision); Assert.True(model.NeedsSavePath); Assert.True(model.CanCancel);
        Assert.False(model.CanEditPath); Assert.False(model.CanOpen); Assert.False(model.CanSave);
        Assert.Contains("открытием", model.DecisionText);
        model.Choose(DocumentCloseChoice.Save); owner.Complete(); model.Refresh();
        Assert.Equal("requested-open.aetherscene", model.PathDraft); Assert.Equal("keep-original.aetherscene", model.SavePathDraft);
        Assert.Same(original, Assert.Single(owner.Store.Writes).Document);
        Assert.EndsWith("keep-original.aetherscene", owner.Store.Writes[0].Path);
        Assert.EndsWith("requested-open.aetherscene", Assert.Single(owner.Store.Reads));
        Assert.False(model.Document!.IsDirty); Assert.False(model.AwaitingDecision); Assert.False(model.NeedsSavePath);
        Assert.EndsWith("requested-open.aetherscene", model.Document.Path); Assert.True(model.CanEditPath);
        var error = new IOException("keep the draft");
        owner.Store.Write = (_, _, _, _) => Task.FromException<SceneFileStamp>(error);
        model.Execute(DocumentCommand.Save); owner.Complete(); model.Refresh();
        Assert.Contains(error.Message, model.ErrorText); Assert.Equal("requested-open.aetherscene", model.PathDraft);
        Assert.Equal("keep-original.aetherscene", model.SavePathDraft); Assert.True(model.CanSave); Assert.False(model.CanCancel);
        owner.Store.Write = null; model.Execute(DocumentCommand.Save); owner.Complete(); model.Refresh();
        Assert.Empty(model.ErrorText); Assert.Equal("requested-open.aetherscene", model.PathDraft);
    }

    [Fact]
    public void Cancel_ResolvesDecisionWithoutSavingAndBusyRefreshDoesNotMutatePathDraft()
    {
        using var owner = new DocumentUiFixture(); using var model = new EditorDocumentViewModel(owner.Commands);
        model.Refresh(); model.PathDraft = "my scene.aetherscene"; model.SavePathDraft = "separate.aetherscene";
        model.Execute(DocumentCommand.New); owner.Complete(); model.Refresh(); var original = model.Document!.Content;
        model.Execute(DocumentCommand.Close); owner.Decision(); model.Refresh();
        Assert.True(model.AwaitingDecision); Assert.True(model.CanCancel); Assert.Contains("закрытием", model.DecisionText);
        model.Cancel(); owner.Complete(); model.Refresh();
        Assert.False(model.AwaitingDecision); Assert.Same(original, model.Document!.Content); Assert.True(model.Document.IsDirty);
        Assert.Equal("my scene.aetherscene", model.PathDraft); Assert.Equal("separate.aetherscene", model.SavePathDraft);
        Assert.Empty(model.ErrorText); Assert.Empty(owner.Store.Writes); Assert.True(model.CanSave);
    }

    [Fact]
    public void RuntimeBlocking_UsesFreshCoordinatorAdmissionBeforeNextPresentationRefresh()
    {
        using var owner = new DocumentUiFixture(); using var model = new EditorDocumentViewModel(owner.Commands); model.Refresh();
        Assert.False(model.BlocksRuntime); Assert.Null(model.Document!.Content);
        owner.Run(DocumentCommand.New); Assert.True(model.BlocksRuntime); Assert.Null(model.Document!.Content);
        model.Refresh(); Assert.NotNull(model.Document!.Content);
        Assert.True(owner.Commands.Execute(DocumentCommand.Close)); owner.Decision(); Assert.True(model.BlocksRuntime);
        Assert.True(owner.Commands.Choose(DocumentCloseChoice.Discard)); owner.Complete(); Assert.False(model.BlocksRuntime);
        owner.RuntimeIdle = false; model.Refresh();
        Assert.False(model.CanNew); Assert.False(model.CanOpen); Assert.False(model.CanEditPath); Assert.False(model.CanSave);
        owner.RuntimeIdle = true; model.Refresh(); Assert.True(model.CanNew); Assert.True(model.CanOpen);
    }

    [Fact]
    public void Dispose_DisconnectsNotificationsRejectsActionsAndPreservesBorrowedCoordinatorAndOwner()
    {
        using var owner = new DocumentUiFixture(); var model = new EditorDocumentViewModel(owner.Commands); model.Refresh();
        var notifications = 0; model.PropertyChanged += (_, _) => notifications++;
        model.Dispose(); model.Dispose(); owner.Run(DocumentCommand.New); Assert.Equal(0, notifications);
        Assert.False(owner.Commands.Read().Closed); Assert.True(owner.Document.Read().IsDirty);
        Assert.Throws<ObjectDisposedException>(model.Refresh);
        Assert.Throws<ObjectDisposedException>(() => model.PathDraft = "closed.aetherscene");
        Assert.Throws<ObjectDisposedException>(() => model.SavePathDraft = "closed.aetherscene");
        Assert.Throws<ObjectDisposedException>(() => model.Execute(DocumentCommand.Save));
        Assert.Throws<ObjectDisposedException>(() => model.Choose(DocumentCloseChoice.Discard));
        Assert.Throws<ObjectDisposedException>(model.Cancel);
        Assert.Equal(0, notifications); Assert.Equal(0, owner.RuntimeStops); Assert.Equal(0, owner.Store.Disposals);
        owner.Run(DocumentCommand.Save, "still-usable.aetherscene"); Assert.False(owner.Document.Read().IsDirty);
    }

    [Fact]
    public void DraftValidationAndPresentationSurface_RejectNullAndKeepControlsThemeAndContainerOutOfModel()
    {
        using var owner = new DocumentUiFixture(); using var model = new EditorDocumentViewModel(owner.Commands);
        Assert.Throws<ArgumentNullException>(() => model.PathDraft = null!); Assert.Throws<ArgumentNullException>(() => model.SavePathDraft = null!);
        Assert.Equal("Main.aetherscene", model.PathDraft); Assert.Equal("Untitled.aetherscene", model.SavePathDraft);
        var type = typeof(EditorDocumentViewModel);
        var types = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Select(field => field.FieldType)
            .Concat(type.GetConstructors().SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType)))
            .Concat(type.GetProperties().Select(property => property.PropertyType));
        Assert.All(types, dependency =>
        {
            Assert.False(typeof(Element).IsAssignableFrom(dependency), dependency.FullName);
            Assert.NotEqual(typeof(Theme), dependency); Assert.NotEqual("TrueMoon.Services.IServiceResolver", dependency.FullName);
        });
    }
}
