namespace TrueMoon.Argentis;

/// <summary>A labeled check box with a bindable boolean value.</summary>
public sealed class CheckBox : Button
{
    /// <summary>Creates a new CheckBox with its constructor defaults.</summary>
    /// <param name="text">The literal label.</param>
    /// <returns>A new independent CheckBox for Fluent configuration.</returns>
    public static CheckBox Create(string text = "") => new(text);

    /// <summary>Checked state.</summary>
    public static readonly UiProperty<bool> IsCheckedProperty = new("IsChecked", false);
    /// <summary>Creates a check box.</summary>
    public CheckBox(string text = "") : base(text) => Padding = new Thickness(36, 8, 12, 8);
    /// <summary>The current value.</summary>
    public bool IsChecked { get => Get(IsCheckedProperty); set => Set(IsCheckedProperty, value); }
    /// <inheritdoc />
    protected override void Activate() { IsChecked = !IsChecked; base.Activate(); }
    /// <inheritdoc />
    protected override void DrawCore(IDrawingContext context)
    {
        base.DrawCore(context);
        var box = new Rect(Bounds.X + 10, Bounds.Y + (Bounds.Height - 16) / 2, 16, 16);
        context.Stroke(box, Theme.Accent, 2, 2);
        if (IsChecked) context.Fill(box.Deflate(new Thickness(4)), Theme.Accent);
    }
}

/// <summary>A horizontal progress indicator in the inclusive range zero to one.</summary>
public class ProgressBar : Element
{
    /// <summary>Creates a new ProgressBar with its constructor defaults.</summary>
    /// <returns>A new independent ProgressBar for Fluent configuration.</returns>
    public static ProgressBar Create() => new();

    /// <summary>Normalized value.</summary>
    public static readonly UiProperty<float> ValueProperty = new("Value", 0, Invalidation.Render, v => float.IsFinite(v) && v >= 0 && v <= 1);
    /// <summary>The normalized progress.</summary>
    public float Value { get => Get(ValueProperty); set => Set(ValueProperty, value); }
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text) => new(160, 20);
    /// <inheritdoc />
    protected override void DrawCore(IDrawingContext context)
    {
        context.Fill(Bounds, Theme.Control, 4);
        context.Fill(Bounds with { Width = Bounds.Width * Value }, Theme.Accent, 4);
    }
}

/// <summary>A normalized slider supporting dragging and arrow keys.</summary>
public sealed class Slider : ProgressBar
{
    /// <summary>Creates a new Slider with its constructor defaults.</summary>
    /// <returns>A new independent Slider for Fluent configuration.</returns>
    public new static Slider Create() => new();

    private bool _dragging;
    /// <inheritdoc />
    public override bool Focusable => true;
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text) => new(160, 28);
    /// <inheritdoc />
    public override bool HandleInput(UiInput input, IInputContext context)
    {
        if (!IsEffectivelyEnabled) return false;
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

/// <summary>Displays a borrowed decoded image; its caller owns the image lifetime.</summary>
public sealed class Image : Element
{
    /// <summary>Creates a new Image with its constructor defaults.</summary>
    /// <returns>A new independent Image for Fluent configuration.</returns>
    public static Image Create() => new();

    private IImageSource? _source;
    /// <summary>The decoded image.</summary>
    public IImageSource? Source { get => _source; set { VerifyAccess(); _source = value; Invalidate(Invalidation.Layout); } }
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text) => Source?.Size ?? default;
    /// <inheritdoc />
    protected override void DrawCore(IDrawingContext context) { if (Source != null) context.Image(Source, Bounds.Deflate(Padding)); }
}
