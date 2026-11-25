using TrueMoon.Argentis.Styling;

namespace TrueMoon.Argentis.Painting;

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
    /// <summary>TrueMoon's silver Argentis moon.</summary>
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
    Preview,
    /// <summary>Generic scene occurrence.</summary>
    Entity,
    /// <summary>Renderable cube.</summary>
    Cube,
    /// <summary>Interactive occurrence marker.</summary>
    Interaction,
    /// <summary>Collapsed branch.</summary>
    ChevronRight,
    /// <summary>Expanded branch.</summary>
    ChevronDown,
    /// <summary>Add a new item.</summary>
    Add,
    /// <summary>Delete an item.</summary>
    Delete,
    /// <summary>Horizontal numeric adjustment.</summary>
    AdjustHorizontal
}

/// <summary>Draws a built-in vector icon with a geometry fallback for other renderers.</summary>
public static class UiSymbols
{
    private static readonly IReadOnlyDictionary<UiSymbol, VectorIcon> Contours = new Dictionary<UiSymbol, VectorIcon>
    {
        [UiSymbol.Minimize] = new("M 4 17 H 20", 2),
        [UiSymbol.Maximize] = new("M 5 5 H 19 V 19 H 5 Z", 2),
        [UiSymbol.Restore] = new("M 5 8 H 16 V 19 H 5 Z M 8 8 V 5 H 19 V 16 H 16", 2),
        [UiSymbol.Close] = new("M 5 5 L 19 19 M 19 5 L 5 19", 2),
        [UiSymbol.Check] = new("M 4 12 L 9.5 17.5 L 20 6.5", 2.8f),
        [UiSymbol.ResizeGrip] = new("M 6 19 L 19 6 M 12 19 L 19 12 M 18 19 L 19 18", 1.7f),
        [UiSymbol.More] = new("M 4 12 H 4.15 M 12 12 H 12.15 M 20 12 H 20.15", 3.6f),
        [UiSymbol.Play] = new("M 6 4.5 Q 6 3.6 7 4 L 20 11 Q 21.6 12 20 13 L 7 20 Q 6 20.4 6 19.5 Z"),
        [UiSymbol.Pause] = new("M 6.5 4 H 8.5 Q 10 4 10 5.5 V 18.5 Q 10 20 8.5 20 H 6.5 Q 5 20 5 18.5 V 5.5 Q 5 4 6.5 4 Z M 15.5 4 H 17.5 Q 19 4 19 5.5 V 18.5 Q 19 20 17.5 20 H 15.5 Q 14 20 14 18.5 V 5.5 Q 14 4 15.5 4 Z"),
        [UiSymbol.Stop] = new("M 6.5 4.5 H 17.5 Q 19.5 4.5 19.5 6.5 V 17.5 Q 19.5 19.5 17.5 19.5 H 6.5 Q 4.5 19.5 4.5 17.5 V 6.5 Q 4.5 4.5 6.5 4.5 Z"),
        [UiSymbol.File] = new("M 6 3 H 14 L 19 8 V 21 H 6 Z M 14 3 V 8 H 19 M 9 12 H 16 M 9 16 H 16", 2),
        [UiSymbol.Folder] = new("M 3.5 18.5 V 6 Q 3.5 4.5 5 4.5 H 9 L 11.5 7 H 18 Q 19.5 7 19.5 8.5 V 10.5 M 3.5 18.5 Q 3.5 20 5 20 H 17.5 Q 19 20 19.5 18.5 L 21.5 12 Q 22 10.5 20.5 10.5 H 7.5 Q 6.5 10.5 6 11.7 Z", 1.9f),
        [UiSymbol.Save] = new("M 4 5 Q 4 4 5 4 H 16.5 L 20 7.5 V 19 Q 20 20 19 20 H 5 Q 4 20 4 19 Z M 8 4 V 9 H 15 V 4 M 8 20 V 14 H 16 V 20", 1.9f),
        [UiSymbol.Undo] = new("M 9 4.5 L 4 9.5 L 9 14.5 M 4 9.5 H 14.5 A 5 5 0 0 1 14.5 19.5 H 10", 2.1f),
        [UiSymbol.Redo] = new("M 15 4.5 L 20 9.5 L 15 14.5 M 20 9.5 H 9.5 A 5 5 0 0 0 9.5 19.5 H 14", 2.1f),
        [UiSymbol.Build] = new("M 4 4 H 15 L 20 8 V 11 H 15 L 13 9 H 10 L 6 20 H 3 L 7 8 H 4 Z"),
        [UiSymbol.Theme] = new("M 12 2 A 10 10 0 1 1 12 22 A 10 10 0 1 1 12 2 Z M 12 4 V 20 A 8 8 0 0 0 12 4 Z"),
        [UiSymbol.Step] = new("M 5 5 L 15.5 12 L 5 19 Z M 18 5 H 21 V 19 H 18 Z"),
        [UiSymbol.Info] = new("M 12 3 A 9 9 0 1 1 12 21 A 9 9 0 1 1 12 3 Z M 12 11 V 16.5 M 12 7.5 H 12.1", 2),
        [UiSymbol.Warning] = new("M 12 3 L 22 20 H 2 Z M 12 9 V 13 M 12 16.5 H 12.1", 2),
        [UiSymbol.Busy] = new("M 5 3 H 19 M 5 21 H 19 M 7 3 V 7 Q 7 8.5 9 10 L 12 12 L 15 10 Q 17 8.5 17 7 V 3 M 7 21 V 17 Q 7 15.5 9 14 L 12 12 L 15 14 Q 17 15.5 17 17 V 21", 1.9f),
        [UiSymbol.Objects] = new("M 3 4 H 9 V 10 H 3 Z M 15 4 H 21 V 10 H 15 Z M 9 14 H 15 V 20 H 9 Z", 1.9f),
        [UiSymbol.Preview] = new("M 2 12 Q 12 0.5 22 12 Q 12 23.5 2 12 Z M 12 9 A 3 3 0 1 1 12 15 A 3 3 0 1 1 12 9 Z", 1.9f),
        [UiSymbol.Entity] = new("M 12 3 L 21 12 L 12 21 L 3 12 Z M 12 12 H 12.1", 2),
        [UiSymbol.Cube] = new("M 12 3 L 20 7.5 V 16.5 L 12 21 L 4 16.5 V 7.5 Z M 4 7.5 L 12 12 L 20 7.5 M 12 12 V 21", 1.9f),
        [UiSymbol.Interaction] = new("M 12 3 L 21 8 V 16 L 12 21 L 3 16 V 8 Z M 12 8 V 16 M 8 12 H 16", 1.9f),
        [UiSymbol.ChevronRight] = new("M 9 5 L 16 12 L 9 19", 2.3f),
        [UiSymbol.ChevronDown] = new("M 5 9 L 12 16 L 19 9", 2.3f),
        [UiSymbol.Add] = new("M 12 4 V 20 M 4 12 H 20", 2.2f),
        [UiSymbol.Delete] = new("M 4 6 H 20 M 9 6 V 3 H 15 V 6 M 6 6 L 7.5 20 H 16.5 L 18 6 M 10 10 L 10.5 16 M 14 10 L 13.5 16", 1.8f),
        [UiSymbol.AdjustHorizontal] = new("M 3 12 H 21 M 3 12 L 8 7 M 3 12 L 8 17 M 21 12 L 16 7 M 21 12 L 16 17", 2.4f)
    };

