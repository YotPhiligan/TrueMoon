using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor;

/// <summary>Scoped popup geometry, typography and material.</summary>
public sealed class EditorStatusPopupViewStyle : IViewStyle<EditorStatusPopupView>
{
    /// <inheritdoc />
    public void Configure(IViewStyleBuilder<EditorStatusPopupView> styles)
    {
        styles.Root().Set(UiProperties.Width, 520f).Padding(10).BorderWidth(1);
        styles.Part(view => view.Body).Spacing(8);
        styles.Part(view => view.Title).FontSize(14);
        // Display rows stay compact; the readonly diagnostic keeps its own selection/scroll.
        styles.Role(Row).FontSize(12);
        styles.Part(view => view.PreviewDetails).FontSize(12);
        styles.Part(view => view.StartPreview).FontSize(12).Tone(ControlTone.Accent);
        styles.Part(view => view.StopPreview).FontSize(12);
        styles.Part(view => view.PreviewActions).Spacing(8);
        styles.Part(view => view.Error).Height(160).FontSize(12).BorderWidth(0).Background(Color.Transparent).Foreground(theme => theme.Error);
    }
    internal static ViewStyleRole<Text> Row { get; } = new("StatusDetailsRow");
}
