using TrueMoon.Aether.Scenes;
using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis;
using static TrueMoon.Aether.Editor.UI.Editing.EditorInspectorView;

namespace TrueMoon.Aether.Editor.UI.Editing;

internal static class InspectorPropertyEditorFactory
{
    internal static Parts Create(IEditorInspectorViewModel model, InspectorProperty property, string component,
        bool showDetails, Action<ScrollViewer> toggleRenderChoices, Action restoreMeshMaterial)
    {
        Element input;
        Element content;
        NumberBox? number = null;
        ScrollViewer? choices = null;
        if (property.IsRenderReference)
        {
            choices = new ScrollViewer { MaxHeight = 134 }.WithRole(RenderChoicesRole);
            choices.IsVisible = false;
            content = input = new Button("Выбрать").WithRole(ActionRole).OnClick(() => toggleRenderChoices(choices));
        }
        else if (property.Kind == PropertyValueKind.Boolean)
            content = input = new CheckBox().WithRole(BooleanRole).BindTwoWay(CheckBox.IsCheckedProperty, property, x => x.BooleanValue);
        else if (property.Kind == PropertyValueKind.String)
            content = input = new RichTextEditor(new() { WordWrap = true }).WithRole(StringRole)
                .BindTwoWay(RichTextEditor.TextProperty, property, x => x.Value);
        else
        {
            number = new NumberBox().WithRole(NumberRole);
            content = number;
            input = ConfigureScalarNumber(model, property, number);
        }
        input.MinWidth = 0; input.MinHeight = 0;
        if (!property.IsRenderReference) input.Bind(UiProperties.Enabled, property, x => x.CanWrite);
        input.ColorRole = property.Kind == PropertyValueKind.Boolean ? EditorColorRole.Boolean :
            property.Kind == PropertyValueKind.String ? EditorColorRole.Text : EditorColorRole.Number;
        input.AccessibleName = component + " " + property.Label;
        var restoreMaterial = property.IsRenderReference && property.Key == "bindings";
        var reset = restoreMaterial
            ? new Button("Вернуть материал меша").WithRole(ActionRole).OnClick(restoreMeshMaterial)
            : Action(UiSymbol.Undo, "Сбросить " + component + " · " + property.Label + " к наследуемому значению или умолчанию").WithRole(ResetRole)
                .Bind(UiProperties.Enabled, property, x => x.CanReset).OnClick(() => model.ResetProperty(property));
        if (restoreMaterial) reset.AccessibleName = reset.Value;
        var nullable = property.IsNullable ? new CheckBox("null").WithRole(NullRole)
            .BindTwoWay(CheckBox.IsCheckedProperty, property, x => x.IsNull)
            .Bind(UiProperties.Enabled, property, x => x.CanChangeNull) : null;
        if (nullable != null) { nullable.ColorRole = input.ColorRole; nullable.AccessibleName = "Нет значения · " + component + " " + property.Key; }
        var source = new Text().WithRole(MetadataRole).BindText(property, x => x.SourceText);
        source.IsVisible = showDetails; reset.IsVisible = restoreMaterial || showDetails && !property.IsRenderReference;
        var row = new FieldRow().WithRole(StackRole);
        var line = new PropertyEditorRow(content, nullable, property.Label).WithRole(FieldRole);
        line.Label.WithRole(VectorLabelRole); line.Empty.WithRole(EmptyValueRole);
        line.Empty.AccessibleName = component + " " + property.Key + ": null";
        line.ValueWidth = property.Kind == PropertyValueKind.Boolean ? 96 : property.Kind == PropertyValueKind.String ? float.NaN : 120;
        line.ShowEmpty(property.IsNull);
        var details = new HStack().WithRole(RowRole);
        if (!restoreMaterial) details.Add(reset);
        details.Add(source);
        details.IsVisible = showDetails;
        row.Add(line);
        var slotPanel = restoreMaterial ? new VStack().WithRole(StackRole) : null;
        if (slotPanel != null) row.Add(slotPanel);
        if (choices != null) row.Add(choices);
        if (restoreMaterial) row.Add(reset);
        row.Add(details);
        var controls = new InspectorPropertyControls(property, row, input, nullable, reset, source)
        {
            SlotPickers = slotPanel == null ? null : (slotPanel, new())
        };
        row.Initialize(controls, line, number, choices);
        return new(controls, row);
    }

    private static TextBox ConfigureScalarNumber(IEditorInspectorViewModel model, InspectorProperty property, NumberBox number)
    {
        number.NumericKind = property.Kind == PropertyValueKind.Integer ? NumericValueKind.Integer : NumericValueKind.Real;
        if (property.Kind == PropertyValueKind.Integer || property.DisplayNumberInDegrees) number.Step = 1;
        if (property.IsLayerMask) { number.Minimum = 0; number.Maximum = uint.MaxValue; }
        var input = number.Editor.WithRole(InputRole).BindTwoWay(TextBox.ValueProperty, property, x => x.Value);
        ConnectNumericGesture(model, number, property);
        return input;
    }

    internal static void ConnectNumericGesture(IEditorInspectorViewModel model, NumberBox box, InspectorProperty property)
    {
        long gestureId = 0;
        box.CanAdjust = () => model.CanAdjustNumeric(property);
        box.IsGestureCurrent = () => model.IsNumericGestureCurrent(property, gestureId);
        box.GestureStarted += () => gestureId = model.BeginNumericGesture(property);
        box.GestureEnded += result => { model.FinishNumericGesture(result, gestureId); gestureId = 0; };
        const string gestureHint = " · перетаскивание: 4 пикселя на шаг; ↑/↓; Shift ×10, Ctrl ×0.1; Esc — отменить жест";
        if (property is TransformField transform) box.Bind(UiProperties.AccessibleName, transform, p => p.NumericGestureHint);
        else box.AccessibleName = property.Label + gestureHint;
    }

    internal sealed record Parts(InspectorPropertyControls Controls, FieldRow Row);
}
