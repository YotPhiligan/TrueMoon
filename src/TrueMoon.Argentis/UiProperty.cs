namespace TrueMoon.Argentis;

/// <summary>The work invalidated by a property or tree mutation.</summary>
[Flags]
public enum Invalidation { Render = 1, Layout = 2, Tree = 4 }

/// <summary>A typed property identifier shared by elements and styles.</summary>
public sealed class UiProperty<T>(string name, T defaultValue, Invalidation affects = Invalidation.Render, Func<T, bool>? validate = null)
{
    /// <summary>The diagnostic name.</summary>
    public string Name { get; } = name;
    /// <summary>The value used when neither local nor styled values exist.</summary>
    public T DefaultValue { get; } = defaultValue;
    /// <summary>The invalidated work.</summary>
    public Invalidation Affects { get; } = affects;
    internal void Validate(T value)
    {
        if (validate != null && !validate(value)) throw new ArgumentOutOfRangeException(Name);
    }
}

/// <summary>Properties shared by the standard widgets.</summary>
public static class UiProperties
{
    /// <summary>Explicit width, or NaN for automatic sizing.</summary>
    public static readonly UiProperty<float> Width = new("Width", float.NaN, Invalidation.Layout, Dimension);
    /// <summary>Explicit height, or NaN for automatic sizing.</summary>
    public static readonly UiProperty<float> Height = new("Height", float.NaN, Invalidation.Layout, Dimension);
    /// <summary>Background fill.</summary>
    public static readonly UiProperty<Color> Background = new("Background", Color.Transparent);
    /// <summary>Text and foreground color.</summary>
    public static readonly UiProperty<Color> Foreground = new("Foreground", new Color(235, 237, 242));
    /// <summary>Font size in logical pixels.</summary>
    public static readonly UiProperty<float> FontSize = new("FontSize", 16, Invalidation.Layout, v => float.IsFinite(v) && v > 0);
    /// <summary>Font family name.</summary>
    public static readonly UiProperty<string> FontFamily = new("FontFamily", "Segoe UI", Invalidation.Layout, v => !string.IsNullOrWhiteSpace(v));
    /// <summary>Whether the element participates in layout and rendering.</summary>
    public static readonly UiProperty<bool> Visible = new("Visible", true, Invalidation.Layout | Invalidation.Tree);
    /// <summary>Whether the element accepts input.</summary>
    public static readonly UiProperty<bool> Enabled = new("Enabled", true, Invalidation.Render | Invalidation.Tree);
    internal static bool Dimension(float v) => float.IsNaN(v) || float.IsFinite(v) && v >= 0;
}
