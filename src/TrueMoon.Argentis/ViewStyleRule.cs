namespace TrueMoon.Argentis;

/// <summary>A declaration of typed property values in a view style layer.</summary>
public sealed class ViewStyleRule
{
    private readonly Element? _part;
    private readonly object? _role;
    private readonly List<Func<Theme, Style, Style>> _values = [];
    private bool _frozen;

    internal ViewStyleRule(Element? part, object? role)
    {
        _part = part;
        _role = role;
    }

    internal bool Matches(Element element) =>
        ReferenceEquals(_part, element) || _role != null && element.HasViewStyleRole(_role);

    internal void Freeze() => _frozen = true;

    internal Style Resolve(Theme theme)
    {
        var result = new Style();
        foreach (var value in _values) result = value(theme, result);
        return result;
    }

    /// <summary>Declares a fixed value. Later matching rules override earlier ones.</summary>
    public ViewStyleRule Set<T>(UiProperty<T> property, T value)
    {
        ArgumentNullException.ThrowIfNull(property);
        property.Validate(value);
        return Add((_, style) => style.With(property, value));
    }

    /// <summary>Declares a theme-dependent value, recomputed before theme changes commit.</summary>
    public ViewStyleRule Set<T>(UiProperty<T> property, Func<Theme, T> value)
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(value);
        return Add((theme, style) => style.With(property, value(theme)));
    }

    private ViewStyleRule Add(Func<Theme, Style, Style> value)
    {
        if (_frozen) throw new InvalidOperationException("A view style declaration is already complete.");
        _values.Add(value);
        return this;
    }

    /// <summary>Declares uniform internal spacing.</summary>
    public ViewStyleRule Padding(float value) => Padding(new Thickness(value));

    /// <summary>Declares internal spacing.</summary>
    public ViewStyleRule Padding(Thickness value) => Set(UiProperties.Padding, value);

    /// <summary>Declares text size.</summary>
    public ViewStyleRule FontSize(float value) => Set(UiProperties.FontSize, value);

    /// <summary>Declares the font family.</summary>
    public ViewStyleRule FontFamily(string value) => Set(UiProperties.FontFamily, value);

    /// <summary>Declares a font family derived from the current theme.</summary>
    public ViewStyleRule FontFamily(Func<Theme, string> value) => Set(UiProperties.FontFamily, value);

    /// <summary>Declares a background color.</summary>
    public ViewStyleRule Background(Color value) => Set(UiProperties.Background, value);

    /// <summary>Declares a background derived from the current theme.</summary>
    public ViewStyleRule Background(Func<Theme, Color> value) => Set(UiProperties.Background, value);
    /// <summary>Declares an optional background material.</summary>
    public ViewStyleRule Backdrop(BackdropMaterial? value) => Set(UiProperties.Backdrop, value);

    /// <summary>Declares a material derived from the current theme.</summary>
    public ViewStyleRule Backdrop(Func<Theme, BackdropMaterial?> value) => Set(UiProperties.Backdrop, value);

    /// <summary>Declares a foreground color.</summary>
    public ViewStyleRule Foreground(Color value) => Set(UiProperties.Foreground, value);

    /// <summary>Declares a foreground derived from the current theme.</summary>
    public ViewStyleRule Foreground(Func<Theme, Color> value) => Set(UiProperties.Foreground, value);

    /// <summary>Declares an inside outline width.</summary>
    public ViewStyleRule BorderWidth(float value) => Set(UiProperties.BorderWidth, value);

    /// <summary>Declares spacing between stack children.</summary>
    public ViewStyleRule Spacing(float value) => Set(UiProperties.Spacing, value);

    /// <summary>Declares a fixed height, or NaN for automatic sizing.</summary>
    public ViewStyleRule Height(float value) => Set(UiProperties.Height, value);

    /// <summary>Declares a semantic action tone.</summary>
    public ViewStyleRule Tone(ControlTone value) => Set(UiProperties.Tone, value);
}