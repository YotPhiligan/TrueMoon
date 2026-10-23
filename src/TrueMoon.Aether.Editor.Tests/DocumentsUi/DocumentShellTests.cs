using System.Collections.Concurrent;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editing;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.DocumentsUi;

public sealed class DocumentShellTests
{
    [Fact]
    public void AuthoringWorkspace_PresentsHierarchyResolvedPropertiesAndReturnsToOriginalEmptyWorkspace()
    {
        using var owner = new DocumentUiFixture(); owner.Run(DocumentCommand.Open, "room.aetherscene");
        var initial = new SceneSnapshot("Сцена не открыта", 0, []); var workspace = new EditorWorkspace(initial);
        var scenes = 0; var selections = 0; workspace.SceneChanged += () => scenes++; workspace.SelectionChanged += () => selections++;
        var state = owner.Document.Read().Content!; workspace.SetDocument(state);
        Assert.Equal(2, workspace.EntityCount);
        Assert.Equal(new[] { DocumentData.Parent.Value.ToString("D"), DocumentData.Child.Value.ToString("D") }, workspace.EntityIds);
        Assert.Equal(DocumentData.Parent.Value.ToString("D"), workspace.SelectedId);
        Assert.Equal(state.Document.Id.Value.ToString("D"), workspace.Scene.Id); Assert.Equal(7, workspace.Scene.Revision);
        Assert.Empty(workspace.Scene.CopyEntities()); Assert.Equal((1, 1), (scenes, selections));
        workspace.Select(DocumentData.Child.Value.ToString("D")); var values = workspace.Properties();
        Assert.Equal(DocumentData.Child.Value.ToString("D"), values.Single(x => x.Key == "ID").Value);
        Assert.Equal(DocumentData.Parent.Value.ToString("D"), values.Single(x => x.Key == "Родитель").Value);
        Assert.Equal(DocumentData.Restored.Value.ToString("D"), values.Single(x => x.Key == "Определение").Value);
        Assert.Contains(values, x => x.Key == "x" && x.Value == "4");
        Assert.Contains(values, x => x.Key == "integer" && x.Value == long.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Contains(values, x => x.Key == "nullable" && x.Value == "null");
        Assert.Contains(values, x => x.Key == "flag" && x.Value == "false");
        var text = values.First(x => x.Key == "text").Value;
        Assert.StartsWith("\"room \\\"A\\\"\\n\\t\\\\ / Мир ", text); Assert.EndsWith("\"", text);
        Assert.DoesNotContain("\n", text); Assert.DoesNotContain("\t", text);
        workspace.SetDocument(state); Assert.Equal((1, 2), (scenes, selections)); Assert.Equal(DocumentData.Child.Value.ToString("D"), workspace.SelectedId);
        Assert.Throws<ArgumentException>(() => workspace.Select(Guid.NewGuid().ToString("D"))); Assert.Equal(DocumentData.Child.Value.ToString("D"), workspace.SelectedId);
        workspace.SetDocument(null); Assert.Same(initial, workspace.Scene); Assert.Empty(workspace.EntityIds); Assert.Equal(0, workspace.EntityCount);
        Assert.Null(workspace.SelectedId); Assert.Empty(workspace.Properties()); Assert.Equal((2, 3), (scenes, selections));
    }

    [Fact]
    public void AuthoringWorkspace_DoesNotTruncateLargerScenesIntoLegacyRuntimeProjection()
    {
        var source = DocumentData.Scene(); var entities = Enumerable.Range(1, 130).Select(i => new EntityInstance(new(DocumentData.Id(1000 + i)), DocumentData.Base)).ToArray();
        var scene = new SceneDocument(source.Id, 12, entities, source.Definitions, source.Schemas);
        using var session = DocumentData.Factory().Create(scene); var workspace = new EditorWorkspace(new("empty", 0, []));
        workspace.SetDocument(session.Read()); Assert.Equal(130, workspace.EntityCount); Assert.Equal(130, workspace.EntityIds.Count);
        Assert.Equal(entities.Select(x => x.Id.Value.ToString("D")), workspace.EntityIds); Assert.Empty(workspace.Scene.CopyEntities());
        workspace.Select(entities[^1].Id.Value.ToString("D")); Assert.Equal(entities[^1].Id.Value.ToString("D"), workspace.SelectedId);
        Assert.Contains(workspace.Properties(), pair => pair.Key == "ID" && pair.Value == entities[^1].Id.Value.ToString("D"));
    }

    [Fact]
    public void ShellRefresh_UpdatesOnlyAuthoringPartsKeepsHeaderAndSelectionAndBlocksLegacyCommands()
    {
        using var owner = new DocumentUiFixture(); using var documents = new EditorDocumentViewModel(owner.Commands);
        var workspace = new EditorWorkspace(new("empty", 0, [])); var gameCalls = new ConcurrentQueue<EditorCommand>();
        var gameCommands = new EditorCommands((command, _) => { gameCalls.Enqueue(command); return Task.CompletedTask; }, CancellationToken.None);
        using var model = new EditorViewModel(workspace, gameCommands, documents);
        var view = new EditorView(model, new EditorViewStyle(), new Factory<EditorDocumentView>(() => new(documents, new EditorDocumentViewStyle())));
        using var ui = DocumentUiFixture.Ui(view); ui.Update(); var nested = view.Documents!; var path = nested.PathInput; var canvas = view.Canvas;
        model.Execute(EditorCommand.Build); DocumentUiFixture.Wait(gameCommands.Pending); Assert.Equal(new[] { EditorCommand.Build }, gameCalls.ToArray());
        owner.Run(DocumentCommand.Open, "room.aetherscene");
        model.Execute(EditorCommand.Build); DocumentUiFixture.Wait(gameCommands.Pending); Assert.Single(gameCalls);
        model.Refresh(new(SessionState.Stopped), EditorWorkspaceTests.Status(GameEditorOperation.Idle, false)); ui.Update();
        Assert.Equal(2, view.Entities.Count); Assert.Equal(2, model.EntityCount); Assert.False(model.CanBuild);
        Assert.Contains("2 объектов", model.StatusText); Assert.Contains("2 объектов", view.Status.Value);
        Assert.Equal(owner.Document.Read().Content!.Document.Id.Value.ToString("D"), view.SceneTitle.Value);
        Assert.Same(path, view.Documents!.PathInput); Assert.Same(canvas, view.Canvas); Assert.True(canvas.Bounds.Height > 0);
        model.Select(DocumentData.Child.Value.ToString("D")); ui.Update(); var entityButton = view.Entities[model.SelectedId!];
        Assert.Equal(ControlTone.Accent, entityButton.Tone); var inspector = view.PropertiesPanel.Child;
        owner.Run(DocumentCommand.Save); model.Refresh(new(SessionState.Stopped), EditorWorkspaceTests.Status(GameEditorOperation.Idle, false)); ui.Update();
        Assert.Same(entityButton, view.Entities[model.SelectedId!]); Assert.Same(inspector, view.PropertiesPanel.Child); Assert.Same(nested, view.Documents);
        Assert.True(owner.Document.Apply(DocumentData.Edit(7, 81)).Succeeded);
        model.Refresh(new(SessionState.Stopped), EditorWorkspaceTests.Status(GameEditorOperation.Idle, false)); ui.Update();
        Assert.Equal(DocumentData.Child.Value.ToString("D"), model.SelectedId); Assert.Same(entityButton, view.Entities[model.SelectedId!]);
        Assert.Contains(model.Properties, x => x.Key == "x" && x.Value == "81"); Assert.True(inspector!.IsDisposed); Assert.False(path.IsDisposed);
        var notifications = 0; model.PropertyChanged += (_, _) => notifications++; model.Dispose(); workspace.SetDocument(null); Assert.Equal(0, notifications);
        Assert.False(owner.Commands.Read().Closed); Assert.False(documents.Document!.IsClosed);
    }

    [Fact]
    public void NestedViewFactoryFailureOrNullProduct_IsObservedAndDoesNotDisposeBorrowedDocumentModel()
    {
        using var owner = new DocumentUiFixture(); using var documents = new EditorDocumentViewModel(owner.Commands);
        using var model = new EditorViewModel(new(new("empty", 0, [])), new((_, _) => Task.CompletedTask, CancellationToken.None), documents);
        var error = Assert.Throws<InvalidOperationException>(() => new EditorView(model, new EditorViewStyle(), new Factory<EditorDocumentView>(() => null!)));
        Assert.Contains("Document view factory returned null", error.Message);
        documents.Refresh(); Assert.True(documents.CanNew); Assert.False(owner.Commands.Read().Closed); Assert.Equal(0, owner.RuntimeStops);
    }
}
