namespace TrueMoon.Argentis;

/// <summary>A retained, responsive label/value row with an optional accessory and explicit empty-value presentation.</summary>
/// <remarks>Owns its parts. Numeric parsing, null semantics and commit belong to the consumer.</remarks>
public sealed class PropertyEditorRow : ElementList
{
    /// <summary>Preferred shared label-column width.</summary>
    public static readonly UiProperty<float> LabelWidthProperty = new("LabelWidth", 88, Invalidation.Layout, v => float.IsFinite(v) && v >= 0);
    /// <summary>Preferred minimum value-column width before placing the label above the editor.</summary>
    public static readonly UiProperty<float> MinimumValueWidthProperty = new("MinimumValueWidth", 80, Invalidation.Layout, v => float.IsFinite(v) && v > 0);
    /// <summary>Maximum preferred value width, or NaN to fill available space.</summary>
    public static readonly UiProperty<float> ValueWidthProperty = new("ValueWidth", float.NaN, Invalidation.Layout, UiProperties.Dimension);
    /// <summary>The retained field label.</summary>
    public Text Label { get; } = new() { VerticalAlignment = LayoutAlignment.Center };
    /// <summary>The owned editor, retained even when an empty value is presented.</summary>
    public Element Editor { get; }
    /// <summary>Optional owned null switch or other small field action.</summary>
    public Element? Accessory { get; }
    /// <summary>Presentation used while the consumer explicitly hides an absent value.</summary>
    public Text Empty { get; } = new("Не задано") { IsVisible = false, VerticalAlignment = LayoutAlignment.Center };
    /// <summary>Effective label-column width.</summary>
    public float LabelWidth { get => Get(LabelWidthProperty); set => Set(LabelWidthProperty, value); }
    /// <summary>Preferred minimum value-column width.</summary>
    public float MinimumValueWidth { get => Get(MinimumValueWidthProperty); set => Set(MinimumValueWidthProperty, value); }
    /// <summary>Preferred maximum width, or NaN for a filling editor.</summary>
    public float ValueWidth { get => Get(ValueWidthProperty); set => Set(ValueWidthProperty, value); }
    /// <summary>Gap between columns and stacked label/value.</summary>
    public float Spacing { get => Get(UiProperties.Spacing); set => Set(UiProperties.Spacing, value); }

    /// <summary>Creates an owned row from an unowned editor and optional unowned accessory.</summary>
    public PropertyEditorRow(Element editor, Element? accessory = null, string label = "")
    {
        ArgumentNullException.ThrowIfNull(editor); ArgumentNullException.ThrowIfNull(label);
        ValidateChild(editor);
        if (accessory != null) { if (ReferenceEquals(editor, accessory)) throw new ArgumentException("Editor and accessory must be distinct.", nameof(accessory)); ValidateChild(accessory); }
        Editor = editor; Accessory = accessory; Label.Value = label;
        Items.Add(Label); Items.Add(editor); Items.Add(Empty); if (accessory != null) Items.Add(accessory);
    }

    /// <summary>Shows an absent-value label without replacing or clearing the editor's draft.</summary>
    public void ShowEmpty(bool empty) { VerifyAccess(); Editor.IsVisible = !empty; Empty.IsVisible = empty; }

    private float AccessoryWidth => Accessory is { IsVisible: true } ? Accessory.DesiredSize.Width : 0;
    private float AccessoryGap => Accessory is { IsVisible: true } ? Spacing : 0;
    private float Threshold => LabelWidth + Spacing + MinimumValueWidth + AccessoryWidth + AccessoryGap;
    private Element ValueElement => Empty.IsVisible ? Empty : Editor;
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text)
    {
        Accessory?.Measure(available, text);
        var stacked = available.Width < Threshold;
        Label.Measure(new(stacked ? available.Width : LabelWidth, available.Height), text);
        var room = Math.Max(0, available.Width - (stacked ? 0 : LabelWidth + Spacing) - AccessoryWidth - AccessoryGap);
        var width = float.IsNaN(ValueWidth) ? room : Math.Min(room, ValueWidth);
        Editor.Measure(new(width, available.Height), text); Empty.Measure(new(width, available.Height), text);
        var height = Math.Max(ValueElement.DesiredSize.Height, Accessory?.DesiredSize.Height ?? 0);
        return new(float.IsFinite(available.Width) ? available.Width : Threshold,
            stacked ? Label.DesiredSize.Height + Spacing + height : Math.Max(Label.DesiredSize.Height, height));
    }
    /// <inheritdoc />
    protected override void ArrangeCore(Rect area)
    {
        var stacked = area.Width < Threshold;
        var height = Math.Max(ValueElement.DesiredSize.Height, Accessory?.DesiredSize.Height ?? 0);
        var labelHeight = Math.Min(area.Height, stacked ? Label.DesiredSize.Height : height);
        Label.Arrange(new(area.X, area.Y, stacked ? area.Width : Math.Min(LabelWidth, area.Width), labelHeight));
        var x = area.X + (stacked ? 0 : Math.Min(area.Width, LabelWidth + Spacing));
        var y = area.Y + Math.Min(area.Height, stacked ? labelHeight + Spacing : 0);
        var room = Math.Max(0, area.Width - (x - area.X));
        var accessory = Math.Min(room, AccessoryWidth); var gap = Math.Min(AccessoryGap, Math.Max(0, room - accessory));
        var width = Math.Max(0, room - accessory - gap);
        if (!float.IsNaN(ValueWidth)) width = Math.Min(width, ValueWidth);
        var slot = new Rect(x, y, width, Math.Max(0, area.Height - (y - area.Y)));
        Editor.Arrange(Editor.IsVisible ? slot : default); Empty.Arrange(Empty.IsVisible ? slot : default);
        Accessory?.Arrange(new(x + width + gap, y, accessory, slot.Height));
    }
}
