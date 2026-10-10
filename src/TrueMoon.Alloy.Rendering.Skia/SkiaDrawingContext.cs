using SkiaSharp;
using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Rendering.Skia;

internal sealed class SkiaDrawingContext(SKCanvas canvas, SkiaDrawingResources resources) : IDrawingContext, ITextLayoutService
{
    private static SKRect Rect(Rect r) => new(r.X, r.Y, r.X + r.Width, r.Y + r.Height);
    private static SKColor Color(Color c) => new(c.R, c.G, c.B, c.A);
    public void Save() => canvas.Save();
    public void Restore() => canvas.Restore();
    public void Clip(Rect rectangle) => canvas.ClipRect(Rect(rectangle));
    public void Translate(float x, float y) => canvas.Translate(x, y);
    public void Fill(Rect rectangle, Color color, float radius = 0)
    { var paint = resources.Paint(Color(color), SKPaintStyle.Fill); canvas.DrawRoundRect(Rect(rectangle), radius, radius, paint); }
    public void Stroke(Rect rectangle, Color color, float width = 1, float radius = 0)
    { var paint = resources.Paint(Color(color), SKPaintStyle.Stroke, width); canvas.DrawRoundRect(Rect(rectangle), radius, radius, paint); }
    public void Text(string text, float x, float y, Color color, float fontSize, string fontFamily)
    {
        var entry = resources.Font(fontFamily, fontSize);
        var paint = resources.Paint(Color(color), SKPaintStyle.Fill);
        canvas.DrawText(text, x, y - entry.Metrics.Ascent, SKTextAlign.Left, entry.Font, paint);
    }
    public Size Measure(string text, float fontSize, string fontFamily)
    { var entry = resources.Font(fontFamily, fontSize); return new Size(entry.Font.MeasureText(text), entry.Metrics.Descent - entry.Metrics.Ascent); }
    public void Image(IImageSource source, Rect destination)
    {
        if (source is not SkiaImageSource image) throw new ArgumentException("Image must be a SkiaImageSource.", nameof(source));
        canvas.DrawImage(image.Image, Rect(destination), new SKSamplingOptions(SKFilterMode.Linear));
    }
}

// One owner thread and surface lifetime; no global cache or borrowed canvas/context ownership.
internal sealed class SkiaDrawingResources : IDisposable
{
    private const int FontCapacity = 16;
    private readonly int _thread = Environment.CurrentManagedThreadId;
    private readonly SKPaint _paint = new() { IsAntialias = true };
    private readonly Dictionary<FontKey, FontEntry> _fonts = new(FontCapacity);
    private readonly FontKey[] _order = new FontKey[FontCapacity];
    private int _next;
    private bool _disposed;
    private readonly record struct FontKey(string Family, float Size);
    internal readonly record struct FontEntry(SKFont Font, SKFontMetrics Metrics);

    private void VerifyAvailable()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Environment.CurrentManagedThreadId != _thread)
            throw new InvalidOperationException("Skia drawing resources require their creator thread.");
    }

    internal SKPaint Paint(SKColor color, SKPaintStyle style, float width = 0)
    {
        VerifyAvailable();
        // Reset every property varied by this adapter, including after a stroke or a failed draw.
        _paint.Color = color; _paint.Style = style; _paint.StrokeWidth = width;
        return _paint;
    }

    internal FontEntry Font(string family, float size)
    {
        VerifyAvailable();
        var key = new FontKey(family, size);
        if (_fonts.TryGetValue(key, out var entry)) return entry;
        // SKFont retains its native typeface reference. Do not retain shared managed typeface wrappers.
        using var typeface = SKTypeface.FromFamilyName(family);
        var font = new SKFont(typeface, size);
        try
        {
            entry = new FontEntry(font, font.Metrics);
            if (_fonts.Count == FontCapacity)
            {
                var previous = _fonts[_order[_next]];
                _fonts.Remove(_order[_next]);
                previous.Font.Dispose();
            }
            _fonts.Add(key, entry);
            _order[_next] = key; _next = (_next + 1) % FontCapacity;
            return entry;
        }
        catch (Exception error) { UiCleanup.Complete(error, font.Dispose); throw; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        VerifyAvailable(); _disposed = true;
        var cleanup = _fonts.Values.Select(entry => (Action)entry.Font.Dispose).Append(_paint.Dispose).ToArray();
        _fonts.Clear();
        UiCleanup.Complete(null, cleanup);
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
