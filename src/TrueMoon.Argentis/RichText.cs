using System.Buffers;
using System.Text;

namespace TrueMoon.Argentis;

/// <summary>Optional character formatting; null members inherit the editor defaults.</summary>
public sealed record RichTextStyle
{
    private string? _fontFamily;
    private float? _fontSize, _lineHeight;
    /// <summary>Font family alias, or null to inherit.</summary>
    public string? FontFamily { get => _fontFamily; init { if (value != null) ArgumentException.ThrowIfNullOrWhiteSpace(value); _fontFamily = value; } }
    /// <summary>Positive logical font size, or null to inherit.</summary>
    public float? FontSize { get => _fontSize; init { ValidatePositive(value); _fontSize = value; } }
    /// <summary>Whether the run uses bold weight.</summary>
    public bool? Bold { get; init; }
    /// <summary>Whether the run uses italic styling.</summary>
    public bool? Italic { get; init; }
    /// <summary>Whether the run is underlined.</summary>
    public bool? Underline { get; init; }
    /// <summary>Whether the run has a strike-through.</summary>
    public bool? StrikeThrough { get; init; }
    /// <summary>Text color, or null to inherit.</summary>
    public Color? Foreground { get; init; }
    /// <summary>Run background color, or null for transparent.</summary>
    public Color? Background { get; init; }
    /// <summary>Positive line-height multiplier, or null for the font default.</summary>
    public float? LineHeight { get => _lineHeight; init { ValidatePositive(value); _lineHeight = value; } }
    private static void ValidatePositive(float? value)
    { if (value.HasValue && (!float.IsFinite(value.Value) || value.Value <= 0)) throw new ArgumentOutOfRangeException(nameof(value)); }
    internal RichTextStyle Merge(RichTextStyle patch) => new()
    {
        FontFamily = patch.FontFamily ?? FontFamily, FontSize = patch.FontSize ?? FontSize,
        Bold = patch.Bold ?? Bold, Italic = patch.Italic ?? Italic, Underline = patch.Underline ?? Underline,
        StrikeThrough = patch.StrikeThrough ?? StrikeThrough, Foreground = patch.Foreground ?? Foreground,
        Background = patch.Background ?? Background, LineHeight = patch.LineHeight ?? LineHeight
    };
}

/// <summary>An immutable piece of LF-normalized rich text.</summary>
public sealed record RichTextRun
{
    /// <summary>Creates a styled run. Text must contain valid UTF-16 and use LF line endings.</summary>
    /// <param name="text">Literal text.</param>
    /// <param name="style">Optional inherited formatting.</param>
    public RichTextRun(string text, RichTextStyle? style = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Contains('\r')) throw new ArgumentException("Rich document runs use LF line endings.", nameof(text));
        if (!IsValidUtf16(text)) throw new ArgumentException("Text must contain valid UTF-16.", nameof(text));
        Text = text; Style = style ?? new();
    }
    internal static bool IsValidUtf16(string text)
    {
        var remaining = text.AsSpan();
        while (!remaining.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(remaining, out _, out var consumed) != OperationStatus.Done)
                return false;
            remaining = remaining[consumed..];
        }
        return true;
    }
    /// <summary>The literal text.</summary>
    public string Text { get; }
    /// <summary>The immutable formatting.</summary>
    public RichTextStyle Style { get; }
}

/// <summary>An immutable rich-document snapshot suitable for one/two-way property binding.</summary>
public sealed class RichTextDocument
{
    /// <summary>The shared empty snapshot.</summary>
    public static RichTextDocument Empty { get; } = new([]);
    /// <summary>Snapshots runs and coalesces adjacent runs with equal formatting.</summary>
    /// <param name="runs">Ordered text runs; empty runs are ignored.</param>
    public RichTextDocument(IEnumerable<RichTextRun> runs)
    {
        ArgumentNullException.ThrowIfNull(runs);
        var result = new List<RichTextRun>();
        foreach (var run in runs)
        {
            ArgumentNullException.ThrowIfNull(run);
            if (run.Text.Length == 0) continue;
            if (result.Count > 0 && result[^1].Style == run.Style)
                result[^1] = new(result[^1].Text + run.Text, run.Style);
            else result.Add(run);
        }
        Runs = Array.AsReadOnly(result.ToArray());
        Text = string.Concat(result.Select(run => run.Text));
    }
    /// <summary>Ordered, immutable styled runs.</summary>
    public IReadOnlyList<RichTextRun> Runs { get; }
    /// <summary>Plain text with LF line endings and UTF-16 indices.</summary>
    public string Text { get; }
    /// <summary>Creates a plain snapshot and normalizes all line endings to LF.</summary>
    /// <param name="text">Literal text.</param>
    /// <returns>An immutable snapshot.</returns>
    public static RichTextDocument FromText(string text)
    { ArgumentNullException.ThrowIfNull(text); return new([new(text.ReplaceLineEndings("\n"))]); }
    internal RichTextStyle StyleAt(int index)
    {
        var offset = 0;
        foreach (var run in Runs) { if (index < offset + run.Text.Length) return run.Style; offset += run.Text.Length; }
        return Runs.Count > 0 ? Runs[^1].Style : new();
    }
    internal IEnumerable<RichTextRun> Slice(int start, int length)
    {
        var end = start + length; var offset = 0;
        foreach (var run in Runs)
        {
            var from = Math.Max(start, offset); var to = Math.Min(end, offset + run.Text.Length);
            if (to > from) yield return new(run.Text.Substring(from - offset, to - from), run.Style);
            offset += run.Text.Length;
        }
    }
    internal RichTextDocument Replace(int start, int length, string text, RichTextStyle style)
        => new(Slice(0, start).Concat([new RichTextRun(text, style)]).Concat(Slice(start + length, Text.Length - start - length)));
    internal RichTextDocument Format(int start, int length, RichTextStyle patch)
        => new(Slice(0, start).Concat(Slice(start, length).Select(run => new RichTextRun(run.Text, run.Style.Merge(patch))))
            .Concat(Slice(start + length, Text.Length - start - length)));
}

