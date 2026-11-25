using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis.Painting;

namespace AlloyVulkanTest;

internal static partial class FontTypographyProbe
{
    private static void ConfigureTypographyPresenters(UiPresenters presenters)
    {
        presenters.Register<TextCompositionBackdrop>(static (panel, context) =>
        {
            var bounds = panel.Bounds;
            var half = bounds.Width / 2;
            var (left, right) = CompositionColors;
            context.Fill(new(bounds.X, bounds.Y, half, bounds.Height), new(left.Red, left.Green, left.Blue));
            context.Fill(new(bounds.X + half, bounds.Y, half, bounds.Height), new(right.Red, right.Green, right.Blue));
        });
        presenters.Register<BackdropSplit>(static (split, context) =>
        {
            var bounds = split.Bounds;
            var half = bounds.Width / 2;
            context.Fill(new(bounds.X, bounds.Y, half, bounds.Height), new(0, 0, 0));
            context.Fill(new(bounds.X + half, bounds.Y, half, bounds.Height), new(255, 255, 255));
        });
        presenters.Register<FractionalLines>(static (lines, context) =>
        {
            var bounds = lines.Bounds;
            var color = lines.Foreground;
            context.Fill(new(bounds.X + 4, bounds.Y + 4, 72, 1), color);
            context.Fill(new(bounds.X + 4, bounds.Y + 14.5f, 72, 1), color);
            context.Fill(new(bounds.X + 94, bounds.Y + 4, 1, 24), color);
            context.Fill(new(bounds.X + 110.5f, bounds.Y + 4, 1, 24), color);
            context.Stroke(new(bounds.X + 132, bounds.Y + 5, 42, 20), color, 1);
            context.Stroke(new(bounds.X + 188.5f, bounds.Y + 5.5f, 42, 20), color, 1);
        });
        presenters.Register<IconCatalog>(static (catalog, context) =>
        {
            var foreground = catalog.Foreground;
            var fontFamily = catalog.FontFamily;
            var background = catalog.Theme.Control;
            UiSymbols.Draw(context, UiSymbol.TrueMoon, new(12, 12, 44, 44), foreground);
            context.Text("Argentis · TrueMoon.Tales", 65, 18, foreground, 21, fontFamily);
            context.Text("12 · 16 · 24 px", 66, 44, foreground, 12, fontFamily);
            for (var i = 0; i < IconCatalogLayout.Symbols.Length; i++)
            {
                var (x, y) = IconCatalogLayout.CellOrigin(i);
                context.Fill(new(x, y, 173, 60), background, 6);
                context.Text(IconCatalogLayout.Labels[i], x + 9, y + 5, foreground, 13, fontFamily);
                for (var s = 0; s < IconCatalogLayout.Sizes.Length; s++)
                {
                    var (symbol, bounds) = IconCatalogLayout.Samples[i * IconCatalogLayout.Sizes.Length + s];
                    UiSymbols.Draw(context, symbol, bounds, foreground);
                }
            }
        });
    }
}
