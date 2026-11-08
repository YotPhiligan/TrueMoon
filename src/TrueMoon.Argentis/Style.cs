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
    internal Style Overlay(Style other)
    {
        var values = new Dictionary<object, object?>(_values);
        foreach (var pair in other._values) values[pair.Key] = pair.Value;
        return new(values);
    }
}

/// <summary>Application-wide colors, typography, and control spacing.</summary>
public sealed record Theme(Color Surface, Color Control, Color Accent, Color Foreground, Color Disabled, float FontSize = 16, string FontFamily = FontDefaults.Family)
{
    private int _fontWeight = 400;
    /// <summary>Default font weight100..900;400 is regular and600 semibold.</summary>
    public int FontWeight
    {
        get => _fontWeight;
        init { UiProperties.FontWeight.Validate(value); _fontWeight = value; }
    }
    /// <summary>Whether default text uses an italic face.</summary>
    public bool FontItalic { get; init; }
    private Thickness _buttonPadding = new(12, 8, 12, 8), _editorPadding = new(8);
    private float _stackSpacing;
    private float _cornerRadius, _borderWidth = 1;
    /// <summary>Normal meaningful outline, also used for inactive window framing.</summary>
    public Color Border { get; init; } = new(104, 121, 144);
    /// <summary>Hovered neutral control surface.</summary>
    public Color ControlHover { get; init; } = new(45, 58, 76);
    /// <summary>Pressed neutral control surface.</summary>
    public Color ControlPressed { get; init; } = new(25, 65, 104);
    /// <summary>Disabled control surface, separate from disabled text.</summary>
    public Color ControlDisabled { get; init; } = new(25, 31, 41);
    /// <summary>Editable field surface.</summary>
    public Color Input { get; init; } = new(22, 28, 38);
    /// <summary>Read-only field surface.</summary>
    public Color ReadOnly { get; init; } = new(27, 34, 45);
    /// <summary>Frosted menu surface; null selects the ordinary opaque Surface fill.</summary>
    public BackdropMaterial? MenuBackdrop { get; init; } = new(12, new(18, 22, 30, 176), new(18, 22, 30));
    /// <summary>Frosted persistent panel surface; null lets a consumer select its opaque Surface fill.</summary>
    public BackdropMaterial? PanelBackdrop { get; init; } = new(8, new(18, 22, 30, 208), new(18, 22, 30));
    /// <summary>Outer shadow for semantic panels; layout containers opt in through their style.</summary>
    public BoxShadow? PanelShadow { get; init; } = new(5, 0, 3, new(0, 0, 0, 90));
    /// <summary>Outer shadow for menus and transient hints.</summary>
    public BoxShadow? MenuShadow { get; init; } = new(7, 0, 4, new(0, 0, 0, 125));
    /// <summary>Keyboard focus outline; also distinguished by its two-pixel shape.</summary>
    public Color Focus { get; init; } = new(255, 201, 92);
    /// <summary>Error outline/marker color.</summary>
    public Color Error { get; init; } = new(255, 112, 128);
    /// <summary>Contrasting symbols on a filled validation badge.</summary>
    public Color OnError { get; init; } = new(18, 22, 30);
    /// <summary>Hovered outline when no stronger state is active.</summary>
    public Color BorderHover { get; init; } = new(153, 176, 203);
    /// <summary>Pending edit outline and structural marker.</summary>
    public Color Modified { get; init; } = new(208, 169, 242);
    /// <summary>Scalar numeric editor accent.</summary>
    public Color NumberAccent { get; init; } = new(117, 188, 226);
    /// <summary>Text editor accent.</summary>
    public Color TextAccent { get; init; } = new(201, 177, 233);
    /// <summary>Boolean editor accent.</summary>
    public Color BooleanAccent { get; init; } = new(112, 200, 184);
    /// <summary>Symbols on semantic editor accent fills, independently of action-button contrast.</summary>
    public Color OnEditorAccent { get; init; } = new(18, 22, 30);
    /// <summary>Object/resource reference editor accent.</summary>
    public Color ReferenceAccent { get; init; } = new(225, 185, 111);
    /// <summary>X-axis accent, also used for pitch in a spatial editor.</summary>
    public Color AxisX { get; init; } = new(238, 132, 142);
    /// <summary>Y-axis accent, also used for yaw in a spatial editor.</summary>
    public Color AxisY { get; init; } = new(134, 204, 151);
    /// <summary>Z-axis accent, also used for roll in a spatial editor.</summary>
    public Color AxisZ { get; init; } = new(126, 180, 241);
    /// <summary>Destructive action background.</summary>
    public Color Danger { get; init; } = new(190, 43, 61);
    /// <summary>Positive action background.</summary>
    public Color Positive { get; init; } = new(24, 119, 91);
    /// <summary>Text/icons on accent, positive and destructive action backgrounds.</summary>
    public Color OnAccent { get; init; } = new(255, 255, 255);
    /// <summary>Corner radius in logical units; default style has square corners.</summary>
    public float CornerRadius { get => _cornerRadius; init { UiProperties.CornerRadius.Validate(value); _cornerRadius = value; } }
    /// <summary>Control outline width in logical units; drawn inside bounds.</summary>
    public float BorderWidth { get => _borderWidth; init { UiProperties.BorderWidth.Validate(value); _borderWidth = value; } }
    /// <summary>Default padding of buttons; check boxes add room for their indicator.</summary>
    public Thickness ButtonPadding { get => _buttonPadding; init { UiProperties.Padding.Validate(value); _buttonPadding = value; } }
    /// <summary>Default padding of single-line editors.</summary>
    public Thickness EditorPadding { get => _editorPadding; init { UiProperties.Padding.Validate(value); _editorPadding = value; } }
    /// <summary>Default distance between visible stack children; zero preserves the original layout default.</summary>
    public float StackSpacing { get => _stackSpacing; init { UiProperties.Spacing.Validate(value); _stackSpacing = value; } }
    /// <summary>The default dark palette.</summary>
    public static Theme Dark { get; } = new(new(18, 22, 30), new(32, 40, 52), new(24, 104, 178), new(231, 237, 245), new(142, 154, 170));
    /// <summary>A light palette.</summary>
    public static Theme Light { get; } = new(new(242, 245, 250), new(255, 255, 255), new(24, 104, 178), new(24, 34, 49), new(92, 105, 122))
    {
        Border = new(114, 128, 147), ControlHover = new(226, 235, 247), ControlPressed = new(202, 220, 241),
        ControlDisabled = new(226, 231, 239), Input = new(255, 255, 255), ReadOnly = new(233, 238, 245),
        Focus = new(142, 72, 0), Error = new(174, 30, 51), OnError = new(255, 255, 255),
        BorderHover = new(60, 83, 113), Modified = new(113, 65, 155),
        NumberAccent = new(27, 104, 143), TextAccent = new(116, 67, 155),
        BooleanAccent = new(22, 113, 94), ReferenceAccent = new(135, 91, 20),
        OnEditorAccent = new(255, 255, 255),
        AxisX = new(171, 48, 65), AxisY = new(30, 117, 55), AxisZ = new(32, 99, 175),
        MenuBackdrop = new(12, new(242, 245, 250, 192), new(242, 245, 250)),
        PanelBackdrop = new(8, new(242, 245, 250, 216), new(242, 245, 250)),
        PanelShadow = new(5, 0, 3, new(20, 30, 45, 48)),
        MenuShadow = new(7, 0, 4, new(20, 30, 45, 76))
    };

    internal Color ActionColor(ControlTone tone) => tone switch { ControlTone.Accent => Accent, ControlTone.Positive => Positive, ControlTone.Danger => Danger, _ => Control };
    /// <summary>Resolves a semantic editor accent in this palette.</summary>
    public Color EditorColor(EditorColorRole role) => role switch
    {
        EditorColorRole.Neutral => Foreground, EditorColorRole.Number => NumberAccent,
        EditorColorRole.Text => TextAccent, EditorColorRole.Boolean => BooleanAccent,
        EditorColorRole.Reference => ReferenceAccent, EditorColorRole.AxisX => AxisX,
        EditorColorRole.AxisY => AxisY, EditorColorRole.AxisZ => AxisZ,
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };
    internal static Color Mix(Color a, Color b, float amount) => new((byte)MathF.Round(a.R + (b.R - a.R) * amount),
        (byte)MathF.Round(a.G + (b.G - a.G) * amount), (byte)MathF.Round(a.B + (b.B - a.B) * amount), a.A);
}
