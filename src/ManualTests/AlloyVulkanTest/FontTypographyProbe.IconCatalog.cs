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
        using var ui = UiSession.Create(gallery, backend, target, viewport);
        ui.SetTheme(theme);
        Require(ui.Update() && !ui.Update(), "The icon catalog did not settle at idle.");
        var pixels = read(ui);
        var coverage = gallery.Specimens.Select(specimen =>
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
        return new(gallery.Specimens.Select(specimen => specimen.Bounds).ToArray(), pixels, IconCoverage: coverage);
    }

    private sealed class IconCatalog : Element
    {
        private static readonly UiSymbol[] Symbols = Enum.GetValues<UiSymbol>();
        private static readonly float[] Sizes = [12, 16, 24];
        internal (UiSymbol Symbol, Rect Bounds)[] Specimens { get; } = new (UiSymbol, Rect)[Symbols.Length * Sizes.Length];

        protected override void DrawCore(IDrawingContext context)
        {
            UiSymbols.Draw(context, UiSymbol.TrueMoon, new(12, 12, 44, 44), Foreground);
            context.Text("Argentis · TrueMoon.Tales", 65, 18, Foreground, 21, FontFamily);
            context.Text("12 · 16 · 24 px", 66, 44, Foreground, 12, FontFamily);
            for (var i = 0; i < Symbols.Length; i++)
            {
                var x = 12 + i % 4 * 181;
                var y = 76 + i / 4 * 68;
                context.Fill(new(x, y, 173, 60), Theme.Control, 6);
                context.Text(Symbols[i].ToString(), x + 9, y + 5, Foreground, 13, FontFamily);
                for (var s = 0; s < Sizes.Length; s++)
                {
                    var size = Sizes[s];
                    var bounds = new Rect(x + 28 + 56 * s - size / 2, y + 40 - size / 2, size, size);
                    UiSymbols.Draw(context, Symbols[i], bounds, Foreground);
                    Specimens[i * Sizes.Length + s] = (Symbols[i], bounds);
                }
            }
        }
    }
}
