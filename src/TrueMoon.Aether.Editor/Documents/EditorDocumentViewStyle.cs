using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.Documents;

/// <summary>Scoped appearance for document actions and decisions; contains no model mutations or retained views.</summary>
public sealed class EditorDocumentViewStyle : IViewStyle<EditorDocumentView>
{
    /// <inheritdoc />
    public void Configure(IViewStyleBuilder<EditorDocumentView> styles)
    {
        styles.Root().Padding(10).FontSize(13).BorderWidth(1).Set(UiProperties.CornerRadius, 8f)
            .Set(UiProperties.Backdrop, theme => theme.PanelBackdrop)
            .Background(theme => theme.PanelBackdrop == null ? theme.Surface : Color.Transparent);
        styles.Part(view => view.Actions).Spacing(6);
        styles.Part(view => view.PathInput).Set(UiProperties.Width, 260f);
        styles.Part(view => view.SavePathInput).Set(UiProperties.Width, 360f);
        styles.Part(view => view.ScenesRoot).FontSize(11).Foreground(theme => theme.Disabled);
        styles.Part(view => view.Error).Foreground(theme => theme.Error);
        styles.Part(view => view.Decision).Padding(8).BorderWidth(1).Set(UiProperties.BorderColor, theme => theme.Accent);
        styles.Part(view => view.Choices).Spacing(8);
        styles.Part(view => view.SaveChoice).Tone(ControlTone.Accent);
        styles.Part(view => view.DiscardChoice).Tone(ControlTone.Danger);
    }
}
