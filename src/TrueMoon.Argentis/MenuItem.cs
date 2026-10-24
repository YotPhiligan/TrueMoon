namespace TrueMoon.Argentis;

/// <summary>Immutable menu action description; contains no controls or native resources.</summary>
/// <param name="Id">Stable action identity.</param><param name="Text">Visible action name.</param><param name="Command">Borrowed shared command.</param>
/// <param name="Icon">Optional geometric symbol.</param><param name="Shortcut">Visible keyboard hint.</param>
public sealed record MenuAction(string Id, string Text, IUiCommand Command, UiSymbol? Icon = null, string Shortcut = "");

/// <summary>A named top-level menu with a frozen action list.</summary>
public sealed class MenuGroup
{
    /// <summary>Creates a nonempty menu.</summary>
    /// <param name="id">Stable identity.</param><param name="label">Visible heading.</param><param name="actions">Action declarations.</param>
    public MenuGroup(string id, string label, IEnumerable<MenuAction> actions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id); ArgumentException.ThrowIfNullOrWhiteSpace(label); ArgumentNullException.ThrowIfNull(actions);
        var values = actions.ToArray();
        if (values.Length == 0 || values.Any(a => a == null || string.IsNullOrWhiteSpace(a.Id) || string.IsNullOrWhiteSpace(a.Text) || a.Command == null || a.Shortcut == null) ||
            values.Select(a => a.Id).Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw new ArgumentException("Menu actions must be nonempty and have distinct valid identities.", nameof(actions));
        Id = id; Label = label; Actions = Array.AsReadOnly(values);
    }
    /// <summary>Gets stable menu identity.</summary>
    public string Id { get; }
    /// <summary>Gets its visible heading.</summary>
    public string Label { get; }
    /// <summary>Gets frozen action declarations.</summary>
    public IReadOnlyList<MenuAction> Actions { get; }
}

/// <summary>A popup action row borrowing its shared command and disconnecting it on disposal.</summary>
public sealed class MenuItem : Button
{
    private readonly MenuAction _action;
    private readonly Action _dismiss;
    /// <summary>Creates a row.</summary>
    /// <param name="action">Borrowed command declaration.</param><param name="dismiss">Closes its owning popup before execution.</param>
    public MenuItem(MenuAction action, Action dismiss) : base(action?.Text ?? throw new ArgumentNullException(nameof(action)))
    {
        ArgumentNullException.ThrowIfNull(action.Command); ArgumentNullException.ThrowIfNull(dismiss);
        _action = action; _dismiss = dismiss; AccessibleName = action.Text; WindowRegion = WindowRegionRole.Client;
        IsEnabled = action.Command.CanExecute; action.Command.CanExecuteChanged += Availability;
    }
    /// <summary>Gets stable action identity.</summary>
    public string ActionId => _action.Id;
    /// <summary>Gets the borrowed shared command.</summary>
    public IUiCommand Command => _action.Command;
    private void Availability() => Dispatch(() => IsEnabled = _action.Command.CanExecute);
    /// <inheritdoc />
    protected override float ThemeBorderWidth => 0;
    /// <inheritdoc />
    protected override Color ThemeBackground => IsEffectivelyEnabled
        ? IsPressed ? Theme.ControlPressed : IsHovered ? Theme.ControlHover : Color.Transparent
        : Color.Transparent;
    /// <inheritdoc />
    protected override Thickness ThemePadding => new(30, 7, 12, 7);
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text)
    {
        var label = text.Measure(Value, FontSize, FontFamily); var shortcut = text.Measure(_action.Shortcut, FontSize, FontFamily);
        _shortcutWidth = shortcut.Width;
        return new(Math.Max(180, label.Width + (_action.Shortcut.Length == 0 ? 0 : shortcut.Width + 32)), Math.Max(18, label.Height));
    }
    /// <inheritdoc />
    protected override void DrawCore(IDrawingContext context)
    {
        var color = IsEffectivelyEnabled ? Foreground : Theme.Disabled;
        if (_action.Icon is { } icon) UiSymbols.Draw(context, icon, new(Bounds.X + 8, Bounds.Y + (Bounds.Height - 12) / 2, 12, 12), color);
        var area = Bounds.Deflate(Padding); context.Text(Value, area.X, area.Y, color, FontSize, FontFamily);
        if (_action.Shortcut.Length != 0) context.Text(_action.Shortcut, Bounds.X + Bounds.Width - Padding.Right - _shortcutWidth, area.Y, Theme.Disabled, FontSize, FontFamily);
    }
    private float _shortcutWidth;
    /// <inheritdoc />
    protected override void Activate() { if (!_action.Command.CanExecute) return; var command = _action.Command; _dismiss(); command.Execute(); }
    /// <inheritdoc />
    public override void Dispose() { try { base.Dispose(); } finally { if (IsDisposed) _action.Command.CanExecuteChanged -= Availability; } }
}
