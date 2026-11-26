using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Menus;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Views;
using System.ComponentModel;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.UI;

/// <summary>Borrowed same-window overlay host for a parent-owned status view.</summary>
public sealed record EditorStatusBarArguments(OverlayHost Overlays);

/// <summary>Compact retained status segments. Borrows presentation, style, factory and overlay host.</summary>
public sealed class EditorStatusBarView : View<IEditorStatusBarViewModel>, IUiInputObserver, IUiFrameParticipant
{
    private static readonly IFactory<Button, (UiSymbol Symbol, string Name)> Segments =
        new Factory<Button, (UiSymbol Symbol, string Name)>(static args => new SegmentButton(args.Symbol, args.Name));
    private static readonly IFactory<Text, UiSymbol> Labels = new Factory<Text, UiSymbol>(static symbol => new SegmentLabel(symbol));
    private static readonly IFactory<IconButton> Details = new Factory<IconButton>(static () => new DetailsAction());
    private static readonly IFactory<StatusBar, BarParts> Layout = new Factory<StatusBar, BarParts>(static parts =>
        new StatusBar(parts.Primary, parts.Action, parts.Secondary, parts.Details));
    private readonly record struct BarParts(Element Primary, Element Action, Element[] Secondary, Element Details);

    private readonly IEditorStatusBarViewModel _model;
    private readonly OverlayHost _overlays;
    private readonly IFactory<EditorStatusPopupView, EditorStatusPopupArguments> _popups;
    private (Element Popup, Element Trigger)? _pendingOverflow;
    /// <inheritdoc />
    public bool NeedsFrame => IsAttached && (_pendingOverflow != null || this.Bar.KeepBackdropVisible != KeepsBackdropVisible);
    private bool OwnsPopup => _overlays.PopupOwner is { } owner &&
        (ReferenceEquals(owner, this.SummaryButton) || ReferenceEquals(owner, this.PreviewButton) ||
         ReferenceEquals(owner, this.SourceButton) || ReferenceEquals(owner, this.DetailsButton));
    private bool KeepsBackdropVisible => _model.HasError || _model.Kind is EditorStatusKind.Busy or EditorStatusKind.Error or EditorStatusKind.Waiting ||
        _model.HasSource && _model.SourceKind is EditorStatusKind.Busy or EditorStatusKind.Waiting || OwnsPopup;
    /// <inheritdoc />
    public int InputPriority => 10;
    /// <inheritdoc />
    public void AdvanceFrame(TimeSpan elapsed, IInputContext context)
    {
        var pending = _pendingOverflow;
        _pendingOverflow = null;
        if (pending is { } overflow && this.Bar.OverflowedItems.Contains(overflow.Trigger))
        {
            if (ReferenceEquals(_overlays.Popup, overflow.Popup) && ReferenceEquals(_overlays.PopupOwner, overflow.Trigger))
                _overlays.Close(false);
            // The root overlay may already have dismissed the hidden trigger this frame.
            if (overflow.Popup.IsDisposed && _overlays.PopupOwner == null && context.FocusedElement == null)
                context.TryFocus(this.DetailsButton);
        }
        this.Bar.KeepBackdropVisible = KeepsBackdropVisible;
    }
    /// <summary>Creates a parent-owned view on its UI owner thread.</summary>
    public EditorStatusBarView(IEditorStatusBarViewModel model, IViewStyle<EditorStatusBarView> style,
        IFactory<EditorStatusPopupView, EditorStatusPopupArguments> popups, EditorStatusBarArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(model); ArgumentNullException.ThrowIfNull(style); ArgumentNullException.ThrowIfNull(popups);
        ArgumentNullException.ThrowIfNull(arguments); ArgumentNullException.ThrowIfNull(arguments.Overlays);
        _model = model; _overlays = arguments.Overlays; _popups = popups;
        Content(model, ui =>
        {
            var summary = ui.Create(Segments, (UiSymbol.Check, "Состояние редактора · открыть сведения"))
                .Named("SummaryButton").Bind(Text.ValueProperty, model, x => x.Summary)
                .Bind(SegmentButton.KindProperty, model, x => x.Kind)
                .OnActivated((button, input) => Open(input, button, preview: false));
            var cancel = ui.Create(Segments, (UiSymbol.Close, "Отменить текущую операцию"))
                .Named("CancelButton").Value("Отмена")
                .Bind(UiProperties.Visible, model, x => x.CanCancel).Bind(UiProperties.Enabled, model, x => x.CanCancel)
                .OnClick(() => { _ = Observe(model.CancelAsync()); });
            var entities = ui.Create(Labels, UiSymbol.Objects).Named("Entities").Bind(Text.ValueProperty, model, x => x.EntityText);
            var preview = ui.Create(Segments, (UiSymbol.Preview, "Управление Edit Preview"))
                .Named("PreviewButton").Bind(Text.ValueProperty, model, x => x.PreviewSummary)
                .OnActivated((button, input) => Open(input, button, preview: true));
            var source = ui.Create(Segments, (UiSymbol.Build, "Состояние исходников"))
                .Named("SourceButton").Bind(Text.ValueProperty, model, x => x.SourceSummary)
                .Bind(UiProperties.Visible, model, x => x.HasSource)
                .Bind(SegmentButton.KindProperty, model, x => x.SourceKind)
                .OnActivated((button, input) => Open(input, button, preview: false));
            var details = ui.Create(Details).Named("DetailsButton").OnActivated((button, input) => Open(input, button, preview: false));
            return ui.Create(Layout, new BarParts(summary, cancel, [entities, preview, source], details)).Named("Bar");
        });
        this.WithViewStyle(style);
    }

