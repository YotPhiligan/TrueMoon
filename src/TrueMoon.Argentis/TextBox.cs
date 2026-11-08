using System.Globalization;

namespace TrueMoon.Argentis;

/// <summary>A single-line editor with grapheme-aware navigation, selection, and clipboard commands.</summary>
public sealed class TextBox : Element
{
    /// <summary>Creates a new TextBox with its constructor defaults.</summary>
    /// <returns>A new independent TextBox for Fluent configuration.</returns>
    public static TextBox Create() => new();

    /// <summary>The editable string.</summary>
    public static readonly UiProperty<string> ValueProperty = new("Value", "", Invalidation.Layout, v => v != null && !v.Contains('\r') && !v.Contains('\n'));
    private int _caret, _anchor;
    private float _scrollX;
    private ITextLayoutService? _text;
    /// <summary>Creates a text editor.</summary>
    public TextBox() { MinWidth = 160; }
    /// <summary>Whether text is selectable/copyable without user edits.</summary>
    public static readonly UiProperty<bool> IsReadOnlyProperty = new("IsReadOnly", false);
    /// <summary>Preserves selection/navigation/copy while rejecting text, cut, paste and delete.</summary>
    public bool IsReadOnly { get => Get(IsReadOnlyProperty); set => Set(IsReadOnlyProperty, value); }
    /// <inheritdoc />
    protected override Color ThemeBackground => !IsEffectivelyEnabled ? Theme.ControlDisabled : IsReadOnly ? Theme.ReadOnly : Theme.Input;
    /// <inheritdoc />
    protected override float ThemeBorderWidth => Theme.BorderWidth;
    /// <inheritdoc />
    protected override Thickness ThemePadding => Theme.EditorPadding;
    /// <inheritdoc />
    public override bool Focusable => true;
    /// <inheritdoc />
    protected override bool SupportsValidationIcon => true;
    /// <summary>The editor content.</summary>
    public string Value { get => Get(ValueProperty); set => Set(ValueProperty, value); }
    /// <inheritdoc />
    protected override void OnPropertyChanged(object property)
    {
        base.OnPropertyChanged(property);
        if (ReferenceEquals(property, ValueProperty)) ClampSelection();
    }
    /// <summary>UTF-16 caret position.</summary>
    public int CaretIndex => _caret;
    /// <summary>Start of the selected UTF-16 range.</summary>
    public int SelectionStart => Math.Min(_caret, _anchor);
    /// <summary>Length of the selected UTF-16 range.</summary>
    public int SelectionLength => Math.Abs(_caret - _anchor);
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text)
    { _text = text; ClampSelection(); return new Size(160, text.Measure("Mg", FontSize, FontFamily, FontWeight, FontItalic).Height); }
    private float MeasurePrefix(int position) => _text?.Measure(Value[..position], FontSize, FontFamily, FontWeight, FontItalic).Width ?? 0;
    private void ClampSelection()
    {
        var value = Value;
        var positions = StringInfo.ParseCombiningCharacters(value);
        int Clamp(int index) => index >= value.Length ? value.Length : positions.LastOrDefault(position => position <= Math.Max(0, index));
        _caret = Clamp(_caret);
        _anchor = Clamp(_anchor);
    }
    private int Previous(int index) => StringInfo.ParseCombiningCharacters(Value).LastOrDefault(i => i < index);
    private int Next(int index) => StringInfo.ParseCombiningCharacters(Value).FirstOrDefault(i => i > index, Value.Length);
    private void Move(int position, bool select) { _caret = position; if (!select) _anchor = position; Invalidate(); }
    private void ReplaceSelection(string replacement)
    {
        var start = SelectionStart;
        Value = Value.Remove(start, SelectionLength).Insert(start, replacement.Replace("\r", "").Replace("\n", ""));
        _caret = _anchor = start + replacement.Replace("\r", "").Replace("\n", "").Length;
        ClampSelection();
        Invalidate(Invalidation.Layout);
    }
    private Rect TextLineArea(float lineHeight)
    {
        var area = EditorContentBounds;
        if (area.Height >= lineHeight) return area;
        // A fixed compact height may not accommodate both the font and requested
        // padding. Give the line priority, retaining the top/bottom padding ratio.
        var border = Math.Min(BorderWidth, Bounds.Height / 2);
        var height = Math.Min(lineHeight, Math.Max(0, Bounds.Height - 2 * border));
        var spare = Math.Max(0, Bounds.Height - 2 * border - height);
        var top = Padding.Vertical > 0 ? spare * (Padding.Top / Padding.Vertical) : spare / 2;
        return area with { Y = Bounds.Y + border + top, Height = height };
    }
    /// <inheritdoc />
    protected override void DrawCore(IDrawingContext context)
    {
        ClampSelection();
        if (IsReadOnly && !HasError) context.Fill(new Rect(Bounds.X + Bounds.Width - 4, Bounds.Y + 4, 2, Math.Max(0, Bounds.Height - 8)), Theme.Border);
        var lineHeight = _text?.Measure("Mg", FontSize, FontFamily, FontWeight, FontItalic).Height ?? EditorContentBounds.Height;
        var area = TextLineArea(lineHeight);
        var border = Math.Min(BorderWidth, Bounds.Height / 2);
        // Padding positions the line; it must not clip glyph ink or antialiased
        // selection edges. Horizontal clipping still excludes the error badge.
        var viewport = area with { Y = Bounds.Y + border, Height = Math.Max(0, Bounds.Height - 2 * border) };
        var textY = area.Y + (area.Height - lineHeight) / 2;
        var caretX = MeasurePrefix(_caret);
        _scrollX = Math.Clamp(_scrollX, Math.Max(0, caretX - Math.Max(0, area.Width - 2)), Math.Max(0, caretX));
        context.Save();
        try
        {
            context.Clip(viewport);
            if (IsFocused && SelectionLength > 0)
                context.Fill(new Rect(area.X + MeasurePrefix(SelectionStart) - _scrollX, textY,
                    MeasurePrefix(SelectionStart + SelectionLength) - MeasurePrefix(SelectionStart), lineHeight), Theme.Accent with { A = 100 });
            context.Text(Value, area.X - _scrollX, textY, IsEffectivelyEnabled ? Foreground : Theme.Disabled, FontSize, FontFamily, FontWeight, FontItalic);
            if (IsFocused) context.Fill(new Rect(area.X + caretX - _scrollX, textY, 1, lineHeight), Foreground);
        }
        finally { context.Restore(); }
    }
    /// <inheritdoc />
    public override bool HandleInput(UiInput input, IInputContext context)
    {
        if (!IsEffectivelyEnabled) return false;
        ClampSelection();
        if (input.Kind == InputKind.PointerDown && input.Button == 0)
        {
            if (HasError && ValidationIconBounds.Contains(input.X, input.Y)) return true;
            context.Focus(this);
            var x = input.X - Bounds.X - Padding.Left + _scrollX;
            var positions = StringInfo.ParseCombiningCharacters(Value).Append(Value.Length);
            Move(positions.MinBy(i => Math.Abs(MeasurePrefix(i) - x)), input.Shift);
            return true;
        }
        if (!IsReadOnly && input.Kind == InputKind.Text && !string.IsNullOrEmpty(input.Text))
        {
            var text = string.Concat(input.Text.Where(c => !char.IsControl(c)));
            if (text.Length == 0) return false;
            ReplaceSelection(text);
            return true;
        }
        if (input.Kind != InputKind.KeyDown) return false;
        if (input.Control)
        {
            switch (input.Key)
            {
                case UiKey.A: _anchor = 0; _caret = Value.Length; Invalidate(); return true;
                case UiKey.C: if (SelectionLength > 0) context.SetClipboardText(Value.Substring(SelectionStart, SelectionLength)); return true;
                case UiKey.X:
                    if (IsReadOnly) return true;
                    if (SelectionLength > 0) { context.SetClipboardText(Value.Substring(SelectionStart, SelectionLength)); ReplaceSelection(""); }
                    return true;
                case UiKey.V: if (IsReadOnly) return true; var value = context.GetClipboardText(); if (value != null) ReplaceSelection(value); return true;
            }
        }
        switch (input.Key)
        {
            case UiKey.Left: Move(!input.Shift && SelectionLength > 0 ? SelectionStart : Previous(_caret), input.Shift); return true;
            case UiKey.Right: Move(!input.Shift && SelectionLength > 0 ? SelectionStart + SelectionLength : Next(_caret), input.Shift); return true;
            case UiKey.Home: Move(0, input.Shift); return true;
            case UiKey.End: Move(Value.Length, input.Shift); return true;
            case UiKey.Backspace:
                if (IsReadOnly) return true;
                if (SelectionLength == 0) _anchor = Previous(_caret);
                ReplaceSelection(""); return true;
            case UiKey.Delete:
                if (IsReadOnly) return true;
                if (SelectionLength == 0) _anchor = Next(_caret);
                ReplaceSelection(""); return true;
            default: return false;
        }
    }
}
