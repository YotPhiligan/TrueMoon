using TrueMoon.Argentis.Painting;

namespace TrueMoon.Argentis;

/// <summary>A single line of display text.</summary>
public class Text : Element, ITextElement
{
    /// <summary>The text content property.</summary>
    public static readonly UiProperty<string> ValueProperty = new("Text", "", Invalidation.Layout, v => v != null);
    /// <summary>Creates a label.</summary>
    public Text(string value = "") => Value = value;
    /// <summary>The displayed string.</summary>
    public string Value { get => Get(ValueProperty); set => Set(ValueProperty, value); }
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text) => text.Measure(Value, FontSize, FontFamily, FontWeight, FontItalic);

    /// <summary>Fits the current value to a single line using the element's current font.</summary>
    /// <param name="text">Measurement service, or <see langword="null"/> if layout has not been measured.</param>
    /// <param name="width">Logical width available for the text alone.</param>
    /// <returns>The original text, the longest fitting prefix followed by an ellipsis, or an empty string if the ellipsis does not fit.</returns>
    protected string FitSingleLine(ITextLayoutService? text, float width)
    {
        var value = Value;
        if (text is null || value.Length == 0) return value;
        if (width <= 0) return "";

        var size = FontSize;
        var family = FontFamily;
        var weight = FontWeight;
        var italic = FontItalic;
        if (text.Measure(value, size, family, weight, italic).Width <= width) return value;

        const string ellipsis = "…";
        if (text.Measure(ellipsis, size, family, weight, italic).Width > width) return "";

        for (var length = value.Length - 1; length > 0; length--)
        {
            if (char.IsSurrogatePair(value[length - 1], value[length])) continue;
            var candidate = string.Concat(value.AsSpan(0, length), ellipsis);
            if (text.Measure(candidate, size, family, weight, italic).Width <= width) return candidate;
        }
        return ellipsis;
    }
}
