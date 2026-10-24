using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Scenes;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Inspector;

public sealed class EditorInspectorViewTests
{
    [Fact]
    public void SchemaEditors_UseCheckboxTextAndMultilineControlsAndRemoveSectionsAbsentFromSelection()
    {
        using var test = new InspectorFixture();
        var view = new EditorInspectorView(test.Model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view); ui.Update();
        Assert.Same(test.Model, view.DataContext); Assert.Equal(5, view.Components.Count); Assert.Equal(23, view.Properties.Count);
        Assert.IsType<CheckBox>(view.Properties[(DocumentData.Custom, "flag")].Input);
        Assert.IsType<TextBox>(view.Properties[(DocumentData.Custom, "integer")].Input);
        Assert.IsType<TextBox>(view.Properties[(DocumentData.Custom, "number")].Input);
        Assert.Equal(12f, view.Properties[(DocumentData.Custom, "number")].Input.FontSize);
        Assert.Equal(new Thickness(4, 2, 4, 2), view.Properties[(DocumentData.Custom, "number")].Input.Padding);
        Assert.Equal(26f, view.Properties[(DocumentData.Custom, "number")].Input.Height);
        Assert.Equal(ControlTone.Accent, view.Apply.Tone);
        var text = Assert.IsType<RichTextEditor>(view.Properties[(DocumentData.Custom, "text")].Input);
        Assert.NotSame(text, view.Properties[(DocumentData.Local, "text")].Input);
        Assert.Equal(DocumentData.Text, text.Text); Assert.False(text.Options.IsReadOnly);
        var nullable = view.Properties[(DocumentData.Custom, "nullable")];
        Assert.True(nullable.Null!.IsChecked); Assert.False(nullable.Input.IsEnabled);
        Assert.Null(view.Properties[(DocumentData.Custom, "text")].Null);
        Assert.False(view.Apply.IsEnabled);
        var interaction = view.Properties[(DocumentData.Interaction, "enabled")].Input;

        test.Model.Refresh(InspectorFixture.Parent); ui.Update();
        Assert.Equal(2, view.Components.Count); Assert.Equal(14, view.Properties.Count);
        Assert.DoesNotContain(DocumentData.Appearance, view.Components.Keys); Assert.DoesNotContain(DocumentData.Interaction, view.Components.Keys);
        Assert.True(interaction.IsDisposed);
        Assert.True(view.RestoreButtons.ContainsKey(DocumentData.Appearance));
        Assert.True(view.RestoreButtons.ContainsKey(DocumentData.Interaction));
        test.Model.Refresh(null); ui.Update();
        Assert.Empty(view.Components); Assert.Empty(view.Properties); Assert.True(view.Empty.IsVisible);
        Assert.False(view.Apply.IsEnabled); Assert.False(view.AddComponent.IsEnabled);
    }

    [Fact]
    public void TypedInputs_CommitBooleanNullIntegerAndMultilineTextAsOneTransactionThroughButtons()
    {
        using var test = new InspectorFixture();
        var view = new EditorInspectorView(test.Model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view); ui.Update();
        var boolean = Assert.IsType<CheckBox>(view.Properties[(DocumentData.Custom, "flag")].Input);
        Click(ui, view, boolean); Assert.True(test.Property(DocumentData.Custom, "flag").BooleanValue);
        var nullable = view.Properties[(DocumentData.Custom, "nullable")];
        Click(ui, view, nullable.Null!); Assert.False(test.Property(DocumentData.Custom, "nullable").IsNull); Assert.True(nullable.Input.IsEnabled);
        Enter(ui, view, nullable.Input, "");
        Enter(ui, view, view.Properties[(DocumentData.Custom, "integer")].Input, "-9223372036854775808");
        const string edited = "UI text\nМир 🛰️";
        Enter(ui, view, view.Properties[(DocumentData.Custom, "text")].Input, edited);
        Assert.True(view.Apply.IsEnabled); Assert.False(view.AddComponent.IsEnabled);
        Click(ui, view, view.Apply); ui.Update();

        var state = test.Owner.Document.Read().Content!; var custom = state.Resolved.Entities[DocumentData.Child].Components[DocumentData.Custom];
        Assert.True(custom.Properties["flag"].Value.GetBoolean()); Assert.Equal(long.MinValue, custom.Properties["integer"].Value.GetInteger());
        Assert.Equal("", custom.Properties["nullable"].Value.GetString()); Assert.Equal(edited, custom.Properties["text"].Value.GetString());
        Assert.Equal(1, state.UndoCount); Assert.False(test.Model.HasDraft); Assert.True(view.Undo.IsEnabled);
        Click(ui, view, view.Undo); ui.Update();
        Assert.False(boolean.IsChecked); Assert.True(nullable.Null!.IsChecked); Assert.False(test.Owner.Document.Read().IsDirty);
        Click(ui, view, view.Redo); ui.Update();
        Assert.Equal(edited, Assert.IsType<RichTextEditor>(view.Properties[(DocumentData.Custom, "text")].Input).Text);
        Assert.True(boolean.IsChecked); Assert.False(nullable.Null!.IsChecked);
    }

