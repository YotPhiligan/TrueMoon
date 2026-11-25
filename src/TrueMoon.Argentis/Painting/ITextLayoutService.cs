namespace TrueMoon.Argentis.Painting;

/// <summary>Backend-independent text measurement. The same service must be used for layout and drawing.</summary>
public interface ITextLayoutService
{
    /// <summary>Measures a single line using the selected font.</summary>
    Size Measure(string text, float fontSize, string fontFamily);
    /// <summary>Measures a styled single line. Legacy providers fall back to their normal face.</summary>
    /// <param name="text">Literal single-line text.</param><param name="fontSize">Logical font size.</param>
    /// <param name="fontFamily">Requested family.</param><param name="fontWeight">Weight100..900;400 is regular and600 semibold.</param>
    /// <param name="fontItalic">Whether to select an italic face.</param><returns>Logical text dimensions.</returns>
    Size Measure(string text, float fontSize, string fontFamily, int fontWeight, bool fontItalic) => Measure(text, fontSize, fontFamily);
    /// <summary>Measures unshaped text in a single-line editor, including prefixes used for hit testing.
    /// Providers without a dedicated editor mode use their normal styled text metrics.</summary>
    Size MeasureEditorText(string text, float fontSize, string fontFamily, int fontWeight, bool fontItalic) =>
        Measure(text, fontSize, fontFamily, fontWeight, fontItalic);
    /// <summary>Measures glyph ink relative to the drawing origin of a line. Legacy providers return its layout box.</summary>
    /// <param name="text">Literal single-line text.</param><param name="fontSize">Logical font size.</param>
    /// <param name="fontFamily">Requested family.</param><param name="fontWeight">Font weight from 100 to 900.</param>
    /// <param name="fontItalic">Whether to select an italic face.</param><returns>The logical bounds relative to the line origin.</returns>
    Rect MeasureInkBounds(string text, float fontSize, string fontFamily, int fontWeight, bool fontItalic)
    {
        var size = Measure(text, fontSize, fontFamily, fontWeight, fontItalic);
        return new(0, 0, size.Width, size.Height);
    }
}