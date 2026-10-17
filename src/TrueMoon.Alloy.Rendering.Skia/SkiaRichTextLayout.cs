using SkiaSharp;
using Topten.RichTextKit;
using TrueMoon.Argentis;
using TextBlock = Topten.RichTextKit.TextBlock;
using KitStyle = Topten.RichTextKit.Style;

namespace TrueMoon.Alloy.Rendering.Skia;

internal sealed class SkiaRichTextLayout : IRichTextLayout
{
    private sealed record Paragraph(TextBlock Block, string Text, int Start, float Y);
    private readonly List<Paragraph> _paragraphs = [];
    private readonly int _thread = Environment.CurrentManagedThreadId;
    private readonly RichTextDocument _document;
    private readonly SkiaDrawingResources _resources;
    private bool _disposed;
    public Size Size { get; }

    internal SkiaRichTextLayout(RichTextDocument document, RichTextStyle defaults, float? width,
        RichTextAlignment alignment, SkiaDrawingResources resources)
    {
        _document = document; _resources = resources;
        float y = 0, maxWidth = 0; var start = 0;
        try
        {
            foreach (var paragraphText in document.Text.Split('\n'))
            {
                var block = new TextBlock
                {
                    MaxWidth = width, RenderWidth = width, FontMapper = resources.RichFonts,
                    Alignment = alignment switch { RichTextAlignment.Center => TextAlignment.Center, RichTextAlignment.Right => TextAlignment.Right, _ => TextAlignment.Left }
                };
                _paragraphs.Add(new(block, paragraphText, start, y));
                var offset = 0;
                foreach (var run in document.Runs)
                {
                    var from = Math.Max(start, offset); var to = Math.Min(start + paragraphText.Length, offset + run.Text.Length);
                    if (to > from)
                    {
                        var style = Style(run.Style, defaults);
                        block.AddText(resources.RichFonts.PrepareText(run.Text.Substring(from - offset, to - from), style), style);
                    }
                    offset += run.Text.Length;
                }
                // An explicit zero-width paragraph terminator supplies empty/trailing caret metrics.
                block.AddText("\u200B", Style(document.Runs.Count == 0 ? new() : StyleAt(document, Math.Max(start, start + paragraphText.Length - 1)), defaults));
                block.Layout();
                maxWidth = Math.Max(maxWidth, block.MeasuredWidth);
                y += block.MeasuredHeight;
                start += paragraphText.Length + 1;
            }
            Size = new(maxWidth, y);
        }
        catch { Dispose(); throw; }
    }
    private static RichTextStyle StyleAt(RichTextDocument document, int index)
    {
        var offset = 0;
        foreach (var run in document.Runs) { if (index < offset + run.Text.Length) return run.Style; offset += run.Text.Length; }
        return document.Runs[^1].Style;
    }
    private static KitStyle Style(RichTextStyle style, RichTextStyle defaults) => new()
    {
        FontFamily = style.FontFamily ?? defaults.FontFamily ?? "Segoe UI",
        FontSize = style.FontSize ?? defaults.FontSize ?? 16,
        FontWeight = style.Bold == true ? 700 : 400, FontItalic = style.Italic ?? false,
        Underline = style.Underline == true ? UnderlineStyle.Solid : UnderlineStyle.None,
        StrikeThrough = style.StrikeThrough == true ? StrikeThroughStyle.Solid : StrikeThroughStyle.None,
        TextColor = Color(style.Foreground ?? defaults.Foreground ?? new Argentis.Color(0, 0, 0)),
        BackgroundColor = Color(style.Background ?? Argentis.Color.Transparent),
        LineHeight = style.LineHeight ?? 1
    };
    private static SKColor Color(Argentis.Color c) => new(c.R, c.G, c.B, c.A);
    private void Verify()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Environment.CurrentManagedThreadId != _thread) throw new InvalidOperationException("Rich text layout requires its owner thread.");
        _resources.VerifyRichTextAccess();
    }
    private Paragraph At(int index)
    {
        if (index < 0 || index > _document.Text.Length) throw new ArgumentOutOfRangeException(nameof(index));
        return _paragraphs.Last(paragraph => paragraph.Start <= index);
    }
    private static int CodePoint(Paragraph paragraph, int index)
    {
        var count = 0;
        foreach (var rune in paragraph.Text.AsSpan(0, Math.Clamp(index - paragraph.Start, 0, paragraph.Text.Length)).EnumerateRunes()) count++;
        return count;
    }
    private static int Character(Paragraph paragraph, int codePoint)
    {
        var index = 0; var count = 0;
        foreach (var rune in paragraph.Text.EnumerateRunes()) { if (count++ >= codePoint) break; index += rune.Utf16SequenceLength; }
        return paragraph.Start + index;
    }
    public int HitTest(float x, float y)
    {
        Verify();
        var paragraph = _paragraphs.LastOrDefault(paragraph => paragraph.Y <= y) ?? _paragraphs[0];
        return Character(paragraph, Math.Max(0, paragraph.Block.HitTest(x, y - paragraph.Y).ClosestCodePointIndex));
    }
    public Rect GetCaretBounds(int index)
    {
        Verify(); var paragraph = At(index);
        var info = paragraph.Block.GetCaretInfo(new CaretPosition(CodePoint(paragraph, index)));
        var rectangle = info.CaretRectangle;
        return new(rectangle.Left, paragraph.Y + rectangle.Top, Math.Max(1, rectangle.Width), Math.Max(1, rectangle.Height));
    }
    public int GetLineBoundary(int index, bool end)
    {
        Verify(); var paragraph = At(index);
        var caret = paragraph.Block.GetCaretInfo(new CaretPosition(CodePoint(paragraph, index)));
        var line = paragraph.Block.Lines[Math.Clamp(caret.LineIndex, 0, paragraph.Block.Lines.Count - 1)];
        return Character(paragraph, end ? line.End : line.Start);
    }
    public int MoveVertical(int index, int direction, float preferredX)
    {
        Verify();
        var paragraph = At(index); var caret = paragraph.Block.GetCaretInfo(new CaretPosition(CodePoint(paragraph, index)));
        var lineIndex = caret.LineIndex + Math.Sign(direction);
        var paragraphIndex = _paragraphs.IndexOf(paragraph);
        if (lineIndex < 0)
        {
            if (paragraphIndex == 0) return index;
            paragraph = _paragraphs[paragraphIndex - 1]; lineIndex = paragraph.Block.Lines.Count - 1;
        }
        else if (lineIndex >= paragraph.Block.Lines.Count)
        {
            if (paragraphIndex + 1 >= _paragraphs.Count) return index;
            paragraph = _paragraphs[paragraphIndex + 1]; lineIndex = 0;
        }
        return Character(paragraph, Math.Max(0, paragraph.Block.HitTestLine(lineIndex, preferredX).ClosestCodePointIndex));
    }
    internal void Paint(SKCanvas canvas, SkiaDrawingResources resources, float x, float y, int selectionStart, int selectionLength, Argentis.Color selectionColor)
    {
        Verify();
        if (!ReferenceEquals(resources, _resources)) throw new ArgumentException("Rich text layout belongs to a different surface.");
        foreach (var paragraph in _paragraphs)
        {
            var from = Math.Clamp(selectionStart - paragraph.Start, 0, paragraph.Text.Length);
            var to = Math.Clamp(selectionStart + selectionLength - paragraph.Start, 0, paragraph.Text.Length);
            var options = new TextPaintOptions { SelectionColor = Color(selectionColor) };
            if (to > from) options.Selection = new TextRange(CodePoint(paragraph, paragraph.Start + from), CodePoint(paragraph, paragraph.Start + to));
            paragraph.Block.Paint(canvas, new SKPoint(x, y + paragraph.Y), options);
            // A selected paragraph separator gets a visible cell even though it is outside the text block.
            var separator = paragraph.Start + paragraph.Text.Length;
            if (selectionStart <= separator && separator < selectionStart + selectionLength && separator < _document.Text.Length)
            {
                var caret = GetCaretBounds(separator);
                using var paint = new SKPaint { Color = Color(selectionColor) };
                canvas.DrawRect(x + caret.X, y + caret.Y, Math.Max(3, caret.Height / 3), caret.Height, paint);
            }
        }
    }
    public void Dispose()
    {
        if (_disposed) return;
        if (Environment.CurrentManagedThreadId != _thread) throw new InvalidOperationException("Rich text layout requires its owner thread.");
        _disposed = true;
        foreach (var paragraph in _paragraphs) paragraph.Block.Clear();
        _paragraphs.Clear();
    }
}

