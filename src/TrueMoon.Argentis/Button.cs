namespace TrueMoon.Argentis;

/// <summary>A pointer- and keyboard-activated button.</summary>
public class Button : Text
{
    private bool _pressed;
    /// <summary>Creates a button with a label.</summary>
    public Button(string text = "") : base(text) => Padding = new Thickness(12, 8, 12, 8);
    /// <inheritdoc />
    public override bool Focusable => true;
    /// <summary>Raised once per completed activation.</summary>
    public event Action? Click;
    /// <summary>Whether an activation is in progress.</summary>
    public bool IsPressed => _pressed;
    /// <summary>Invokes the button action.</summary>
    protected virtual void Activate() => Click?.Invoke();
    /// <inheritdoc />
    protected override void DrawCore(IDrawingContext context)
    {
        context.Fill(Bounds, _pressed ? Theme.Accent : Theme.Control, 4);
        if (IsHovered && IsEffectivelyEnabled) context.Stroke(Bounds.Deflate(new Thickness(1)), Theme.Accent, 1, 4);
        base.DrawCore(context);
    }
    /// <inheritdoc />
    public override bool HandleInput(UiInput input, IInputContext context)
    {
        if (!IsEffectivelyEnabled) return false;
        if (input.Kind == InputKind.PointerDown && input.Button == 0)
        {
            context.Focus(this); context.Capture(this); _pressed = true; Invalidate(); return true;
        }
        if (input.Kind == InputKind.PointerUp && input.Button == 0 && _pressed)
        {
            _pressed = false; context.ReleaseCapture(this); Invalidate();
            if (Bounds.Contains(input.X, input.Y)) Activate();
            return true;
        }
        if (input.Kind == InputKind.KeyDown && input.Key is UiKey.Enter or UiKey.Space)
        { _pressed = true; Invalidate(); return true; }
        if (input.Kind == InputKind.KeyUp && input.Key is UiKey.Enter or UiKey.Space && _pressed)
        { _pressed = false; Invalidate(); Activate(); return true; }
        return false;
    }
    /// <inheritdoc />
    public override void OnInputCancelled() { _pressed = false; Invalidate(); }
    /// <inheritdoc />
    public override void Dispose() { Click = null; base.Dispose(); }
}
