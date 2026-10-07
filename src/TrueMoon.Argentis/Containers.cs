namespace TrueMoon.Argentis;

/// <summary>Places children in the same slot, painting later children on top.</summary>
public class Panel : ElementList
{
    /// <summary>Creates a new Panel with its constructor defaults.</summary>
    /// <returns>A new independent Panel for Fluent configuration.</returns>
    public static Panel Create() => new();

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

/// <summary>An element with replaceable content.</summary>
public class ContentControl : Element
{
    /// <summary>Creates a new ContentControl with its constructor defaults.</summary>
    /// <returns>A new independent ContentControl for Fluent configuration.</returns>
    public static ContentControl Create() => new();

    private Element? _content;
    private Element[] _children = [];
    /// <summary>The current child.</summary>
    public Element? Child => _content;
    /// <inheritdoc />
    public override IReadOnlyList<Element> Children => _children;
    /// <summary>Replaces content. The caller owns the detached old subtree and must reuse or dispose it.</summary>
    /// <param name="content">An unowned live subtree, or null to clear content.</param>
    /// <remarks>Validation failures preserve the tree. Notification failures are reported after the replacement commits.</remarks>
    public void SetContent(Element? content)
    {
        VerifyTreeAccess();
        if (ReferenceEquals(content, _content)) return;
        if (content != null) ValidateChild(content);
        var previous = _content;
        using var change = new TreeChange(this, previous, content);
        _content = content;
        _children = content == null ? [] : [content];
        if (previous != null) Orphan(previous, change);
        if (content != null) Adopt(content, change);
        change.Schedule(() => Invalidate(Invalidation.Tree | Invalidation.Layout));
        change.Complete();
    }
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text) { Child?.Measure(available, text); return Child?.DesiredSize ?? default; }
    /// <inheritdoc />
    protected override void ArrangeCore(Rect area) => Child?.Arrange(area);
}

/// <summary>A content container with a theme-colored outline.</summary>
public class Border : ContentControl
{
    /// <summary>Creates a new Border with its constructor defaults.</summary>
    /// <returns>A new independent Border for Fluent configuration.</returns>
    public new static Border Create() => new();

    /// <inheritdoc />
    protected override void DrawCore(IDrawingContext context) => context.Stroke(Bounds.Deflate(new Thickness(.5f)), Theme.Control, 1, 4);
}

/// <summary>A vertically scrolling, clipped content container.</summary>
public class ScrollViewer : ContentControl
{
    /// <summary>Creates a new ScrollViewer with its constructor defaults.</summary>
    /// <returns>A new independent ScrollViewer for Fluent configuration.</returns>
    public new static ScrollViewer Create() => new();

    private float _offset;
    /// <summary>Scroll offset in logical pixels, clamped during layout.</summary>
    public float Offset { get => _offset; set { VerifyAccess(); if (!float.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value)); _offset = Math.Max(0, value); Invalidate(Invalidation.Layout); } }
    /// <summary>The full content height.</summary>
    public float Extent { get; private set; }
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text)
    {
        Child?.Measure(new Size(available.Width, float.PositiveInfinity), text);
        Extent = Child?.DesiredSize.Height ?? 0;
        return new Size(Math.Min(available.Width, Child?.DesiredSize.Width ?? 0), Math.Min(available.Height, Extent));
    }
    /// <inheritdoc />
    protected override void ArrangeCore(Rect area)
    {
        _offset = Math.Clamp(_offset, 0, Math.Max(0, Extent - area.Height));
        Child?.Arrange(new Rect(area.X, area.Y - _offset, area.Width, Math.Max(area.Height, Extent)));
    }
    /// <inheritdoc />
    public override bool HandleInput(UiInput input, IInputContext context)
    {
        if (input.Kind != InputKind.Wheel) return false;
        var next = Math.Clamp(Offset - input.WheelDelta * 36, 0, Math.Max(0, Extent - Bounds.Deflate(Padding).Height));
        if (next == Offset) return false;
        Offset = next; return true;
    }
}
