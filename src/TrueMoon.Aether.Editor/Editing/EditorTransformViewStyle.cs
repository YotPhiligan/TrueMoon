using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.Editing;

/// <summary>Scoped Transform form appearance; contains no screen state.</summary>
public sealed class EditorTransformViewStyle : IViewStyle<EditorTransformView>
{
    /// <inheritdoc />
    public void Configure(IViewStyleBuilder<EditorTransformView> styles)
    {
        styles.Part(v => v.Form).Spacing(3);
        styles.Role(EditorTransformView.FieldRole).Spacing(4);
        styles.Role(EditorTransformView.LabelRole).Set(UiProperties.Width, 88f).FontSize(12);
        styles.Role(EditorTransformView.InputRole).Set(UiProperties.Width, 132f).Height(26).Padding(new Thickness(4, 2, 4, 2)).FontSize(13);
        styles.Role(EditorTransformView.ErrorRole).Foreground(t => t.Error).FontSize(11);
        styles.Part(v => v.Actions).Spacing(4); styles.Part(v => v.History).Spacing(4); styles.Part(v => v.PreviewActions).Spacing(4);
        styles.Part(v => v.Apply).Tone(ControlTone.Accent);
        styles.Part(v => v.Error).Foreground(t => t.Error).FontSize(12);
        styles.Part(v => v.PreviewStatus).FontSize(11);
    }
}
