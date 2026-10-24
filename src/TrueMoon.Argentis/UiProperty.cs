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
    /// <summary>Opacity of the whole rendered subtree; does not change layout or input admission.</summary>
    public static readonly UiProperty<float> Opacity = new("Opacity", 1f, Invalidation.Render, v => float.IsFinite(v) && v >= 0 && v <= 1);
    /// <summary>Semantic control color.</summary>
    public static readonly UiProperty<ControlTone> Tone = new("Tone", ControlTone.Neutral, validate: Enum.IsDefined);
    /// <summary>Visual error state, including an outline and a structural marker.</summary>
    public static readonly UiProperty<bool> HasError = new("HasError", false);
    /// <summary>Accessible semantic name; no operating-system accessibility bridge is implied.</summary>
    public static readonly UiProperty<string> AccessibleName = new("AccessibleName", "", validate: value => value != null);
    /// <summary>Explicit corner radius; local/style values override the theme.</summary>
    public static readonly UiProperty<float> CornerRadius = new("CornerRadius", 0, validate: value => float.IsFinite(value) && value >= 0 && value <= 64);
    /// <summary>Explicit inside outline width; zero hides the normal outline.</summary>
    public static readonly UiProperty<float> BorderWidth = new("BorderWidth", 0, Invalidation.Layout, value => float.IsFinite(value) && value >= 0 && value <= 16);
    /// <summary>Explicit outline color.</summary>
    public static readonly UiProperty<Color> BorderColor = new("BorderColor", Color.Transparent);
    /// <summary>Custom title-bar role; independent of any native window API.</summary>
    public static readonly UiProperty<WindowRegionRole> WindowRegion = new("WindowRegion", WindowRegionRole.Inherit,
        Invalidation.Tree, value => Enum.IsDefined(value));
    /// <summary>Explicit width, or NaN for automatic sizing.</summary>
    public static readonly UiProperty<float> Width = new("Width", float.NaN, Invalidation.Layout, Dimension);
    /// <summary>Explicit height, or NaN for automatic sizing.</summary>
    public static readonly UiProperty<float> Height = new("Height", float.NaN, Invalidation.Layout, Dimension);
    /// <summary>Internal spacing in logical pixels; local and style values override the control's theme padding.</summary>
    public static readonly UiProperty<Thickness> Padding = new("Padding", default, Invalidation.Layout, ThicknessValue);
    /// <summary>Distance between visible stack children in logical pixels.</summary>
    public static readonly UiProperty<float> Spacing = new("Spacing", 0, Invalidation.Layout, v => float.IsFinite(v) && v >= 0);
    /// <summary>Background fill.</summary>
    public static readonly UiProperty<Color> Background = new("Background", Color.Transparent);
    /// <summary>Optional frosted material drawn before background, foreground and children.</summary>
    public static readonly UiProperty<BackdropMaterial?> Backdrop = new("Backdrop", null);
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
    internal static bool ThicknessValue(Thickness value) => float.IsFinite(value.Left) && value.Left >= 0
        && float.IsFinite(value.Top) && value.Top >= 0 && float.IsFinite(value.Right) && value.Right >= 0
        && float.IsFinite(value.Bottom) && value.Bottom >= 0;
}
