using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Editing;

public sealed class EditorTransformViewTests
{
    [Fact]
    public void TwoWayInputs_CommitAndCancelThroughButtonsAndShowFieldValidation()
    {
        using var test = new EditingFixture(); var model = test.Model;
        var view = new EditorTransformView(model, new EditorTransformViewStyle()); using var ui = DocumentUiFixture.Ui(view); ui.Update();
        Assert.Same(model, view.DataContext); Assert.Equal(9, view.Fields.Count); Assert.All(view.Fields.Values, field => Assert.True(field.IsEnabled));
        Assert.False(view.Apply.IsEnabled); Assert.False(view.Cancel.IsEnabled); Assert.False(view.Undo.IsEnabled); Assert.False(view.Redo.IsEnabled);
        Assert.Equal(132, view.Fields["x"].Width); Assert.Equal(26, view.Fields["x"].Height); Assert.Equal(new Thickness(4, 2, 4, 2), view.Fields["x"].Padding); Assert.Equal(ControlTone.Accent, view.Apply.Tone);
        Enter(ui, view.Fields["x"], "14.5"); ui.Update(); Assert.Equal("14.5", test.Field("x").Value); Assert.True(view.Apply.IsEnabled);
        DocumentUiFixture.Click(ui, view.Apply); ui.Update();
        Assert.Equal(14.5, DocumentData.X(test.Owner.Document.Read().Content!)); Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
        Assert.True(view.Undo.IsEnabled); Assert.False(view.Apply.IsEnabled); Assert.False(model.HasDraft);
        Enter(ui, view.Fields["sy"], "0"); ui.Update(); Assert.False(view.Apply.IsEnabled); Assert.True(view.Cancel.IsEnabled);
        var error = Assert.Single(DocumentUiFixture.Tree(view).OfType<Text>(), text => text.Value == test.Field("sy").Error && text.Value.Length != 0);
        Assert.True(error.IsVisible); Assert.Equal(view.Theme.Error, error.Foreground);
        DocumentUiFixture.Click(ui, view.Apply); Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
        DocumentUiFixture.Click(ui, view.Cancel); ui.Update(); Assert.Equal("1", view.Fields["sy"].Value); Assert.Empty(test.Field("sy").Error);
        DocumentUiFixture.Click(ui, view.Undo); ui.Update(); Assert.Equal("4", view.Fields["x"].Value); Assert.True(view.Redo.IsEnabled);
        DocumentUiFixture.Click(ui, view.Redo); ui.Update(); Assert.Equal("14.5", view.Fields["x"].Value); Assert.False(view.Redo.IsEnabled);
    }

    [Fact]
    public void RefreshAndThemeStyleChange_PreserveDraftTreeFocusSelectionAndInputIdentity()
    {
        using var test = new EditingFixture(); var view = new EditorTransformView(test.Model, new EditorTransformViewStyle()); using var ui = DocumentUiFixture.Ui(view); ui.Update();
        var input = view.Fields["x"]; Enter(ui, input, "73.25"); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
        var selection = input.SelectionLength; var tree = DocumentUiFixture.Tree(view).ToArray(); var fields = test.Model.Fields.ToArray();
        test.Model.Refresh(DocumentData.Child.Value.ToString()); ui.Update(); ui.SetTheme(Theme.Light); view.WithViewStyle(new EditorTransformViewStyle()); ui.Update();
        Assert.Equal(tree, DocumentUiFixture.Tree(view)); Assert.Equal(fields, test.Model.Fields); Assert.Same(input, view.Fields["x"]);
        Assert.True(input.IsFocused); Assert.Equal(selection, input.SelectionLength); Assert.Equal("73.25", input.Value); Assert.Equal("73.25", test.Field("x").Value);
        Assert.True(test.Model.CanApply); Assert.Equal(Theme.Light.Error, view.Error.Foreground);
        var notifications = 0; test.Model.PropertyChanged += (_, _) => notifications++;
        foreach (var field in test.Model.Fields) field.PropertyChanged += (_, _) => notifications++;
        test.Model.Refresh(DocumentData.Child.Value.ToString()); Assert.False(ui.Update()); Assert.Equal(0, notifications); Assert.All(tree, element => Assert.False(element.IsDisposed));
    }

