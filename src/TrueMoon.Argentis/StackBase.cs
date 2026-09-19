namespace TrueMoon.Argentis;

/// <summary>A stack measuring children along one unconstrained axis.</summary>
public abstract class StackBase : ElementList
{
    private float _spacing;
    /// <summary>Distance between visible children.</summary>
    public float Spacing { get => _spacing; set { VerifyAccess(); if (!float.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(nameof(value)); _spacing = value; Invalidate(Invalidation.Layout); } }
    /// <summary>Whether the stacking axis is vertical.</summary>
    protected virtual bool IsVertical => true;
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text)
    {
        float main = 0, cross = 0; var count = 0;
        foreach (var child in Children)
        {
            child.Measure(IsVertical ? new Size(available.Width, float.PositiveInfinity) : new Size(float.PositiveInfinity, available.Height), text);
            if (!child.IsVisible) continue;
            main += IsVertical ? child.DesiredSize.Height : child.DesiredSize.Width;
            cross = Math.Max(cross, IsVertical ? child.DesiredSize.Width : child.DesiredSize.Height);
            count++;
        }
        main += Math.Max(0, count - 1) * Spacing;
        return IsVertical ? new Size(cross, main) : new Size(main, cross);
    }
    /// <inheritdoc />
    protected override void ArrangeCore(Rect area)
    {
        float offset = 0;
        foreach (var child in Children)
        {
            if (!child.IsVisible) { child.Arrange(default); continue; }
            var extent = IsVertical ? child.DesiredSize.Height : child.DesiredSize.Width;
            child.Arrange(IsVertical ? new Rect(area.X, area.Y + offset, area.Width, extent) : new Rect(area.X + offset, area.Y, extent, area.Height));
            offset += extent + Spacing;
        }
    }
}
