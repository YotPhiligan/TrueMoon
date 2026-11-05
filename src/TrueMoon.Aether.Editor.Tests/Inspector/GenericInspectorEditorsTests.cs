using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Scenes;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Inspector;

public sealed class GenericInspectorEditorsTests
{
    [Fact]
    public void GenericRows_AlignWithTransformAndKeepTypedInputsWhileCappingNumericBooleanAndFillingStrings()
    {
        using var test = new InspectorFixture(); var view = new EditorInspectorView(test.Model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view); Drain(ui);
        Assert.Equal(14, view.GenericEditors.Count); Assert.DoesNotContain(view.GenericEditors.Keys, key => key.Item1 == DocumentData.Transform);
        var numeric = view.GenericEditors[(DocumentData.Custom, "number")]; var boolean = view.GenericEditors[(DocumentData.Custom, "flag")];
        var text = view.GenericEditors[(DocumentData.Custom, "text")];
        var number = Assert.IsType<NumberBox>(numeric.Editor); Assert.Equal("", number.Prefix.Value);
        Assert.Same(number.Editor, view.Properties[(DocumentData.Custom, "number")].Input);
        Assert.Same(boolean.Editor, view.Properties[(DocumentData.Custom, "flag")].Input); Assert.IsType<CheckBox>(boolean.Editor);
        Assert.Same(text.Editor, view.Properties[(DocumentData.Custom, "text")].Input); Assert.IsType<RichTextEditor>(text.Editor);
        Assert.Equal(120, numeric.Editor.Bounds.Width); Assert.Equal(96, boolean.Editor.Bounds.Width);
        Assert.Equal(text.Bounds.Width - 92, text.Editor.Bounds.Width, 3);
        Assert.Equal(88, numeric.Label.Bounds.Width); Assert.Equal(88, boolean.Label.Bounds.Width); Assert.Equal(88, text.Label.Bounds.Width);
        Assert.Equal(view.TransformEditors[DocumentData.Transform].Position.Label.Bounds.Width, numeric.Label.Bounds.Width);
        Assert.Equal(numeric.Bounds.X + 92, numeric.Editor.Bounds.X); Assert.Equal(text.Bounds.X + 92, text.Editor.Bounds.X);
        Assert.Equal(26, numeric.Editor.Bounds.Height); Assert.Equal(26, boolean.Editor.Bounds.Height);
        Assert.All(view.Properties.Values, property => { Assert.False(property.Source.IsVisible); Assert.False(property.Reset.IsVisible); });
        Assert.False(test.Model.HasDraft); Assert.Equal(0, test.Owner.Document.Read().Content!.UndoCount);
    }

    [Theory]
    [InlineData("integer", "-")] [InlineData("integer", "9223372036854775808")]
    [InlineData("number", "1e-")] [InlineData("number", "NaN")]
    public void NumericWrapper_PreservesInvalidStringDraftAndCancelNeverCommitsAnotherField(string key, string draft)
    {
        using var test = new InspectorFixture(); var view = new EditorInspectorView(test.Model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view); Drain(ui);
        var parts = view.Properties[(DocumentData.Custom, key)]; var input = Assert.IsType<TextBox>(parts.Input);
        var wrapper = Assert.IsType<NumberBox>(view.GenericEditors[(DocumentData.Custom, key)].Editor);
        var before = test.Owner.Document.Read().Content; var initial = input.Value;
        Enter(ui, view, input, draft); Enter(ui, view, view.Properties[(DocumentData.Local, "text")].Input, "another draft");
        Assert.Same(input, wrapper.Editor); Assert.Equal(draft, input.Value); Assert.Equal(draft, parts.Property.Value);
        Assert.True(input.IsModified); Assert.True(input.HasError); Assert.False(view.Apply.IsEnabled); Assert.True(view.Error.IsVisible);
        Assert.Contains(parts.Property.Error, view.Error.Text); Assert.False(parts.Source.IsVisible);
        view.HandleShortcut(new(InputKind.KeyDown, Key: UiKey.Enter, Control: true)); Drain(ui);
        Assert.Same(before, test.Owner.Document.Read().Content); Assert.Equal(0, before!.UndoCount);
        view.HandleShortcut(new(InputKind.KeyDown, Key: UiKey.Escape)); Drain(ui);
        Assert.Equal(initial, input.Value); Assert.False(input.IsModified); Assert.False(input.HasError); Assert.False(test.Model.HasDraft);
        Assert.Equal(DocumentData.Text, Assert.IsType<RichTextEditor>(view.Properties[(DocumentData.Local, "text")].Input).Text);
        Assert.Same(wrapper, view.GenericEditors[(DocumentData.Custom, key)].Editor);
    }

