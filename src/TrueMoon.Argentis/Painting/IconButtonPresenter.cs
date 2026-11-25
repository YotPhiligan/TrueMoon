using TrueMoon.Argentis.Styling;

namespace TrueMoon.Argentis.Painting;

/// <summary>Visual inputs for the icon glyph; independent of the button's lifetime.</summary>
public readonly record struct IconButtonPaintState(Rect Bounds, float IconSize, UiSymbol Symbol, Color Foreground, bool UseVectorSymbol)
{
    internal static IconButtonPaintState From(IconButton button) => new(button.Bounds, button.IconSize, button.Symbol,
        button.IsEffectivelyEnabled ? button.Foreground : button.Theme.Disabled, button.UseVectorSymbol);
}

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
