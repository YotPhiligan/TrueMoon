namespace TrueMoon.Argentis;

/// <summary>A bounded, keyboard-navigable popup of shared menu commands. Its OverlayHost owns disposal.</summary>
public sealed class MenuPopup : ElementList, IUiInputObserver
{
    private readonly Action _dismiss;
    /// <summary>Creates fresh menu rows; each row borrows its command and disconnects on disposal.</summary>
    /// <param name="actions">One to 64 distinct actions.</param><param name="dismiss">Closes the popup before command execution.</param>
    public MenuPopup(IEnumerable<MenuAction> actions, Action dismiss)
    {
        ArgumentNullException.ThrowIfNull(actions); ArgumentNullException.ThrowIfNull(dismiss);
        _dismiss = dismiss;
        var values = new List<MenuAction>();
        foreach (var value in actions) { if (values.Count == 64) throw new ArgumentOutOfRangeException(nameof(actions)); values.Add(value); }
        var group = new MenuGroup("popup", "Popup", values);
        WindowRegion = WindowRegionRole.Client;
        foreach (var action in group.Actions) Add(new MenuItem(action, dismiss));
    }
    /// <inheritdoc />
    public override bool Focusable => true;
    /// <inheritdoc />
    public int InputPriority => 20;
    /// <inheritdoc />
    public bool PreviewInput(UiInput input, IInputContext context)
    {
        if (input.Kind != InputKind.KeyDown || !(IsFocused || Children.Any(row => row.IsFocused))) return false;
        if (input.Key == UiKey.Escape) { _dismiss(); return true; }
        return input.Key == UiKey.Tab && HandleInput(input, context);
    }
    /// <inheritdoc />
    protected override BackdropMaterial? ThemeBackdrop => Theme.MenuBackdrop;
    /// <inheritdoc />
    protected override Color ThemeBackground => Backdrop == null ? Theme.Surface : Color.Transparent;
    /// <inheritdoc />
    protected override float ThemeBorderWidth => 1;
    /// <inheritdoc />
    protected override Thickness ThemePadding => new(3);
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text)
    { foreach (var row in Children) row.Measure(available, text); return new(Children.Max(row => row.DesiredSize.Width), Children.Sum(row => row.DesiredSize.Height)); }
    /// <inheritdoc />
    protected override void ArrangeCore(Rect area)
    {
        var y = area.Y;
        foreach (var row in Children) { var height = Math.Min(row.DesiredSize.Height, Math.Max(0, area.Y + area.Height - y)); row.Arrange(new(area.X, y, area.Width, height)); y += height; }
    }
    /// <summary>Focuses the first currently enabled command, or the popup when every command is disabled.</summary>
    /// <param name="context">Borrowed focus services for this call.</param>
    public void FocusFirst(IInputContext context)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(context);
        context.Focus(Children.OfType<MenuItem>().FirstOrDefault(row => row.IsEffectivelyEnabled && row.Command.CanExecute) ?? (Element)this);
    }
    /// <inheritdoc />
    public override bool HandleInput(UiInput input, IInputContext context)
    {
        if (input.Kind != InputKind.KeyDown || input.Key is not (UiKey.Up or UiKey.Down or UiKey.Home or UiKey.End or UiKey.Tab)) return false;
        var rows = Children.OfType<MenuItem>().Where(row => row.IsEffectivelyEnabled && row.Command.CanExecute).ToArray(); if (rows.Length == 0) return true;
        var index = Array.FindIndex(rows, row => row.IsFocused);
        var next = input.Key switch { UiKey.Home => 0, UiKey.End => rows.Length - 1, UiKey.Up => (Math.Max(0, index) + rows.Length - 1) % rows.Length,
            UiKey.Tab when input.Shift => (Math.Max(0, index) + rows.Length - 1) % rows.Length, _ => (index + 1) % rows.Length };
        context.Focus(rows[next]); return true;
    }
}
