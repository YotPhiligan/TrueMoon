using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Painting;
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
    protected override void Activate() { if (IsReadOnly) return; IsChecked = !IsChecked; base.Activate(); }
    private static readonly ElementPresentation CheckBoxPresentation = ElementPresentation.Create<CheckBox>(static (checkBox, context) =>
    {
        var bounds = checkBox.Bounds;
        var theme = checkBox.Theme;
        var area = bounds.Deflate(checkBox.Padding);
        context.Text(checkBox.Value, area.X, area.Y + Math.Max(0, (area.Height - checkBox.LabelSize.Height) / 2),
            checkBox.IsEffectivelyEnabled ? checkBox.Foreground : theme.Disabled, checkBox.FontSize, checkBox.FontFamily, checkBox.FontWeight, checkBox.FontItalic);
        var box = new Rect(bounds.X + 10, bounds.Y + (bounds.Height - 16) / 2, 16, 16);
        var radius = Math.Min(checkBox.CornerRadius, box.Width / 2);
        var color = !checkBox.IsEffectivelyEnabled ? theme.Disabled : checkBox.IsReadOnly ? theme.Border :
            checkBox.ColorRole == EditorColorRole.Neutral ? theme.Accent : theme.EditorColor(checkBox.ColorRole);
        if (checkBox.IsChecked) { context.Fill(box, color, radius); UiSymbols.Draw(context, UiSymbol.Check, box.Deflate(new Thickness(3)),
            checkBox.ColorRole == EditorColorRole.Neutral ? theme.OnAccent : theme.OnEditorAccent); }
        if (!checkBox.IsChecked || checkBox.ColorRole != EditorColorRole.Neutral || checkBox.HasError || checkBox.IsModified || checkBox.IsFocused || checkBox.IsHovered || checkBox.IsReadOnly)
            context.Stroke(box.Deflate(new Thickness(.5f)), checkBox.BorderColor, 1, Math.Max(0, radius - .5f));
    });
    /// <inheritdoc />
    public override ElementPresentation Presentation => CheckBoxPresentation;
}

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
    private static readonly ElementPresentation ProgressBarPresentation = ElementPresentation.Create<ProgressBar>(ProgressBarPresenter.DrawFill);
    /// <inheritdoc />
    public override ElementPresentation Presentation => ProgressBarPresentation;
}

/// <summary>A normalized slider supporting dragging and arrow keys.</summary>
public sealed class Slider : ProgressBar
{
    /// <inheritdoc />
    protected override float ThemeBorderWidth => 0;
    /// <inheritdoc />
    protected override Color ThemeBackground => Theme.ControlTintAlpha == 255 ? Color.Transparent : base.ThemeBackground;
    private static readonly ElementPresentation SliderPresentation = ElementPresentation.Create<Slider>(static (slider, context) =>
    {
        var bounds = slider.Bounds;
        var theme = slider.Theme;
        var track = new Rect(bounds.X + 8, bounds.Y + (bounds.Height - 4) / 2, Math.Max(0, bounds.Width - 16), 4);
        var color = slider.IsEffectivelyEnabled ? theme.Accent : theme.Disabled;
        context.Fill(track, theme.Border);
        context.Fill(track with { Width = track.Width * slider.Value }, color);
        var thumb = new Rect(track.X + track.Width * slider.Value - 7, bounds.Y + (bounds.Height - 16) / 2, 14, 16);
        context.Fill(thumb, slider._dragging ? theme.ControlPressed : slider.IsHovered ? theme.ControlHover : theme.Control);
        context.Stroke(thumb.Deflate(new Thickness(.5f)), color, 1);
    });
    /// <inheritdoc />
    public override ElementPresentation Presentation => SliderPresentation;
    private bool _dragging;
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

/// <summary>Displays a borrowed decoded image; its caller owns the image lifetime.</summary>
public sealed class Image : Element
{
    private IImageSource? _source;
    /// <summary>The decoded image.</summary>
    public IImageSource? Source { get => _source; set { VerifyAccess(); _source = value; Invalidate(Invalidation.Layout); } }
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text) => Source?.Size ?? default;
    private static readonly ElementPresentation ImagePresentation = ElementPresentation.Create<Image>(static (image, context) =>
    {
        if (image.Source is { } source) context.Image(source, image.Bounds.Deflate(image.Padding));
    });
    /// <inheritdoc />
    public override ElementPresentation Presentation => ImagePresentation;
}
