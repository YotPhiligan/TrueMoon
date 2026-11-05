using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Scenes;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Inspector;

public sealed class CompactInspectorTests
{
    [Fact]
    public void BuiltInTransform_UsesThreeCompactRowsWithExactStablePropertyAddressesAndAxisParts()
    {
        using var test = new InspectorFixture(); var view = new EditorInspectorView(test.Model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view); ui.Update();
        var transform = Assert.Single(view.TransformEditors); Assert.Equal(DocumentData.Transform, transform.Key);
        var editor = transform.Value;
        Assert.Equal(new Element[] { editor.Position, editor.Rotation, editor.Scale, editor.LinkScale }, editor.Children);
        Assert.Equal(3, editor.Children.OfType<Vector3Editor>().Count());
        Assert.Equal(new[] { "Позиция", "Поворот, рад", "Масштаб" }, new[] { editor.Position.Label.Value, editor.Rotation.Label.Value, editor.Scale.Label.Value });
        Check(editor.Position, ["x", "y", "z"], ["X", "Y", "Z"]);
        Check(editor.Rotation, ["pitch", "yaw", "roll"], ["P", "Y", "R"]);
        Check(editor.Scale, ["sx", "sy", "sz"], ["X", "Y", "Z"]);
        Assert.Equal(116, editor.Bounds.Height);
        Assert.Equal(editor.Position.Bounds.Height + editor.Rotation.Bounds.Height + editor.Scale.Bounds.Height + editor.LinkScale.Bounds.Height + editor.Spacing * 3, editor.Bounds.Height);
        Assert.All(view.Components.Where(pair => pair.Key != DocumentData.Transform), pair => Assert.Empty(DocumentUiFixture.Tree(pair.Value.Body).OfType<Vector3Editor>()));
        Assert.IsType<TextBox>(view.Properties[(DocumentData.Custom, "number")].Input);
        Assert.Equal(EditorColorRole.Number, view.Properties[(DocumentData.Custom, "number")].Input.ColorRole);
        Assert.Equal(EditorColorRole.Boolean, view.Properties[(DocumentData.Custom, "flag")].Input.ColorRole);
        Assert.Equal(EditorColorRole.Text, view.Properties[(DocumentData.Custom, "text")].Input.ColorRole);
        Assert.False(test.Model.HasDraft); Assert.Equal(0, test.Owner.Document.Read().Content!.UndoCount);

        void Check(Vector3Editor row, string[] keys, string[] prefixes)
        {
            var boxes = new[] { row.First, row.Second, row.Third }; var cells = new[] { row.FirstCell, row.SecondCell, row.ThirdCell };
            for (var index = 0; index < 3; index++)
            {
                var property = view.Properties[(DocumentData.Transform, keys[index])];
                Assert.Same(cells[index], property.Row); Assert.Same(boxes[index].Editor, property.Input);
                Assert.Same(test.Property(DocumentData.Transform, keys[index]), property.Property);
                Assert.Same(boxes[index], cells[index].Children[0]); Assert.Same(boxes[index], property.Input.Parent);
                Assert.Equal(prefixes[index], boxes[index].Prefix.Value); Assert.Equal((EditorColorRole)((int)EditorColorRole.AxisX + index), property.Input.ColorRole);
                Assert.Equal(test.Property(DocumentData.Transform, keys[index]).Value, boxes[index].Editor.Value);
                Assert.Equal(row.FirstCell.Bounds.Width, cells[index].Bounds.Width, 3);
                Assert.True(float.IsNaN(row.Label.Width)); Assert.True(float.IsNaN(boxes[index].Prefix.Width));
            }
        }
    }

    [Fact]
    public void UnknownTypeWithTransformKeys_UsesGenericScalarFallbackInsteadOfSpecializedRows()
    {
        var scene = TransformLikeScene(DocumentData.CustomType, 1);
        using var test = new InspectorFixture(scene); var view = new EditorInspectorView(test.Model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view); ui.Update();
        Assert.Empty(view.TransformEditors); Assert.Equal(9, view.Properties.Count);
        Assert.Empty(DocumentUiFixture.Tree(view).OfType<Vector3Editor>());
        Assert.All(scene.Schemas.Single().Properties.Keys, key =>
        {
            var field = view.Properties[(DocumentData.Transform, key)]; Assert.IsType<TextBox>(field.Input);
            Assert.Same(test.Property(DocumentData.Transform, key), field.Property); Assert.False(field.Reset.IsVisible);
        });
        Assert.Equal(0, test.Owner.Document.Read().Content!.UndoCount); Assert.False(test.Model.HasDraft);
    }

