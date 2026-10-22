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
    protected override float ThemeBorderWidth => Theme.BorderWidth;
}

/// <summary>A vertically scrolling, clipped content container.</summary>
public class ScrollViewer : ContentControl
{
    /// <summary>Creates a new ScrollViewer with its constructor defaults.</summary>
    /// <returns>A new independent ScrollViewer for Fluent configuration.</returns>
    public new static ScrollViewer Create() => new();

    private float _offset;
    private bool _scrollDragging;
    private float _grab;
    /// <summary>Shows a square scrollbar when content overflows; setting false preserves a wheel-only viewport.</summary>
    public static readonly UiProperty<bool> ShowScrollBarProperty = new("ShowScrollBar", true, Invalidation.Layout);
    /// <summary>Whether an overflowing viewport reserves a ten-unit track.</summary>
    public bool ShowScrollBar { get => Get(ShowScrollBarProperty); set => Set(ShowScrollBarProperty, value); }
    private bool HasScrollBar => ShowScrollBar && Extent > Bounds.Deflate(Padding).Height;
    private Rect Track
    {
        get
        {
            var area = Bounds.Deflate(Padding); var width = Math.Min(10, area.Width);
            return new(area.X + area.Width - width, area.Y, width, area.Height);
        }
    }
    private Rect Thumb
    {
        get
        {
            var track = Track; var height = Math.Min(track.Height, Math.Max(20, track.Height * track.Height / Math.Max(1, Extent)));
            var inset = Math.Min(2, track.Width / 2);
            return new(track.X + inset, track.Y + (track.Height - height) * Offset / Math.Max(1, Extent - track.Height), Math.Max(0, track.Width - inset * 2), height);
        }
    }
    /// <summary>Scroll offset in logical pixels, clamped during layout.</summary>
    public float Offset { get => _offset; set { VerifyAccess(); if (!float.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value)); var next = Math.Max(0, value); if (_offset == next) return; _offset = next; Invalidate(Invalidation.Layout); } }
    /// <summary>The full content height.</summary>
    public float Extent { get; private set; }
    /// <inheritdoc />
    public override Rect ChildClipBounds => Bounds.Deflate(Padding).Deflate(new Thickness(0, 0, HasScrollBar ? 10 : 0, 0));
    /// <summary>Scrolls enough to expose a root-coordinate rectangle, clamping to the current extent.</summary>
    /// <param name="rectangle">The arranged bounds of content to reveal; oversized content aligns at the top.</param>
    public void BringIntoView(Rect rectangle)
    {
        VerifyAccess();
        if (!float.IsFinite(rectangle.X) || !float.IsFinite(rectangle.Y) || !float.IsFinite(rectangle.Width)
            || !float.IsFinite(rectangle.Height) || rectangle.Width < 0 || rectangle.Height < 0)
            throw new ArgumentOutOfRangeException(nameof(rectangle));
        var viewport = ChildClipBounds;
        var delta = rectangle.Y < viewport.Y || rectangle.Height > viewport.Height ? rectangle.Y - viewport.Y
            : Math.Max(0, rectangle.Y + rectangle.Height - viewport.Y - viewport.Height);
        var next = Math.Clamp(Offset + delta, 0, Math.Max(0, Extent - viewport.Height));
        if (next != Offset) Offset = next;
    }
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text)
    {
        Child?.Measure(new Size(available.Width, float.PositiveInfinity), text);
        Extent = Child?.DesiredSize.Height ?? 0;
        if (ShowScrollBar && Extent > available.Height)
        {
            Child?.Measure(new Size(Math.Max(0, available.Width - 10), float.PositiveInfinity), text);
            Extent = Child?.DesiredSize.Height ?? 0;
        }
        return new Size(Math.Min(available.Width, (Child?.DesiredSize.Width ?? 0) + (ShowScrollBar && Extent > available.Height ? 10 : 0)), Math.Min(available.Height, Extent));
    }
    /// <inheritdoc />
    protected override void ArrangeCore(Rect area)
    {
        _offset = Math.Clamp(_offset, 0, Math.Max(0, Extent - area.Height));
        Child?.Arrange(new Rect(area.X, area.Y - _offset, Math.Max(0, area.Width - (HasScrollBar ? 10 : 0)), Math.Max(area.Height, Extent)));
    }
    /// <inheritdoc />
    protected override void DrawCore(IDrawingContext context)
    {
        if (!HasScrollBar) return;
        context.Fill(Track, Theme.Surface);
        context.Fill(Thumb, !IsEffectivelyEnabled ? Theme.Disabled : _scrollDragging || IsHovered ? Theme.Accent : Theme.Border);
    }
    /// <inheritdoc />
    public override bool HandleInput(UiInput input, IInputContext context)
    {
        if (!IsEffectivelyEnabled) return false;
        if (_scrollDragging && !HasScrollBar) { _scrollDragging = false; context.ReleaseCapture(this); Invalidate(); }
        if (input.Kind is InputKind.PointerDown or InputKind.PointerUp && input.Button != 0) return false;
        if (input.Kind == InputKind.PointerDown && input.Button == 0 && HasScrollBar && Track.Contains(input.X, input.Y))
        {
            var thumb = Thumb; _grab = thumb.Contains(input.X, input.Y) ? input.Y - thumb.Y : thumb.Height / 2;
            _scrollDragging = true; context.Capture(this); Invalidate();
        }
        if (_scrollDragging && input.Kind is InputKind.PointerDown or InputKind.PointerMove or InputKind.PointerUp)
        {
            var track = Track; var thumb = Thumb;
            Offset = Math.Clamp((input.Y - track.Y - _grab) / Math.Max(1, track.Height - thumb.Height), 0, 1) * Math.Max(0, Extent - track.Height);
            if (input.Kind == InputKind.PointerUp && input.Button == 0) { _scrollDragging = false; context.ReleaseCapture(this); Invalidate(); }
            return true;
        }
        if (input.Kind != InputKind.Wheel) return false;
        var next = Math.Clamp(Offset - input.WheelDelta * 36, 0, Math.Max(0, Extent - Bounds.Deflate(Padding).Height));
        if (next == Offset) return false;
        Offset = next; return true;
    }
    /// <inheritdoc />
    public override void OnInputCancelled() { _scrollDragging = false; Invalidate(); }
}
