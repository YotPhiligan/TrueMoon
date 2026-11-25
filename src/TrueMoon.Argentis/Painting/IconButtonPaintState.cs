namespace TrueMoon.Argentis.Painting;

/// <summary>Visual inputs for the icon glyph; independent of the button's lifetime.</summary>
public readonly record struct IconButtonPaintState(Rect Bounds, float IconSize, UiSymbol Symbol, Color Foreground, bool UseVectorSymbol)
{
    internal static IconButtonPaintState From(IconButton button) => new(button.Bounds, button.IconSize, button.Symbol,
        button.IsEffectivelyEnabled ? button.Foreground : button.Theme.Disabled, button.UseVectorSymbol);
}
