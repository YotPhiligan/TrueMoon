using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;

namespace TrueMoon.Argentis.Layout;

/// <summary>A vertically scrolling, clipped content container.</summary>
public class ScrollViewer : ContentControl
{
    private float _offset;
    private bool _scrollToEnd;
    private bool _scrollDragging;
    private float _grab;
    private float _measuredViewport;
    /// <summary>Shows a square scrollbar when content overflows; setting false preserves a wheel-only viewport.</summary>
    public static readonly UiProperty<bool> ShowScrollBarProperty = new("ShowScrollBar", true, Invalidation.Layout);
    /// <summary>Whether an overflowing viewport reserves a ten-unit track.</summary>
    public bool ShowScrollBar { get => Get(ShowScrollBarProperty); set => Set(ShowScrollBarProperty, value); }
    internal bool HasScrollBar => ShowScrollBar && Extent > Bounds.Deflate(Padding).Height;
    internal Rect Track
    {
        get
        {
            var area = Bounds.Deflate(Padding); var width = Math.Min(10, area.Width);
            return new(area.X + area.Width - width, area.Y, width, area.Height);
        }
    }
    internal Rect Thumb
    {
        get
        {
            var track = Track; var height = Math.Min(track.Height, Math.Max(20, track.Height * track.Height / Math.Max(1, Extent)));
            var inset = Math.Min(2, track.Width / 2);
            return new(track.X + inset, track.Y + (track.Height - height) * Offset / Math.Max(1, Extent - track.Height), Math.Max(0, track.Width - inset * 2), height);
        }
    }
    /// <summary>Scroll offset in logical pixels, clamped during layout. Setting it cancels a pending scroll to the end.</summary>
    public float Offset
    {
        get => _offset;
        set
        {
            VerifyAccess();
            if (!float.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
            _scrollToEnd = false;
            var next = Math.Max(0, value);
            if (_offset == next) return;
            _offset = next;
            Invalidate(Invalidation.Layout);
            // The child has to re-arrange at the new offset even when its own size is cached; only its measure may
            // stay valid, so the window of a virtualizing container is the part that additionally drops it.
            if (Child is { RealizesScrollWindow: true } windowed) windowed.Invalidate(Invalidation.Layout);
        }
    }
    /// <summary>The full content height.</summary>
    public float Extent { get; private set; }
    /// <summary>Height of the visible area: the arranged viewport, or the measured area before the first arrangement.</summary>
    public float ViewportHeight => Bounds.Height > 0f ? Bounds.Deflate(Padding).Height : _measuredViewport;
    /// <inheritdoc />
    public override Rect ChildClipBounds => Bounds.Deflate(Padding).Deflate(new Thickness(0, 0, HasScrollBar ? 10 : 0, 0));
    /// <summary>Scrolls to the end on the next arrangement, using the measured extent and the padded viewport.</summary>
    /// <remarks>This is a one-shot request. It does not keep content pinned on subsequent layouts.</remarks>
    public void ScrollToEnd()
    {
        VerifyAccess();
        if (_scrollToEnd) return;
        _scrollToEnd = true;
        Invalidate(Invalidation.Layout);
    }
    /// <summary>Cancels a pending end-scroll request without moving the current offset.</summary>
    public void CancelScrollToEnd()
    {
        VerifyAccess();
        _scrollToEnd = false;
    }
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
        _measuredViewport = available.Height;
        Child?.Measure(new Size(available.Width, float.PositiveInfinity), text);
        Extent = Child?.DesiredSize.Height ?? 0;
        if (_scrollToEnd && Child is { RealizesScrollWindow: true } windowed)
        {
            // A window follows the offset, so it has to see the requested position before it measures its rows again.
            // The measure constraint can differ from the viewport arrange hands over, so the exact pin stays in
            // ArrangeCore and the request stays pending; an ordinary child never needs this estimate at all.
            _offset = Math.Max(0, Extent - available.Height);
            windowed.Invalidate(Invalidation.Layout);
            windowed.Measure(new Size(available.Width, float.PositiveInfinity), text);
            Extent = windowed.DesiredSize.Height;
        }

        // The narrower track is only worth a second walk when the content really needs those ten units;
        // otherwise the first constraint already covers the narrowed one and the cached sizes stand.
        if (ShowScrollBar && Extent > available.Height && (Child?.DesiredSize.Width ?? 0) > available.Width - 10)
        {
            Child?.Measure(new Size(Math.Max(0, available.Width - 10), float.PositiveInfinity), text);
            Extent = Child?.DesiredSize.Height ?? 0;
        }
        return new Size(Math.Min(available.Width, (Child?.DesiredSize.Width ?? 0) + (ShowScrollBar && Extent > available.Height ? 10 : 0)), Math.Min(available.Height, Extent));
    }
    /// <inheritdoc />
    protected override void ArrangeCore(Rect area)
    {
        // The first measure ran before any arrangement, so it sized the window from the measure constraint; one
        // extra pass aligns it with the viewport the child actually received. An open-ended measure has no viewport
        // to compare against, and only a windowed container cares, so an ordinary child never churns here.
        if (Child is { RealizesScrollWindow: true } windowed && float.IsFinite(area.Height) && area.Height != _measuredViewport)
        {
            _measuredViewport = area.Height;
            windowed.Invalidate(Invalidation.Layout);
        }

        var end = Math.Max(0, Extent - area.Height);
        _offset = _scrollToEnd ? end : Math.Clamp(_offset, 0, end);
        _scrollToEnd = false;
        Child?.Arrange(new Rect(area.X, area.Y - _offset, Math.Max(0, area.Width - (HasScrollBar ? 10 : 0)), Math.Max(area.Height, Extent)));
    }
    internal bool IsScrollDragging => _scrollDragging;
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