using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Editing;
using TrueMoon.Aether.Sessions;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Editing;

public sealed class EditorEditingShellTests
{
    [Fact]
    public void ShellSelectionAndNormalizedKeyboardInput_EditSharedDocumentRetainFormAndPreserveFocusedTextShortcutOwnership()
    {
        using var test = new EditingFixture(); using var documents = new EditorDocumentViewModel(test.Owner.Commands);
        var workspace = new EditorWorkspace(new("empty", 0, [])); var calls = new List<EditorCommand>();
        var commands = new EditorCommands((command, _) => { calls.Add(command); return Task.CompletedTask; }, CancellationToken.None);
        using var model = new EditorViewModel(workspace, commands, documents, test.Model);
        var documentProducts = 0; var transformProducts = 0;
        var view = new EditorView(model, new EditorViewStyle(),
            new Factory<EditorDocumentView>(() => { documentProducts++; return new(documents, new EditorDocumentViewStyle()); }),
            new Factory<EditorTransformView>(() => { transformProducts++; return new(test.Model, new EditorTransformViewStyle()); }));
        using var ui = DocumentUiFixture.Ui(view); Refresh();
        Assert.Equal(1, documentProducts); Assert.Equal(1, transformProducts); Assert.Equal(2, view.Entities.Count); Assert.False(view.Commands[EditorCommand.Build].IsEnabled);
        var transform = view.Transform!; var input = transform.Fields["x"]; var canvas = view.Canvas; var documentView = view.Documents;
        Assert.Equal("2", input.Value); DocumentUiFixture.Click(ui, view.Entities[DocumentData.Child.Value.ToString()]); ui.Update();
        Assert.Equal(DocumentData.Child.Value.ToString(), model.SelectedId); Assert.Equal("4", input.Value); Assert.Equal(ControlTone.Accent, view.Entities[model.SelectedId!].Tone);
        ui.Focus(input); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true)); ui.HandleInput(new(InputKind.Text, Text: "41")); ui.Update();
        Assert.Equal("41", test.Field("x").Value); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Z, Control: true));
        Assert.Equal(7, test.Owner.Document.Read().Content!.Document.Revision); Assert.Equal("41", input.Value); Assert.Equal("41", test.Field("x").Value);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true)); ui.HandleInput(new(InputKind.Text, Text: "42"));
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Enter, Control: true)); Refresh();
        Assert.Equal(42, DocumentData.X(test.Owner.Document.Read().Content!)); Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
        Assert.Contains(model.Properties, property => property.Key == "x" && property.Value == "42"); Assert.Equal("42", input.Value);
        Assert.Same(transform, view.Transform); Assert.Same(input, view.Transform!.Fields["x"]); Assert.Same(canvas, view.Canvas); Assert.Same(documentView, view.Documents);
        ui.Focus(view.Entities[model.SelectedId!]); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Z, Control: true)); Refresh();
        Assert.Equal(4, DocumentData.X(test.Owner.Document.Read().Content!)); Assert.Equal("4", input.Value); Assert.False(test.Owner.Document.Read().IsDirty);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Y, Control: true)); Refresh(); Assert.Equal("42", input.Value); Assert.True(test.Owner.Document.Read().IsDirty);
        ui.Focus(input); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true)); ui.HandleInput(new(InputKind.Text, Text: "43"));
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape)); Refresh(); Assert.Equal("42", input.Value); Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
        model.Execute(EditorCommand.Build); DocumentUiFixture.Wait(commands.Pending); Assert.Empty(calls);
        Assert.True(test.Owner.Commands.Execute(DocumentCommand.Close)); test.Owner.Decision(); Assert.True(test.Owner.Commands.Choose(DocumentCloseChoice.Discard)); test.Owner.Complete(); Refresh();
        Assert.Empty(view.Entities); Assert.False(test.Model.CanEdit); Assert.False(input.IsEnabled); Assert.Same(transform, view.Transform); Assert.Same(input, view.Transform.Fields["x"]);

        void Refresh() { model.Refresh(new(SessionState.Stopped), EditorWorkspaceTests.Status(GameEditorOperation.Idle, false)); ui.Update(); }
    }

    [Fact]
    public void NullTransformFactoryProduct_CleansCreatedDocumentViewAndPreservesBorrowedModels()
    {
        using var test = new EditingFixture(); using var documents = new EditorDocumentViewModel(test.Owner.Commands);
        using var model = new EditorViewModel(new(new("empty", 0, [])), new((_, _) => Task.CompletedTask, CancellationToken.None), documents, test.Model);
        EditorDocumentView created = null!;
        var error = Assert.Throws<InvalidOperationException>(() => new EditorView(model, new EditorViewStyle(),
            new Factory<EditorDocumentView>(() => created = new(documents, new EditorDocumentViewStyle())), new Factory<EditorTransformView>(() => null!)));
        Assert.Contains("Transform view factory returned null", error.Message); Assert.True(created.IsDisposed); Assert.True(created.PathInput.IsDisposed);
        documents.Refresh(); test.Model.Refresh(DocumentData.Child.Value.ToString()); Assert.True(test.Model.CanEdit); Assert.False(test.Owner.Commands.Read().Closed);
    }
}
