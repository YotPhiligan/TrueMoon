namespace TrueMoon.Argentis;

/// <summary>Disclosure/menu capability independent of a concrete renderer.</summary>
public interface IMenu
{
    /// <summary>Gets whether top-level headings are being revealed.</summary>
    bool IsExpanded { get; }
    /// <summary>Gets whether a dropdown is open.</summary>
    bool IsMenuOpen { get; }
    /// <summary>Closes the dropdown and collapses its disclosure.</summary>
    void CloseMenu();
}

/// <summary>Compact hover/click/keyboard menu bar with reversible disclosure. Borrows an overlay host and commands.</summary>
public sealed class MenuBar : ElementList, IMenu, IUiFrameParticipant, IUiInputObserver
{
    private readonly OverlayHost _overlays;
    private readonly MenuGroup[] _groups;
    private readonly Dictionary<string, Button> _headings = new(StringComparer.Ordinal);
    private readonly ScalarTransition _reveal = new();
    private readonly MoreTrigger _more;
    private TimeSpan _closeRemaining;
    private int _open = -1;
    private bool _expanded;
    private bool _pinned;
    private bool _openFirstWhenRevealed;
    private TimeSpan _animationDuration = TimeSpan.FromMilliseconds(180), _closeDelay = TimeSpan.FromMilliseconds(240);
    /// <summary>Creates an independent bar. Its parent owns it, and the host owns each open popup.</summary>
    /// <param name="overlays">Borrowed host in the same UI tree.</param><param name="groups">Frozen top-level groups.</param>
    public MenuBar(OverlayHost overlays, IEnumerable<MenuGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(overlays); ArgumentNullException.ThrowIfNull(groups);
        _overlays = overlays; _groups = groups.ToArray();
        if (_groups.Length == 0 || _groups.Any(g => g == null) || _groups.Select(g => g.Id).Distinct(StringComparer.Ordinal).Count() != _groups.Length)
            throw new ArgumentException("Menus must have distinct valid identities.", nameof(groups));
        WindowRegion = WindowRegionRole.Client;
        _more = new(this); Add(_more);
        for (var i = 0; i < _groups.Length; i++) { var button = new Heading(this, i, _groups[i].Label); _headings.Add(_groups[i].Id, button); Add(button); }
        _overlays.Closed += PopupClosed;
        AttachmentChanged += Attachment;
    }
    /// <summary>Gets the small disclosure button.</summary>
    public IconButton MoreButton => _more;
    /// <summary>Gets stable heading controls by identity.</summary>
    public IReadOnlyDictionary<string, Button> Headings => _headings;
    /// <summary>Gets frozen groups.</summary>
    public IReadOnlyList<MenuGroup> Groups => Array.AsReadOnly(_groups);
    /// <summary>Gets the interpolation fraction.</summary>
    public float RevealProgress => _reveal.Value;
    /// <summary>Gets/sets reveal duration; zero disables motion.</summary>
    public TimeSpan AnimationDuration
    {
        get => _animationDuration;
        set { VerifyAccess(); if (value < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(value)); _animationDuration = value; _reveal.MoveTo(_expanded ? 1 : 0, value); Invalidate(Invalidation.Layout); }
    }
    /// <summary>Gets/sets pointer-leave grace period.</summary>
    public TimeSpan CloseDelay { get => _closeDelay; set { VerifyAccess(); if (value < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(value)); _closeDelay = value; } }
    /// <inheritdoc />
    public bool IsExpanded => _expanded;
    /// <inheritdoc />
    public bool IsMenuOpen => _open >= 0 && ReferenceEquals(_overlays.PopupOwner, this);
    /// <inheritdoc />
    public bool NeedsFrame => _reveal.IsRunning || _closeRemaining > TimeSpan.Zero || _openFirstWhenRevealed;
    /// <inheritdoc />
    public int InputPriority => 10;
    private void Expand(bool expanded, bool immediate = false)
    {
        _expanded = expanded; _closeRemaining = TimeSpan.Zero;
        if (!expanded) _openFirstWhenRevealed = false;
        _reveal.MoveTo(expanded ? 1 : 0, immediate ? TimeSpan.Zero : AnimationDuration); Invalidate(Invalidation.Layout);
    }
    /// <summary>Opens a known group, also used by keyboard adapters. Headings are revealed immediately for stable anchors.</summary>
    /// <param name="id">Group identity.</param><param name="context">Borrowed input services.</param>
    public void OpenGroup(string id, IInputContext context)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(context); var index = Array.FindIndex(_groups, g => g.Id == id);
        if (index < 0) throw new ArgumentException("Unknown menu.", nameof(id));
        _openFirstWhenRevealed = false;
        Expand(true, true);
        var heading = _headings[id]; var popup = new Popup(this, _groups[index].Actions);
        try { _overlays.Open(this, popup, heading.Bounds, context, _more); _open = index; Expand(true, true); }
        catch { if (popup.Parent == null && !popup.IsDisposed) popup.Dispose(); throw; }
        popup.FocusFirst(context);
    }
    /// <inheritdoc />
    public void CloseMenu() { VerifyAccess(); _pinned = false; if (ReferenceEquals(_overlays.PopupOwner, this)) _overlays.Close(); _open = -1; Expand(false); }
    private void PopupClosed() { if (!ReferenceEquals(_overlays.PopupOwner, this)) { _open = -1; _pinned = false; Expand(false); } }
    private static bool FocusWithin(Element element) => element.IsFocused || element.Children.Any(FocusWithin);
    /// <inheritdoc />
    public bool PreviewInput(UiInput input, IInputContext context)
    {
        if (input.Kind == InputKind.FocusLost) { if (IsMenuOpen) _overlays.Close(false); _pinned = false; _open = -1; Expand(false, true); return false; }
        if (input.Kind == InputKind.KeyDown && input.Key == UiKey.F10) { _pinned = true; _openFirstWhenRevealed = false; Expand(true, true); context.Focus(_headings.Values.First()); return true; }
        if (input.Kind == InputKind.KeyDown && input.Key == UiKey.Escape && (_expanded || IsMenuOpen)) { CloseMenu(); context.Focus(_more); return true; }
        if (input.Kind == InputKind.KeyDown && input.Key is UiKey.Left or UiKey.Right && IsMenuOpen)
        { OpenGroup(_groups[(_open + (input.Key == UiKey.Right ? 1 : _groups.Length - 1)) % _groups.Length].Id, context); return true; }
        if (input.Kind == InputKind.KeyDown && input.Key == UiKey.Tab && (IsMenuOpen || _openFirstWhenRevealed)) { _overlays.Close(false); _pinned = false; Expand(false); context.Focus(_more); return false; }
        if (input.Kind is InputKind.PointerMove or InputKind.PointerDown)
        {
            var inside = Bounds.Contains(input.X, input.Y) || IsMenuOpen && _overlays.Popup!.Bounds.Contains(input.X, input.Y);
            if (inside) { _closeRemaining = TimeSpan.Zero; if (!_expanded) Expand(true); }
            else if (input.Kind == InputKind.PointerDown && _expanded && !IsMenuOpen) { _pinned = false; Expand(false); }
            else if (_expanded && !IsMenuOpen && !_pinned && !FocusWithin(this) && _closeRemaining == TimeSpan.Zero)
            { if (CloseDelay == TimeSpan.Zero) Expand(false); else _closeRemaining = CloseDelay; }
        }
        return false;
    }
    /// <inheritdoc />
    public void AdvanceFrame(TimeSpan elapsed, IInputContext context)
    {
        if (_closeRemaining > TimeSpan.Zero) { _closeRemaining -= elapsed; if (_closeRemaining <= TimeSpan.Zero) Expand(false); }
        if (_reveal.Advance(elapsed)) Invalidate(Invalidation.Layout);
        OpenFirstIfReady(context);
    }
    private void OpenFirstIfReady(IInputContext context)
    {
        if (_openFirstWhenRevealed && !_reveal.IsRunning && _headings[_groups[0].Id].Bounds.Width > 0)
            OpenGroup(_groups[0].Id, context);
    }
    private void Attachment()
    {
        if (IsAttached) return;
        _open = -1; _pinned = false; _openFirstWhenRevealed = false; _expanded = false; _closeRemaining = TimeSpan.Zero; _reveal.MoveTo(0, TimeSpan.Zero);
    }
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text)
    { foreach (var child in Children) child.Measure(available, text); return new(32 + _reveal.Value * Children.Skip(1).Sum(c => c.DesiredSize.Width), 32); }
    /// <inheritdoc />
    protected override void ArrangeCore(Rect area)
    {
        var x = area.X; _more.Arrange(new(x, area.Y + (area.Height - 32) / 2, 32, Math.Min(32, area.Height))); x += 32;
        foreach (var heading in Children.Skip(1)) { heading.Arrange(new(x, area.Y, heading.DesiredSize.Width, area.Height)); x += heading.DesiredSize.Width; }
    }
    /// <inheritdoc />
    public override void Dispose()
    {
        try { base.Dispose(); }
        finally { if (IsDisposed) { _overlays.Closed -= PopupClosed; AttachmentChanged -= Attachment; } }
    }
    private sealed class MoreTrigger(MenuBar bar) : IconButton(UiSymbol.More, "Меню · F10")
    {
        private IInputContext? _context;
        protected override float ThemeBorderWidth => 0;
        protected override Color ThemeBackground => IsPressed ? Theme.ControlPressed : IsHovered ? Theme.ControlHover : Color.Transparent;
        protected override void DrawCore(IDrawingContext context)
        {
            var angle = (1 - bar.RevealProgress) * MathF.PI / 2;
            var unit = Math.Min(12, Math.Min(Bounds.Width, Bounds.Height)) / 12;
            var x = Bounds.X + Bounds.Width / 2; var y = Bounds.Y + Bounds.Height / 2;
            var color = IsEffectivelyEnabled ? Foreground : Theme.Disabled;
            for (var i = -1; i <= 1; i++)
                context.Fill(new(x + i * 4 * unit * MathF.Cos(angle) - unit,
                    y + i * 4 * unit * MathF.Sin(angle) - unit, 2 * unit, 2 * unit), color, unit);
        }
        public override bool HandleInput(UiInput input, IInputContext context) { _context = context; try { return base.HandleInput(input, context); } finally { _context = null; } }
        protected override void Activate()
        {
            if (bar._pinned || bar.IsMenuOpen) { bar.CloseMenu(); _context!.Focus(this); }
            else
            {
                bar._pinned = true; bar.Expand(true); bar._openFirstWhenRevealed = true;
                _context!.Focus(this); bar.OpenFirstIfReady(_context);
            }
        }
    }
    private sealed class Heading(MenuBar bar, int index, string label) : Button(label)
    {
        public override bool Focusable => bar.IsExpanded;
        private IInputContext? _context;
        protected override float ThemeBorderWidth => 0;
        protected override Thickness ThemePadding => new(9, 6, 9, 6);
        public override bool HandleInput(UiInput input, IInputContext context)
        {
            if (input.Kind == InputKind.PointerMove && bar.IsMenuOpen && bar._open != index) { bar.OpenGroup(bar._groups[index].Id, context); return true; }
            if (input.Kind == InputKind.KeyDown && input.Key is UiKey.Down or UiKey.Up) { bar.OpenGroup(bar._groups[index].Id, context); return true; }
            if (input.Kind == InputKind.KeyDown && input.Key is UiKey.Right or UiKey.Left)
            { context.Focus(bar.Children[1 + (index + (input.Key == UiKey.Right ? 1 : bar._groups.Length - 1)) % bar._groups.Length]); return true; }
            _context = context; try { return base.HandleInput(input, context); } finally { _context = null; }
        }
        protected override void Activate() { if (bar._open == index && bar.IsMenuOpen) bar._overlays.Close(); else bar.OpenGroup(bar._groups[index].Id, _context!); }
    }
    private sealed class Popup : ElementList
    {
        public Popup(MenuBar bar, IReadOnlyList<MenuAction> actions)
        {
            WindowRegion = WindowRegionRole.Client;
            var host = bar._overlays;
            foreach (var action in actions) Add(new MenuItem(action, () => host.Close()));
        }
        protected override BackdropMaterial? ThemeBackdrop => Theme.MenuBackdrop;
        protected override Color ThemeBackground => Backdrop == null ? Theme.Surface : Color.Transparent;
        protected override float ThemeBorderWidth => 1;
        protected override Thickness ThemePadding => new(3);
        protected override Size MeasureCore(Size available, ITextLayoutService text)
        { foreach (var row in Children) row.Measure(available, text); return new(Children.Max(c => c.DesiredSize.Width), Children.Sum(c => c.DesiredSize.Height)); }
        protected override void ArrangeCore(Rect area)
        { var y = area.Y; foreach (var row in Children) { var h = Math.Min(row.DesiredSize.Height, Math.Max(0, area.Y + area.Height - y)); row.Arrange(new(area.X, y, area.Width, h)); y += h; } }
        public void FocusFirst(IInputContext context) { var row = Children.FirstOrDefault(c => c.IsEffectivelyEnabled); if (row != null) context.Focus(row); }
        public override bool HandleInput(UiInput input, IInputContext context)
        {
            if (input.Kind != InputKind.KeyDown || input.Key is not (UiKey.Up or UiKey.Down or UiKey.Home or UiKey.End)) return false;
            var rows = Children.Where(c => c.IsEffectivelyEnabled).ToArray(); if (rows.Length == 0) return true;
            var index = Array.FindIndex(rows, r => r.IsFocused);
            context.Focus(rows[input.Key switch { UiKey.Home => 0, UiKey.End => rows.Length - 1, UiKey.Down => (index + 1) % rows.Length, _ => (Math.Max(0, index) + rows.Length - 1) % rows.Length }]); return true;
        }
    }
}
