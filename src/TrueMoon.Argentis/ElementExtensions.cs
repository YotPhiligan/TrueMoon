using System.ComponentModel;
using System.Linq.Expressions;

namespace TrueMoon.Argentis;

/// <summary>Fluent composition and configuration helpers.</summary>
public static class ElementExtensions
{
    /// <summary>Sets a fixed width.</summary>
    public static T Width<T>(this T element, float width) where T : Element { element.Width = width; return element; }
    /// <summary>Sets a fixed height.</summary>
    public static T Height<T>(this T element, float height) where T : Element { element.Height = height; return element; }
    /// <summary>Sets a typed property.</summary>
    public static T SetValue<T, TValue>(this T element, UiProperty<TValue> property, TValue value) where T : Element { element.Set(property, value); return element; }
    /// <summary>Binds a typed UI property to one directly selected source property until element disposal.</summary>
    /// <param name="element">The element, preserving its concrete type in the Fluent chain.</param>
    /// <param name="property">The destination UI property; only one binding per property is allowed.</param>
    /// <param name="source">The notifying source. Its getter must be safe to read on the UI owner thread.</param>
    /// <param name="selector">A direct instance property access, for example x =&gt; x.Name. Nested paths and computed expressions are unsupported.</param>
    /// <returns>The configured element.</returns>
    /// <remarks>One-way only. Initial synchronization runs on attach, including reattach. Source events queue reads through Dispatch for the next host update. Detach removes the subscription; local UI edits do not update the source.</remarks>
    public static T Bind<T, TSource, TValue>(this T element, UiProperty<TValue> property, TSource source, Expression<Func<TSource, TValue>> selector)
        where T : Element where TSource : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        element.OwnBinding(property, () => new PropertyBinding<TSource, TValue>(element, property, source, selector));
        return element;
    }
    /// <summary>Binds a typed UI property to a writable source property in both directions until disposal.</summary>
    /// <param name="element">The element, preserving its concrete type.</param>
    /// <param name="property">The UI property; one binding of either direction is allowed per property.</param>
    /// <param name="source">The notifying source, whose getter and setter run on the UI owner thread.</param>
    /// <param name="selector">A direct public readable/writable property of exactly TValue; conversions and init-only setters are unsupported.</param>
    /// <returns>The configured element.</returns>
    /// <remarks>Initial attach reads the source. Attached UI changes write immediately and read back its canonical value. On failure, readback restoration is attempted before the error propagates; partial source changes are not rolled back. Detach disconnects both directions.</remarks>
    public static T BindTwoWay<T, TSource, TValue>(this T element, UiProperty<TValue> property, TSource source, Expression<Func<TSource, TValue>> selector)
        where T : Element where TSource : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        element.OwnBinding(property, () => new PropertyBinding<TSource, TValue>(element, property, source, selector, twoWay: true));
        return element;
    }
    /// <summary>Binds plain text to a directly selected notifying source property.</summary>
    /// <param name="element">The text element.</param>
    /// <param name="source">The notifying source.</param>
    /// <param name="selector">A direct string property access, for example x =&gt; x.Name.</param>
    /// <returns>The same concrete text element.</returns>
    public static T BindText<T, TSource>(this T element, TSource source, Expression<Func<TSource, string>> selector)
        where T : Text where TSource : class, INotifyPropertyChanged => element.Bind(Text.ValueProperty, source, selector);
    /// <summary>Applies arbitrary strongly typed configuration.</summary>
    public static T Configure<T>(this T element, Action<T> configure) where T : Element { configure(element); return element; }
    /// <summary>Applies internal spacing.</summary>
    public static T Padding<T>(this T element, float padding) where T : Element { element.Padding = new Thickness(padding); return element; }
    /// <summary>Applies a background color.</summary>
    public static T Background<T>(this T element, Color color) where T : Element { element.Background = color; return element; }
    /// <summary>Adds child elements in paint order.</summary>
    public static T WithChildren<T>(this T element, params Element[] children) where T : ElementList { foreach (var child in children) element.Items.Add(child); return element; }
    /// <summary>Assigns replaceable content.</summary>
    public static T Content<T>(this T element, Element? content) where T : ContentControl { element.SetContent(content); return element; }
    /// <summary>Registers a button action.</summary>
    public static T OnClick<T>(this T element, Action action) where T : Button { element.Click += action; return element; }
}