    [Theory]
    [InlineData("integer", "00042", "42")] [InlineData("number", "01.50", "1.5")]
    public void NumericDraft_IsNormalizedOnlyByExplicitAuthoringCommitAndUsesSharedUndo(string key, string draft, string committed)
    {
        using var test = new InspectorFixture(); var view = new EditorInspectorView(test.Model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view); Drain(ui); var input = Assert.IsType<TextBox>(view.Properties[(DocumentData.Custom, key)].Input);
        var initial = input.Value; Enter(ui, view, input, draft);
        Assert.Equal(draft, input.Value); Assert.True(input.IsModified); Assert.False(input.HasError); Assert.False(test.Owner.Document.Read().IsDirty);
        view.HandleShortcut(new(InputKind.KeyDown, Key: UiKey.Enter, Control: true)); Drain(ui);
        var state = test.Owner.Document.Read().Content!;
        Assert.Equal(committed, InspectorProperty.Format(state.Resolved.Entities[DocumentData.Child].Components[DocumentData.Custom].Properties[key].Value));
        Assert.Equal(committed, input.Value); Assert.Equal(1, state.UndoCount); Assert.False(input.IsModified);
        Click(ui, view, view.Undo); Assert.Equal(initial, input.Value); Assert.False(test.Owner.Document.Read().IsDirty);
        Click(ui, view, view.Redo); Assert.Equal(committed, input.Value); Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
        Assert.Same(input, view.Properties[(DocumentData.Custom, key)].Input);
    }

    [Fact]
    public void MultilineString_CompactsAndGrowsWithinBoundsWithoutSwappingEditorOrLosingDraftHistoryFocusSelection()
    {
        using var test = new InspectorFixture(); var view = new EditorInspectorView(test.Model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view); Drain(ui);
        var row = view.GenericEditors[(DocumentData.Custom, "text")]; var input = Assert.IsType<RichTextEditor>(row.Editor);
        var tree = DocumentUiFixture.Tree(view).ToArray();
        Enter(ui, view, input, "one line"); Assert.Equal(26, input.Bounds.Height); Assert.True(float.IsNaN(input.Height));
        var longText = string.Join("\n", Enumerable.Repeat("Мир 👩‍💻", 12)); Enter(ui, view, input, longText);
        Assert.Equal(78, input.Bounds.Height); Assert.True(input.CanUndo); input.Select(2, 4); var selected = input.SelectedText;
        Assert.NotEmpty(selected); Assert.Equal(2, input.SelectionStart);
        ui.SetTheme(Theme.Light); view.WithViewStyle(new EditorInspectorViewStyle()); ui.Resize(new(280, 800)); Drain(ui);
        Assert.Equal(tree, DocumentUiFixture.Tree(view)); Assert.Same(input, row.Editor); Assert.Same(input, view.Properties[(DocumentData.Custom, "text")].Input);
        Assert.Equal(longText, input.Text); Assert.Equal(selected, input.SelectedText); Assert.True(input.IsFocused); Assert.True(input.CanUndo);
        Assert.Equal(78, input.Bounds.Height); Assert.True(input.IsModified);
        Assert.True(input.Undo()); Drain(ui); Assert.Equal("one line", input.Text); Assert.Equal(26, input.Bounds.Height);
        Assert.True(input.Redo()); Drain(ui); Assert.Equal(longText, input.Text); Assert.Equal(78, input.Bounds.Height);
        view.HandleShortcut(new(InputKind.KeyDown, Key: UiKey.Escape)); Drain(ui);
        Assert.Equal(DocumentData.Text, input.Text); Assert.False(input.IsModified); Assert.False(test.Model.HasDraft); Assert.Equal(0, test.Owner.Document.Read().Content!.UndoCount);
    }

