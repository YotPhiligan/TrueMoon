using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.Editing;

/// <summary>Scoped compact inspector appearance; applies to current and future component sections.</summary>
public sealed class EditorInspectorViewStyle : IViewStyle<EditorInspectorView>
{
    internal static Style HintBorder { get; } = new Style().With(UiProperties.Padding, new Thickness(7)).With(UiProperties.BorderWidth, 1f);
    internal static Style HintText { get; } = new Style().With(UiProperties.FontSize, 12f);
    /// <inheritdoc />
    public void Configure(IViewStyleBuilder<EditorInspectorView> styles)
    {
        styles.Root().FontSize(12);
        styles.Role(EditorInspectorView.SectionRole).Background(Color.Transparent).Set(UiProperties.BorderColor, t => t.Border).BorderWidth(1).Padding(new Thickness(6));
        styles.Role(EditorInspectorView.StackRole).Spacing(4);
        styles.Role(EditorInspectorView.RowRole).Spacing(3);
        styles.Role(EditorInspectorView.HeaderRole).FontSize(13).Height(26).Padding(new Thickness(20, 2, 4, 2));
        styles.Role(EditorInspectorView.ActionRole).FontSize(12).Height(26).Padding(new Thickness(4, 2, 4, 2));
        styles.Role(EditorInspectorView.IconRole).Height(26).Set(UiProperties.Width, 26f).Padding(new Thickness(0));
        styles.Role(EditorInspectorView.LabelRole).FontSize(11).Set(UiProperties.Width, 58f);
        styles.Role(EditorInspectorView.InputRole).FontSize(12).Height(26).Padding(new Thickness(4, 2, 4, 2));
        styles.Role(EditorInspectorView.BooleanRole).FontSize(12).Height(26).Padding(new Thickness(24, 2, 2, 2));
        styles.Role(EditorInspectorView.NullRole).FontSize(10).Height(26).Set(UiProperties.Width, 60f).Padding(new Thickness(32, 2, 2, 2));
        styles.Role(EditorInspectorView.StringRole).FontSize(12).Height(70).Padding(new Thickness(4));
        styles.Role(EditorInspectorView.ResetRole).Height(24).Set(UiProperties.Width, 24f).Padding(new Thickness(0));
        styles.Role(EditorInspectorView.MetadataRole).FontSize(10).Foreground(t => t.Disabled);
        styles.Role(EditorInspectorView.ErrorRole).FontSize(11).Foreground(t => t.Error);
        styles.Part(v => v.Apply).Tone(ControlTone.Accent);
        styles.Part(v => v.Error).FontSize(11).Height(72).Foreground(t => t.Error);
        styles.Part(v => v.AddComponent).Height(26);
    }
}
