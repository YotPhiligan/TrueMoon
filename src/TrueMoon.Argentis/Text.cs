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
    private static readonly ElementPresentation Label = ElementPresentation.Create<Text>(
        new Factory<IElementPresenter<Text>>(static () => new TextPresenter()));
    /// <inheritdoc />
    public override ElementPresentation Presentation => Label;
}
