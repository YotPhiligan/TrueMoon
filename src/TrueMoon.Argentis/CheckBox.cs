using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Styling;

namespace TrueMoon.Argentis;

/// <summary>A labeled check box with a bindable boolean value.</summary>
public sealed class CheckBox : Button
{
    /// <summary>Read-only values retain focus/navigation but cannot be toggled or activate Click handlers.</summary>
    public static readonly UiProperty<bool> IsReadOnlyProperty = new("IsReadOnly", false);
    /// <summary>Whether user activation is suppressed while retaining focus.</summary>
    public bool IsReadOnly { get => Get(IsReadOnlyProperty); set => Set(IsReadOnlyProperty, value); }
    /// <summary>Checked state.</summary>
    public static readonly UiProperty<bool> IsCheckedProperty = new("IsChecked", false);
    /// <summary>Creates a check box.</summary>
    public CheckBox(string text = "") : base(text) { }
    /// <inheritdoc />
    protected override Thickness ThemePadding => Theme.ButtonPadding with { Left = Theme.ButtonPadding.Left + 24 };
    /// <inheritdoc />
    protected override bool CentersLabel => false;
    /// <inheritdoc />
    protected override float ThemeBorderWidth => 0;
    /// <inheritdoc />
    protected override Color ThemeForeground => Theme.Foreground;
    /// <inheritdoc />
    protected override Color ThemeBackground => IsReadOnly ? Theme.ReadOnly : IsEffectivelyEnabled && IsHovered ? Theme.ControlHover :
        Theme.ControlTintAlpha == 255 ? Color.Transparent : IsEffectivelyEnabled ? Theme.Control : Theme.ControlDisabled;
    /// <summary>The current value.</summary>
    public bool IsChecked { get => Get(IsCheckedProperty); set => Set(IsCheckedProperty, value); }
    /// <inheritdoc />
    protected override void Activate(IInputContext context) { if (IsReadOnly) return; IsChecked = !IsChecked; base.Activate(context); }
}