    [Fact]
    public void NullSwitch_UsesCompactPlaceholderAndRetainsStoredStringThroughToggleCommitUndoAndRedo()
    {
        using var test = new InspectorFixture(); var view = new EditorInspectorView(test.Model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view); Drain(ui);
        var parts = view.Properties[(DocumentData.Custom, "nullable")]; var row = view.GenericEditors[(DocumentData.Custom, "nullable")];
        var input = Assert.IsType<RichTextEditor>(parts.Input); var nullable = Assert.IsType<CheckBox>(parts.Null);
        Assert.Same(nullable, row.Accessory); Assert.Same(input, row.Editor); Assert.True(row.Empty.IsVisible); Assert.False(input.IsVisible); Assert.False(input.IsEnabled);
        Assert.Equal("null", nullable.Value); Assert.Contains("nullable", nullable.AccessibleName); Assert.EndsWith(": null", row.Empty.AccessibleName);
        Assert.Equal(26, row.Bounds.Height); Assert.True(nullable.IsChecked); Assert.False(row.Empty.IsModified);
        Click(ui, view, nullable); Assert.False(nullable.IsChecked); Assert.True(input.IsVisible); Assert.True(input.IsEnabled); Assert.Equal("", input.Text);
        const string stored = "stored\nМир"; Enter(ui, view, input, stored); Click(ui, view, nullable);
        Assert.True(nullable.IsChecked); Assert.True(row.Empty.IsVisible); Assert.False(input.IsVisible); Assert.Equal(stored, input.Text);
        Assert.True(row.Empty.IsModified); Assert.True(nullable.IsModified); Assert.Equal(26, row.Bounds.Height);
        Click(ui, view, nullable); Assert.Equal(stored, input.Text); Assert.False(row.Empty.IsVisible); Assert.True(input.IsVisible);
        Click(ui, view, view.Apply); Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
        Assert.Equal(stored, test.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].Components[DocumentData.Custom].Properties["nullable"].Value.GetString());
        Assert.False(nullable.IsModified); Assert.False(input.IsModified);
        Click(ui, view, view.Undo); Assert.True(nullable.IsChecked); Assert.True(row.Empty.IsVisible); Assert.Equal(26, row.Bounds.Height);
        Click(ui, view, view.Redo); Assert.False(nullable.IsChecked); Assert.Equal(stored, input.Text); Assert.Same(input, row.Editor);
        Enter(ui, view, input, ""); Click(ui, view, view.Apply);
        Assert.False(nullable.IsChecked); Assert.False(row.Empty.IsVisible); Assert.Equal("", input.Text);
        Assert.Equal("", test.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].Components[DocumentData.Custom].Properties["nullable"].Value.GetString());
        Assert.Equal(2, test.Owner.Document.Read().Content!.UndoCount);
    }

    [Fact]
    public void BooleanAndReset_UseSemanticStatesAndFullWidthDisclosureWithoutReplacingInputsOrHistory()
    {
        using var test = new InspectorFixture(); var view = new EditorInspectorView(test.Model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view); Drain(ui);
        var parts = view.Properties[(DocumentData.Custom, "flag")]; var row = view.GenericEditors[(DocumentData.Custom, "flag")];
        var boolean = Assert.IsType<CheckBox>(parts.Input); var number = view.Properties[(DocumentData.Custom, "number")].Input;
        Assert.Equal("Нет", boolean.Value); Assert.False(boolean.IsChecked); Assert.Equal(EditorColorRole.Boolean, boolean.ColorRole);
        Click(ui, view, boolean); Assert.Equal("Да", boolean.Value); Assert.True(boolean.IsModified); Assert.False(test.Owner.Document.Read().IsDirty);
        ui.Focus(boolean); ui.SetTheme(Theme.Light); view.WithViewStyle(new EditorInspectorViewStyle()); Drain(ui);
        Assert.True(boolean.IsFocused); Assert.Equal("Да", boolean.Value); Assert.Same(boolean, row.Editor); Assert.Same(number, view.Properties[(DocumentData.Custom, "number")].Input);
        Click(ui, view, view.Apply); Assert.False(boolean.IsModified); Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
        Click(ui, view, view.Undo); Assert.Equal("Нет", boolean.Value); Assert.False(boolean.IsChecked);
        Click(ui, view, view.Redo); Assert.Equal("Да", boolean.Value); Assert.True(boolean.IsChecked);
        Assert.False(parts.Source.Parent!.IsVisible); Click(ui, view, view.DetailsButtons[DocumentData.Custom]);
        Assert.True(parts.Source.IsVisible); Assert.True(parts.Source.Parent.IsVisible); Assert.Equal(parts.Row.Bounds.Width, parts.Source.Parent.Bounds.Width);
        var nullable = view.Properties[(DocumentData.Custom, "nullable")]; var nullRow = view.GenericEditors[(DocumentData.Custom, "nullable")];
        Click(ui, view, nullable.Reset); Assert.True(nullable.Property.IsReset); Assert.True(nullRow.Empty.IsModified);
        Click(ui, view, view.Apply); Assert.Equal("inherited", Assert.IsType<RichTextEditor>(nullable.Input).Text);
        Assert.False(nullRow.Empty.IsVisible); Assert.True(nullable.Input.IsVisible); Assert.Equal(2, test.Owner.Document.Read().Content!.UndoCount);
        Assert.Same(boolean, parts.Input); Assert.Same(row, view.GenericEditors[(DocumentData.Custom, "flag")]);
    }

    [Fact]
    public void SelectionRemovalAndDisposal_ClearGenericAddressesAndReleaseOnlyOwnedEditors()
    {
        using var test = new InspectorFixture(); var view = new EditorInspectorView(test.Model, new EditorInspectorViewStyle());
        var ui = InspectorFixture.Ui(view); Drain(ui);
        var removed = view.GenericEditors[(DocumentData.Local, "text")]; var kept = view.GenericEditors[(DocumentData.Custom, "number")];
        test.Model.Refresh(InspectorFixture.Parent); Drain(ui);
        Assert.Equal(5, view.GenericEditors.Count); Assert.True(removed.IsDisposed); Assert.True(removed.Editor.IsDisposed);
        Assert.DoesNotContain(view.GenericEditors.Keys, address => address.Item1 == DocumentData.Local || address.Item1 == DocumentData.Appearance || address.Item1 == DocumentData.Interaction);
        Assert.Same(kept, view.GenericEditors[(DocumentData.Custom, "number")]); Assert.All(view.GenericEditors.Values, row => Assert.False(row.IsDisposed));
        var tree = DocumentUiFixture.Tree(view).ToArray(); ui.Dispose(); Assert.All(tree, element => Assert.True(element.IsDisposed));
        test.Property(DocumentData.Custom, "number").Value = "2.5"; test.Model.Apply();
        Assert.Equal(2.5, test.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Parent].Components[DocumentData.Custom].Properties["number"].Value.GetNumber());
        Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount); Assert.Equal(0, test.Owner.Authoring.Products[0].Disposals); Assert.False(test.Owner.Commands.Read().Closed);
        Assert.Equal("1", Assert.IsType<NumberBox>(kept.Editor).Editor.Value);
    }

    [Theory]
    [InlineData(PropertyValueKind.Integer, "42")]
    [InlineData(PropertyValueKind.Number, "1.5")]
    [InlineData(PropertyValueKind.Boolean, "true")]
    public void NullableScalarKinds_RetainTypedDraftAcrossNullTogglesAndShareCommitUndo(PropertyValueKind kind, string value)
    {
        var schema = new ComponentSchema(DocumentData.CustomType, 1, [new("value", kind, PropertyValue.Null, true)]);
        var definition = new EntityDefinition(DocumentData.Restored, new(addedComponents: [new(DocumentData.Custom, schema.TypeId, 1)]));
        var scene = new SceneDocument(new(DocumentData.Id(100)), 7, [new(DocumentData.Child, DocumentData.Restored)], [definition], [schema]);
        using var test = new InspectorFixture(scene); var view = new EditorInspectorView(test.Model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view); Drain(ui);
        var parts = view.Properties[(DocumentData.Custom, "value")]; var row = view.GenericEditors[(DocumentData.Custom, "value")];
        var input = parts.Input; var content = row.Editor; var nullable = Assert.IsType<CheckBox>(parts.Null);
        Assert.Equal(kind, parts.Property.Kind); Assert.True(row.Empty.IsVisible); Assert.False(content.IsVisible); Assert.True(nullable.IsChecked); Assert.Equal(26, row.Bounds.Height);
        Click(ui, view, nullable); Assert.False(parts.Property.IsNull); Assert.True(content.IsVisible);
        if (kind == PropertyValueKind.Boolean) Click(ui, view, Assert.IsType<CheckBox>(input));
        else { Assert.IsType<TextBox>(input); Enter(ui, view, input, value); }
        Assert.Equal(value, parts.Property.Value); Assert.True(input.IsModified); Assert.True(view.Apply.IsEnabled);
        Click(ui, view, nullable); Assert.True(parts.Property.IsNull); Assert.True(row.Empty.IsVisible); Assert.Equal(value, parts.Property.Value);
        Click(ui, view, nullable); Assert.False(parts.Property.IsNull); Assert.Equal(value, parts.Property.Value); Assert.Same(input, parts.Input); Assert.Same(content, row.Editor);
        Click(ui, view, view.Apply); var state = test.Owner.Document.Read().Content!;
        var stored = state.Resolved.Entities[DocumentData.Child].Components[DocumentData.Custom].Properties["value"].Value;
        Assert.Equal(kind, stored.Kind); Assert.Equal(value, InspectorProperty.Format(stored)); Assert.Equal(1, state.UndoCount); Assert.False(input.IsModified);
        Click(ui, view, view.Undo); Assert.True(parts.Property.IsNull); Assert.True(row.Empty.IsVisible); Assert.False(content.IsVisible);
        Assert.Equal(PropertyValueKind.Null, test.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].Components[DocumentData.Custom].Properties["value"].Value.Kind);
        Assert.False(test.Owner.Document.Read().IsDirty); Assert.Same(input, view.Properties[(DocumentData.Custom, "value")].Input); Assert.Same(row, view.GenericEditors[(DocumentData.Custom, "value")]);
    }

    [Fact]
    public void TemporaryRuntimeAdmission_DisablesInputsAndNullActionWithoutChangingDraftNullResetOrHistory()
    {
        using var test = new InspectorFixture(); var view = new EditorInspectorView(test.Model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view); Drain(ui);
        var number = Assert.IsType<TextBox>(view.Properties[(DocumentData.Custom, "number")].Input);
        var nullable = view.Properties[(DocumentData.Custom, "nullable")]; var nullAction = nullable.Null!;
        Click(ui, view, nullAction); Enter(ui, view, nullable.Input, "stored nullable"); Enter(ui, view, number, "12.5");
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Left, Shift: true)); var caret = number.CaretIndex; var selection = number.SelectionLength;
        var before = test.Owner.Document.Read().Content; var tree = DocumentUiFixture.Tree(view).ToArray();
        test.Owner.RuntimeIdle = false; test.Refresh(); Drain(ui);
        Assert.False(number.IsEnabled); Assert.False(nullable.Input.IsEnabled); Assert.False(nullAction.IsEnabled); Assert.False(view.Apply.IsEnabled);
        Assert.False(nullable.Property.IsNull); Assert.False(nullable.Property.IsReset); Assert.Equal("stored nullable", nullable.Property.Value);
        Assert.Equal("12.5", number.Value); Assert.True(number.IsModified); Assert.True(test.Model.HasDraft); Assert.False(number.IsFocused);
        Assert.Equal(caret, number.CaretIndex); Assert.Equal(selection, number.SelectionLength);
        DocumentUiFixture.Click(ui, nullAction); view.HandleShortcut(new(InputKind.KeyDown, Key: UiKey.Enter, Control: true)); Drain(ui);
        Assert.False(nullable.Property.IsNull); Assert.Equal("stored nullable", nullable.Property.Value); Assert.Same(before, test.Owner.Document.Read().Content); Assert.Equal(0, before!.UndoCount);
        test.Owner.RuntimeIdle = true; test.Refresh(); Drain(ui);
        Assert.True(number.IsEnabled); Assert.True(nullable.Input.IsEnabled); Assert.True(nullAction.IsEnabled); Assert.True(view.Apply.IsEnabled);
        Assert.Equal(tree, DocumentUiFixture.Tree(view)); Assert.Equal("12.5", number.Value); Assert.Equal("stored nullable", nullable.Property.Value); Assert.False(nullable.Property.IsReset);
        Click(ui, view, view.Apply); Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
        Assert.Equal(12.5, test.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].Components[DocumentData.Custom].Properties["number"].Value.GetNumber());
        Assert.Equal("stored nullable", test.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].Components[DocumentData.Custom].Properties["nullable"].Value.GetString());
        Assert.False(test.Model.HasDraft);
    }

    private static void Enter(UiSession ui, EditorInspectorView view, Element input, string value)
    {
        Drain(ui); view.Scroll.BringIntoView(input.Bounds); Drain(ui); ui.Focus(input);
        if (input is RichTextEditor rich) { rich.SelectAll(); rich.InsertText(value); }
        else
        {
            ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
            ui.HandleInput(value.Length == 0 ? new(InputKind.KeyDown, Key: UiKey.Backspace) : new(InputKind.Text, Text: value));
        }
        Drain(ui);
    }
    private static void Click(UiSession ui, EditorInspectorView view, Button button)
    { Drain(ui); view.Scroll.BringIntoView(button.Bounds); Drain(ui); DocumentUiFixture.Click(ui, button); Drain(ui); }
    private static void Drain(UiSession ui) { ui.Update(); ui.Update(); }
}
