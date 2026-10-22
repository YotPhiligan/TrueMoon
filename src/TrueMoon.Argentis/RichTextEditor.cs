using System.Globalization;

namespace TrueMoon.Argentis;

/// <summary>A retained multiline rich editor. Documents/options are immutable snapshots; indices are UTF-16 grapheme boundaries.</summary>
public sealed class RichTextEditor : Element
{
    /// <summary>Bindable rich content. Replacing it externally clears undo/redo history.</summary>
    public static readonly UiProperty<RichTextDocument> DocumentProperty = new("Document", RichTextDocument.Empty, Invalidation.Layout, value => value != null);
    /// <summary>Bindable plain text. External assignment replaces formatting and normalizes line endings.</summary>
    public static readonly UiProperty<string> TextProperty = new("Text", "", Invalidation.Layout,
        value => value != null && !value.Contains('\r') && RichTextRun.IsValidUtf16(value));
    /// <summary>Bindable immutable editing options.</summary>
    public static readonly UiProperty<RichTextEditorOptions> OptionsProperty = new("Options", new(), Invalidation.Layout,
        value => value != null && Enum.IsDefined(value.Alignment));

    private readonly List<Snapshot> _undo = [], _redo = [];
    private int _caret, _anchor;
    private bool _syncing, _editing, _dragging, _revealCaret;
    private RichTextStyle? _typingStyle, _defaults;
    private ITextLayoutService? _service;
    private IRichTextLayout? _layout;
    private float? _layoutWidth, _preferredX;
    private float _scrollX, _scrollY;
    private sealed record Snapshot(RichTextDocument Document, int Caret, int Anchor);

    /// <summary>Creates a rich editor with optional immutable configuration.</summary>
    /// <param name="options">Editing options; omitted uses the defaults.</param>
    public RichTextEditor(RichTextEditorOptions? options = null)
    {
        MinWidth = 160; MinHeight = 80;
        if (options != null) Options = options;
        Own(new LayoutLifetime(this));
        AttachmentChanged += () => { if (!IsAttached) { ReleaseLayout(); _service = null; } };
    }
    /// <summary>Creates an independent rich editor for Fluent configuration.</summary>
    /// <param name="options">Optional configuration.</param><returns>A new editor.</returns>
    public static RichTextEditor Create(RichTextEditorOptions? options = null) => new(options);
    /// <summary>Immutable rich content.</summary>
    public RichTextDocument Document { get => Get(DocumentProperty); set => Set(DocumentProperty, value); }
    /// <summary>Plain text with normalized LF line endings.</summary>
    public string Text { get => Get(TextProperty); set { ArgumentNullException.ThrowIfNull(value); Set(TextProperty, value.ReplaceLineEndings("\n")); } }
    /// <summary>Immutable configuration. Use with-expressions to change individual options.</summary>
    public RichTextEditorOptions Options { get => Get(OptionsProperty); set => Set(OptionsProperty, value); }
    /// <summary>Current UTF-16 caret position.</summary>
    public int CaretIndex => _caret;
    /// <summary>Selected UTF-16 range start.</summary>
    public int SelectionStart => Math.Min(_caret, _anchor);
    /// <summary>Selected UTF-16 range length.</summary>
    public int SelectionLength => Math.Abs(_caret - _anchor);
    /// <summary>Selected plain text, suitable for clipboard export.</summary>
    public string SelectedText => Document.Text.Substring(SelectionStart, SelectionLength);
    /// <summary>Whether an editable undo operation is available.</summary>
    public bool CanUndo => Editable && _undo.Count != 0;
    /// <summary>Whether an editable redo operation is available.</summary>
    public bool CanRedo => Editable && _redo.Count != 0;
    /// <summary>Current horizontal logical scroll offset.</summary>
    public float HorizontalOffset => _scrollX;
    /// <summary>Current vertical logical scroll offset.</summary>
    public float VerticalOffset => _scrollY;
    /// <summary>Formatting at the caret, including pending insertion formatting.</summary>
    public RichTextStyle SelectionStyle => _typingStyle ?? Document.StyleAt(Math.Max(0, _caret - 1));
    /// <summary>Raised when document, selection or undo availability changes; handlers run on the owner thread.</summary>
    public event Action? StateChanged;
    /// <inheritdoc />
    public override bool Focusable => true;
    /// <inheritdoc />
    protected override Thickness ThemePadding => Theme.EditorPadding;
    /// <inheritdoc />
    protected override Color ThemeBackground => !IsEffectivelyEnabled ? Theme.ControlDisabled : Options.IsReadOnly ? Theme.ReadOnly : Theme.Input;
    /// <inheritdoc />
    protected override float ThemeBorderWidth => Theme.BorderWidth;

