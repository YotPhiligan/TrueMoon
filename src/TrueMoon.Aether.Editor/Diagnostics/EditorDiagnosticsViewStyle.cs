using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.Diagnostics;

/// <summary>Scoped console appearance; models and commands remain independent of the theme.</summary>
public sealed class EditorDiagnosticsViewStyle : IViewStyle<EditorDiagnosticsView>
{
    public void Configure(IViewStyleBuilder<EditorDiagnosticsView> styles)
    {
        styles.Root().Padding(8).BorderWidth(1).Background(theme => theme.Surface).FontSize(12);
        styles.Role(EditorDiagnosticsView.RowRole).Spacing(6);
        styles.Role(EditorDiagnosticsView.FilterRole).Set(UiProperties.Width, 190f).Height(26).Padding(new Thickness(4, 2, 4, 2)).FontSize(12);
        styles.Role(EditorDiagnosticsView.CommandRole).Height(26).Padding(new Thickness(6, 2, 6, 2)).FontSize(12);
        styles.Role(EditorDiagnosticsView.LabelRole).FontSize(12);
        styles.Part(view => view.Actions).Height(28);
        styles.Part(view => view.Summary).FontSize(11);
        styles.Part(view => view.Details).FontSize(12).BorderWidth(1);
        styles.Part(view => view.Events).FontSize(12);
        styles.Part(view => view.ExportPath).Set(UiProperties.Width, 640f).Height(26).Padding(new Thickness(4, 2, 4, 2)).FontSize(12);
        styles.Part(view => view.ExportStatus).FontSize(11);
    }
}
