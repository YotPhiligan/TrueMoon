using System.Globalization;

namespace TrueMoon.Argentis;

public sealed partial class OverlayHost
{
    private Element? _validationTarget, _validationPopup;
    private Rect _validationBounds;
    private string _validationMessage = "";
    private float _validationX, _validationY;
    private TimeSpan _validationDelay;
    private readonly List<Element> _validationAncestors = [];
    private bool NeedsValidationFrame => _validationTarget != null && (_validationDelay > TimeSpan.Zero || !ValidationIsCurrent());

    private static Element? ValidationAt(Element node, float x, float y)
    {
        if (node is OverlayHost || node.IsDisposed || !node.IsVisible || !node.Bounds.Contains(x, y)) return null;
        if (node.ChildClipBounds.Contains(x, y))
            for (var i = node.Children.Count - 1; i >= 0; i--)
                if (node.Children[i].IsVisible && node.Children[i].Bounds.Contains(x, y))
                    return ValidationAt(node.Children[i], x, y); // A covering sibling must not expose errors beneath it.
        return node.HasError && !string.IsNullOrWhiteSpace(node.ErrorMessage) && node.ValidationIconBounds.Contains(x, y) ? node : null;
    }
    private bool ValidationIsCurrent() => _validationTarget is { IsDisposed: false, IsAttached: true } target &&
        target.ValidationIconBounds == _validationBounds && target.ErrorMessage == _validationMessage &&
        ReferenceEquals(ValidationAt(Content, _validationX, _validationY), target);

    private void ObserveValidationHint(UiInput input)
    {
        if (input.Kind != InputKind.PointerMove)
        {
            if (input.Kind == InputKind.Wheel && _validationPopup != null && _validationPopup.Bounds.Contains(input.X, input.Y)) return;
            if (input.Kind is InputKind.PointerDown or InputKind.KeyDown or InputKind.Text or InputKind.FocusLost or InputKind.Wheel) CancelValidationHint();
            return;
        }
        if (_validationPopup != null && ReferenceEquals(Popup, _validationPopup) && _validationPopup.Bounds.Contains(input.X, input.Y)) return;
        var target = ValidationAt(Content, input.X, input.Y);
        if (ReferenceEquals(target, _validationTarget) && ValidationIsCurrent()) return;
        CancelValidationHint();
        if (target == null || Popup != null) return;
        _validationTarget = target; _validationBounds = target.ValidationIconBounds; _validationMessage = target.ErrorMessage;
        _validationX = input.X; _validationY = input.Y; _validationDelay = TimeSpan.FromMilliseconds(500);
        for (Element? node = target; node != null; node = node.Parent)
        { _validationAncestors.Add(node); node.PropertyChanged += ValidationAncestorChanged; }
        Invalidate();
    }
    private void AdvanceValidationHint(TimeSpan elapsed, IInputContext context)
    {
        if (_validationTarget == null) return;
        if (!ValidationIsCurrent()) { CancelValidationHint(); return; }
        if (_validationDelay <= TimeSpan.Zero) return;
        _validationDelay -= elapsed;
        if (_validationDelay > TimeSpan.Zero) return;
        _validationDelay = TimeSpan.Zero;
        if (Popup != null) { CancelValidationHint(); return; }
        var popup = new ValidationHintSurface().Content(new ScrollViewer { MaxHeight = 240 }.Content(
            new ValidationHintText(_validationMessage) { AccessibleName = _validationMessage, FontSize = 12 }));
        _validationPopup = popup;
        try { Open(this, popup, _validationBounds, context, tooltip: true); }
        catch { ForgetValidationHint(); if (popup.Parent == null) popup.Dispose(); throw; }
    }
    private void CancelValidationHint()
    {
        var popup = _validationPopup; ForgetValidationHint();
        if (popup != null && ReferenceEquals(_popup, popup)) Close(false);
    }
    private void ForgetValidationHint()
    {
        foreach (var node in _validationAncestors) node.PropertyChanged -= ValidationAncestorChanged;
        _validationAncestors.Clear();
        _validationTarget = _validationPopup = null; _validationMessage = ""; _validationDelay = TimeSpan.Zero;
    }
    private void ValidationAncestorChanged(Element sender, object property)
    {
        if (ReferenceEquals(property, UiProperties.Visible) && !sender.IsVisible ||
            ReferenceEquals(property, UiProperties.Enabled) && !IsEffectivelyEnabled) CancelValidationHint();
    }

    private sealed class ValidationHintSurface : Border
    {
        public ValidationHintSurface() { Padding = new(8); CornerRadius = 4; FontSize = 12; }
        protected override Color ThemeBackground => Theme.Surface;
        protected override BoxShadow? ThemeShadow => Theme.MenuShadow;
    }
    private sealed class ValidationHintText(string value) : Text(value)
    {
        private readonly List<string> _lines = [];
        private float _lineHeight;
        protected override Size MeasureCore(Size available, ITextLayoutService text)
        {
            var width = Math.Min(400, available.Width); var widest = 0f;
            _lineHeight = Math.Max(FontSize, text.Measure("Mg", FontSize, FontFamily, FontWeight, FontItalic).Height) + 2;
            _lines.Clear();
            foreach (var paragraph in Value.ReplaceLineEndings("\n").Split('\n'))
            {
                var line = ""; var elements = StringInfo.GetTextElementEnumerator(paragraph);
                while (elements.MoveNext())
                {
                    var next = elements.GetTextElement();
                    if (line.Length != 0 && text.Measure(line + next, FontSize, FontFamily, FontWeight, FontItalic).Width > width)
                    { _lines.Add(line); widest = Math.Max(widest, text.Measure(line, FontSize, FontFamily, FontWeight, FontItalic).Width); line = next; }
                    else line += next;
                }
                _lines.Add(line); widest = Math.Max(widest, text.Measure(line, FontSize, FontFamily, FontWeight, FontItalic).Width);
            }
            return new(Math.Min(width, widest), _lines.Count * _lineHeight);
        }
        protected override void DrawCore(IDrawingContext context)
        {
            var y = Bounds.Y;
            foreach (var line in _lines) { context.Text(line, Bounds.X, y, Foreground, FontSize, FontFamily, FontWeight, FontItalic); y += _lineHeight; }
        }
    }
}
