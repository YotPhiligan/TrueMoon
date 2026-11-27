using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.UI.Editing;

/// <summary>Scoped compact inspector appearance; applies to current and future component sections.</summary>
public sealed class EditorInspectorViewStyle : IViewStyle<EditorInspectorView>
{
    private const float ActionFontSize = 12;
    private const float ActionHeight = 26;
    private static readonly Thickness ActionPadding = new(4, 2, 4, 2);
    internal const float MaterialListMaxHeight = 134;
    internal static Style MaterialRowAction { get; } = new Style().With(UiProperties.FontSize, ActionFontSize)
        .With(UiProperties.Height, ActionHeight).With(UiProperties.Padding, ActionPadding);
    internal static Style HintBorder { get; } = new Style().With(UiProperties.Padding, new Thickness(7)).With(UiProperties.BorderWidth, 1f);
    internal static Style HintText { get; } = new Style().With(UiProperties.FontSize, 12f);
    /// <inheritdoc />
    public void Configure(IViewStyleBuilder<EditorInspectorView> styles)
    {
        styles.Root().FontSize(12);
        styles.Role(EditorInspectorView.SectionRole).Background(Color.Transparent).Set(UiProperties.BorderColor, t => t.Border).BorderWidth(1).Padding(new Thickness(6));
        styles.Role(EditorInspectorView.StackRole).Spacing(4);
        styles.Role(EditorInspectorView.RenderChoicesRole).Padding(new Thickness(0, 2, 0, 2));
        styles.Role(EditorInspectorView.RowRole).Spacing(3);
        styles.Role(EditorInspectorView.TransformGroupRole).Spacing(4);
        styles.Role(EditorInspectorView.HeaderRole).FontSize(13).Height(26).Padding(new Thickness(20, 2, 4, 2));
        styles.Role(EditorInspectorView.ActionRole).FontSize(ActionFontSize).Height(ActionHeight).Padding(ActionPadding);
        styles.Role(EditorInspectorView.IconRole).Height(26).Set(UiProperties.Width, 26f).Padding(new Thickness(0));
        styles.Role(EditorInspectorView.LabelRole).FontSize(11).Set(UiProperties.Width, 58f);
        styles.Role(EditorInspectorView.InputRole).FontSize(12).Height(26).Padding(new Thickness(4, 2, 4, 2));
        styles.Role(EditorInspectorView.NumberRole).Set(NumberBox.AdjustmentEnabledProperty, true).Set(NumberBox.StepProperty, .1d);
        styles.Role(EditorInspectorView.VectorRole).Spacing(4).Set(Vector3Editor.LabelWidthProperty, 88f).Set(Vector3Editor.ColumnWidthProperty, 60f);
        styles.Role(EditorInspectorView.VectorLabelRole).FontSize(11);
        styles.Role(EditorInspectorView.FieldRole).Spacing(4).Set(PropertyEditorRow.LabelWidthProperty, 88f).Set(PropertyEditorRow.MinimumValueWidthProperty, 80f);
        styles.Role(EditorInspectorView.EmptyValueRole).FontSize(11).Height(26).Foreground(t => t.Disabled);
        styles.Role(EditorInspectorView.BooleanRole).FontSize(12).Height(26).Padding(new Thickness(28, 2, 4, 2));
        styles.Role(EditorInspectorView.NullRole).FontSize(10).Height(26).Set(UiProperties.Width, 58f).Padding(new Thickness(28, 2, 2, 2));
        styles.Role(EditorInspectorView.StringRole).FontSize(12).Padding(new Thickness(4))
            .Set(RichTextEditor.MinimumContentHeightProperty, 18f).Set(RichTextEditor.MaximumContentHeightProperty, 70f);
        styles.Role(EditorInspectorView.ResetRole).Height(24).Set(UiProperties.Width, 24f).Padding(new Thickness(0));
        styles.Role(EditorInspectorView.MetadataRole).FontSize(10).Foreground(t => t.Disabled);
        styles.Role(EditorInspectorView.ErrorRole).FontSize(11).Foreground(t => t.Error);
        styles.Role(EditorInspectorView.MaterialListRole).Padding(new Thickness(0, 2, 0, 2));
        styles.Role(EditorInspectorView.MaterialStatusRole).FontSize(11).Padding(new Thickness(3))
            .Set(RichTextEditor.MinimumContentHeightProperty, 16f).Set(RichTextEditor.MaximumContentHeightProperty, 64f);
        styles.Role(EditorInspectorView.MaterialNameRole).FontSize(12).Padding(new Thickness(4, 2, 4, 2))
            .Set(RichTextEditor.MinimumContentHeightProperty, 20f).Set(RichTextEditor.MaximumContentHeightProperty, 58f);
        styles.Role(EditorInspectorView.TextureMetadataRole).FontSize(11).Padding(new Thickness(3))
            .Set(RichTextEditor.MinimumContentHeightProperty, 16f).Set(RichTextEditor.MaximumContentHeightProperty, 100f);
        styles.Part(v => v.ApplyPart).Tone(ControlTone.Accent);
        styles.Part(v => v.ErrorPart).FontSize(11).Height(72).Foreground(t => t.Error);
        styles.Part(v => v.AddComponentPart).Height(26);
    }
}
