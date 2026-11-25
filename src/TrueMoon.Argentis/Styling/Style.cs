using TrueMoon.Argentis.Layout;

namespace TrueMoon.Argentis.Styling;

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
    internal Style Overlay(Style other)
    {
        var values = new Dictionary<object, object?>(_values);
        foreach (var pair in other._values) values[pair.Key] = pair.Value;
        return new(values);
    }
}