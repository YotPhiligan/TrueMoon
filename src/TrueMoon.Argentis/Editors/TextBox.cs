using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using System.Globalization;

namespace TrueMoon.Argentis.Editors;

/// <summary>A single-line editor with grapheme-aware navigation, selection, and clipboard commands.</summary>
public sealed class TextBox : Element
{
    /// <summary>The editable string.</summary>
    [FluentTwoWayBinding]
    public static readonly UiProperty<string> ValueProperty = new("Value", "", Invalidation.Layout, v => v != null && !v.Contains('\r') && !v.Contains('\n'));
    /// <summary>Requests single-line shaping when the renderer provides rich text layout and drawing.</summary>
    public static readonly UiProperty<bool> UseShapedTextProperty = new("UseShapedText", false, Invalidation.Layout);
    private int _caret, _anchor;
    private float _scrollX, _caretX, _anchorX, _lineHeight, _lineY;
    private Rect _viewport;
    private string _boundaryText = "";
    private int[] _boundaries = [];
    private ITextLayoutService? _text;
    private IRichTextLayout? _shapedLine;
    private ShapedLineSource _shapedSource;

    private readonly record struct ShapedLineSource(ITextLayoutService Service, string Text, string Family,
        float Size, int Weight, bool Italic, Color Foreground);

