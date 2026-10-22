using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor;

/// <summary>Editor appearance expressed as scoped rules, reusable between view instances.</summary>
public sealed class EditorViewStyle : IViewStyle<EditorView>
{
    public void Configure(IViewStyleBuilder<EditorView> styles)
    {
        styles.Part(view => view.Toolbar).Padding(new Thickness(10, 8, 10, 8)).Spacing(6);
        styles.Part(view => view.EntityList).Spacing(6);
        styles.Role(EditorView.EntityButtonRole).Height(34);
        styles.Role(EditorView.PaneRole).Padding(10);
        styles.Role(EditorView.PaneContentRole).Spacing(10);
        styles.Role(EditorView.PaneItemsRole).Spacing(6);
        styles.Role(EditorView.PaneTitleRole).FontSize(13);
        styles.Role(EditorView.PropertyRowsRole).Spacing(2);
        styles.Role(EditorView.PropertyRowRole).Height(36);
        styles.Role(EditorView.PropertyNameRole).FontSize(14).Padding(8);
        styles.Part(view => view.SceneTitle).FontSize(14);
        styles.Part(view => view.InspectorHint).FontSize(12);
        styles.Part(view => view.ViewportFrame).Padding(1).BorderWidth(1);
        styles.Part(view => view.ViewportTitle).FontSize(12).Padding(8).Height(30);
        styles.Part(view => view.Error).Foreground(theme => theme.Error);
        styles.Part(view => view.Commands[EditorCommand.Start]).Tone(ControlTone.Accent);
        styles.Part(view => view.Commands[EditorCommand.Play]).Tone(ControlTone.Positive);
        styles.Part(view => view.Commands[EditorCommand.Stop]).Tone(ControlTone.Danger);
    }
}