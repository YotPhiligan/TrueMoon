using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.UI;

/// <summary>Scoped header appearance; state and command admission belong to presentation models.</summary>
public sealed class EditorHeaderViewStyle : IViewStyle<EditorHeaderView>
{
    /// <inheritdoc />
    public void Configure(IViewStyleBuilder<EditorHeaderView> styles)
    {
        styles.Root().Set(UiProperties.WindowRegion, WindowRegionRole.Caption);
        styles.Part(view => view.BrandIcon).Foreground(theme => theme.Accent);
        styles.Part(view => view.Caption).FontSize(14).FontWeight(600);
        styles.Role(EditorHeaderView.ToolbarButtonRole).Set(IconButton.IconSizeProperty, 16f)
            .BorderWidth(0);
        styles.Part(view => view.RunButton).Tone(ControlTone.Positive);
        styles.Part(view => view.StopButton).Tone(ControlTone.Danger);
    }
}
