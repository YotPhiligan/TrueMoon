using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.UI;

/// <summary>Editor appearance expressed as scoped rules, reusable between view instances.</summary>
public sealed class EditorViewStyle : IViewStyle<EditorView>
{
    public void Configure(IViewStyleBuilder<EditorView> styles)
    {
        styles.Role(EditorViewRoles.RuntimeToolbar).Padding(new Thickness(10, 8, 10, 8)).Spacing(6);
        styles.Role(EditorView.EntityListRole).Spacing(6);
        styles.Role(EditorView.EntityButtonRole).Height(34);
        styles.Role(EditorView.PaneRole).Padding(10);
        styles.Role(EditorView.IslandRole).Padding(8);
        styles.Role(EditorView.IslandSurfaceRole).Set(UiProperties.CornerRadius, 8f).BorderWidth(1)
            .Set(UiProperties.Backdrop, theme => theme.PanelBackdrop)
            .Set(UiProperties.Shadow, theme => theme.PanelShadow)
            .Background(theme => theme.PanelBackdrop == null ? theme.Surface : Color.Transparent);
        styles.Role(EditorView.PaneContentRole).Spacing(10);
        styles.Role(EditorView.PaneItemsRole).Spacing(6);
        styles.Role(EditorView.PaneTitleRole).FontSize(13).FontWeight(600);
        styles.Role(EditorView.PropertyRowsRole).Spacing(2);
        styles.Role(EditorView.PropertyRowRole).Height(36);
        styles.Role(EditorView.PropertyNameRole).FontSize(14).Padding(8);
        styles.Role(EditorView.SceneCaptionRole).FontSize(14).FontWeight(600);
        styles.Role(EditorViewRoles.LegacyInspectorHint).FontSize(12);
        styles.Part(view => view.ViewportFrame).Padding(1).BorderWidth(1);
        styles.Part(view => view.ViewportTitle).FontSize(12).FontWeight(600).Padding(8).Height(30);
        styles.Part(view => view.Error).Foreground(theme => theme.Error);
        styles.Role(EditorViewRoles.StartCommand).Tone(ControlTone.Accent);
        styles.Role(EditorViewRoles.PlayCommand).Tone(ControlTone.Positive);
        styles.Role(EditorViewRoles.StopCommand).Tone(ControlTone.Danger);
    }
}
