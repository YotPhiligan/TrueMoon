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
    protected override Size MeasureCore(Size available, ITextLayoutService text) => text.Measure(Value, FontSize, FontFamily);
    /// <inheritdoc />
    protected override void DrawCore(IDrawingContext context)
    {
        var area = Bounds.Deflate(Padding);
        context.Text(Value, area.X, area.Y, IsEffectivelyEnabled ? Foreground : Theme.Disabled, FontSize, FontFamily);
    }
}