    [Fact]
    public void UnsupportedBuiltInVersion_FailsAdmissionWithoutPublishingComponentControlsOrSelection()
    {
        using var test = new InspectorFixture(TransformLikeScene(BuiltInComponents.TransformId, 2));
        var view = new EditorInspectorView(test.Model, new EditorInspectorViewStyle()); using var ui = InspectorFixture.Ui(view); ui.Update();
        var state = test.Owner.Document.Read();
        Assert.Null(state.Content); Assert.IsType<InvalidDataException>(state.Error);
        Assert.Empty(view.TransformEditors); Assert.Empty(view.Properties); Assert.Empty(view.Components);
        Assert.False(test.Model.HasSelection); Assert.True(view.Empty.IsVisible); Assert.False(view.Apply.IsEnabled);
        Assert.False(test.Owner.Commands.Read().Closed); Assert.Empty(test.Owner.Authoring.Products);
    }

    [Fact]
    public void Details_ArePerComponentCollapsedAndRevealRetainedFullWidthResetProvenanceAndMetadata()
    {
        using var test = new InspectorFixture(); var view = new EditorInspectorView(test.Model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view); ui.Update();
        Assert.Equal(view.Components.Keys.OrderBy(id => id.Value), view.DetailsButtons.Keys.OrderBy(id => id.Value));
        Assert.All(view.Properties.Values, field => { Assert.False(field.Source.IsVisible); Assert.False(field.Reset.IsVisible); });
        Assert.All(view.Components.Values, section => Assert.False(section.Metadata.IsVisible));
        var editor = view.TransformEditors[DocumentData.Transform]; var field = view.Properties[(DocumentData.Transform, "x")];
        var input = field.Input; var source = field.Source; var reset = field.Reset;
        Click(ui, view, view.DetailsButtons[DocumentData.Custom]);
        Assert.True(view.Components[DocumentData.Custom].Metadata.IsVisible); Assert.False(view.Components[DocumentData.Transform].Metadata.IsVisible);
        Assert.True(view.Properties[(DocumentData.Custom, "number")].Source.IsVisible); Assert.False(source.IsVisible);
        Click(ui, view, view.DetailsButtons[DocumentData.Transform]);
        Assert.True(source.IsVisible); Assert.True(reset.IsVisible); Assert.True(view.Components[DocumentData.Transform].Metadata.IsVisible);
        Assert.Equal(ControlTone.Accent, view.DetailsButtons[DocumentData.Transform].Tone);
        Assert.Equal(view.Components[DocumentData.Transform].Body.Bounds.Width, source.Parent!.Parent!.Bounds.Width, 3);
        Assert.DoesNotContain(source, DocumentUiFixture.Tree(field.Row)); Assert.DoesNotContain(reset, DocumentUiFixture.Tree(field.Row));
        Assert.Equal(field.Property.SourceText, source.Value); Assert.Contains("schema 1", view.Components[DocumentData.Transform].Metadata.AccessibleName);
        Assert.Equal(24, reset.Bounds.Width); Assert.Equal(24, reset.Bounds.Height);
        Click(ui, view, view.DetailsButtons[DocumentData.Transform]);
        Assert.False(source.IsVisible); Assert.False(reset.IsVisible); Assert.False(view.Components[DocumentData.Transform].Metadata.IsVisible);
        Assert.True(view.Components[DocumentData.Custom].Metadata.IsVisible);
        Assert.Same(input, view.Properties[(DocumentData.Transform, "x")].Input); Assert.Same(source, field.Source); Assert.Same(reset, field.Reset);
        Assert.Same(editor, view.TransformEditors[DocumentData.Transform]); Assert.False(test.Model.HasDraft);
    }

