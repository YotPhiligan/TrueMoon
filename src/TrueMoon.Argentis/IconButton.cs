namespace TrueMoon.Argentis;

/// <summary>A square, keyboard-focusable button with a geometric icon and semantic name.</summary>
public class IconButton : Button
{
    /// <summary>Current geometric symbol.</summary>
    public static readonly UiProperty<UiSymbol> SymbolProperty = new("Symbol", UiSymbol.Close, validate: Enum.IsDefined);
    /// <summary>The preferred logical side length of the glyph.</summary>
    public static readonly UiProperty<float> IconSizeProperty = new("IconSize", 12f, validate: size => float.IsFinite(size) && size > 0);
    /// <summary>Whether to use the contour on renderers that support it.</summary>
    public static readonly UiProperty<bool> UseVectorSymbolProperty = new("UseVectorSymbol", true);
    /// <summary>Creates an icon-only action without depending on a symbol font.</summary>
    /// <param name="symbol">Geometric symbol.</param><param name="accessibleName">Nonempty action name.</param>
    public IconButton(UiSymbol symbol, string accessibleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessibleName);
        Symbol = symbol; AccessibleName = accessibleName; Width = 32; Height = 32; Padding = default;
    }
    /// <summary>Current symbol, which may change from maximize to restore.</summary>
    public UiSymbol Symbol { get => Get(SymbolProperty); set => Set(SymbolProperty, value); }
    /// <summary>Preferred icon size independent of the hit target.</summary>
    public float IconSize { get => Get(IconSizeProperty); set => Set(IconSizeProperty, value); }
    /// <summary>Requests the vector contour when supported by the renderer.</summary>
    public bool UseVectorSymbol { get => Get(UseVectorSymbolProperty); set => Set(UseVectorSymbolProperty, value); }
    /// <inheritdoc />
    protected override void DrawCore(IDrawingContext context)
    {
        var size = IconSize;
        var bounds = new Rect(Bounds.X + (Bounds.Width - size) / 2, Bounds.Y + (Bounds.Height - size) / 2, size, size);
        var color = IsEffectivelyEnabled ? Foreground : Theme.Disabled;
        UiSymbols.Draw(context, Symbol, bounds, color, UseVectorSymbol);
    }
}
