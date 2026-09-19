namespace TrueMoon.Argentis;

/// <summary>Backend-independent text measurement. The same service must be used for layout and drawing.</summary>
public interface ITextLayoutService
{
    /// <summary>Measures a single line using the selected font.</summary>
    Size Measure(string text, float fontSize, string fontFamily);
}
/// <summary>A borrowed or owned decoded image. The creator controls its lifetime.</summary>
public interface IImageSource : IDisposable
{
    /// <summary>Image dimensions.</summary>
    Size Size { get; }
}
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
    /// <summary>Draws an image into a destination rectangle.</summary>
    void Image(IImageSource source, Rect destination);
}
