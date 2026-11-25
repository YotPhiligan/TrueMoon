using System.Buffers;
using System.Text;

namespace TrueMoon.Argentis.Editors;

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