    /// <inheritdoc />
    protected override void OnPropertyChanged(object property)
    {
        base.OnPropertyChanged(property);
        if (_syncing)
        {
            if (ReferenceEquals(property, DocumentProperty)) { _caret = Clamp(_caret); _anchor = Clamp(_anchor); }
            return;
        }
        if (ReferenceEquals(property, TextProperty))
        {
            _syncing = true;
            try
            {
                var document = RichTextDocument.FromText(Text);
                ResetExternalState(document.Text);
                Document = document;
                _caret = Clamp(_caret); _anchor = Clamp(_anchor);
                if (Text != document.Text) Text = document.Text;
            }
            finally { _syncing = false; ReleaseLayout(); }
        }
        else if (ReferenceEquals(property, DocumentProperty))
        {
            if (!_editing) ResetExternalState(Document.Text);
            _caret = Clamp(_caret); _anchor = Clamp(_anchor);
            _syncing = true;
            try { Text = Document.Text; }
            finally { _syncing = false; ReleaseLayout(); }
        }
        else if (ReferenceEquals(property, OptionsProperty))
        {
            Trim(_undo); Trim(_redo); ReleaseLayout();
        }
        else { ReleaseLayout(); return; }
        StateChanged?.Invoke();
    }
    private void ResetExternalState(string text)
    {
        _undo.Clear(); _redo.Clear(); _typingStyle = null; _preferredX = null; _revealCaret = true;
        _caret = Math.Min(_caret, text.Length); _anchor = Math.Min(_anchor, text.Length);
    }
    private int Clamp(int index)
    {
        var text = Document.Text;
        if (index >= text.Length) return text.Length;
        return StringInfo.ParseCombiningCharacters(text).LastOrDefault(position => position <= Math.Max(0, index));
    }
    private int Previous(int index) => StringInfo.ParseCombiningCharacters(Document.Text).LastOrDefault(position => position < index);
    private int Next(int index) => StringInfo.ParseCombiningCharacters(Document.Text).FirstOrDefault(position => position > index, Document.Text.Length);
    private void Trim(List<Snapshot> history)
    { if (history.Count > Options.UndoLimit) history.RemoveRange(0, history.Count - Options.UndoLimit); }
    private Snapshot Capture() => new(Document, _caret, _anchor);
    private void ApplySnapshot(Snapshot snapshot)
    {
        _editing = true; _caret = snapshot.Caret; _anchor = snapshot.Anchor;
        _typingStyle = null; _preferredX = null; _revealCaret = true;
        try { Document = snapshot.Document; }
        finally { _editing = false; RevealCaret(); Invalidate(Invalidation.Layout); }
    }
    private void Commit(RichTextDocument document, int caret, int anchor)
    {
        if (Options.UndoLimit > 0) { _undo.Add(Capture()); Trim(_undo); }
        _redo.Clear();
        ApplySnapshot(new(document, caret, anchor));
    }
    private bool Editable => IsEffectivelyEnabled && !Options.IsReadOnly;

