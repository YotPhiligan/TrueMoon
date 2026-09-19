namespace TrueMoon.Argentis;

/// <summary>An immutable collection of typed property overrides.</summary>
public sealed class Style
{
    private readonly Dictionary<object, object?> _values;
    /// <summary>Creates an empty style.</summary>
    public Style() => _values = [];
    private Style(Dictionary<object, object?> values) => _values = values;
    /// <summary>Returns a new style with the given override.</summary>
    public Style With<T>(UiProperty<T> property, T value)
    {
        property.Validate(value);
        var values = new Dictionary<object, object?>(_values) { [property] = value };
        return new Style(values);
    }
    internal bool TryGet<T>(UiProperty<T> property, out T value)
    {
        if (_values.TryGetValue(property, out var item)) { value = (T)item!; return true; }
        value = default!;
        return false;
    }
}

/// <summary>Application-wide colors and typography.</summary>
public sealed record Theme(Color Surface, Color Control, Color Accent, Color Foreground, Color Disabled, float FontSize = 16, string FontFamily = "Segoe UI")
{
    /// <summary>The default dark palette.</summary>
    public static Theme Dark { get; } = new(new(25, 28, 36), new(48, 54, 67), new(93, 153, 255), new(235, 237, 242), new(120, 125, 137));
    /// <summary>A light palette.</summary>
    public static Theme Light { get; } = new(new(244, 246, 250), new(222, 227, 236), new(35, 100, 205), new(27, 32, 42), new(130, 135, 145));
}