    [Fact]
    public void ThemeRefreshAndStyleChange_PreserveRetainedEditorsDraftFocusSelectionAndScroll()
    {
        using var test = new InspectorFixture();
        var view = new EditorInspectorView(test.Model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view); ui.Update();
        var address = (DocumentData.Custom, "text"); var input = Assert.IsType<RichTextEditor>(view.Properties[address].Input);
        Enter(ui, view, input, "retained draft\nМир");
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
        var selection = input.SelectionLength; var offset = view.Scroll.Offset;
        var tree = DocumentUiFixture.Tree(view).ToArray(); var row = view.Properties[address].Row;
        test.Refresh(); ui.Update(); ui.SetTheme(Theme.Light); view.WithViewStyle(new EditorInspectorViewStyle()); ui.Update();
        Assert.Same(input, view.Properties[address].Input); Assert.Same(row, view.Properties[address].Row);
        Assert.Equal(tree, DocumentUiFixture.Tree(view)); Assert.True(input.IsFocused);
        Assert.Equal(selection, input.SelectionLength); Assert.Equal("retained draft\nМир", input.Text);
        Assert.Equal(offset, view.Scroll.Offset); Assert.True(test.Model.HasDraft); Assert.True(view.Apply.IsEnabled);
        Assert.Equal(Theme.Light.Error, view.Error.Foreground);
        test.Refresh(); Assert.False(ui.Update());
    }