    /// <summary>Draws the available contour, or the geometric symbol on other renderers.</summary>
    /// <param name="context">Current drawing scope.</param><param name="symbol">Symbol to paint.</param>
    /// <param name="bounds">Logical icon bounds.</param><param name="color">Foreground of monochrome symbols and opacity of the brand mark.</param>
    public static void DrawVector(IDrawingContext context, UiSymbol symbol, Rect bounds, Color color) =>
        Draw(context, symbol, bounds, color);

    /// <summary>Draws the symbol inside the requested logical bounds.</summary>
    /// <param name="context">Current drawing scope.</param><param name="symbol">Supported symbol.</param>
    /// <param name="bounds">Logical icon bounds.</param><param name="color">Foreground of monochrome symbols and opacity of the brand mark.</param>
    public static void Draw(IDrawingContext context, UiSymbol symbol, Rect bounds, Color color) =>
        Draw(context, symbol, bounds, color, true);

    internal static void Draw(IDrawingContext context, UiSymbol symbol, Rect bounds, Color color, bool useVector)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!Enum.IsDefined(symbol)) throw new ArgumentOutOfRangeException(nameof(symbol));
        if (!float.IsFinite(bounds.X) || !float.IsFinite(bounds.Y) || !float.IsFinite(bounds.Width) || !float.IsFinite(bounds.Height) || bounds.Width < 0 || bounds.Height < 0)
            throw new ArgumentOutOfRangeException(nameof(bounds));
        if (bounds.Width == 0 || bounds.Height == 0) return;
        if (useVector && context is IVectorIconDrawingContext vector)
        {
            if (symbol == UiSymbol.TrueMoon) TrueMoonMark.Draw(vector, bounds, color.A);
            else vector.DrawVectorIcon(Contours[symbol], bounds, color);
            return;
        }
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
            case UiSymbol.Entity: Box(2, 2, 8, 8); Dot(5, 5, 2, 2); break;
            case UiSymbol.Cube: Box(1, 4, 7, 7); Box(4, 1, 7, 7); for (var i = 0; i < 4; i++) { Dot(1 + i, 4 - i); Dot(8 + i, 11 - i); } break;
            case UiSymbol.Interaction: Box(1, 1, 10, 10); Dot(5, 3, 2, 6); Dot(3, 5, 6, 2); break;
            case UiSymbol.ChevronRight: for (var i = 0; i < 4; i++) { Dot(4 + i, 2 + i, 1.5f, 1.5f); Dot(4 + i, 8 - i, 1.5f, 1.5f); } break;
            case UiSymbol.ChevronDown: for (var i = 0; i < 4; i++) { Dot(2 + i, 4 + i, 1.5f, 1.5f); Dot(8 - i, 4 + i, 1.5f, 1.5f); } break;
            case UiSymbol.Add: Dot(5, 1, 2, 10); Dot(1, 5, 10, 2); break;
            case UiSymbol.Delete: Dot(1, 2, 10); Box(3, 1, 6, 1); Box(2, 3, 8, 8); Dot(4, 5, 1, 4); Dot(7, 5, 1, 4); break;
            case UiSymbol.AdjustHorizontal:
                Dot(2, 6, 8);
                for (var i = 0; i < 3; i++) { Dot(2 + i, 6 - i, 1, 1 + 2 * i); Dot(9 - i, 6 - i, 1, 1 + 2 * i); }
                break;
            case UiSymbol.TrueMoon: context.Fill(new(x + unit, y + unit, unit * 10, unit * 10), color, unit * 5); break;
        }
    }
}
