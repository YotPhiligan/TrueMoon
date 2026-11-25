using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;

namespace AlloyVulkanTest;

internal static partial class FontTypographyProbe
{
    internal static bool UseIconCatalogScene { get; set; }

    private static Capture ExerciseIconCatalog(IRenderBackend backend, UiRenderTarget target, UiViewport viewport,
        bool light, Func<UiSession, byte[]> read)
    {
        var theme = light ? Theme.Light : Theme.Dark;
        var gallery = new IconCatalog { Background = theme.Surface };
        using var ui = UiSession.Create(gallery, backend, target, viewport, null, new UiDispatchOptions(), configurePresenters: ConfigureTypographyPresenters);
        ui.SetTheme(theme);
        Require(ui.Update() && !ui.Update(), "The icon catalog did not settle at idle.");
        var pixels = read(ui);
        var coverage = IconCatalogLayout.Samples.Select(specimen =>
        {
            var (symbol, bounds) = specimen;
            var ink = 0;
            var rim = 0;
            var silver = 0;
            for (var y = (int)(bounds.Y * viewport.Scale); y < (bounds.Y + bounds.Height) * viewport.Scale; y++)
            for (var x = (int)(bounds.X * viewport.Scale); x < (bounds.X + bounds.Width) * viewport.Scale; x++)
            {
                var index = (y * viewport.Width + x) * 4;
                var color = new Color(pixels[index], pixels[index + 1], pixels[index + 2], pixels[index + 3]);
                if (Math.Abs(color.R - theme.Control.R) + Math.Abs(color.G - theme.Control.G) + Math.Abs(color.B - theme.Control.B) > 32)
                    ink++;
                if (color == new Color(113, 102, 124)) rim++;
                if (color == new Color(244, 240, 245)) silver++;
            }
            Require(ink >= 5, $"Missing contour for {symbol} at {bounds.Width}px, scale {viewport.Scale}.");
            if (symbol == UiSymbol.TrueMoon && bounds.Width == 24)
                Require(rim > 0 && silver > 0, "The Argentis mark lost its colored rim or silver fill.");
            return new { Symbol = symbol.ToString(), Size = bounds.Width, Ink = ink };
        }).ToArray();
        return new(IconCatalogLayout.Samples.Select(specimen => specimen.Bounds).ToArray(), pixels, IconCoverage: coverage);
    }

    private sealed class IconCatalog : Element;

    private static class IconCatalogLayout
    {
        internal static readonly UiSymbol[] Symbols = Enum.GetValues<UiSymbol>();
        internal static readonly float[] Sizes = [12, 16, 24];
        internal static readonly string[] Labels = Array.ConvertAll(Symbols, static symbol => symbol.ToString());
        internal static readonly (UiSymbol Symbol, Rect Bounds)[] Samples = CreateSamples();

        internal static (int X, int Y) CellOrigin(int index) => (12 + index % 4 * 181, 76 + index / 4 * 68);

        private static (UiSymbol Symbol, Rect Bounds)[] CreateSamples()
        {
            var result = new (UiSymbol, Rect)[Symbols.Length * Sizes.Length];
            for (var i = 0; i < Symbols.Length; i++)
            {
                var (x, y) = CellOrigin(i);
                for (var s = 0; s < Sizes.Length; s++)
                {
                    var size = Sizes[s];
                    result[i * Sizes.Length + s] = (Symbols[i], new Rect(x + 28 + 56 * s - size / 2, y + 40 - size / 2, size, size));
                }
            }
            return result;
        }
    }
}
