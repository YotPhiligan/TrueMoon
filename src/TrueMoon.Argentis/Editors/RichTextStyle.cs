using TrueMoon.Argentis.Styling;

namespace TrueMoon.Argentis.Editors;

/// <summary>Optional character formatting; null members inherit the editor defaults.</summary>
public sealed record RichTextStyle
{
    private string? _fontFamily;
    private float? _fontSize, _lineHeight;
    private int? _fontWeight;
    /// <summary>Font family alias, or null to inherit.</summary>
    public string? FontFamily { get => _fontFamily; init { if (value != null) ArgumentException.ThrowIfNullOrWhiteSpace(value); _fontFamily = value; } }
    /// <summary>Positive logical font size, or null to inherit.</summary>
    public float? FontSize { get => _fontSize; init { ValidatePositive(value); _fontSize = value; } }
    /// <summary>Whether the run uses bold weight.</summary>
    public bool? Bold { get; init; }
    /// <summary>Weight100..900, or null to inherit. An explicit weight takes precedence over Bold.</summary>
    public int? FontWeight
    {
        get => _fontWeight;
        init { if (value.HasValue) UiProperties.FontWeight.Validate(value.Value); _fontWeight = value; }
    }
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
        FontWeight = patch.FontWeight ?? (patch.Bold.HasValue ? null : FontWeight),
        Bold = patch.Bold ?? (patch.FontWeight.HasValue ? null : Bold), Italic = patch.Italic ?? Italic, Underline = patch.Underline ?? Underline,
        StrikeThrough = patch.StrikeThrough ?? StrikeThrough, Foreground = patch.Foreground ?? Foreground,
        Background = patch.Background ?? Background, LineHeight = patch.LineHeight ?? LineHeight
    };
}