    [Fact]
    public void Keyboard_ExplicitCommitEscapeAndHistoryShareModelWhileFocusedTextKeepsShortcutOwnership()
    {
        using var test = new EditingFixture(); var view = new EditorTransformView(test.Model, new EditorTransformViewStyle()); using var ui = DocumentUiFixture.Ui(view); ui.Update();
        var input = view.Fields["x"]; Enter(ui, input, "31");
        Assert.False(view.HandleShortcut(new(InputKind.KeyDown, Key: UiKey.Z, Control: true)));
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Z, Control: true)); Assert.Equal(7, test.Owner.Document.Read().Content!.Document.Revision);
        Assert.Equal("31", input.Value); Assert.Equal("31", test.Field("x").Value);
        Enter(ui, input, "32"); Assert.True(view.HandleShortcut(new(InputKind.KeyDown, Key: UiKey.Enter, Control: true))); ui.Update();
        Assert.Equal(32, DocumentData.X(test.Owner.Document.Read().Content!)); Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
        Enter(ui, input, "33"); Assert.True(view.HandleShortcut(new(InputKind.KeyDown, Key: UiKey.Escape))); ui.Update();
        Assert.Equal("32", input.Value); Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
        ui.Focus(view.Undo); Assert.True(view.HandleShortcut(new(InputKind.KeyDown, Key: UiKey.Z, Control: true))); ui.Update();
        Assert.Equal("4", input.Value); Assert.True(view.HandleShortcut(new(InputKind.KeyDown, Key: UiKey.Z, Control: true, Shift: true))); ui.Update(); Assert.Equal("32", input.Value);
        ui.Focus(view.Undo); Assert.True(view.HandleShortcut(new(InputKind.KeyDown, Key: UiKey.Z, Control: true))); ui.Update();
        Assert.True(view.HandleShortcut(new(InputKind.KeyDown, Key: UiKey.Y, Control: true))); ui.Update(); Assert.Equal("32", input.Value);
        Assert.False(view.HandleShortcut(new(InputKind.KeyUp, Key: UiKey.Z, Control: true))); Assert.False(view.HandleShortcut(new(InputKind.KeyDown, Key: UiKey.Z)));
        Assert.False(view.HandleShortcut(new(InputKind.KeyDown, Key: UiKey.Escape)));
    }

    [Fact]
    public void PreviewButtons_ShowActualCommittedRevisionAndStopWithoutCommittingUnfinishedInput()
    {
        using var test = new EditingFixture(); var view = new EditorTransformView(test.Model, new EditorTransformViewStyle()); using var ui = DocumentUiFixture.Ui(view); ui.Update();
        Enter(ui, view.Fields["x"], "51"); ui.Update(); DocumentUiFixture.Click(ui, view.ShowPreview); DocumentUiFixture.Wait(test.Preview.Pending);
        test.Model.Refresh(DocumentData.Child.Value.ToString()); ui.Update(); Assert.Contains("кадр r7", view.PreviewStatus.Value);
        Assert.Equal(7, Assert.Single(test.Shown).Revision); Assert.Equal(4, DocumentData.X(test.Owner.Document.Read().Content!)); Assert.Equal("51", view.Fields["x"].Value);
        DocumentUiFixture.Click(ui, view.Apply); DocumentUiFixture.Wait(test.Preview.Pending); test.Model.Refresh(DocumentData.Child.Value.ToString()); ui.Update();
        Assert.Equal(new long[] { 7, 8 }, test.Shown.Select(s => s.Revision)); Assert.Contains("кадр r8", view.PreviewStatus.Value); Assert.True(view.StopPreview.IsEnabled);
        DocumentUiFixture.Click(ui, view.StopPreview); DocumentUiFixture.Wait(test.Preview.Pending); test.Model.Refresh(DocumentData.Child.Value.ToString()); ui.Update();
        Assert.False(test.Preview.Read().Active); Assert.False(view.StopPreview.IsEnabled); Assert.True(view.ShowPreview.IsEnabled); Assert.Contains("выключен", view.PreviewStatus.Value);
        Assert.Equal(51, DocumentData.X(test.Owner.Document.Read().Content!)); Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
    }

    [Fact]
    public void ViewDisposal_DetachesBindingsAndPreservesBorrowedModelDocumentAndPreview()
    {
        using var test = new EditingFixture(); var view = new EditorTransformView(test.Model, new EditorTransformViewStyle()); var ui = DocumentUiFixture.Ui(view); ui.Update();
        var input = view.Fields["x"]; ui.Dispose(); Assert.True(view.IsDisposed); Assert.True(input.IsDisposed); Assert.True(view.Form.IsDisposed);
        test.Field("x").Value = "63"; test.Model.Apply(); Assert.Equal(63, DocumentData.X(test.Owner.Document.Read().Content!)); Assert.Equal("4", input.Value);
        test.Model.ShowPreview(); DocumentUiFixture.Wait(test.Preview.Pending); Assert.NotNull(test.Preview.Read().Presented);
        Assert.False(test.Owner.Commands.Read().Closed); Assert.Equal(0, test.Owner.RuntimeStops); Assert.Equal(0, test.Owner.Authoring.Disposals);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void StyleFailure_DisposesUnpublishedFormAndBindingsWithoutOwningBorrowedModelOrDocument(bool cleanupFailure)
    {
        using var test = new EditingFixture(); var failure = new IOException("transform style failed"); var cleanup = new IOException("form cleanup failed"); EditorTransformView unpublished = null!;
        var resource = new Resource(cleanupFailure ? cleanup : null);
        var error = Record.Exception(() => new EditorTransformView(test.Model, new FailingStyle(view => { unpublished = view; view.Own(resource); }, failure)));
        if (cleanupFailure) Assert.Collection(Assert.IsType<AggregateException>(error).Flatten().InnerExceptions, e => Assert.Same(failure, e), e => Assert.Same(cleanup, e));
        else Assert.Same(failure, error);
        Assert.Equal(1, resource.Disposals); Assert.True(unpublished.IsDisposed); Assert.True(unpublished.Form.IsDisposed); Assert.All(unpublished.Fields.Values, field => Assert.True(field.IsDisposed));
        test.Field("x").Value = "71"; test.Model.Apply(); Assert.Equal(71, DocumentData.X(test.Owner.Document.Read().Content!)); Assert.False(test.Owner.Commands.Read().Closed);
    }

    private sealed class FailingStyle(Action<EditorTransformView> capture, Exception failure) : IViewStyle<EditorTransformView>
    { public void Configure(IViewStyleBuilder<EditorTransformView> styles) { styles.Part(view => { capture(view); return view.Error; }).FontSize(13); throw failure; } }
    private sealed class Resource(Exception? failure) : IDisposable
    { public int Disposals; public void Dispose() { Disposals++; if (failure != null) throw failure; } }

    private static void Enter(TrueMoon.Alloy.Runtime.UiSession ui, TextBox field, string value)
    { ui.Focus(field); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true)); ui.HandleInput(new(InputKind.Text, Text: value)); }
}
