using TrueMoon.Argentis.Painting;

namespace TrueMoon.Argentis.Layout;

/// <summary>Places children in the same slot, painting later children on top.</summary>
public class Panel : ElementList
{
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text)
    {
        float width = 0, height = 0;
        foreach (var child in Children) { child.Measure(available, text); width = Math.Max(width, child.DesiredSize.Width); height = Math.Max(height, child.DesiredSize.Height); }
        return new Size(width, height);
    }
    /// <inheritdoc />
    protected override void ArrangeCore(Rect area) { foreach (var child in Children) child.Arrange(area); }
}