namespace TrueMoon.Argentis;

/// <summary>Small vector symbols, independent of font glyph availability.</summary>
public enum UiSymbol
{
    /// <summary>Minimize line.</summary>
    Minimize,
    /// <summary>Maximize outline.</summary>
    Maximize,
    /// <summary>Overlapping restore outlines.</summary>
    Restore,
    /// <summary>Close cross.</summary>
    Close,
    /// <summary>Checked-state tick.</summary>
    Check,
    /// <summary>Bottom-right sizing marks.</summary>
    ResizeGrip
}

/// <summary>Draws a tiny geometric icon using backend-independent rectangles.</summary>
public static class UiSymbols
{
    /// <summary>Draws the symbol inside the requested bounds, with square pixel-independent strokes.</summary>
    /// <param name="context">Current drawing scope.</param><param name="symbol">Supported symbol.</param>
    /// <param name="bounds">Logical icon bounds.</param><param name="color">Effective foreground.</param>
    public static void Draw(IDrawingContext context, UiSymbol symbol, Rect bounds, Color color)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!Enum.IsDefined(symbol)) throw new ArgumentOutOfRangeException(nameof(symbol));
        if (!float.IsFinite(bounds.X) || !float.IsFinite(bounds.Y) || !float.IsFinite(bounds.Width) || !float.IsFinite(bounds.Height) || bounds.Width < 0 || bounds.Height < 0)
            throw new ArgumentOutOfRangeException(nameof(bounds));
        if (bounds.Width == 0 || bounds.Height == 0) return;
        var unit = Math.Min(bounds.Width, bounds.Height) / 12;
        var x = bounds.X + (bounds.Width - unit * 12) / 2; var y = bounds.Y + (bounds.Height - unit * 12) / 2;
        void Dot(float px, float py, float width = 1, float height = 1) => context.Fill(new(x + px * unit, y + py * unit, width * unit, height * unit), color);
        void Box(float px, float py, float width, float height) => context.Stroke(new(x + px * unit, y + py * unit, width * unit, height * unit), color, unit);
        switch (symbol)
        {
            case UiSymbol.Minimize: Dot(1, 9, 10); break;
            case UiSymbol.Maximize: Box(1, 1, 10, 10); break;
            case UiSymbol.Restore: Box(4, 1, 7, 7); Box(1, 4, 7, 7); break;
            case UiSymbol.Close: for (var i = 1; i < 11; i++) { Dot(i, i); Dot(11 - i, i); } break;
            case UiSymbol.Check: for (var i = 0; i < 4; i++) Dot(1 + i, 6 + i, 1.5f, 1.5f); for (var i = 0; i < 7; i++) Dot(4 + i, 9 - i, 1.5f, 1.5f); break;
            case UiSymbol.ResizeGrip: for (var i = 0; i < 3; i++) for (var j = 0; j <= i; j++) Dot(10 - j * 3, 10 - (i - j) * 3, 1.5f, 1.5f); break;
        }
    }
}
