namespace TrueMoon.Argentis;

/// <summary>Adaptive status layout; overflow content remains owned and available to a details presenter.</summary>
public interface IStatusBar
{
    /// <summary>Gets secondary elements currently hidden by the available width, in declaration order.</summary>
    IReadOnlyList<Element> OverflowedItems { get; }
    /// <summary>Raised when responsive layout changes the overflow set.</summary>
    event Action? OverflowChanged;
}

/// <summary>One primary message/action, ordered optional segments, and an always-reserved details trigger.</summary>
public sealed class StatusBar : ElementList, IStatusBar, IUiFrameParticipant
{
    private readonly Element _primary, _action, _details;
    private readonly ContentControl[] _secondary;
    private IReadOnlyList<Element> _overflow = Array.Empty<Element>();
    private bool _returnFocus;
    /// <summary>Adopts fresh elements. Earlier secondary items collapse before later items.</summary>
    /// <param name="primary">Flexible primary message.</param><param name="action">Essential action; its own visibility is respected.</param>
    /// <param name="secondary">Optional elements, from lowest to highest retention priority.</param>
    /// <param name="details">Essential details trigger.</param>
    public StatusBar(Element primary, Element action, IEnumerable<Element> secondary, Element details)
    {
        ArgumentNullException.ThrowIfNull(primary); ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(secondary); ArgumentNullException.ThrowIfNull(details);
        var optional = secondary.ToArray();
        var all = new[] { primary, action, details }.Concat(optional).ToArray();
        if (all.Any(e => e == null || e.Parent != null || e.IsAttached || e.IsDisposed) || all.Distinct().Count() != all.Length)
            throw new ArgumentException("Status parts must be distinct, live and unowned.");
        _primary = primary; _action = action; _details = details;
        _secondary = optional.Select(e => new ContentControl().Content(e)).ToArray();
        try { Add(primary); Add(action); foreach (var slot in _secondary) Add(slot); Add(details); }
        catch { Dispose(); foreach (var slot in _secondary) if (slot.Parent == null) slot.Dispose(); throw; }
        AttachmentChanged += Attached;
    }
    /// <inheritdoc />
    public IReadOnlyList<Element> OverflowedItems => _overflow;
    /// <inheritdoc />
    public event Action? OverflowChanged;
    /// <inheritdoc />
    public bool NeedsFrame => _returnFocus;
    /// <inheritdoc />
    public void AdvanceFrame(TimeSpan elapsed, IInputContext context)
    {
        _returnFocus = false;
        if (_details.IsVisible && _details.IsEffectivelyEnabled && _details.Focusable) context.Focus(_details);
    }
    private void Attached() { if (!IsAttached) _returnFocus = false; }
    /// <inheritdoc />
    protected override Thickness ThemePadding => new(8, 0, 8, 0);
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text)
    {
        _primary.Measure(available, text); _action.Measure(available, text); _details.Measure(available, text);
        // Measure content even while its owned slot is collapsed, so expansion can restore it.
        foreach (var slot in _secondary) slot.Child!.Measure(available, text);
        return new(_primary.DesiredSize.Width + _action.DesiredSize.Width + _details.DesiredSize.Width +
            _secondary.Sum(s => s.Child!.DesiredSize.Width) + 8 * (_secondary.Length + 2), 28);
    }
    /// <inheritdoc />
    protected override void ArrangeCore(Rect area)
    {
        area = area with { X = Math.Clamp(area.X, Bounds.X, Bounds.X + Bounds.Width),
            Y = Math.Clamp(area.Y, Bounds.Y, Bounds.Y + Bounds.Height) };
        var gap = Math.Min(8, area.Width / 8);
        var right = area.X + area.Width;
        var detailsWidth = Math.Min(_details.DesiredSize.Width, area.Width);
        right -= detailsWidth; Place(_details, right, detailsWidth);
        var available = Math.Max(0, right - area.X - (detailsWidth > 0 ? gap : 0));
        var actionWidth = Math.Min(_action.DesiredSize.Width, available);
        var actionGap = actionWidth > 0 ? gap : 0;
        var primaryReserve = Math.Min(120, Math.Max(0, available - actionWidth - actionGap));
        var budget = Math.Max(0, available - actionWidth - actionGap - primaryReserve);
        var retained = new bool[_secondary.Length];
        for (var i = _secondary.Length - 1; i >= 0; i--)
        {
            var content = _secondary[i].Child!; var width = content.DesiredSize.Width;
            retained[i] = content.IsVisible && width > 0 && width + gap <= budget;
            if (retained[i]) budget -= width + gap;
        }
        var overflow = new List<Element>();
        if (detailsWidth > 0) right -= gap;
        for (var i = _secondary.Length - 1; i >= 0; i--)
        {
            var slot = _secondary[i]; var content = slot.Child!;
            if (!retained[i] && content.IsVisible) overflow.Insert(0, content);
            if (!retained[i] && slot.IsVisible && FocusWithin(content)) _returnFocus = true;
            if (slot.IsVisible != retained[i]) slot.IsVisible = retained[i];
            if (retained[i]) { var width = content.DesiredSize.Width; right -= width; Place(slot, right, width); right -= gap; }
            else slot.Arrange(default);
        }
        var overflowChanged = !_overflow.SequenceEqual(overflow);
        _overflow = overflow.AsReadOnly();
        var primaryWidth = Math.Max(0, right - area.X - actionWidth - actionGap);
        Place(_primary, area.X, primaryWidth);
        Place(_action, area.X + primaryWidth + actionGap, actionWidth);
        if (overflowChanged) OverflowChanged?.Invoke();
        void Place(Element element, float x, float width) => element.Arrange(new(x, area.Y + Math.Max(0, (area.Height - 24) / 2), width, Math.Min(24, area.Height)));
    }
    private static bool FocusWithin(Element element) => element.IsFocused || element.Children.Any(FocusWithin);
    /// <inheritdoc />
    protected override void DrawCore(IDrawingContext context) => context.Fill(new(Bounds.X, Bounds.Y, Bounds.Width, Math.Min(1, Bounds.Height)), Theme.Border);
    /// <inheritdoc />
    public override void Dispose()
    { try { base.Dispose(); } finally { if (IsDisposed) { AttachmentChanged -= Attached; _returnFocus = false; _overflow = Array.Empty<Element>(); OverflowChanged = null; } } }
}
