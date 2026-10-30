using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.Hierarchy;

/// <summary>Hierarchy-specific scoped appearance; no commands or model mutation.</summary>
public sealed class EditorHierarchyViewStyle : IViewStyle<EditorHierarchyView>
{
    /// <inheritdoc />
    public void Configure(IViewStyleBuilder<EditorHierarchyView> styles)
    {
        styles.Root().Padding(8).BorderWidth(1).Set(UiProperties.CornerRadius, 8f).Set(UiProperties.Backdrop, theme => theme.PanelBackdrop)
            .Background(theme => theme.PanelBackdrop == null ? theme.Surface : Color.Transparent);
        styles.Part(view => view.Layout).Set(UiProperties.Spacing, 4f);
        styles.Part(view => view.Title).FontSize(13);
        styles.Part(view => view.Caption).FontSize(11).Height(18).Foreground(theme => theme.Disabled);
        styles.Part(view => view.Hint).FontSize(10).Height(18).Foreground(theme => theme.Disabled);
        styles.Part(view => view.RootButton).FontSize(11).Height(24).Padding(new Thickness(4));
        styles.Part(view => view.Actions).Set(UiProperties.Spacing, 4f);
        styles.Role(EditorHierarchyView.ActionRole).Set(UiProperties.Width, 26f).Height(26).Padding(0);
        styles.Part(view => view.SearchRow).Set(UiProperties.Spacing, 4f);
        styles.Part(view => view.Search).FontSize(11).Set(UiProperties.Width, 156f).Height(26).Padding(3);
        styles.Part(view => view.FindButton).FontSize(11).Set(UiProperties.Width, 52f).Height(26).Padding(3);
        styles.Part(view => view.Tree).FontSize(12).Set(TreeView.RowHeightProperty, 24f).Set(TreeView.IndentProperty, 16f);
        styles.Part(view => view.NameEditor).FontSize(12).Padding(new Thickness(2, 0, 2, 0)).BorderWidth(1);
        styles.Part(view => view.Error).FontSize(11).Height(64).Padding(4).BorderWidth(0)
            .Background(Color.Transparent).Foreground(theme => theme.Error);
    }
}
