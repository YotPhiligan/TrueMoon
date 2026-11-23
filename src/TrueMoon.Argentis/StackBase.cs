namespace TrueMoon.Argentis;

/// <summary>A stack with natural sizing and optional use of the remaining space.</summary>
public abstract class StackBase : ElementList
{
    internal static TStack CreateWithChildren<TStack>(TStack stack, Element[] children) where TStack : StackBase
    {
        ArgumentNullException.ThrowIfNull(children);
        try { return stack.WithChildren(children); }
        catch (Exception original)
        {
            // A failed factory cannot leave its accepted children owned by an unreachable stack.
            try { stack.Items.Clear(); }
            catch (Exception cleanup) { throw new AggregateException(original, cleanup); }
            throw;
        }
    }

    /// <summary>Whether a child shares the available remainder along its parent stack's axis.</summary>
    public static readonly UiProperty<bool> FillProperty = new("StackFill", false, Invalidation.Layout);

    /// <summary>Distance between visible children.</summary>
    public float Spacing { get => Get(UiProperties.Spacing); set => Set(UiProperties.Spacing, value); }
    /// <summary>Whether the stacking axis is vertical.</summary>
    protected virtual bool IsVertical => true;
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text)
    {
        var limit = Main(available);
        var fill = float.IsFinite(limit) && Children.Any(child => child.IsVisible && child.Get(FillProperty));
        var fixedConstraint = fill ? available : Along(float.PositiveInfinity, Cross(available));
        foreach (var child in Children)
        {
            if (!fill || !child.IsVisible || !child.Get(FillProperty)) child.Measure(fixedConstraint, text);
        }

        if (fill)
        {
            var (fixedSize, count, fills) = Slots();
            var constraint = Along(Share(limit, fixedSize, count, fills), Cross(available));
            foreach (var child in Children)
                if (child.IsVisible && child.Get(FillProperty)) child.Measure(constraint, text);
        }

        float main = 0, cross = 0;
        var visible = 0;
        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;
            main += Main(child.DesiredSize);
            cross = Math.Max(cross, Cross(child.DesiredSize));
            visible++;
        }
        main += Math.Max(0, visible - 1) * Spacing;
        return Along(fill ? Math.Min(limit, main) : main, cross);
    }
    /// <inheritdoc />
    protected override void ArrangeCore(Rect area)
    {
        var limit = Main(new Size(area.Width, area.Height));
        var (fixedSize, count, fills) = Slots();
        var boundedFill = fills != 0 && float.IsFinite(limit);
        var share = boundedFill ? Share(limit, fixedSize, count, fills) : 0;
        var emptyFillGap = boundedFill && count > 1
            ? Math.Min(Spacing, Math.Max(0, limit - fixedSize) / (count - 1))
            : 0;
        float offset = 0;
        foreach (var child in Children)
        {
            if (!child.IsVisible) { child.Arrange(default); continue; }
            var childFills = boundedFill && child.Get(FillProperty);
            var extent = childFills ? share : Main(child.DesiredSize);
            if (boundedFill) extent = Math.Min(extent, Math.Max(0, limit - offset));
            child.Arrange(IsVertical ? new Rect(area.X, area.Y + offset, area.Width, extent) : new Rect(area.X + offset, area.Y, extent, area.Height));
            if (!boundedFill) { offset += extent + Spacing; continue; }
            offset += extent;
            var gap = childFills && extent == 0 ? emptyFillGap : Spacing;
            offset += Math.Min(gap, Math.Max(0, limit - offset));
        }
    }

    private (float Fixed, int Visible, int Filled) Slots()
    {
        float fixedSize = 0;
        int count = 0, fills = 0;
        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;
            count++;
            if (child.Get(FillProperty)) fills++;
            else fixedSize += Main(child.DesiredSize);
        }
        return (fixedSize, count, fills);
    }

    private float Share(float limit, float fixedSize, int count, int fills) =>
        Math.Max(0, limit - fixedSize - Math.Max(0, count - 1) * Spacing) / fills;

    private float Main(Size size) => IsVertical ? size.Height : size.Width;
    private float Cross(Size size) => IsVertical ? size.Width : size.Height;
    private Size Along(float main, float cross) => IsVertical ? new Size(cross, main) : new Size(main, cross);
}
