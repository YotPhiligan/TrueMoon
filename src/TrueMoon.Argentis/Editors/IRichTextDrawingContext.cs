namespace TrueMoon.Argentis.Editors;

/// <summary>Optional drawing capability paired with IRichTextLayoutService.</summary>
public interface IRichTextDrawingContext
{
    /// <summary>Paints shaped text and the selected UTF-16 range at a translated origin.</summary>
    void RichText(IRichTextLayout layout, float x, float y, int selectionStart, int selectionLength, Color selectionColor);
}