    [Fact]
    public void Keyboard_ExplicitCommitCancelAndSharedHistoryRespectMultilineEditorsOwnUndo()
    {
        using var test = new InspectorFixture();
        var view = new EditorInspectorView(test.Model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view); ui.Update();
        var input = Assert.IsType<RichTextEditor>(view.Properties[(DocumentData.Custom, "text")].Input);
        Enter(ui, view, input, "local edit");
        Assert.False(view.HandleShortcut(new(InputKind.KeyDown, Key: UiKey.Z, Control: true)));
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Z, Control: true)); ui.Update();
        Assert.Equal(7, test.Owner.Document.Read().Content!.Document.Revision);
        Assert.Equal(DocumentData.Text, input.Text); Assert.False(test.Model.HasDraft);
        Enter(ui, view, input, "committed text");
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Enter, Control: true)); ui.Update();
        Assert.Equal("committed text", test.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].Components[DocumentData.Custom].Properties["text"].Value.GetString());
        Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
        Enter(ui, view, input, "cancel me");
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape));
        Assert.False(test.Model.HasDraft, $"Error={test.Model.ErrorText}");
        Assert.Equal("committed text", test.Property(DocumentData.Custom, "text").Value);
        ui.Update(); ui.Update();
        Assert.Equal("committed text", input.Text); Assert.False(test.Model.HasDraft);
        ui.Focus(view.Undo); Assert.True(view.HandleShortcut(new(InputKind.KeyDown, Key: UiKey.Z, Control: true))); ui.Update(); ui.Update();
        Assert.Equal(DocumentData.Text, input.Text); Assert.False(test.Owner.Document.Read().IsDirty);
        Assert.True(view.HandleShortcut(new(InputKind.KeyDown, Key: UiKey.Y, Control: true))); ui.Update(); ui.Update();
        Assert.Equal("committed text", input.Text); Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
    }

    [Fact]
    public void FieldValidation_ShowsAddressedErrorAndCancelRestoresControlWithoutPublishingOtherDrafts()
    {
        using var test = new InspectorFixture();
        var view = new EditorInspectorView(test.Model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view); ui.Update();
        Enter(ui, view, view.Properties[(DocumentData.Custom, "number")].Input, "NaN");
        Enter(ui, view, view.Properties[(DocumentData.Custom, "text")].Input, "do not publish");
        var field = test.Property(DocumentData.Custom, "number");
        var error = Assert.Single(DocumentUiFixture.Tree(view.Properties[(DocumentData.Custom, "number")].Row).OfType<Text>(), item => item.Value == field.Error && item.Value.Length != 0);
        Assert.True(error.IsVisible); Assert.Equal(view.Theme.Error, error.Foreground);
        Assert.False(view.Apply.IsEnabled); Assert.True(view.Cancel.IsEnabled);
        Click(ui, view, view.Apply); Assert.Equal(0, test.Owner.Document.Read().Content!.UndoCount);
        Click(ui, view, view.Cancel); ui.Update();
        Assert.Equal("1", Assert.IsType<TextBox>(view.Properties[(DocumentData.Custom, "number")].Input).Value);
        Assert.Equal(DocumentData.Text, Assert.IsType<RichTextEditor>(view.Properties[(DocumentData.Custom, "text")].Input).Text);
        Assert.False(error.IsVisible); Assert.False(test.Model.HasDraft); Assert.False(test.Owner.Document.Read().IsDirty);
    }

    [Fact]
    public void ComponentChanges_DisposeOnlyRemovedCardAndRetainUnchangedEditorsAndExpansion()
    {
        using var test = new InspectorFixture();
        var view = new EditorInspectorView(test.Model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view); ui.Update();
        var unchanged = view.Properties[(DocumentData.Custom, "text")].Input;
        var localCard = view.Components[DocumentData.Local]; var localInput = view.Properties[(DocumentData.Local, "text")].Input;
        var header = view.Components[DocumentData.Custom].Header; Click(ui, view, header);
        Assert.False(view.Components[DocumentData.Custom].Body.IsVisible);
        Click(ui, view, localCard.Remove); ui.Update();
        Assert.DoesNotContain(DocumentData.Local, view.Components.Keys); Assert.True(localInput.IsDisposed);
        Assert.Same(unchanged, view.Properties[(DocumentData.Custom, "text")].Input); Assert.False(unchanged.IsDisposed);
        Assert.False(view.Components[DocumentData.Custom].Body.IsVisible);
        Click(ui, view, view.Undo); ui.Update();
        Assert.Contains(DocumentData.Local, view.Components.Keys); Assert.NotSame(localInput, view.Properties[(DocumentData.Local, "text")].Input);
        Assert.Same(unchanged, view.Properties[(DocumentData.Custom, "text")].Input); Assert.False(view.Components[DocumentData.Custom].Body.IsVisible);
        Assert.False(test.Owner.Document.Read().IsDirty);
    }

    [Fact]
    public void ResetAndRestoreButtons_UseInheritanceInsteadOfCreatingDuplicateOccurrences()
    {
        using var test = new InspectorFixture();
        var view = new EditorInspectorView(test.Model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view); ui.Update();
        var reset = view.Properties[(DocumentData.Custom, "nullable")].Reset;
        Click(ui, view, reset); ui.Update(); Assert.True(test.Property(DocumentData.Custom, "nullable").IsReset);
        Click(ui, view, view.Apply); ui.Update();
        Assert.Equal("inherited", Assert.IsType<RichTextEditor>(view.Properties[(DocumentData.Custom, "nullable")].Input).Text);
        Assert.False(reset.IsEnabled);
        test.Model.Refresh(InspectorFixture.Parent); ui.Update();
        var count = view.Components.Count;
        Click(ui, view, view.RestoreButtons[DocumentData.Interaction]); ui.Update();
        Assert.Equal(count + 1, view.Components.Count); Assert.Contains(DocumentData.Interaction, view.Components.Keys);
        Assert.False(view.RestoreButtons.ContainsKey(DocumentData.Interaction));
        Assert.Equal(2, test.Owner.Document.Read().Content!.UndoCount);
    }

    [Fact]
    public void SchemaChoices_AddCorrectOccurrenceAndApplyScopedStylesToNewDynamicEditors()
    {
        using var test = new InspectorFixture();
        var view = new EditorInspectorView(test.Model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view); ui.Update();
        var before = view.Components.Keys.ToHashSet(); Assert.False(view.AddChoices.IsVisible);
        Click(ui, view, view.AddComponent); Assert.True(view.AddChoices.IsVisible);
        Assert.False(view.AddButtons[(BuiltInComponents.TransformId, 1)].IsEnabled);
        var choice = view.AddButtons[(DocumentData.CustomType, 2)]; Assert.True(choice.IsEnabled);
        Click(ui, view, choice); ui.Update();
        Assert.False(view.AddChoices.IsVisible); Assert.Equal(6, view.Components.Count);
        var added = Assert.Single(view.Components.Keys, id => !before.Contains(id));
        var state = test.Owner.Document.Read().Content!;
        Assert.Equal(DocumentData.CustomType, state.Resolved.Entities[DocumentData.Child].Components[added].TypeId);
        Assert.Equal(2, state.Resolved.Entities[DocumentData.Child].Components[added].SchemaVersion); Assert.Equal(1, state.UndoCount);
        var number = Assert.IsType<TextBox>(view.Properties[(added, "number")].Input);
        Assert.Equal(12f, number.FontSize); Assert.Equal(new Thickness(4, 2, 4, 2), number.Padding);
        Assert.Equal(26f, number.Height); Assert.Equal("1", number.Value);
        Assert.True(number.Bounds.Width > 0); Assert.True(number.Bounds.X + number.Bounds.Width <= view.Components[added].Body.Bounds.X + view.Components[added].Body.Bounds.Width + .01f);
        Assert.Equal(70f, Assert.IsType<RichTextEditor>(view.Properties[(added, "text")].Input).Height);
        Assert.True(Assert.IsType<CheckBox>(view.Properties[(added, "flag")].Input).IsChecked);
        Assert.True(view.Properties[(added, "nullable")].Null!.IsChecked);
    }

    [Fact]
    public void Disposal_DetachesDynamicBindingsAndLeavesBorrowedModelDocumentAndPreviewAlive()
    {
        using var test = new InspectorFixture();
        var view = new EditorInspectorView(test.Model, new EditorInspectorViewStyle()); var ui = InspectorFixture.Ui(view); ui.Update();
        var input = Assert.IsType<RichTextEditor>(view.Properties[(DocumentData.Custom, "text")].Input);
        var tree = DocumentUiFixture.Tree(view).ToArray(); ui.Dispose();
        Assert.True(view.IsDisposed); Assert.All(tree, item => Assert.True(item.IsDisposed));
        test.Property(DocumentData.Custom, "text").Value = "after disposal"; test.Model.Apply();
        Assert.Equal("after disposal", test.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].Components[DocumentData.Custom].Properties["text"].Value.GetString());
        Assert.Equal(DocumentData.Text, input.Text); Assert.False(test.Owner.Commands.Read().Closed);
        Assert.Equal(0, test.Owner.Authoring.Products[0].Disposals);
        test.Model.ShowPreview(); DocumentUiFixture.Wait(test.Preview.Pending); Assert.Equal(8, test.Preview.Read().Presented!.Revision);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StyleFailure_CleansUnpublishedFormAndAggregatesCleanupFailureWithoutOwningModel(bool cleanupFailure)
    {
        using var test = new InspectorFixture(); EditorInspectorView unpublished = null!;
        var failure = new IOException("inspector style failed"); var cleanup = new IOException("inspector cleanup failed");
        var resource = new Resource(cleanupFailure ? cleanup : null);
        var error = Record.Exception(() => new EditorInspectorView(test.Model, new FailingStyle(view => { unpublished = view; view.Own(resource); }, failure)));
        if (cleanupFailure) Assert.Collection(Assert.IsType<AggregateException>(error).Flatten().InnerExceptions, item => Assert.Same(failure, item), item => Assert.Same(cleanup, item));
        else Assert.Same(failure, error);
        Assert.Equal(1, resource.Disposals); Assert.True(unpublished.IsDisposed); Assert.True(unpublished.Form.IsDisposed);
        Assert.True(unpublished.Scroll.IsDisposed); Assert.True(unpublished.ComponentList.IsDisposed);
        test.Property(DocumentData.Custom, "flag").BooleanValue = true; test.Model.Apply();
        Assert.True(test.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].Components[DocumentData.Custom].Properties["flag"].Value.GetBoolean());
        Assert.False(test.Owner.Commands.Read().Closed);
    }

    private static void Click(UiSession ui, EditorInspectorView view, Button button)
    {
        ui.Update(); view.Scroll.BringIntoView(button.Bounds); ui.Update();
        DocumentUiFixture.Click(ui, button); ui.Update();
    }
    private static void Enter(UiSession ui, EditorInspectorView view, Element input, string value)
    {
        ui.Update(); view.Scroll.BringIntoView(input.Bounds); ui.Update(); ui.Focus(input);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
        if (value.Length == 0) ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Backspace));
        else if (input is RichTextEditor)
        {
            var lines = value.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (i != 0) ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Enter));
                if (lines[i].Length != 0) ui.HandleInput(new(InputKind.Text, Text: lines[i]));
            }
        }
        else ui.HandleInput(new(InputKind.Text, Text: value));
        ui.Update();
    }
    private sealed class FailingStyle(Action<EditorInspectorView> capture, Exception failure) : IViewStyle<EditorInspectorView>
    { public void Configure(IViewStyleBuilder<EditorInspectorView> styles) { styles.Part(view => { capture(view); return view.Error; }).FontSize(13); throw failure; } }
    private sealed class Resource(Exception? failure) : IDisposable
    { public int Disposals; public void Dispose() { Disposals++; if (failure != null) throw failure; } }
}
