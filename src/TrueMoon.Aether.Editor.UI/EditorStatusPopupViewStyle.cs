using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.UI;

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
        styles.Part(view => view.ReimportActions).Spacing(4);
        styles.Part(view => view.ReadModelCatalog).FontSize(12);
        styles.Part(view => view.ModelCatalogRows).Spacing(2);
        styles.Role(Reimport).FontSize(12);
        styles.Part(view => view.Error).Height(160).FontSize(12).BorderWidth(0).Background(Color.Transparent).Foreground(theme => theme.Error);
    }
    internal static ViewStyleRole<Text> Row { get; } = new("StatusDetailsRow");
    internal static ViewStyleRole<Button> Reimport { get; } = new("ModelReimportAction");
    internal static ViewStyleRole<CheckBox> CatalogChoice { get; } = new("ProjectModelCatalogChoice");
}
