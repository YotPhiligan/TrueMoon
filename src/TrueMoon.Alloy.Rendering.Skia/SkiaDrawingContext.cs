using TrueMoon.Alloy.Runtime;
using SkiaSharp;
using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Rendering.Skia;

internal sealed class SkiaDrawingContext(SKCanvas canvas, SkiaDrawingResources resources, SKSurface? surface = null) : IDrawingContext, ITextLayoutService, IRichTextLayoutService, IRichTextDrawingContext, IBackdropDrawingContext, IOpacityDrawingContext
{
    // Contexts are created at the surface's root clip, before Element.Draw narrows it.
    private readonly SKRectI _surfaceBounds = canvas.DeviceClipBounds;
    private static SKRect Rect(Rect r) => new(r.X, r.Y, r.X + r.Width, r.Y + r.Height);
    private static SKColor Color(Color c) => new(c.R, c.G, c.B, c.A);
    public void Save() => canvas.Save();
    public void Restore() => canvas.Restore();
    public void Clip(Rect rectangle) => canvas.ClipRect(Rect(rectangle));
    public void Translate(float x, float y) => canvas.Translate(x, y);
    public void SaveOpacity(Rect bounds, float opacity)
    {
        resources.VerifyRichTextAccess();
        if (!float.IsFinite(bounds.X) || !float.IsFinite(bounds.Y) || !float.IsFinite(bounds.Width) || !float.IsFinite(bounds.Height) || bounds.Width < 0 || bounds.Height < 0)
            throw new ArgumentOutOfRangeException(nameof(bounds));
        if (!float.IsFinite(opacity) || opacity < 0 || opacity > 1) throw new ArgumentOutOfRangeException(nameof(opacity));
        using var paint = new SKPaint { Color = new SKColor(255, 255, 255, (byte)MathF.Round(opacity * 255)) };
        canvas.SaveLayer(Rect(bounds), paint);
    }
    public void Fill(Rect rectangle, Color color, float radius = 0)
    { var paint = resources.Paint(Color(color), SKPaintStyle.Fill); canvas.DrawRoundRect(Rect(rectangle), radius, radius, paint); }
    public void Stroke(Rect rectangle, Color color, float width = 1, float radius = 0)
    {
        if (radius == 0 && width > 0 && rectangle.Width >= width && rectangle.Height >= width)
        {
            // Four non-overlapping bands avoid backend-dependent hairline joins on square outlines.
            // The public stroke remains centered; its callers can inset it to keep the border inside.
            var half = width / 2; var x = rectangle.X - half; var y = rectangle.Y - half;
            var right = rectangle.X + rectangle.Width + half; var bottom = rectangle.Y + rectangle.Height + half;
            var paint = resources.Paint(Color(color), SKPaintStyle.Fill);
            canvas.DrawRect(new SKRect(x, y, right, y + width), paint);
            canvas.DrawRect(new SKRect(x, bottom - width, right, bottom), paint);
            canvas.DrawRect(new SKRect(x, y + width, x + width, bottom - width), paint);
            canvas.DrawRect(new SKRect(right - width, y + width, right, bottom - width), paint);
            return;
        }
        var outline = resources.Paint(Color(color), SKPaintStyle.Stroke, width);
        canvas.DrawRoundRect(Rect(rectangle), radius, radius, outline);
    }
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
        image.VerifyContext(resources.GpuOwner);
        canvas.DrawImage(image.Image, Rect(destination), new SKSamplingOptions(SKFilterMode.Linear));
    }
    public bool TryDrawBackdrop(Rect rectangle, BackdropMaterial material, float cornerRadius)
    {
        resources.VerifyRichTextAccess();
        ArgumentNullException.ThrowIfNull(material);
        if (!float.IsFinite(rectangle.X) || !float.IsFinite(rectangle.Y) || !float.IsFinite(rectangle.Width) ||
            !float.IsFinite(rectangle.Height) || rectangle.Width < 0 || rectangle.Height < 0 ||
            !float.IsFinite(cornerRadius) || cornerRadius < 0)
            throw new ArgumentOutOfRangeException(nameof(rectangle));
        if (rectangle.Width == 0 || rectangle.Height == 0) return true;
        // Tint-only and opaque materials require no snapshot or intermediate texture.
        if (material.BlurSigma == 0 || material.Tint.A == 255)
        { Fill(rectangle, material.Tint, cornerRadius); return true; }
        if (surface == null) return false;
        var matrix = canvas.TotalMatrix;
        // UI drawing exposes scale/translation. Other transforms use the explicit fallback.
        if (matrix.SkewX != 0 || matrix.SkewY != 0 || matrix.Persp0 != 0 || matrix.Persp1 != 0 || matrix.Persp2 != 1)
            return false;
        var sigmaX = material.BlurSigma * Math.Abs(matrix.ScaleX);
        var sigmaY = material.BlurSigma * Math.Abs(matrix.ScaleY);
        var area = matrix.MapRect(Rect(rectangle));
        var clip = canvas.DeviceClipBounds;
        area = SKRect.Intersect(area, new(clip.Left, clip.Top, clip.Right, clip.Bottom));
        if (area.IsEmpty) return true;
        // Capture the visible area plus the blur kernel halo, bounded by the current surface.
        area.Inflate(3 * sigmaX, 3 * sigmaY);
        var pixels = new SKRectI(Math.Max(_surfaceBounds.Left, (int)MathF.Floor(area.Left)), Math.Max(_surfaceBounds.Top, (int)MathF.Floor(area.Top)),
            Math.Min(_surfaceBounds.Right, (int)MathF.Ceiling(area.Right)), Math.Min(_surfaceBounds.Bottom, (int)MathF.Ceiling(area.Bottom)));
        if (pixels.IsEmpty) return true;
        using var image = surface.Snapshot(pixels);
        if (image == null) return false;
        using var filter = SKImageFilter.CreateBlur(sigmaX, sigmaY, SKShaderTileMode.Clamp);
        if (filter == null) return false;
        using var paint = new SKPaint { ImageFilter = filter, BlendMode = SKBlendMode.Src };
        canvas.Save();
        try
        {
            using var rounded = new SKRoundRect(Rect(rectangle), cornerRadius, cornerRadius);
            canvas.ClipRoundRect(rounded, SKClipOperation.Intersect, true);
            canvas.ResetMatrix();
            // Replace earlier pixels, avoiding a second alpha blend of a translucent backdrop.
            canvas.DrawImage(image, pixels.Left, pixels.Top, new SKSamplingOptions(SKFilterMode.Nearest), paint);
        }
        finally { canvas.Restore(); }
        Fill(rectangle, material.Tint, cornerRadius);
        return true;
    }
    public IRichTextLayout CreateRichTextLayout(RichTextDocument document, RichTextStyle defaults, float? wrapWidth, RichTextAlignment alignment)
    {
        resources.VerifyRichTextAccess();
        ArgumentNullException.ThrowIfNull(document); ArgumentNullException.ThrowIfNull(defaults);
        if (wrapWidth.HasValue && (!float.IsFinite(wrapWidth.Value) || wrapWidth.Value <= 0)) throw new ArgumentOutOfRangeException(nameof(wrapWidth));
        if (!Enum.IsDefined(alignment)) throw new ArgumentOutOfRangeException(nameof(alignment));
        return new SkiaRichTextLayout(document, defaults, wrapWidth, alignment, resources);
    }
    public void RichText(IRichTextLayout layout, float x, float y, int selectionStart, int selectionLength, Color selectionColor)
    {
        if (layout is not SkiaRichTextLayout skia) throw new ArgumentException("Rich text layout must use the Skia backend.", nameof(layout));
        skia.Paint(canvas, resources, x, y, selectionStart, selectionLength, selectionColor);
    }
}

