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
    ResizeGrip,
    /// <summary>Three horizontal menu dots.</summary>
    More,
    /// <summary>Play triangle.</summary>
    Play,
    /// <summary>Pause bars.</summary>
    Pause,
    /// <summary>Stop square.</summary>
    Stop,
    /// <summary>File outline.</summary>
    File,
    /// <summary>Open folder.</summary>
    Folder,
    /// <summary>Save disk.</summary>
    Save,
    /// <summary>Undo arrow.</summary>
    Undo,
    /// <summary>Redo arrow.</summary>
    Redo,
    /// <summary>Build/tool mark.</summary>
    Build,
    /// <summary>Theme half-disc.</summary>
    Theme,
    /// <summary>Forward single step.</summary>
    Step,
    /// <summary>TrueMoon crescent and star brand mark.</summary>
    TrueMoon,
    /// <summary>Information marker.</summary>
    Info,
    /// <summary>Error/warning marker, distinct from normal status.</summary>
    Warning,
    /// <summary>Busy hourglass.</summary>
    Busy,
    /// <summary>Scene objects.</summary>
    Objects,
    /// <summary>Preview eye.</summary>
    Preview
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
            case UiSymbol.More: Dot(1, 5, 2, 2); Dot(5, 5, 2, 2); Dot(9, 5, 2, 2); break;
            case UiSymbol.Play: for (var i = 0; i < 5; i++) Dot(2 + i, 1 + i, 1.2f, 10 - 2 * i); break;
            case UiSymbol.Pause: Dot(2, 1, 3, 10); Dot(7, 1, 3, 10); break;
            case UiSymbol.Stop: Dot(2, 2, 8, 8); break;
            case UiSymbol.File: Box(2, 1, 8, 10); Dot(4, 4, 4); Dot(4, 7, 4); break;
            case UiSymbol.Folder: Box(1, 3, 10, 8); Dot(1, 1, 4, 2); break;
            case UiSymbol.Save: Box(1, 1, 10, 10); Box(3, 1, 5, 3); Box(3, 7, 6, 4); break;
            case UiSymbol.Undo: Dot(1, 4, 8); Dot(8, 4, 2, 6); for (var i = 0; i < 4; i++) { Dot(1 + i, 4 - i); Dot(1 + i, 4 + i); } break;
            case UiSymbol.Redo: Dot(3, 4, 8); Dot(2, 4, 2, 6); for (var i = 0; i < 4; i++) { Dot(10 - i, 4 - i); Dot(10 - i, 4 + i); } break;
            case UiSymbol.Build: Dot(1, 1, 8, 3); for (var i = 0; i < 8; i++) Dot(4 + i / 2, 3 + i, 2); break;
            case UiSymbol.Theme: Box(1, 1, 10, 10); Dot(2, 2, 4, 8); break;
            case UiSymbol.Step: for (var i = 0; i < 4; i++) Dot(1 + i, 2 + i, 1.2f, 8 - 2 * i); Dot(9, 1, 2, 10); break;
            case UiSymbol.Info: Box(1, 1, 10, 10); Dot(5, 3, 2); Dot(5, 5, 2, 4); break;
            case UiSymbol.Warning: Box(1, 1, 10, 10); Dot(5, 3, 2, 4); Dot(5, 8, 2); break;
            case UiSymbol.Busy: Dot(2, 1, 8); Dot(2, 10, 8); for (var i = 0; i < 4; i++) { Dot(2 + i, 2 + i); Dot(9 - i, 2 + i); Dot(2 + i, 9 - i); Dot(9 - i, 9 - i); } break;
            case UiSymbol.Objects: Box(1, 1, 4, 4); Box(7, 1, 4, 4); Box(4, 7, 4, 4); break;
            case UiSymbol.Preview: for (var i = 0; i < 5; i++) { Dot(1 + i, 5 - i / 2); Dot(1 + i, 6 + i / 2); Dot(10 - i, 5 - i / 2); Dot(10 - i, 6 + i / 2); } Dot(5, 4, 2, 4); break;
            case UiSymbol.TrueMoon:
                // Fill the crescent itself; the cutout stays transparent on every theme/background.
                for (var row = 0; row < 20; row++)
                {
                    var py = 1 + row * .5f; var cy = py + .5f;
                    var outerSquared = 25 - (cy - 6) * (cy - 6);
                    if (outerSquared <= 0) continue;
                    var outer = MathF.Sqrt(outerSquared);
                    var left = 6 - outer; var right = 6 + outer;
                    var innerSquared = 4.8f * 4.8f - (cy - 3.9f) * (cy - 3.9f);
                    // Overlap adjacent bands so antialiasing cannot leave seams at fractional DPI.
                    if (innerSquared <= 0) Dot(left, py, right - left, 1);
                    else
                    {
                        var inner = MathF.Sqrt(innerSquared);
                        var end = Math.Min(right, 8.1f - inner); var start = Math.Max(left, 8.1f + inner);
                        if (end > left) Dot(left, py, end - left, 1);
                        if (right > start) Dot(start, py, right - start, 1);
                    }
                }
                Dot(9.5f, 1, .5f, 3); Dot(8.25f, 2.25f, 3, .5f);
                break;
        }
    }
}