// Font instances are reused across edits on one surface, instead of creating a face per keystroke.
internal sealed class SkiaRichTextFontMapper(SkiaFontCatalog? catalog) : FontMapper, IDisposable
{
    // RichTextKit retains shapers/typefaces in a process cache. Reuse their face identities across
    // surfaces/catalog snapshots instead of accumulating a new retained shaper per editor session.
    private static readonly object FaceGate = new();
    private static readonly Dictionary<string, WeakReference<SKTypeface>> SharedFaces = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Family, int Weight, bool Italic), SKTypeface> _faces = [];
    private bool _disposed;
    internal string PrepareText(string text, IStyle style)
    {
        if (catalog == null) return text;
        // Keep explicit catalogs independent of system fallback. Substitution preserves code-point counts.
        using var font = new SKFont(TypefaceFromStyle(style, false), style.FontSize);
        System.Text.StringBuilder? result = null;
        var offset = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            var value = rune.ToString();
            var missing = font.GetGlyphs(value).Any(glyph => glyph == 0);
            if (missing)
            {
                if (font.GetGlyphs("\uFFFD").Any(glyph => glyph == 0))
                    throw new InvalidOperationException("An explicit rich-text font must contain the replacement glyph U+FFFD.");
                result ??= new System.Text.StringBuilder(text[..offset]);
                result.Append('\uFFFD');
            }
            else result?.Append(value);
            offset += rune.Utf16SequenceLength;
        }
        return result?.ToString() ?? text;
    }
    public override SKTypeface TypefaceFromStyle(IStyle style, bool ignoreFontVariants)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var key = (style.FontFamily, style.FontWeight, style.FontItalic);
        if (_faces.TryGetValue(key, out var face)) return face;
        var identity = catalog == null ? $"system:{style.FontFamily}:{style.FontWeight}:{style.FontItalic}"
            : "file:" + catalog.RichFaceIdentity(style.FontFamily, style.FontWeight, style.FontItalic);
        lock (FaceGate)
        {
            if (!SharedFaces.TryGetValue(identity, out var weak) || !weak.TryGetTarget(out face) || face.Handle == IntPtr.Zero)
            {
                face = catalog?.CreateTypeface(style.FontFamily, style.FontWeight, style.FontItalic) ?? base.TypefaceFromStyle(style, ignoreFontVariants);
                SharedFaces[identity] = new(face);
            }
        }
        _faces.Add(key, face);
        return face;
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // These faces are borrowed from RichTextKit's process-wide shaping lifetime.
        _faces.Clear();
    }
}
