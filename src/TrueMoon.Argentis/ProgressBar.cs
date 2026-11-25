using TrueMoon.Argentis.Painting;

namespace TrueMoon.Argentis;

/// <summary>A horizontal progress indicator in the inclusive range zero to one.</summary>
public class ProgressBar : Element
{
    /// <inheritdoc />
    protected override bool UsesThemedControlBackdrop => true;
    /// <inheritdoc />
    protected override Color ThemeBackground => IsEffectivelyEnabled ? Theme.Control : Theme.ControlDisabled;
    /// <inheritdoc />
    protected override float ThemeBorderWidth => Theme.BorderWidth;
    /// <summary>Normalized value.</summary>
    public static readonly UiProperty<float> ValueProperty = new("Value", 0, Invalidation.Render, v => float.IsFinite(v) && v >= 0 && v <= 1);
    /// <summary>The normalized progress.</summary>
    public float Value { get => Get(ValueProperty); set => Set(ValueProperty, value); }
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text) => new(160, 20);
}