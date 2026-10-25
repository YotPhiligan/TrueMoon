namespace TrueMoon.Argentis;

/// <summary>Owns ordinary content and one transient popup above it. Works within the same retained tree, renderer and input session.</summary>
public sealed class OverlayHost : ElementList, IUiInputObserver, IUiFrameParticipant
{
    private Element? _popup, _owner, _returnFocus;
    private IInputContext? _context;
    private Rect _anchor;
    private bool _tooltip;
    /// <summary>Space reserved from popup placement, without reducing the main content area.</summary>
    public static readonly UiProperty<Thickness> PopupInsetsProperty = new("PopupInsets", default, Invalidation.Layout,
        value => float.IsFinite(value.Left) && float.IsFinite(value.Top) && float.IsFinite(value.Right) && float.IsFinite(value.Bottom) &&
            value.Left >= 0 && value.Top >= 0 && value.Right >= 0 && value.Bottom >= 0);
    /// <summary>Insets restricting popup placement, for example beneath overlaid window chrome.</summary>
    public Thickness PopupInsets { get => Get(PopupInsetsProperty); set => Set(PopupInsetsProperty, value); }
    /// <summary>Creates a host and adopts fresh content.</summary>
    /// <param name="content">Unowned main content.</param>
    public OverlayHost(Element content) { ArgumentNullException.ThrowIfNull(content); Add(content); AttachmentChanged += Attachment; }
    /// <summary>Gets the main content.</summary>
    public Element Content => Children[0];
    /// <summary>Gets the owned popup, or null.</summary>
    public Element? Popup => _popup is { IsVisible: true } ? _popup : null;
    /// <summary>Gets the borrowed owner of the active popup.</summary>
    public Element? PopupOwner => _owner;
    /// <inheritdoc />
    public bool NeedsFrame => _popup != null && (_owner is not { IsDisposed: false, IsAttached: true, IsVisible: true, IsEffectivelyEnabled: true } || Hidden(_owner));
    /// <inheritdoc />
    public void AdvanceFrame(TimeSpan elapsed, IInputContext context) { if (NeedsFrame) Close(false); }
    /// <summary>Raised after a popup is removed, before its owner may open another.</summary>
    public event Action? Closed;
    /// <summary>Adopts a fresh popup, replacing and disposing the previous one.</summary>
    /// <param name="owner">Live trigger/control; may be a sibling outside this host.</param><param name="popup">Unowned transient tree.</param>
    /// <param name="anchor">Root-coordinate anchor, normally the menu heading.</param><param name="context">Borrowed focus services until Close/detach.</param>
    /// <param name="returnFocus">Live focus target on dismissal.</param><param name="tooltip">Non-focusable hint; outside input passes through.</param>
    public void Open(Element owner, Element popup, Rect anchor, IInputContext context, Element? returnFocus = null, bool tooltip = false)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(owner); ArgumentNullException.ThrowIfNull(popup); ArgumentNullException.ThrowIfNull(context);
        if (owner.IsDisposed || !owner.IsAttached) throw new ArgumentException("Popup owner must be attached.", nameof(owner));
        static Element RootOf(Element element) { while (element.Parent != null) element = element.Parent; return element; }
        if (!IsAttached || !ReferenceEquals(RootOf(owner), RootOf(this))) throw new ArgumentException("Popup owner must belong to the same UI tree.", nameof(owner));
        if (!float.IsFinite(anchor.X) || !float.IsFinite(anchor.Y) || !float.IsFinite(anchor.Width) || !float.IsFinite(anchor.Height) || anchor.Width < 0 || anchor.Height < 0)
            throw new ArgumentOutOfRangeException(nameof(anchor));
        ValidateChild(popup); Close(false);
        _owner = owner; _anchor = anchor; _context = context; _returnFocus = returnFocus; _tooltip = tooltip;
        _popup = popup;
        try { Add(popup); }
        catch { if (!ReferenceEquals(popup.Parent, this)) _popup = null; _owner = null; _context = null; _returnFocus = null; throw; }
    }
    /// <summary>Removes and disposes the active popup, returning focus only to a still-live target.</summary>
    /// <param name="restoreFocus">Whether to restore focus.</param>
    public void Close(bool restoreFocus = true)
    {
        VerifyAccess(); var popup = _popup; if (popup == null) return;
        var context = _context; var target = _returnFocus;
        _popup = _owner = _returnFocus = null; _context = null;
        List<Exception> errors = [];
        try { Items.Remove(popup); } catch (Exception error) { errors.Add(error); }
        if (popup.Parent == null) try { popup.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (restoreFocus && target is { IsDisposed: false, IsAttached: true, IsVisible: true, IsEffectivelyEnabled: true, Focusable: true })
            try { context?.Focus(target); } catch (ArgumentException) { /* A hidden ancestor/ended attachment cannot receive focus. */ }
        try { Closed?.Invoke(); } catch (Exception error) { errors.Add(error); }
        TreeChange.ThrowErrors(errors);
    }
    private void Attachment()
    {
        if (IsAttached) { if (_popup != null && _owner == null) Dispatch(() => Close(false)); return; }
        // A detached retained tree remains caller-owned. Structural changes cannot run in
        // attachment notifications; hide immediately, release host/focus references and
        // dispose the retained popup on reattachment/Close or parent disposal.
        _owner = _returnFocus = null; _context = null;
        if (_popup is { IsDisposed: false }) _popup.IsVisible = false;
    }
    /// <inheritdoc />
    public bool PreviewInput(UiInput input, IInputContext context)
    {
        if (_popup == null || !_popup.IsVisible) return false;
        if (_owner is not { IsDisposed: false, IsAttached: true, IsVisible: true, IsEffectivelyEnabled: true } || Hidden(_owner)) { Close(false); return false; }
        if (input.Kind == InputKind.FocusLost) { Close(false); return false; }
        if (input.Kind == InputKind.KeyDown && input.Key == UiKey.Escape) { var consume = !_tooltip; Close(); return consume; }
        if (input.Kind == InputKind.PointerDown && !_popup.Bounds.Contains(input.X, input.Y) && !_owner.Bounds.Contains(input.X, input.Y))
        { var consume = !_tooltip; Close(); return consume; }
        return false;
    }
    private static bool Hidden(Element owner) { for (Element? node = owner; node != null; node = node.Parent) if (!node.IsVisible) return true; return false; }
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text)
    {
        Content.Measure(available, text);
        _popup?.Measure(new(Math.Max(0, available.Width - PopupInsets.Left - PopupInsets.Right), Math.Max(0, available.Height - PopupInsets.Top - PopupInsets.Bottom)), text);
        return Content.DesiredSize;
    }
    /// <inheritdoc />
    protected override void ArrangeCore(Rect area)
    {
        Content.Arrange(area);
        if (_popup == null) return;
        var left = Math.Min(area.Width, PopupInsets.Left); var top = Math.Min(area.Height, PopupInsets.Top);
        area = new(area.X + left, area.Y + top, Math.Max(0, area.Width - left - PopupInsets.Right), Math.Max(0, area.Height - top - PopupInsets.Bottom));
        var width = Math.Min(area.Width, _popup.DesiredSize.Width); var height = Math.Min(area.Height, _popup.DesiredSize.Height);
        var x = Math.Clamp(_anchor.X, area.X, Math.Max(area.X, area.X + area.Width - width));
        var below = _anchor.Y + _anchor.Height;
        var y = below + height <= area.Y + area.Height ? below : _anchor.Y - height;
        y = Math.Clamp(y, area.Y, Math.Max(area.Y, area.Y + area.Height - height));
        _popup.Arrange(new(x, y, width, height));
    }
    /// <inheritdoc />
    public override void Dispose()
    {
        try { base.Dispose(); }
        finally { if (IsDisposed) { _popup = _owner = _returnFocus = null; _context = null; Closed = null; AttachmentChanged -= Attachment; } }
    }
}
