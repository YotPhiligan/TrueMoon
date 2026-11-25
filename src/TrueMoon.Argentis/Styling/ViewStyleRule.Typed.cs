namespace TrueMoon.Argentis.Styling;

/// <summary>A view style declaration that preserves its target control type in Fluent chains.</summary>
/// <typeparam name="TElement">The type selected by the style part.</typeparam>
public sealed class ViewStyleRule<TElement> where TElement : Element
{
    private readonly ViewStyleRule _rule;

    internal ViewStyleRule(ViewStyleRule rule) => _rule = rule;

    /// <summary>Declares a fixed property value in the existing view style layer.</summary>
    public ViewStyleRule<TElement> Set<T>(UiProperty<T> property, T value)
    {
        _rule.Set(property, value);
        return this;
    }

    /// <summary>Declares a theme-dependent property value in the existing view style layer.</summary>
    public ViewStyleRule<TElement> Set<T>(UiProperty<T> property, Func<Theme, T> value)
    {
        _rule.Set(property, value);
        return this;
    }

    /// <summary>Declares text size, retaining the selected control type.</summary>
    public ViewStyleRule<TElement> FontSize(float value) => Set(UiProperties.FontSize, value);
}
