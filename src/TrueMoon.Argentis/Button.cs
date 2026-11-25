using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;

namespace TrueMoon.Argentis;

/// <summary>A pointer- and keyboard-activated button.</summary>
public class Button : Text
{
    private bool _pressed;
    private float? _labelInkCenter;
    /// <summary>Measured label dimensions in the current face.</summary>
    protected internal Size LabelSize { get; private set; }
    /// <summary>Whether to center the plain label inside the padded button area.</summary>
    protected virtual bool CentersLabel => Theme.CenterButtonLabels;
    /// <summary>Creates a button with a label.</summary>
    public Button(string text = "") : base(text) { }
    /// <inheritdoc />
    protected override Thickness ThemePadding => Theme.ButtonPadding;
    /// <inheritdoc />
    protected override float ThemeCornerRadius => Parent?.SquareDescendantButtons == true ? 0 : Theme.ButtonCornerRadius;
    /// <inheritdoc />
    protected override float ThemeBorderWidth => Theme.BorderWidth;
    /// <inheritdoc />
    protected override Color ThemeForeground => Tone == ControlTone.Neutral ? Theme.Foreground : Theme.OnAccent;
    /// <inheritdoc />
    protected override bool UsesThemedControlBackdrop => true;
    /// <inheritdoc />
    protected override Color ThemeBackground => !IsEffectivelyEnabled ? Theme.ControlDisabled : Tone == ControlTone.Neutral
        ? _pressed ? Theme.ControlPressed : IsHovered ? Theme.ControlHover : Theme.Control
        : Theme.Mix(Theme.ActionColor(Tone), _pressed ? new Color(0, 0, 0) : new Color(255, 255, 255), _pressed ? .16f : IsHovered ? .08f : 0);
    /// <inheritdoc />
    public override bool Focusable => true;
    /// <summary>Raised once per completed activation.</summary>
    public event Action? Click;
    /// <summary>Whether an activation is in progress.</summary>
    public bool IsPressed => _pressed;
    /// <summary>Invokes the button action.</summary>
    protected virtual void Activate() => Click?.Invoke();
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text)
    {
        LabelSize = base.MeasureCore(available, text);
        _labelInkCenter = null;
        if (CentersLabel && Value.Length > 0)
        {
            var ink = text.MeasureInkBounds("Hg", FontSize, FontFamily, FontWeight, FontItalic);
            _labelInkCenter = ink.Y + ink.Height / 2;
        }
        return LabelSize;
    }
    internal float? LabelInkCenter => _labelInkCenter;
    private static readonly ElementPresentation ButtonLabel = ElementPresentation.Create<Button>(
        new Factory<IElementPresenter<Button>>(static () => new ButtonPresenter()));
    /// <inheritdoc />
    public override ElementPresentation Presentation => ButtonLabel;
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
    public override void Dispose()
    {
        try { base.Dispose(); }
        finally { if (IsDisposed) Click = null; }
    }
}
