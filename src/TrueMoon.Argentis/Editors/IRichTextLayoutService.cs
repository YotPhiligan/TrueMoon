namespace TrueMoon.Argentis.Editors;

/// <summary>Optional text-backend capability required by RichTextEditor.</summary>
public interface IRichTextLayoutService
{
    /// <summary>Creates a caller-owned layout using the same shaping/font service as painting.</summary>
    /// <param name="document">Immutable content.</param><param name="defaults">Resolved editor text defaults.</param>
    /// <param name="wrapWidth">Positive logical wrapping width, or null for no wrapping.</param>
    /// <param name="alignment">Paragraph alignment.</param><returns>An owned layout.</returns>
    IRichTextLayout CreateRichTextLayout(RichTextDocument document, RichTextStyle defaults, float? wrapWidth, RichTextAlignment alignment);
}