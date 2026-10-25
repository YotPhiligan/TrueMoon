using System.ComponentModel;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor;

/// <summary>Borrowed same-window overlay host for a parent-owned status view.</summary>
public sealed record EditorStatusBarArguments(OverlayHost Overlays);

/// <summary>Compact retained status segments. Borrows presentation, style, factory and overlay host.</summary>
public sealed class EditorStatusBarView : View<EditorStatusBarViewModel>, IUiInputObserver, IUiFrameParticipant
{
    private readonly EditorStatusBarViewModel _model;
    private readonly OverlayHost _overlays;
    private readonly IFactory<EditorStatusPopupView, EditorStatusPopupArguments> _popups;
    private bool _restoreDetailsFocus;
    /// <summary>Primary status also opens the complete details.</summary>
    public Button SummaryButton { get; }
    /// <summary>Current-operation cancellation.</summary>
    public Button CancelButton { get; }
    /// <summary>Optional scene count.</summary>
    public Element Entities { get; }
    /// <summary>Preview status and popup trigger.</summary>
    public Button PreviewButton { get; }
    /// <summary>Always-reserved details action.</summary>
    public IconButton DetailsButton { get; }
    /// <summary>Shared adaptive UI layout.</summary>
    public StatusBar Bar { get; }
    /// <inheritdoc />
    public bool NeedsFrame => _restoreDetailsFocus || IsAttached && Bar.KeepBackdropVisible != KeepsBackdropVisible;
    private bool OwnsPopup => ReferenceEquals(_overlays.PopupOwner, SummaryButton) ||
        ReferenceEquals(_overlays.PopupOwner, PreviewButton) || ReferenceEquals(_overlays.PopupOwner, DetailsButton);
    private bool KeepsBackdropVisible => _model.HasError || _model.Kind is EditorStatusKind.Busy or EditorStatusKind.Error or EditorStatusKind.Waiting || OwnsPopup;
    /// <inheritdoc />
    public int InputPriority => 10;
    /// <inheritdoc />
    public void AdvanceFrame(TimeSpan elapsed, IInputContext context)
    {
        if (_restoreDetailsFocus)
        {
            _restoreDetailsFocus = false;
            if (ReferenceEquals(_overlays.PopupOwner, PreviewButton)) _overlays.Close(false);
            if (_overlays.Popup == null) context.Focus(DetailsButton);
        }
        Bar.KeepBackdropVisible = KeepsBackdropVisible;
    }
    /// <summary>Creates a parent-owned view on its UI owner thread.</summary>
    public EditorStatusBarView(EditorStatusBarViewModel model, IViewStyle<EditorStatusBarView> style,
        IFactory<EditorStatusPopupView, EditorStatusPopupArguments> popups, EditorStatusBarArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(model); ArgumentNullException.ThrowIfNull(style); ArgumentNullException.ThrowIfNull(popups);
        ArgumentNullException.ThrowIfNull(arguments); ArgumentNullException.ThrowIfNull(arguments.Overlays);
        _model = model; _overlays = arguments.Overlays; _popups = popups;
        try
        {
            SummaryButton = new SegmentButton(UiSymbol.Check, "Состояние редактора", (context, button) => Open(context, button, false))
                .SetValue(UiProperties.AccessibleName, "Состояние редактора · открыть сведения").BindText(model, x => x.Summary);
            CancelButton = new SegmentButton(UiSymbol.Close, "Отменить текущую операцию", (_, _) => { _ = Observe(model.CancelAsync()); })
                { Value = "Отмена", AccessibleName = "Отменить текущую операцию" }.Bind(UiProperties.Visible, model, x => x.CanCancel).Bind(UiProperties.Enabled, model, x => x.CanCancel);
            Entities = new SegmentLabel(UiSymbol.Objects).BindText(model, x => x.EntityText);
            PreviewButton = new SegmentButton(UiSymbol.Preview, "Управление Edit Preview", (context, button) => Open(context, button, true))
                .SetValue(UiProperties.AccessibleName, "Управление Edit Preview").BindText(model, x => x.PreviewSummary);
            DetailsButton = new DetailsAction((context, button) => Open(context, button, false));
            Bar = new(SummaryButton, CancelButton, [Entities, PreviewButton], DetailsButton);
            void OverflowChanged()
            {
                if (ReferenceEquals(_overlays.PopupOwner, PreviewButton) && Bar.OverflowedItems.Contains(PreviewButton))
                    _restoreDetailsFocus = true;
            }
            Bar.OverflowChanged += OverflowChanged; Own(new Release(() => Bar.OverflowChanged -= OverflowChanged));
            void Attachment() { if (!IsAttached) _restoreDetailsFocus = false; }
            AttachmentChanged += Attachment;
            Own(new Release(() => { AttachmentChanged -= Attachment; _restoreDetailsFocus = false; }));
            Content(model, _ => Bar); this.WithViewStyle(style);
            model.PropertyChanged += Changed; Own(new Release(() => model.PropertyChanged -= Changed));
            UpdateKind();
        }
        catch (Exception original)
        {
            try { Dispose(); } catch (Exception cleanup) { throw new AggregateException(original, cleanup); }
            throw;
        }
    }
    private void Changed(object? sender, PropertyChangedEventArgs args)
    { if (args.PropertyName is nameof(EditorStatusBarViewModel.Kind) or nameof(EditorStatusBarViewModel.HasError)) Dispatch(UpdateKind); }
    private void UpdateKind()
    {
        ((SegmentButton)SummaryButton).Symbol = _model.Kind switch
        {
            EditorStatusKind.Busy => UiSymbol.Busy, EditorStatusKind.Error or EditorStatusKind.Waiting => UiSymbol.Warning,
            EditorStatusKind.Paused => UiSymbol.Pause, EditorStatusKind.Running => UiSymbol.Play, _ => UiSymbol.Check
        };
        ((SegmentButton)SummaryButton).Kind = _model.Kind;
        Bar.KeepBackdropVisible = KeepsBackdropVisible;
    }
    private void Open(IInputContext context, Element trigger, bool preview)
    {
        if (ReferenceEquals(_overlays.PopupOwner, trigger)) { _overlays.Close(); Bar.KeepBackdropVisible = KeepsBackdropVisible; return; }
        var popup = _popups.Create(new(preview)) ?? throw new InvalidOperationException("Status popup factory returned null.");
        var anchor = new Rect(trigger.Bounds.X, Bounds.Y, trigger.Bounds.Width, Bounds.Height);
        try { _overlays.Open(trigger, popup, anchor, context, trigger); popup.FocusFirst(context); Bar.KeepBackdropVisible = KeepsBackdropVisible; }
        catch { if (popup.Parent == null && !popup.IsDisposed) popup.Dispose(); throw; }
    }
    /// <inheritdoc />
    public bool PreviewInput(UiInput input, IInputContext context)
    {
        if (!OwnsPopup || _overlays.PopupOwner is not { } owner) return false;
        if (input.Kind == InputKind.KeyDown && input.Key == UiKey.Tab) _overlays.Close();
        else if (input.Kind == InputKind.PointerDown && Bounds.Contains(input.X, input.Y) && !owner.Bounds.Contains(input.X, input.Y))
            _overlays.Close(false);
        return false;
    }
    private static async Task Observe(Task pending) { try { await pending.ConfigureAwait(false); } catch { /* Diagnostics stay with the command/preview owner. */ } }
    private sealed class Release(Action release) : IDisposable { public void Dispose() => release(); }
    private sealed class DetailsAction : IconButton
    {
        private IInputContext? _context;
        private readonly Action<IInputContext, Element> _action;
        public DetailsAction(Action<IInputContext, Element> action) : base(UiSymbol.Info, "Сведения о редакторе")
        { _action = action; Clear(UiProperties.Width); Clear(UiProperties.Height); }
        protected override float ThemeBorderWidth => 0;
        protected override Color ThemeBackground => IsPressed ? Theme.ControlPressed : IsHovered ? Theme.ControlHover : Color.Transparent;
        protected override void DrawCore(IDrawingContext context) => UiSymbols.Draw(context, Symbol,
            new(Bounds.X + (Bounds.Width - 14) / 2, Bounds.Y + (Bounds.Height - 14) / 2, 14, 14), Foreground);
        public override bool HandleInput(UiInput input, IInputContext context)
        { _context = context; try { return base.HandleInput(input, context); } finally { _context = null; } }
        protected override void Activate() => _action(_context!, this);
    }
    private sealed class SegmentButton : Button
    {
        private IInputContext? _context;
        private UiSymbol _symbol;
        private readonly Action<IInputContext, Element> _action;
        public SegmentButton(UiSymbol symbol, string name, Action<IInputContext, Element> action)
        { _symbol = symbol; AccessibleName = name; _action = action; }
        private EditorStatusKind _kind;
        public EditorStatusKind Kind { set { if (_kind != value) { VerifyAccess(); _kind = value; Invalidate(); } } }
        public UiSymbol Symbol { get => _symbol; set { if (_symbol != value) { VerifyAccess(); _symbol = value; Invalidate(); } } }
        protected override Color ThemeForeground => EditorStatusBarViewStyle.StatusColor(Theme, _kind);
        protected override float ThemeBorderWidth => 0;
        protected override Thickness ThemePadding => new(4, 2, 4, 2);
        protected override Color ThemeBackground => Color.Transparent;
        protected override Size MeasureCore(Size available, ITextLayoutService text)
        { _text = text; var size = base.MeasureCore(available, text); return new(size.Width + 20, Math.Max(14, size.Height)); }
        private ITextLayoutService? _text;
        protected override void DrawCore(IDrawingContext context)
        {
            if (IsHovered || IsPressed)
            {
                var width = Math.Min(Bounds.Width, (_text?.Measure(Value, FontSize, FontFamily).Width ?? 0) + 20 + Padding.Left + Padding.Right);
                context.Fill(new(Bounds.X, Bounds.Y, width, Bounds.Height), IsPressed ? Theme.ControlPressed : Theme.ControlHover);
            }
            DrawSegment(context, this, Symbol, Value, _text);
        }
        public override bool HandleInput(UiInput input, IInputContext context)
        { _context = context; try { return base.HandleInput(input, context); } finally { _context = null; } }
        protected override void Activate() => _action(_context!, this);
    }
    private sealed class SegmentLabel(UiSymbol symbol) : Text
    {
        private ITextLayoutService? _text;
        protected override Size MeasureCore(Size available, ITextLayoutService text)
        { _text = text; var size = base.MeasureCore(available, text); return new(size.Width + 20, Math.Max(14, size.Height)); }
        protected override void DrawCore(IDrawingContext context) => DrawSegment(context, this, symbol, Value, _text);
    }
    private static void DrawSegment(IDrawingContext context, Element element, UiSymbol symbol, string text, ITextLayoutService? layout)
    {
        var area = element.Bounds.Deflate(element.Padding); var color = element.IsEffectivelyEnabled ? element.Foreground : element.Theme.Disabled;
        UiSymbols.Draw(context, symbol, new(area.X, area.Y + (area.Height - 14) / 2, 14, 14), color);
        var width = Math.Max(0, area.Width - 20); var display = text;
        if (layout != null && layout.Measure(display, element.FontSize, element.FontFamily).Width > width)
        {
            var count = display.Length;
            while (count > 0 && layout.Measure(display[..count] + "…", element.FontSize, element.FontFamily).Width > width) count--;
            if (count > 0 && char.IsHighSurrogate(display[count - 1])) count--;
            display = count == 0 ? "" : display[..count] + "…";
        }
        context.Text(display, area.X + 20, area.Y + (area.Height - element.FontSize) / 2, color, element.FontSize, element.FontFamily);
    }
}