/// <summary>Horizontal alignment of wrapped rich paragraphs.</summary>
public enum RichTextAlignment
{
    /// <summary>Align paragraphs to the left.</summary>
    Left,
    /// <summary>Center paragraphs.</summary>
    Center,
    /// <summary>Align paragraphs to the right.</summary>
    Right
}

/// <summary>Immutable editing and presentation options. Assign a new snapshot to reconfigure an editor.</summary>
public sealed record RichTextEditorOptions
{
    private int _undoLimit = 100;
    /// <summary>Allows selection/copy/navigation while preventing user edits and formatting.</summary>
    public bool IsReadOnly { get; init; }
    /// <summary>Wraps paragraphs to the available content width.</summary>
    public bool WordWrap { get; init; } = true;
    /// <summary>Inserts a new paragraph for Enter.</summary>
    public bool AcceptsReturn { get; init; } = true;
    /// <summary>Consumes Tab and inserts four spaces. Otherwise Tab remains focus navigation.</summary>
    public bool AcceptsTab { get; init; }
    /// <summary>Whether an unfocused editor paints its selection.</summary>
    public bool ShowSelectionWhenUnfocused { get; init; }
    /// <summary>Paragraph alignment.</summary>
    public RichTextAlignment Alignment { get; init; }
    /// <summary>Optional selection highlight; null uses the theme accent.</summary>
    public Color? SelectionColor { get; init; }
    /// <summary>Maximum retained undo steps, from zero to one thousand. Zero disables history.</summary>
    public int UndoLimit { get => _undoLimit; init { if (value is < 0 or > 1000) throw new ArgumentOutOfRangeException(nameof(value)); _undoLimit = value; } }
}

/// <summary>Optional text-backend capability required by RichTextEditor.</summary>
public interface IRichTextLayoutService
{
    /// <summary>Creates a caller-owned layout using the same shaping/font service as painting.</summary>
    /// <param name="document">Immutable content.</param><param name="defaults">Resolved editor text defaults.</param>
    /// <param name="wrapWidth">Positive logical wrapping width, or null for no wrapping.</param>
    /// <param name="alignment">Paragraph alignment.</param><returns>An owned layout.</returns>
    IRichTextLayout CreateRichTextLayout(RichTextDocument document, RichTextStyle defaults, float? wrapWidth, RichTextAlignment alignment);
}

/// <summary>Backend-owned rich-text metrics with UTF-16 indices.</summary>
public interface IRichTextLayout : IDisposable
{
    /// <summary>The content extent in logical units.</summary>
    Size Size { get; }
    /// <summary>Maps a content point to a UTF-16 caret index.</summary>
    int HitTest(float x, float y);
    /// <summary>Gets the caret bounds for a UTF-16 index.</summary>
    Rect GetCaretBounds(int index);
    /// <summary>Moves the caret one visual line, preserving a preferred logical x coordinate.</summary>
    int MoveVertical(int index, int direction, float preferredX);
    /// <summary>Gets the start or end of the caret's visual line.</summary>
    int GetLineBoundary(int index, bool end);
}

/// <summary>Optional drawing capability paired with IRichTextLayoutService.</summary>
public interface IRichTextDrawingContext
{
    /// <summary>Paints shaped text and the selected UTF-16 range at a translated origin.</summary>
    void RichText(IRichTextLayout layout, float x, float y, int selectionStart, int selectionLength, Color selectionColor);
}