    [Theory]
    [InlineData("")] [InlineData("-")] [InlineData("1e-")] [InlineData("NaN")] [InlineData("Infinity")]
    public void InvalidAxisDraft_RemainsExactAndShowsCompleteDiagnosticsWhileDetailsStayCollapsed(string draft)
    {
        using var test = new InspectorFixture(); var view = new EditorInspectorView(test.Model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view, width: 260); ui.Update();
        var parts = view.Properties[(DocumentData.Transform, "x")]; var input = Assert.IsType<TextBox>(parts.Input);
        var before = test.Owner.Document.Read().Content;
        Enter(ui, view, input, draft);
        var scale = view.Properties[(DocumentData.Transform, "sx")];
        Enter(ui, view, Assert.IsType<TextBox>(scale.Input), "0");
        Assert.Equal(draft, input.Value); Assert.Equal(draft, parts.Property.Value);
        Assert.True(input.IsModified); Assert.True(input.HasError); Assert.True(parts.Property.HasError);
        var error = Assert.Single(DocumentUiFixture.Tree(parts.Row).OfType<Text>(), text => text.AccessibleName == parts.Property.Error);
        Assert.Equal("Ошибка", error.Value); Assert.Equal(parts.Property.Error, error.AccessibleName);
        Assert.True(error.IsVisible); Assert.Equal(view.Theme.Error, error.Foreground);
        Assert.True(view.Error.IsVisible); Assert.True(view.Error.Options.IsReadOnly); Assert.True(view.Error.Options.WordWrap);
        Assert.Contains(parts.Property.Error, view.Error.Text); Assert.Contains(parts.Property.Label, view.Error.Text);
        Assert.True(scale.Property.HasError); Assert.Contains(scale.Property.Error, view.Error.Text);
        Assert.Equal("Transform · X: Введите конечное число с точкой\nTransform · Scale X: Масштаб должен быть > 0", view.Error.Text);
        Assert.Equal(test.Model.DiagnosticsText, view.Error.Text); Assert.True(test.Model.HasDiagnostics); Assert.False(test.Model.HasError); Assert.Equal("", test.Model.ErrorText);
        Assert.False(parts.Source.IsVisible); Assert.False(parts.Reset.IsVisible); Assert.False(view.Components[DocumentData.Transform].Metadata.IsVisible);
        Assert.False(view.Apply.IsEnabled); Assert.True(view.Cancel.IsEnabled);
        Assert.True(view.HandleShortcut(new(InputKind.KeyDown, Key: UiKey.Enter, Control: true))); ui.Update();
        Assert.Same(before, test.Owner.Document.Read().Content); Assert.Equal(0, before!.UndoCount);
        Assert.True(view.HandleShortcut(new(InputKind.KeyDown, Key: UiKey.Escape))); Drain(ui);
        Assert.Equal("4", input.Value); Assert.False(input.HasError); Assert.False(input.IsModified); Assert.False(error.IsVisible);
        Assert.Equal("1", Assert.IsType<TextBox>(scale.Input).Value); Assert.False(scale.Input.IsModified); Assert.False(scale.Input.HasError);
        Assert.False(view.Error.IsVisible); Assert.False(test.Model.HasDiagnostics); Assert.False(test.Model.HasDraft);
        Assert.Same(input, view.TransformEditors[DocumentData.Transform].Position.First.Editor);
    }

