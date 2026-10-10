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
    /// <inheritdoc />
    protected override Thickness ThemePadding => Theme.EditorPadding;
    /// <inheritdoc />
    public override bool Focusable => true;
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
    { _text = text; ClampSelection(); return new Size(160, text.Measure("Mg", FontSize, FontFamily).Height); }
    private float MeasurePrefix(int position) => _text?.Measure(Value[..position], FontSize, FontFamily).Width ?? 0;
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
    /// <inheritdoc />
    protected override void DrawCore(IDrawingContext context)
    {
        ClampSelection();
        context.Fill(Bounds, Theme.Control, 4);
        var area = Bounds.Deflate(Padding);
        var caretX = MeasurePrefix(_caret);
        _scrollX = Math.Clamp(_scrollX, Math.Max(0, caretX - area.Width + 2), Math.Max(0, caretX));
        context.Save();
        try
        {
            context.Clip(area);
            if (IsFocused && SelectionLength > 0)
                context.Fill(new Rect(area.X + MeasurePrefix(SelectionStart) - _scrollX, area.Y,
                    MeasurePrefix(SelectionStart + SelectionLength) - MeasurePrefix(SelectionStart), area.Height), Theme.Accent with { A = 100 });
            context.Text(Value, area.X - _scrollX, area.Y, IsEffectivelyEnabled ? Foreground : Theme.Disabled, FontSize, FontFamily);
            if (IsFocused) context.Fill(new Rect(area.X + caretX - _scrollX, area.Y, 1, area.Height), Foreground);
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
            context.Focus(this);
            var x = input.X - Bounds.X - Padding.Left + _scrollX;
            var positions = StringInfo.ParseCombiningCharacters(Value).Append(Value.Length);
            Move(positions.MinBy(i => Math.Abs(MeasurePrefix(i) - x)), input.Shift);
            return true;
        }
        if (input.Kind == InputKind.Text && !string.IsNullOrEmpty(input.Text))
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
                    if (SelectionLength > 0) { context.SetClipboardText(Value.Substring(SelectionStart, SelectionLength)); ReplaceSelection(""); }
                    return true;
                case UiKey.V: var value = context.GetClipboardText(); if (value != null) ReplaceSelection(value); return true;
            }
        }
        switch (input.Key)
        {
            case UiKey.Left: Move(!input.Shift && SelectionLength > 0 ? SelectionStart : Previous(_caret), input.Shift); return true;
            case UiKey.Right: Move(!input.Shift && SelectionLength > 0 ? SelectionStart + SelectionLength : Next(_caret), input.Shift); return true;
            case UiKey.Home: Move(0, input.Shift); return true;
            case UiKey.End: Move(Value.Length, input.Shift); return true;
            case UiKey.Backspace:
                if (SelectionLength == 0) _anchor = Previous(_caret);
                ReplaceSelection(""); return true;
            case UiKey.Delete:
                if (SelectionLength == 0) _anchor = Next(_caret);
                ReplaceSelection(""); return true;
            default: return false;
        }
    }
}
