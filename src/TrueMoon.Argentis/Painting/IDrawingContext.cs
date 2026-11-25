namespace TrueMoon.Argentis.Painting;

/// <summary>The rendering operations available to built-in and custom widgets.</summary>
public interface IDrawingContext
{
    /// <summary>Saves clipping and transformation state.</summary>
    void Save();
    /// <summary>Restores the most recently saved state.</summary>
    void Restore();
    /// <summary>Intersects the current clip with a rectangle.</summary>
    void Clip(Rect rectangle);
    /// <summary>Translates subsequent drawing operations.</summary>
    void Translate(float x, float y);
    /// <summary>Fills a rectangle, optionally with rounded corners.</summary>
    void Fill(Rect rectangle, Color color, float radius = 0);
    /// <summary>Draws a rectangle outline.</summary>
    void Stroke(Rect rectangle, Color color, float width = 1, float radius = 0);
    /// <summary>Draws one line of text at its top-left position.</summary>
    void Text(string text, float x, float y, Color color, float fontSize, string fontFamily);
    /// <summary>Draws a styled line. Legacy providers fall back to their normal face, as in measurement.</summary>
    /// <param name="text">Literal single-line text.</param><param name="x">Logical left position.</param><param name="y">Logical top position.</param>
    /// <param name="color">Text color.</param><param name="fontSize">Logical font size.</param><param name="fontFamily">Requested family.</param>
    /// <param name="fontWeight">Weight100..900;400 is regular and600 semibold.</param><param name="fontItalic">Whether to select an italic face.</param>
    void Text(string text, float x, float y, Color color, float fontSize, string fontFamily, int fontWeight, bool fontItalic) =>
        Text(text, x, y, color, fontSize, fontFamily);
    /// <summary>Draws unshaped single-line editor text using the same positioning as
    /// <see cref="ITextLayoutService.MeasureEditorText"/>. Other providers use normal styled text.</summary>
    void EditorText(string text, float x, float y, Color color, float fontSize, string fontFamily, int fontWeight, bool fontItalic) =>
        Text(text, x, y, color, fontSize, fontFamily, fontWeight, fontItalic);
    /// <summary>Draws an image into a destination rectangle.</summary>
    void Image(IImageSource source, Rect destination);
}