    /// <summary>Selects a UTF-16 range. Boundaries inside a grapheme are rounded down.</summary>
    /// <param name="start">Range start.</param><param name="length">Range length.</param>
    public void Select(int start, int length)
    {
        VerifyAccess();
        if (start < 0 || length < 0 || start > Document.Text.Length - length) throw new ArgumentOutOfRangeException(nameof(start));
        _anchor = Clamp(start); _caret = Clamp(start + length); _typingStyle = null; _preferredX = null; _revealCaret = true;
        RevealCaret(); Invalidate(); StateChanged?.Invoke();
    }
    /// <summary>Selects the whole document.</summary>
    public void SelectAll() => Select(0, Document.Text.Length);
    /// <summary>Replaces selection with normalized plain text using the current insertion style.</summary>
    /// <param name="text">Text to insert; CR/CRLF become LF.</param><returns>Whether the document changed.</returns>
    public bool InsertText(string text)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(text);
        if (!Editable) return false;
        text = text.ReplaceLineEndings("\n");
        // Validate before modifying history or selection.
        _ = new RichTextRun(text);
        if (text.Length == 0 && SelectionLength == 0) return false;
        var start = SelectionStart; var style = SelectionStyle;
        Commit(Document.Replace(start, SelectionLength, text, style), start + text.Length, start + text.Length);
        _typingStyle = style;
        return true;
    }
    /// <summary>Applies a formatting patch to selection, or to subsequent typing at a collapsed caret.</summary>
    /// <param name="style">Non-null members change formatting; other members are preserved.</param><returns>Whether the command was accepted.</returns>
    public bool ApplyStyle(RichTextStyle style)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(style);
        if (!Editable) return false;
        if (SelectionLength == 0) { _typingStyle = SelectionStyle.Merge(style); Invalidate(); StateChanged?.Invoke(); }
        else Commit(Document.Format(SelectionStart, SelectionLength, style), _caret, _anchor);
        return true;
    }
    /// <summary>Restores document, formatting and selection from the last edit.</summary>
    /// <returns>Whether an operation was undone.</returns>
    public bool Undo()
    {
        VerifyAccess(); if (!Editable || _undo.Count == 0) return false;
        var previous = _undo[^1]; _undo.RemoveAt(_undo.Count - 1); _redo.Add(Capture()); Trim(_redo);
        ApplySnapshot(previous); return true;
    }
    /// <summary>Reapplies the last undone edit.</summary>
    /// <returns>Whether an operation was redone.</returns>
    public bool Redo()
    {
        VerifyAccess(); if (!Editable || _redo.Count == 0) return false;
        var next = _redo[^1]; _redo.RemoveAt(_redo.Count - 1); _undo.Add(Capture()); Trim(_undo);
        ApplySnapshot(next); return true;
    }
    /// <summary>Scrolls to a logical offset, clamped to the currently measured document extent.</summary>
    /// <param name="horizontal">Nonnegative horizontal offset.</param><param name="vertical">Nonnegative vertical offset.</param>
    public void ScrollTo(float horizontal, float vertical)
    {
        VerifyAccess();
        if (!float.IsFinite(horizontal) || !float.IsFinite(vertical) || horizontal < 0 || vertical < 0)
            throw new ArgumentOutOfRangeException(nameof(horizontal));
        _scrollX = horizontal; _scrollY = vertical; _revealCaret = false; ClampScroll(); Invalidate();
    }
    private RichTextStyle Defaults => new() { FontFamily = FontFamily, FontSize = FontSize, Foreground = IsEffectivelyEnabled ? Foreground : Theme.Disabled };
    private void ReleaseLayout()
    { _layout?.Dispose(); _layout = null; _defaults = null; }
    private void EnsureLayout(float width, ITextLayoutService service)
    {
        var defaults = Defaults;
        float? wrap = Options.WordWrap ? Math.Max(1, float.IsFinite(width) ? width : 320) : null;
        if (_layout != null && ReferenceEquals(service, _service) && defaults == _defaults && wrap == _layoutWidth) return;
        ReleaseLayout();
        if (service is not IRichTextLayoutService rich) throw new NotSupportedException("RichTextEditor requires an IRichTextLayoutService backend.");
        _service = service; _defaults = defaults; _layoutWidth = wrap;
        _layout = rich.CreateRichTextLayout(Document, defaults, wrap, Options.Alignment);
        _revealCaret = true;
    }
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text)
    {
        EnsureLayout(available.Width, text);
        return new(Math.Min(float.IsFinite(available.Width) ? available.Width : 320, Math.Max(160, _layout!.Size.Width)),
            Math.Min(float.IsFinite(available.Height) ? available.Height : 160, Math.Max(80, _layout.Size.Height)));
    }
    /// <inheritdoc />
    protected override void ArrangeCore(Rect contentBounds)
    {
        if (_service != null) EnsureLayout(contentBounds.Width, _service);
        ClampScroll();
    }
    private void ClampScroll()
    {
        var area = Bounds.Deflate(Padding);
        _scrollX = Options.WordWrap ? 0 : Math.Clamp(_scrollX, 0, Math.Max(0, (_layout?.Size.Width ?? 0) - area.Width + 2));
        _scrollY = Math.Clamp(_scrollY, 0, Math.Max(0, (_layout?.Size.Height ?? 0) - area.Height));
    }
    private void RevealCaret()
    {
        if (_layout == null) return;
        var area = Bounds.Deflate(Padding); var caret = _layout.GetCaretBounds(_caret);
        if (caret.X < _scrollX) _scrollX = caret.X;
        else if (caret.X + caret.Width > _scrollX + area.Width) _scrollX = caret.X + caret.Width - area.Width;
        if (caret.Y < _scrollY) _scrollY = caret.Y;
        else if (caret.Y + caret.Height > _scrollY + area.Height) _scrollY = caret.Y + caret.Height - area.Height;
        ClampScroll();
    }
    /// <inheritdoc />
    protected override void DrawCore(IDrawingContext context)
    {
        if (_service == null) return;
        EnsureLayout(Bounds.Deflate(Padding).Width, _service);
        if (_revealCaret) { RevealCaret(); _revealCaret = false; }
        if (context is not IRichTextDrawingContext rich) throw new NotSupportedException("RichTextEditor requires an IRichTextDrawingContext backend.");
        if (Options.IsReadOnly) context.Fill(new Rect(Bounds.X + Bounds.Width - 4, Bounds.Y + 4, 2, Math.Max(0, Bounds.Height - 8)), Theme.Border);
        var area = Bounds.Deflate(Padding);
        context.Save();
        try
        {
            context.Clip(area);
            var selected = IsFocused || Options.ShowSelectionWhenUnfocused;
            rich.RichText(_layout!, area.X - _scrollX, area.Y - _scrollY, SelectionStart, selected ? SelectionLength : 0,
                Options.SelectionColor ?? (Theme.Accent with { A = 100 }));
            if (IsFocused && IsEffectivelyEnabled)
            {
                var caret = _layout!.GetCaretBounds(_caret);
                context.Fill(new(area.X + caret.X - _scrollX, area.Y + caret.Y - _scrollY, 1, caret.Height), Foreground);
            }
        }
        finally { context.Restore(); }
    }
    private void Move(int position, bool extend, bool vertical = false)
    {
        _caret = Clamp(position); if (!extend) _anchor = _caret;
        _typingStyle = null; _revealCaret = true; if (!vertical) _preferredX = null;
        RevealCaret(); Invalidate(); StateChanged?.Invoke();
    }
    private int Word(int direction)
    {
        var text = Document.Text; var index = _caret;
        if (direction < 0)
        {
            while (index > 0 && char.IsWhiteSpace(text, Previous(index))) index = Previous(index);
            while (index > 0 && !char.IsWhiteSpace(text, Previous(index))) index = Previous(index);
        }
        else
        {
            while (index < text.Length && !char.IsWhiteSpace(text, index)) index = Next(index);
            while (index < text.Length && char.IsWhiteSpace(text, index)) index = Next(index);
        }
        return index;
    }
    /// <inheritdoc />
    public override bool HandleInput(UiInput input, IInputContext context)
    {
        VerifyAccess();
        if (!IsEffectivelyEnabled) return false;
        if (input is { Kind: InputKind.PointerDown, Button: 0 })
        {
            context.Focus(this); context.Capture(this); _dragging = true;
            try { Move(Hit(input), input.Shift); }
            catch { _dragging = false; context.ReleaseCapture(this); throw; }
            return true;
        }
        if (_dragging && input.Kind is InputKind.PointerMove or InputKind.PointerUp)
        {
            try { Move(Hit(input), true); }
            finally { if (input.Kind == InputKind.PointerUp) { _dragging = false; context.ReleaseCapture(this); } }
            return true;
        }
        if (input.Kind == InputKind.Wheel)
        {
            ScrollTo(_scrollX, Math.Max(0, _scrollY - input.WheelDelta * FontSize * 3)); return true;
        }
        if (input.Kind == InputKind.Text && !string.IsNullOrEmpty(input.Text))
        {
            var text = string.Concat(input.Text.Where(c => !char.IsControl(c)));
            return text.Length != 0 && InsertText(text);
        }
        if (input.Kind != InputKind.KeyDown) return false;
        if (input.Control)
        {
            switch (input.Key)
            {
                case UiKey.A: SelectAll(); return true;
                case UiKey.C: if (SelectionLength > 0) context.SetClipboardText(SelectedText); return true;
                case UiKey.X:
                    if (Editable && SelectionLength > 0) { context.SetClipboardText(SelectedText); InsertText(""); }
                    return true;
                case UiKey.V: if (Editable && context.GetClipboardText() is { } text) InsertText(text); return true;
                case UiKey.Z: if (input.Shift) Redo(); else Undo(); return true;
                case UiKey.Y: Redo(); return true;
                case UiKey.B: ApplyStyle(new() { Bold = !(SelectionStyle.Bold ?? false) }); return true;
                case UiKey.I: ApplyStyle(new() { Italic = !(SelectionStyle.Italic ?? false) }); return true;
                case UiKey.U: ApplyStyle(new() { Underline = !(SelectionStyle.Underline ?? false) }); return true;
            }
        }
        switch (input.Key)
        {
            case UiKey.Left: Move(!input.Shift && SelectionLength > 0 ? SelectionStart : input.Control ? Word(-1) : Previous(_caret), input.Shift); return true;
            case UiKey.Right: Move(!input.Shift && SelectionLength > 0 ? SelectionStart + SelectionLength : input.Control ? Word(1) : Next(_caret), input.Shift); return true;
            case UiKey.Home: Move(input.Control ? 0 : _layout?.GetLineBoundary(_caret, false) ?? 0, input.Shift); return true;
            case UiKey.End: Move(input.Control ? Document.Text.Length : _layout?.GetLineBoundary(_caret, true) ?? Document.Text.Length, input.Shift); return true;
            case UiKey.Up:
            case UiKey.Down:
                if (_layout == null) return false;
                _preferredX ??= _layout.GetCaretBounds(_caret).X;
                Move(_layout.MoveVertical(_caret, input.Key == UiKey.Up ? -1 : 1, _preferredX.Value), input.Shift, true); return true;
            case UiKey.Backspace:
            case UiKey.Delete:
                if (!Editable) return true;
                if (SelectionLength == 0) _anchor = input.Key == UiKey.Backspace ? Previous(_caret) : Next(_caret);
                InsertText(""); return true;
            case UiKey.Enter: if (!Options.AcceptsReturn) return false; InsertText("\n"); return true;
            case UiKey.Tab: if (!Options.AcceptsTab || input.Shift || !Editable) return false; InsertText("    "); return true;
            default: return false;
        }
    }
    private int Hit(UiInput input) => _layout?.HitTest(input.X - Bounds.X - Padding.Left + _scrollX, input.Y - Bounds.Y - Padding.Top + _scrollY) ?? 0;
    /// <inheritdoc />
    public override void OnInputCancelled() { _dragging = false; _preferredX = null; }
    /// <inheritdoc />
    public override void Dispose()
    {
        try { base.Dispose(); }
        finally { if (IsDisposed) { StateChanged = null; _undo.Clear(); _redo.Clear(); _service = null; } }
    }
    private sealed class LayoutLifetime(RichTextEditor owner) : IDisposable
    { public void Dispose() => owner.ReleaseLayout(); }
}
