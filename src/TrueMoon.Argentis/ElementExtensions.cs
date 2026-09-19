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