    [Fact]
    public void GroupedDrafts_ApplyAsOneTransactionUndoRedoAndResetPreserveRetainedTreeAndFocus()
    {
        using var test = new InspectorFixture(); var view = new EditorInspectorView(test.Model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view); ui.Update();
        var transform = view.TransformEditors[DocumentData.Transform]; var x = transform.Position.First.Editor;
        var scale = transform.Scale.Third.Editor; var rotation = transform.Rotation.Second.Editor;
        var tree = DocumentUiFixture.Tree(view).ToArray();
        Enter(ui, view, x, "12.5"); Enter(ui, view, rotation, "0.25"); Enter(ui, view, scale, "2.5");
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Left, Shift: true));
        var selection = scale.SelectionStart; var length = scale.SelectionLength;
        ui.SetTheme(Theme.Light); view.WithViewStyle(new EditorInspectorViewStyle()); ui.Resize(new(260, 800)); Drain(ui);
        Assert.Equal(tree, DocumentUiFixture.Tree(view)); Assert.True(scale.IsFocused);
        Assert.Equal(selection, scale.SelectionStart); Assert.Equal(length, scale.SelectionLength); Assert.Equal("2.5", scale.Value);
        Assert.Equal(Theme.Light.AxisZ, transform.Scale.Third.Prefix.Foreground);
        Assert.True(transform.Position.Label.Bounds.Y + transform.Position.Label.Bounds.Height < transform.Position.FirstCell.Bounds.Y);
        Assert.True(view.Apply.IsEnabled); Assert.False(test.Owner.Document.Read().IsDirty);
        Assert.True(view.HandleShortcut(new(InputKind.KeyDown, Key: UiKey.Enter, Control: true))); Drain(ui);
        var state = test.Owner.Document.Read().Content!; var committed = state.Resolved.Entities[DocumentData.Child].Components[DocumentData.Transform];
        Assert.Equal(12.5, committed.Properties["x"].Value.GetNumber()); Assert.Equal(.25, committed.Properties["yaw"].Value.GetNumber()); Assert.Equal(2.5, committed.Properties["sz"].Value.GetNumber());
        Assert.Equal(1, state.UndoCount); Assert.False(test.Model.HasDraft); Assert.False(x.IsModified); Assert.True(scale.IsFocused);
        ui.Focus(view.Undo); Assert.True(view.HandleShortcut(new(InputKind.KeyDown, Key: UiKey.Z, Control: true))); Drain(ui);
        Assert.Equal("4", x.Value); Assert.Equal("0", rotation.Value); Assert.Equal("1", scale.Value); Assert.False(test.Owner.Document.Read().IsDirty);
        Assert.True(view.HandleShortcut(new(InputKind.KeyDown, Key: UiKey.Y, Control: true))); Drain(ui);
        Assert.Equal("12.5", x.Value); Assert.Equal("0.25", rotation.Value); Assert.Equal("2.5", scale.Value); Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
        Click(ui, view, view.DetailsButtons[DocumentData.Transform]); Click(ui, view, view.Properties[(DocumentData.Transform, "x")].Reset);
        Assert.True(test.Property(DocumentData.Transform, "x").IsReset); Assert.True(x.IsModified); Assert.False(x.IsEnabled);
        Assert.True(view.HandleShortcut(new(InputKind.KeyDown, Key: UiKey.Enter, Control: true))); Drain(ui);
        Assert.Equal("2", x.Value); Assert.False(x.IsModified); Assert.True(x.IsEnabled); Assert.Equal(2, test.Owner.Document.Read().Content!.UndoCount);
        Assert.Equal(tree, DocumentUiFixture.Tree(view)); Assert.Same(transform, view.TransformEditors[DocumentData.Transform]);
    }

    [Fact]
    public void DetachDispose_ReleasesGroupedBindingsAndOwnedPartsWithoutOwningBorrowedModel()
    {
        using var test = new InspectorFixture(); var view = new EditorInspectorView(test.Model, new EditorInspectorViewStyle());
        var parent = new Panel().WithChildren(view); using var ui = InspectorFixture.Ui(parent); ui.Update();
        var editor = view.TransformEditors[DocumentData.Transform]; var x = editor.Position.First.Editor; var tree = DocumentUiFixture.Tree(view).ToArray();
        parent.Items.Remove(view); ui.Update(); test.Property(DocumentData.Transform, "x").Value = "11";
        Assert.Equal("4", x.Value); Assert.False(view.IsDisposed); Assert.False(x.IsAttached);
        parent.Items.Add(view); Drain(ui); Assert.Equal("11", x.Value); Assert.True(x.IsModified);
        parent.Items.Remove(view); view.Dispose(); view.Dispose(); Drain(ui);
        Assert.All(tree, node => Assert.True(node.IsDisposed)); Assert.False(parent.IsDisposed);
        test.Property(DocumentData.Transform, "x").Value = "22"; test.Model.Apply();
        Assert.Equal(22, DocumentData.X(test.Owner.Document.Read().Content!)); Assert.Equal("11", x.Value);
        Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount); Assert.Equal(0, test.Owner.Authoring.Products[0].Disposals);
        Assert.False(test.Owner.Commands.Read().Closed); Assert.False(test.Preview.Read().Busy); Assert.False(test.Preview.Read().Active);
    }

    private static void Enter(UiSession ui, EditorInspectorView view, TextBox input, string value)
    {
        Drain(ui); view.Scroll.BringIntoView(input.Bounds); Drain(ui); ui.Focus(input);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
        ui.HandleInput(value.Length == 0 ? new(InputKind.KeyDown, Key: UiKey.Backspace) : new(InputKind.Text, Text: value)); Drain(ui);
    }
    private static void Click(UiSession ui, EditorInspectorView view, Button button)
    { Drain(ui); view.Scroll.BringIntoView(button.Bounds); Drain(ui); DocumentUiFixture.Click(ui, button); Drain(ui); }
    private static void Drain(UiSession ui) { ui.Update(); ui.Update(); }
    private static SceneDocument TransformLikeScene(ComponentTypeId type, int version)
    {
        var original = BuiltInComponents.Schemas.Single(schema => schema.TypeId == BuiltInComponents.TransformId);
        var schema = new ComponentSchema(type, version, original.Properties.Values);
        var definition = new EntityDefinition(DocumentData.Restored, new(addedComponents: [new(DocumentData.Transform, type, version)]));
        return new(new(DocumentData.Id(100)), 7, [new(DocumentData.Child, DocumentData.Restored)], [definition], [schema]);
    }
}
