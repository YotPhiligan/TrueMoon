using TrueMoon.Argentis;
using TrueMoon.Argentis.Painting;

namespace TrueMoon.Alloy.Runtime;

/// <summary>Default rendering of the centered geometric glyph of an icon button.</summary>
public sealed class IconButtonPresenter : IElementPresenter<IconButtonPaintState>
{

    /// <inheritdoc />
    public void Draw(IconButtonPaintState state, IDrawingContext context)
    {
        var size = state.IconSize;
        var area = state.Bounds;
        var glyph = new Rect(area.X + (area.Width - size) / 2, area.Y + (area.Height - size) / 2, size, size);
        UiSymbols.Draw(context, state.Symbol, glyph, state.Foreground, state.UseVectorSymbol);
    }
}
