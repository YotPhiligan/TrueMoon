using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Windows;

namespace TrueMoon.Argentis.Menus;

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
        var label = text.Measure(Value, FontSize, FontFamily, FontWeight, FontItalic); var shortcut = text.Measure(_action.Shortcut, FontSize, FontFamily, FontWeight, FontItalic);
        ShortcutWidth = shortcut.Width;
        return new(Math.Max(180, label.Width + (_action.Shortcut.Length == 0 ? 0 : shortcut.Width + 32)), Math.Max(18, label.Height));
    }
    internal UiSymbol? ActionIcon => _action.Icon;
    internal string Shortcut => _action.Shortcut;
    internal float ShortcutWidth { get; private set; }
    /// <inheritdoc />
    protected override void Activate(IInputContext context) { if (!_action.Command.CanExecute) return; var command = _action.Command; _dismiss(); command.Execute(); }
    /// <inheritdoc />
    public override void Dispose() { try { base.Dispose(); } finally { if (IsDisposed) _action.Command.CanExecuteChanged -= Availability; } }
}
