using SkiaSharp;
using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Rendering.Skia;

internal sealed class SkiaDrawingContext(SKCanvas canvas) : IDrawingContext, ITextLayoutService
{
    private static SKRect Rect(Rect r) => new(r.X, r.Y, r.X + r.Width, r.Y + r.Height);
    private static SKColor Color(Color c) => new(c.R, c.G, c.B, c.A);
    public void Save() => canvas.Save();
    public void Restore() => canvas.Restore();
    public void Clip(Rect rectangle) => canvas.ClipRect(Rect(rectangle));
    public void Translate(float x, float y) => canvas.Translate(x, y);
    public void Fill(Rect rectangle, Color color, float radius = 0)
    { using var paint = new SKPaint { Color = Color(color), IsAntialias = true }; canvas.DrawRoundRect(Rect(rectangle), radius, radius, paint); }
    public void Stroke(Rect rectangle, Color color, float width = 1, float radius = 0)
    { using var paint = new SKPaint { Color = Color(color), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = width }; canvas.DrawRoundRect(Rect(rectangle), radius, radius, paint); }
    public void Text(string text, float x, float y, Color color, float fontSize, string fontFamily)
    {
        using var typeface = SKTypeface.FromFamilyName(fontFamily);
        using var font = new SKFont(typeface, fontSize);
        using var paint = new SKPaint { Color = Color(color), IsAntialias = true };
        canvas.DrawText(text, x, y - font.Metrics.Ascent, SKTextAlign.Left, font, paint);
    }
    public Size Measure(string text, float fontSize, string fontFamily)
    { using var typeface = SKTypeface.FromFamilyName(fontFamily); using var font = new SKFont(typeface, fontSize); return new Size(font.MeasureText(text), font.Metrics.Descent - font.Metrics.Ascent); }
    public void Image(IImageSource source, Rect destination)
    {
        if (source is not SkiaImageSource image) throw new ArgumentException("Image must be a SkiaImageSource.", nameof(source));
        canvas.DrawImage(image.Image, Rect(destination), new SKSamplingOptions(SKFilterMode.Linear));
    }
}
/// <summary>An owned decoded image for the Skia drawing adapter.</summary>
public sealed class SkiaImageSource : IImageSource
{
    internal SKImage Image { get; }
    /// <summary>Decodes an image. The creator must dispose it after UI use ends.</summary>
    public SkiaImageSource(string path) { SkiaNativeLibrary.Initialize(); Image = SKImage.FromEncodedData(path) ?? throw new ArgumentException("Could not decode image.", nameof(path)); }
    /// <inheritdoc />
    public Size Size => new(Image.Width, Image.Height);
    /// <inheritdoc />
    public void Dispose() => Image.Dispose();
}
