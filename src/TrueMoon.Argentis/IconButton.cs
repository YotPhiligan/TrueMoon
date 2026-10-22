namespace TrueMoon.Argentis;

/// <summary>A square, keyboard-focusable button with a geometric icon and semantic name.</summary>
public sealed class IconButton : Button
{
    /// <summary>Current geometric symbol.</summary>
    public static readonly UiProperty<UiSymbol> SymbolProperty = new("Symbol", UiSymbol.Close, validate: Enum.IsDefined);
    /// <summary>Creates an icon-only action without depending on a symbol font.</summary>
    /// <param name="symbol">Geometric symbol.</param><param name="accessibleName">Nonempty action name.</param>
    public IconButton(UiSymbol symbol, string accessibleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessibleName);
        Symbol = symbol; AccessibleName = accessibleName; Width = 32; Height = 32; Padding = default;
    }
    /// <summary>Current symbol, which may change from maximize to restore.</summary>
    public UiSymbol Symbol { get => Get(SymbolProperty); set => Set(SymbolProperty, value); }
    /// <inheritdoc />
    protected override void DrawCore(IDrawingContext context) => UiSymbols.Draw(context, Symbol,
        new(Bounds.X + (Bounds.Width - 12) / 2, Bounds.Y + (Bounds.Height - 12) / 2, 12, 12), IsEffectivelyEnabled ? Foreground : Theme.Disabled);
}
