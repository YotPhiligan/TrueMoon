using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor;

/// <summary>Compact status appearance; independent of presentation commands.</summary>
public sealed class EditorStatusBarViewStyle : IViewStyle<EditorStatusBarView>
{
    /// <inheritdoc />
    public void Configure(IViewStyleBuilder<EditorStatusBarView> styles)
    {
        styles.Root().Height(28).Background(theme => theme.Control);
        styles.Part(view => view.Bar).Padding(new Thickness(8, 0, 24, 0));
        styles.Part(view => view.SummaryButton).FontSize(12);
        styles.Part(view => view.CancelButton).FontSize(12);
        styles.Part(view => view.Entities).FontSize(12).Foreground(theme => theme.Disabled);
        styles.Part(view => view.PreviewButton).FontSize(12);
        styles.Part(view => view.DetailsButton).Set(UiProperties.Width, 24f).Height(24);
    }
    internal static Color StatusColor(Theme theme, EditorStatusKind kind) => kind switch
    {
        EditorStatusKind.Error => theme.Error,
        EditorStatusKind.Waiting or EditorStatusKind.Paused => theme.Focus,
        EditorStatusKind.Busy or EditorStatusKind.Running => theme.Accent,
        _ => theme.Foreground
    };
}
