using TrueMoon.Argentis.Bindings;
using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis;
using static TrueMoon.Aether.Editor.UI.Editing.EditorInspectorView;

namespace TrueMoon.Aether.Editor.UI.Editing;

internal static class InspectorTransformEditorFactory
{
    internal static Parts Create(IDataContext<IEditorInspectorViewModel> context,
        IEditorInspectorViewModel model, InspectorComponent component)
    {
        var editor = new TransformEditor().WithRole(StackRole);
        ConfigureAction(editor.ResetPosition).WithRole(ResetRole).Bind(UiProperties.Enabled, context, p => p.CanResetPosition)
            .OnClick(() => model.ResetTransformGroup(TransformGroup.Position));
        ConfigureAction(editor.ResetRotation).WithRole(ResetRole).Bind(UiProperties.Enabled, context, p => p.CanResetRotation)
            .OnClick(() => model.ResetTransformGroup(TransformGroup.Rotation));
        ConfigureAction(editor.ResetScale).WithRole(ResetRole).Bind(UiProperties.Enabled, context, p => p.CanResetScale)
            .OnClick(() => model.ResetTransformGroup(TransformGroup.Scale));
        editor.Degrees.WithRole(BooleanRole).BindTwoWay(CheckBox.IsCheckedProperty, context, p => p.AnglesInDegrees)
            .Bind(UiProperties.Enabled, context, p => p.CanChangeAngleUnits);
        editor.Rotation.Label.BindText(context, p => p.RotationLabel);
        editor.LinkScale.WithRole(BooleanRole).BindTwoWay(CheckBox.IsCheckedProperty, context, p => p.ScaleLinked)
            .Bind(UiProperties.Enabled, context, p => p.CanLinkScale);
        var details = new VStack().WithRole(StackRole).Configure(panel => panel.IsVisible = false);
        var fields = new List<Field>(9);
        Configure(editor.Position, ["x", "y", "z"]);
        Configure(editor.Rotation, ["pitch", "yaw", "roll"]);
        Configure(editor.Scale, ["sx", "sy", "sz"]);
        return new(editor, details, fields);

        void Configure(Vector3Editor row, string[] keys)
        {
            row.WithRole(VectorRole); row.Label.WithRole(VectorLabelRole);
            var boxes = new[] { row.First, row.Second, row.Third };
            var cells = new[] { row.FirstCell, row.SecondCell, row.ThirdCell };
            for (var i = 0; i < keys.Length; i++)
            {
                var property = (TransformField)component.Properties.Single(p => p.Key == keys[i]);
                boxes[i].WithRole(NumberRole);
                boxes[i].Minimum = property.Key is "sx" or "sy" or "sz" ? float.Epsilon : -float.MaxValue;
                boxes[i].Maximum = float.MaxValue;
                if (property.IsAngle)
                {
                    boxes[i].Bind(NumberBox.StepProperty, context, p => p.RotationStep)
                        .Bind(NumberBox.MinimumProperty, context, p => p.RotationMinimum)
                        .Bind(NumberBox.MaximumProperty, context, p => p.RotationMaximum);
                }
                else if (property.Key is "sx" or "sy" or "sz") boxes[i].Step = .01;
                InspectorPropertyEditorFactory.ConnectNumericGesture(model, boxes[i], property);
                var input = boxes[i].Editor.WithRole(InputRole)
                    .SemanticId($"editor.inspector.transform.{component.Id.Value:N}.{property.Key}")
                    .BindTwoWay(TextBox.ValueProperty, property, p => p.DisplayValue)
                    .Bind(UiProperties.AccessibleName, property, p => p.InputAccessibleName)
                    .Bind(UiProperties.Enabled, property, p => p.CanWrite);
                input.MinHeight = 0;
                boxes[i].Prefix.WithRole(VectorLabelRole);
                var reset = Action(UiSymbol.Undo, "Сбросить " + component.Title + " · " + property.Label + " к наследуемому значению или умолчанию")
                    .Bind(UiProperties.AccessibleName, property, p => p.ResetAccessibleName)
                    .WithRole(ResetRole).Bind(UiProperties.Enabled, property, p => p.CanReset).OnClick(() => model.ResetProperty(property));
                var source = new Text().WithRole(MetadataRole).BindText(property, p => p.SourceText);
                reset.IsVisible = false; source.IsVisible = false;
                var cell = cells[i].WithRole(StackRole);
                details.Add(new HStack().WithRole(RowRole).WithChildren(reset,
                    new Text().WithRole(LabelRole).BindText(property, p => p.DisplayLabel).Configure(x => x.VerticalAlignment = LayoutAlignment.Center), source));
                fields.Add(new(new(property, cell, input, null, reset, source), boxes[i]));
            }
        }
    }

    internal sealed record Parts(TransformEditor Editor, VStack Details, IReadOnlyList<Field> Fields);
    internal sealed record Field(InspectorPropertyControls Controls, NumberBox Number);
}