    /// <inheritdoc />
    protected override void OnContentPrepared()
    {
        var bar = this.Bar;
        var preview = this.PreviewButton;
        var source = this.SourceButton;
        void OverflowChanged()
        {
            if (_overlays.Popup is { } popup && _overlays.PopupOwner is { } owner &&
                (ReferenceEquals(owner, preview) || ReferenceEquals(owner, source)) && bar.OverflowedItems.Contains(owner))
                _pendingOverflow = (popup, owner);
        }
        void Attachment() { if (!IsAttached) _pendingOverflow = null; }
        bar.OverflowChanged += OverflowChanged;
        AttachmentChanged += Attachment;
        Own(new Release(() => { bar.OverflowChanged -= OverflowChanged; AttachmentChanged -= Attachment; }));
        this.ObservePropertyChanged(_model, Changed, UpdateBackdrop);
    }
    private void Changed(object? sender, PropertyChangedEventArgs args)
    {
        if (string.IsNullOrEmpty(args.PropertyName) || args.PropertyName is nameof(IEditorStatusBarViewModel.Kind) or
            nameof(IEditorStatusBarViewModel.HasError) or nameof(IEditorStatusBarViewModel.SourceKind) or nameof(IEditorStatusBarViewModel.HasSource))
            UpdateBackdrop();
    }
    private void UpdateBackdrop() => this.Bar.KeepBackdropVisible = KeepsBackdropVisible;
    private void Open(IInputContext context, Element trigger, bool preview)
    {
        if (ReferenceEquals(_overlays.PopupOwner, trigger)) { _overlays.Close(); this.Bar.KeepBackdropVisible = KeepsBackdropVisible; return; }
        var popup = _popups.Create(new(preview)) ?? throw new InvalidOperationException("Status popup factory returned null.");
        var anchor = new Rect(trigger.Bounds.X, Bounds.Y, trigger.Bounds.Width, Bounds.Height);
        try { _overlays.Open(trigger, popup, anchor, context, trigger); popup.FocusFirst(context); this.Bar.KeepBackdropVisible = KeepsBackdropVisible; }
        catch (Exception original)
        {
            if (popup.Parent == null && !popup.IsDisposed)
                try { popup.Dispose(); }
                catch (Exception cleanup) { throw new AggregateException(original, cleanup); }
            throw;
        }
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
    internal sealed class DetailsAction : IconButton
    {
        public DetailsAction() : base(UiSymbol.Info, "Сведения о редакторе")
        { Clear(UiProperties.Width); Clear(UiProperties.Height); }
        protected override float ThemeBorderWidth => 0;
        protected override Color ThemeBackground => IsPressed ? Theme.ControlPressed : IsHovered ? Theme.ControlHover : Color.Transparent;
    }
    internal sealed class SegmentButton : Button
    {
        internal static readonly UiProperty<EditorStatusKind> KindProperty = new("Kind", EditorStatusKind.Ready);
        private readonly UiSymbol _symbol;
        public SegmentButton(UiSymbol symbol, string name) { _symbol = symbol; AccessibleName = name; }
        public UiSymbol Symbol => (_symbol, Get(KindProperty)) switch
        {
            (UiSymbol.Check or UiSymbol.Build, EditorStatusKind.Busy) => UiSymbol.Busy,
            (UiSymbol.Check, EditorStatusKind.Error or EditorStatusKind.Waiting) or
                (UiSymbol.Build, EditorStatusKind.Waiting) => UiSymbol.Warning,
            (UiSymbol.Check, EditorStatusKind.Paused) => UiSymbol.Pause,
            (UiSymbol.Check, EditorStatusKind.Running) => UiSymbol.Play,
            _ => _symbol
        };
        protected override Color ThemeForeground => EditorStatusBarViewStyle.StatusColor(Theme, Get(KindProperty));
        protected override float ThemeBorderWidth => 0;
        protected override Thickness ThemePadding => new(4, 2, 4, 2);
        protected override Color ThemeBackground => Color.Transparent;
        protected override Size MeasureCore(Size available, ITextLayoutService text)
        {
            _text = text;
            var size = base.MeasureCore(available, text);
            return new(size.Width + 20, Math.Max(14, size.Height));
        }
        private ITextLayoutService? _text;
        internal float LabelWidth => LabelSize.Width;
        internal string DisplayText { get; private set; } = "";
        protected override void ArrangeCore(Rect area) => DisplayText = FitSingleLine(_text, area.Width - 20);
    }
    internal sealed class SegmentLabel(UiSymbol symbol) : Text
    {
        private ITextLayoutService? _text;
        protected override Size MeasureCore(Size available, ITextLayoutService text)
        { _text = text; var size = base.MeasureCore(available, text); return new(size.Width + 20, Math.Max(14, size.Height)); }
        internal UiSymbol Symbol { get; } = symbol;
        internal string DisplayText { get; private set; } = "";
        protected override void ArrangeCore(Rect area) => DisplayText = FitSingleLine(_text, area.Width - 20);
    }
}
