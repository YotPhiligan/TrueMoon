namespace TrueMoon.Argentis;

/// <summary>Three equal numeric draft columns with a responsive shared label. Has no engine or vector-type dependency.</summary>
/// <remarks>The owned cells may contain consumer diagnostics/details beneath each NumberBox. Resize never replaces the editors.</remarks>
public sealed class Vector3Editor : ElementList
{
    /// <summary>Label-column width, shared by sibling rows.</summary>
    public static readonly UiProperty<float> LabelWidthProperty = new("LabelWidth", 88, Invalidation.Layout, v => float.IsFinite(v) && v >= 0);
    /// <summary>Minimum preferred column width before moving the label above the columns.</summary>
    public static readonly UiProperty<float> ColumnWidthProperty = new("ColumnWidth", 64, Invalidation.Layout, v => float.IsFinite(v) && v > 0);
    /// <summary>The shared row label.</summary>
    public Text Label { get; } = new() { VerticalAlignment = LayoutAlignment.Center };
    /// <summary>The first numeric editor.</summary>
    public NumberBox First { get; } = new("X") { ColorRole = EditorColorRole.AxisX };
    /// <summary>The second numeric editor.</summary>
    public NumberBox Second { get; } = new("Y") { ColorRole = EditorColorRole.AxisY };
    /// <summary>The third numeric editor.</summary>
    public NumberBox Third { get; } = new("Z") { ColorRole = EditorColorRole.AxisZ };
    /// <summary>Owns the first editor and optional diagnostics/details.</summary>
    public VStack FirstCell { get; } = new();
    /// <summary>Owns the second editor and optional diagnostics/details.</summary>
    public VStack SecondCell { get; } = new();
    /// <summary>Owns the third editor and optional diagnostics/details.</summary>
    public VStack ThirdCell { get; } = new();
    /// <summary>Effective label-column width.</summary>
    public float LabelWidth { get => Get(LabelWidthProperty); set => Set(LabelWidthProperty, value); }
    /// <summary>Preferred minimum width of each column.</summary>
    public float ColumnWidth { get => Get(ColumnWidthProperty); set => Set(ColumnWidthProperty, value); }
    /// <summary>Gap between label and columns and between the columns.</summary>
    public float Spacing { get => Get(UiProperties.Spacing); set => Set(UiProperties.Spacing, value); }
    private float Threshold => LabelWidth + ColumnWidth * 3 + Spacing * 3;
    /// <summary>Creates a row with three owned numeric fields.</summary>
    public Vector3Editor(string label = "")
    {
        ArgumentNullException.ThrowIfNull(label); Label.Value = label;
        FirstCell.Add(First); SecondCell.Add(Second); ThirdCell.Add(Third);
        Items.Add(Label); Items.Add(FirstCell); Items.Add(SecondCell); Items.Add(ThirdCell);
    }
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text)
    {
        var stacked = available.Width < Threshold;
        Label.Measure(new(stacked ? available.Width : LabelWidth, available.Height), text);
        var width = Math.Max(0, (available.Width - (stacked ? 0 : LabelWidth + Spacing) - Spacing * 2) / 3);
        for (var i = 1; i < Children.Count; i++) Children[i].Measure(new(width, available.Height), text);
        var height = Math.Max(FirstCell.DesiredSize.Height, Math.Max(SecondCell.DesiredSize.Height, ThirdCell.DesiredSize.Height));
        return new(float.IsFinite(available.Width) ? available.Width : Threshold,
            stacked ? Label.DesiredSize.Height + Spacing + height : Math.Max(Label.DesiredSize.Height, height));
    }
    /// <inheritdoc />
    protected override void ArrangeCore(Rect area)
    {
        var stacked = area.Width < Threshold;
        var lineHeight = Math.Max(First.DesiredSize.Height, Math.Max(Second.DesiredSize.Height, Third.DesiredSize.Height));
        var labelHeight = stacked ? Label.DesiredSize.Height : Math.Min(lineHeight, area.Height);
        Label.Arrange(new(area.X, area.Y, stacked ? area.Width : Math.Min(LabelWidth, area.Width), labelHeight));
        var x = area.X + (stacked ? 0 : LabelWidth + Spacing);
        var y = area.Y + Math.Min(area.Height, stacked ? labelHeight + Spacing : 0);
        var remaining = Math.Max(0, area.Width - (x - area.X));
        var gap = Math.Min(Spacing, remaining / 2);
        var width = Math.Max(0, (remaining - gap * 2) / 3);
        for (var i = 1; i < Children.Count; i++)
        { Children[i].Arrange(new(x, y, width, Math.Max(0, area.Height - (y - area.Y)))); x += width + gap; }
    }
}