    /// <summary>Creates a text editor.</summary>
    public TextBox()
    {
        MinWidth = 160;
        AttachmentChanged += () => { if (!IsAttached) { _text = null; ReleaseShapedLine(); } };
    }
    /// <summary>Whether text is selectable/copyable without user edits.</summary>
    public static readonly UiProperty<bool> IsReadOnlyProperty = new("IsReadOnly", false);
    /// <summary>Preserves selection/navigation/copy while rejecting text, cut, paste and delete.</summary>
    public bool IsReadOnly { get => Get(IsReadOnlyProperty); set => Set(IsReadOnlyProperty, value); }
    /// <inheritdoc />
    protected override Color ThemeBackground => !IsEffectivelyEnabled ? Theme.ControlDisabled : IsReadOnly ? Theme.ReadOnly : Theme.Input;
    /// <inheritdoc />
    protected override bool UsesThemedControlBackdrop => true;
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
    /// <summary>Shares the shaped line between drawing and caret placement on capable renderers. Defaults to false.</summary>
    public bool UseShapedText { get => Get(UseShapedTextProperty); set => Set(UseShapedTextProperty, value); }
    /// <inheritdoc />
    protected override void OnPropertyChanged(object property)
    {
        base.OnPropertyChanged(property);
        if (ReferenceEquals(property, ValueProperty)) ClampSelection();
        else if (ReferenceEquals(property, UiProperties.Foreground) && _shapedLine != null)
            GetShapedLine(); // The shaped run carries its own color; its metrics have not changed.
    }
    /// <summary>UTF-16 caret position.</summary>
    public int CaretIndex => _caret;
    /// <summary>Start of the selected UTF-16 range.</summary>
    public int SelectionStart => Math.Min(_caret, _anchor);
    /// <summary>Length of the selected UTF-16 range.</summary>
    public int SelectionLength => Math.Abs(_caret - _anchor);
    /// <summary>Current horizontal logical scroll offset of the text line.</summary>
    public float HorizontalOffset => _scrollX;
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text)
    {
        _text = text;
        ClampSelection();
        _lineHeight = GetShapedLine()?.Size.Height ?? text.MeasureEditorText("Mg", FontSize, FontFamily, FontWeight, FontItalic).Height;
        return new Size(160, _lineHeight);
    }
    private float MeasurePrefix(int position) => _shapedLine?.GetCaretBounds(position).X ??
        (position == 0 ? 0 : _text?.MeasureEditorText(Value[..position], FontSize, FontFamily, FontWeight, FontItalic).Width ?? 0);

    private IRichTextLayout? GetShapedLine()
    {
        if (!UseShapedText || _text is not IRichTextLayoutService rich)
        {
            ReleaseShapedLine();
            return null;
        }
        var source = new ShapedLineSource(_text, Value, FontFamily, FontSize, FontWeight, FontItalic,
            IsEffectivelyEnabled ? Foreground : Theme.Disabled);
        if (_shapedLine != null && ReferenceEquals(source.Service, _shapedSource.Service) && source == _shapedSource)
            return _shapedLine;

        ReleaseShapedLine();
        _shapedLine = rich.CreateRichTextLayout(RichTextDocument.FromText(source.Text), new RichTextStyle
        {
            FontFamily = source.Family, FontSize = source.Size, FontWeight = source.Weight,
            Italic = source.Italic, Foreground = source.Foreground
        }, null, RichTextAlignment.Left);
        _shapedSource = source;
        return _shapedLine;
    }

    private void ReleaseShapedLine()
    {
        var line = _shapedLine;
        _shapedLine = null;
        _shapedSource = default;
        line?.Dispose();
    }
    private void ClampSelection()
    {
        var text = Value;
        if (!ReferenceEquals(text, _boundaryText))
        {
            _boundaryText = text;
            _boundaries = StringInfo.ParseCombiningCharacters(text);
        }
        _caret = Clamp(_caret);
        _anchor = Clamp(_anchor);
    }
    private int Clamp(int index)
    {
        if (index >= _boundaryText.Length) return _boundaryText.Length;
        var found = Array.BinarySearch(_boundaries, index);
        return _boundaries[found >= 0 ? found : Math.Max(0, ~found - 1)];
    }
    private int Previous(int index) => index == 0 ? 0 : Clamp(index - 1);
    private int Next(int index)
    {
        var next = Array.BinarySearch(_boundaries, index) + 1;
        return next > 0 && next < _boundaries.Length ? _boundaries[next] : _boundaryText.Length;
    }
    private void Move(int position, bool select)
    {
        _caret = position;
        if (!select) _anchor = position;
        UpdateCaretGeometry();
        Invalidate();
    }
    private void ReplaceSelection(string replacement)
    {
        var start = SelectionStart;
        var singleLine = replacement.Replace("\r", "").Replace("\n", "");
        Value = Value.Remove(start, SelectionLength).Insert(start, singleLine);
        _caret = _anchor = start + singleLine.Length;
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
    protected override void ArrangeCore(Rect contentBounds)
    {
        var area = TextLineArea(_lineHeight);
        var border = Math.Min(BorderWidth, Bounds.Height / 2);
        // Allow ink outside the vertical padding, but not outside the border or under the badge.
        _viewport = area with { Y = Bounds.Y + border, Height = Math.Max(0, Bounds.Height - 2 * border) };
        _lineY = area.Y + (area.Height - _lineHeight) / 2;
        UpdateCaretGeometry();
    }

    private void UpdateCaretGeometry()
    {
        _caretX = MeasurePrefix(_caret);
        _anchorX = _anchor == _caret ? _caretX : MeasurePrefix(_anchor);
        _scrollX = Math.Clamp(_scrollX, Math.Max(0, _caretX - Math.Max(0, _viewport.Width - 2)), Math.Max(0, _caretX));
    }

    internal Rect TextViewport => _viewport;
    internal float LineY => _lineY;
    internal float LineHeight => _lineHeight;
    internal float CaretX => _caretX;
    internal float AnchorX => _anchorX;
    internal IRichTextLayout? ShapedLine => _shapedLine;

    /// <inheritdoc />
    public override bool HandleInput(UiInput input, IInputContext context)
    {
        if (!IsEffectivelyEnabled) return false;
        if (input.Kind == InputKind.PointerDown && input.Button == 0)
        {
            if (HasError && ValidationIconBounds.Contains(input.X, input.Y)) return true;
            context.Focus(this);
            var x = input.X - _viewport.X + _scrollX;
            var nearest = 0;
            var distance = float.PositiveInfinity;
            // Carets need not be monotonic for a shaped bidirectional line.
            foreach (var position in _boundaries)
            {
                var delta = Math.Abs(MeasurePrefix(position) - x);
                if (delta < distance) { nearest = position; distance = delta; }
            }
            if (Math.Abs(MeasurePrefix(Value.Length) - x) < distance) nearest = Value.Length;
            Move(nearest, input.Shift);
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
                case UiKey.A: _anchor = 0; Move(Value.Length, true); return true;
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
    /// <inheritdoc />
    public override void Dispose()
    {
        try { base.Dispose(); }
        finally
        {
            if (IsDisposed)
            {
                _text = null;
                ReleaseShapedLine();
            }
        }
    }
}
