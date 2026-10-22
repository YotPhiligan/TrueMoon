using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor;

/// <summary>A clipped two-column inspector row; values are selectable read-only fields.</summary>
public sealed class PropertyRow : ElementList
{
    public PropertyRow(string name, string value)
    {
        Add(Text.Plain(name).WithRole(EditorView.PropertyNameRole));
        Add(new TextBox { Value = value, IsReadOnly = true, MinWidth = 0, AccessibleName = name });
    }
    protected override Size MeasureCore(Size available, ITextLayoutService text)
    { foreach (var child in Children) child.Measure(available, text); return new(260, 36); }
    protected override void ArrangeCore(Rect area)
    { var label = Math.Min(116, area.Width * .45f); Children[0].Arrange(new(area.X, area.Y, label, area.Height)); Children[1].Arrange(new(area.X + label, area.Y, Math.Max(0, area.Width - label), area.Height)); }
}