// One owner thread and surface lifetime; no global cache or borrowed canvas/context ownership.
internal sealed class SkiaDrawingResources(SkiaFontCatalog? fonts = null, object? gpuOwner = null) : IDisposable
{
    internal object? GpuOwner => gpuOwner;
    private const int FontCapacity = 16;
    private readonly int _thread = Environment.CurrentManagedThreadId;
    private readonly SKPaint _paint = new() { IsAntialias = true };
    private readonly Dictionary<FontKey, FontEntry> _fonts = new(FontCapacity);
    private readonly FontKey[] _order = new FontKey[FontCapacity];
    private int _next;
    private bool _disposed;
    private SkiaRichTextFontMapper? _richFonts;
    internal SkiaRichTextFontMapper RichFonts { get { VerifyAvailable(); return _richFonts ??= new(fonts); } }
    internal void VerifyRichTextAccess() => VerifyAvailable();
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
        using var typeface = fonts == null ? SKTypeface.FromFamilyName(family) : fonts.CreateTypeface(family);
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
        var cleanup = _fonts.Values.Select(entry => (Action)entry.Font.Dispose).Append(_paint.Dispose)
            .Append(() => _richFonts?.Dispose()).ToArray();
        _fonts.Clear();
        UiCleanup.Complete(null, cleanup);
    }
}
/// <summary>An owned decoded image for the Skia drawing adapter.</summary>
public sealed class SkiaImageSource : IImageSource
{
    private readonly SKImage _image;
    private readonly object? _gpuOwner;
    private readonly Action? _verify;
    private bool _disposed;
    internal SKImage Image { get { ObjectDisposedException.ThrowIf(_disposed, this); _verify?.Invoke(); return _image; } }
    internal SkiaImageSource(SKImage image, object? gpuOwner = null, Action? verify = null)
    { _image = image; _gpuOwner = gpuOwner; _verify = verify; }
    internal void VerifyContext(object? current)
    { if (_gpuOwner != null && !ReferenceEquals(_gpuOwner, current)) throw new InvalidOperationException("GPU image requires its originating Skia OpenGL surface."); }
    /// <summary>Copies tightly packed top-left BGRA8 premultiplied sRGB pixels into an owned image.</summary>
    /// <param name="width">Positive pixel width.</param>
    /// <param name="height">Positive pixel height.</param>
    /// <param name="pixels">Exactly width × height × 4 bytes; each color channel is premultiplied by alpha.</param>
    /// <returns>An independent image owned by the caller.</returns>
    /// <remarks>The caller may reuse its buffer when this method returns. Dispose the image after owner-thread drawing ends.</remarks>
    public static SkiaImageSource CopyBgraPixels(int width, int height, ReadOnlySpan<byte> pixels)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        var stride = checked(width * 4);
        if (pixels.Length != checked(stride * height)) throw new ArgumentException("Expected tightly packed BGRA pixels.", nameof(pixels));
        SkiaNativeLibrary.Initialize();
        using var colorSpace = SKColorSpace.CreateSrgb();
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul, colorSpace);
        return new(SKImage.FromPixelCopy(info, pixels, stride) ?? throw new InvalidOperationException("Could not copy pixels."));
    }
    /// <summary>Decodes an image. The creator must dispose it after UI use ends.</summary>
    public SkiaImageSource(string path) { SkiaNativeLibrary.Initialize(); _image = SKImage.FromEncodedData(path) ?? throw new ArgumentException("Could not decode image.", nameof(path)); }
    /// <inheritdoc />
    public Size Size { get { var image = Image; return new(image.Width, image.Height); } }
    /// <inheritdoc />
    public void Dispose() { if (_disposed) return; _verify?.Invoke(); _image.Dispose(); _disposed = true; }
}
