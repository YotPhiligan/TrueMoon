using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Painting;

namespace TrueMoon.Argentis;

/// <summary>A normalized slider supporting dragging and arrow keys.</summary>
public sealed class Slider : ProgressBar
{
    /// <inheritdoc />
    protected override float ThemeBorderWidth => 0;
    /// <inheritdoc />
    protected override Color ThemeBackground => Theme.ControlTintAlpha == 255 ? Color.Transparent : base.ThemeBackground;
    private bool _dragging;
    internal bool IsDragging => _dragging;
    /// <inheritdoc />
    public override bool Focusable => true;
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text) => new(160, 28);
    /// <inheritdoc />
    public override bool HandleInput(UiInput input, IInputContext context)
    {
        if (!IsEffectivelyEnabled) return false;
        if (input.Kind is InputKind.PointerDown or InputKind.PointerUp && input.Button != 0) return false;
        if (input.Kind == InputKind.PointerDown && input.Button == 0) { _dragging = true; context.Capture(this); context.Focus(this); }
        if (_dragging && input.Kind is InputKind.PointerDown or InputKind.PointerMove or InputKind.PointerUp)
        {
            try { Value = Bounds.Width == 0 ? 0 : Math.Clamp((input.X - Bounds.X) / Bounds.Width, 0, 1); }
            catch { _dragging = false; context.ReleaseCapture(this); throw; }
            if (input.Kind == InputKind.PointerUp) { _dragging = false; context.ReleaseCapture(this); }
            return true;
        }
        if (input.Kind == InputKind.KeyDown && input.Key is UiKey.Left or UiKey.Right)
        { Value = Math.Clamp(Value + (input.Key == UiKey.Left ? -.05f : .05f), 0, 1); return true; }
        return false;
    }
    /// <inheritdoc />
    public override void OnInputCancelled() => _dragging = false;
}