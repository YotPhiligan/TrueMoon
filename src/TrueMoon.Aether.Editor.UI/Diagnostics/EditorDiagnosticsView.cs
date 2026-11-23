using System.ComponentModel;
using System.Linq.Expressions;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Diagnostics;

namespace TrueMoon.Aether.Editor.UI.Diagnostics;

/// <summary>Parent-owned diagnostic console over the current borrowed App model.</summary>
public sealed class EditorDiagnosticsView : View<IEditorDiagnosticsViewModel>, IUiFrameParticipant
{
    public static ViewStyleRole<TextBox> FilterRole { get; } = new("DiagnosticFilter");
    public static ViewStyleRole<HStack> RowRole { get; } = new("DiagnosticRow");
    public static ViewStyleRole<Button> CommandRole { get; } = new("DiagnosticCommand");
    public static ViewStyleRole<Text> LabelRole { get; } = new("DiagnosticLabel");
    public static ViewStyleRole<CheckBox> ExportChoiceRole { get; } = new("DiagnosticExportChoice");

    private readonly DelegateUiCommand _clear;
    private readonly DelegateUiCommand _refreshDetails;
    private bool _synchronizing, _followPending, _followReady;
    private EditorDiagnosticMode _appliedMode;
    private bool _presentationPending = true;
    private UiSession? _presentationSession;

    /// <summary>Gets the Clear View button through its retained part.</summary>
    public Button Clear => this.ClearButton;

    /// <summary>Creates a prepared console for callers that construct it directly.</summary>
    public EditorDiagnosticsView(IEditorDiagnosticsViewModel model, IViewStyle<EditorDiagnosticsView> style)
        : this(model)
    {
        ArgumentNullException.ThrowIfNull(style);
        ViewCreation.Prepare(this, style);
    }

    /// <summary>Prepares the console and applies its style before the factory publishes it.</summary>
    public static EditorDiagnosticsView Create(IEditorDiagnosticsViewModel model, IViewStyle<EditorDiagnosticsView> style)
    {
        ArgumentNullException.ThrowIfNull(style);
        return ViewCreation.Prepare(new EditorDiagnosticsView(model), style);
    }

    private EditorDiagnosticsView(IEditorDiagnosticsViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        DataContext = model;
        _clear = new(() => DataContext?.Clear(), () => DataContext?.CanClear == true);
        _refreshDetails = new(() => DataContext?.RefreshDetails(), () => DataContext?.CanRefreshDetails == true);
    }

    protected override Element BuildContent() => new ConsoleLayout(
        [
            Row(
                Text.Plain("ДИАГНОСТИКА"),
                Button.Simple().Named("Mode").OnClick(AdvanceMode),
                Button.Simple().Named("Level").Bind(UiProperties.Enabled, this, data => data.CanClear).OnClick(AdvanceLevel),
                Button.Simple().Named("Follow").OnClick(() =>
                {
                    if (DataContext is { } data) data.FollowTail = !data.FollowTail;
                }),
                Button.Simple().Named("Group").Bind(UiProperties.Enabled, this, data => data.CanClear).OnClick(() =>
                {
                    if (DataContext is { } data) data.GroupRepeats = !data.GroupRepeats;
                }),
                Button.Simple("Очистить вид").Named("ClearButton").WithCommand(_clear),
                Button.Simple("Обновить детали").Named("RefreshDetails").WithCommand(_refreshDetails),
                Button.Simple("Закрыть").Named("Close").OnClick(() =>
                {
                    if (DataContext is { } data) data.IsOpen = false;
                })).Named("Actions"),
            Row(
                Text.Plain("Поиск"), Filter("Поиск по событию", data => data.Search).Named("Search"),
                Text.Plain("Source"), Filter("Source", data => data.Source).Named("Source"),
                Text.Plain("Event"), Filter("EventName", data => data.EventName, data => data.CanClear).Named("EventName")),
            Row(
                Text.Plain("Session"), Filter("SessionId (точно)", data => data.Session).Named("Session"),
                Text.Plain("Generation"), Filter("Generation (точно)", data => data.Generation).Named("Generation"),
                Text.Plain("Role"), Filter("RuntimeRole (точно)", data => data.RuntimeRole).Named("RuntimeRole")),
            Text.Plain().Named("Summary").BindText(this, data => data.Summary)
        ],
        HStack.Create(
            ScrollViewer.Create().Named("Scroll").Fill().Content(
                TreeView.Create().Named("Events").AccessibleName("Диагностические события").OnSelectionChanged(Selected)),
            new RichTextEditor(new RichTextEditorOptions { IsReadOnly = true, WordWrap = true })
                .Named("Details")
                .AccessibleName("Детали события · выделение и Ctrl+C")
                .Bind(RichTextEditor.TextProperty, this, data => data.Details)
                .Fill())
            .SetValue(UiProperties.Spacing, 8f),
        VStack.Create(
            Row(
                CheckBox.Create("Включить текст").Named("IncludeText")
                    .BindTwoWay(CheckBox.IsCheckedProperty, this, data => data.ExportIncludesText)
                    .Bind(UiProperties.Enabled, this, data => data.CanChangeExportOptions),
                CheckBox.Create("История запусков").Named("IncludeHistory")
                    .BindTwoWay(CheckBox.IsCheckedProperty, this, data => data.ExportIncludesHistory)
                    .Bind(UiProperties.Enabled, this, data => data.CanChangeExportOptions),
                Button.Simple("Отменить экспорт").Named("CancelExport")
                    .Bind(UiProperties.Enabled, this, data => data.CanCancelExport)
                    .OnClick(() => DataContext?.CancelExport()),
                Text.Plain().Named("ExportPrivacy").BindText(this, data => data.ExportPrivacyNotice)),
            Row(
                Button.Simple("Открыть исходник").Named("OpenSource")
                    .Bind(UiProperties.Enabled, this, data => data.CanOpenSource)
                    .OnClick(() => DataContext?.OpenSource()),
                TextBox.Create().Named("ExportPath").AccessibleName("Путь нового отчёта JSON")
                    .BindValueTwoWay(this, data => data.ExportPath)
                    .Bind(UiProperties.Enabled, this, data => data.CanChangeExportOptions),
                Button.Simple("Экспорт без текста").Named("Export")
                    .Bind(UiProperties.Enabled, this, data => data.CanExport)
                    .OnClick(ExportReport))),
        VStack.Create(
            Text.Plain().Named("NavigationStatus").WithRole(LabelRole).BindText(this, data => data.NavigationStatus),
            Text.Plain().Named("ExportStatus").WithRole(LabelRole).BindText(this, data => data.ExportStatus)));

    protected override void OnContentPrepared()
    {
        this.Bind(UiProperties.Visible, this, data => data.IsOpen)
            .ObservePropertyChanged(this, Changed, SynchronizeFromModel);
        AttachmentChanged += ResetPresentation;
        SynchronizeFromModel();
    }

    private TextBox Filter(string name, Expression<Func<IEditorDiagnosticsViewModel, string>> value,
        Expression<Func<IEditorDiagnosticsViewModel, bool>>? available = null) =>
        TextBox.Create().WithRole(FilterRole).AccessibleName(name)
            .BindValueTwoWay(this, value)
            .Bind(UiProperties.Enabled, this, available ?? (data => data.CanFilter));

    private static HStack Row(params Element[] elements)
    {
        foreach (var element in elements)
        {
            if (element is CheckBox choice) choice.WithRole(ExportChoiceRole);
            else if (element is Button button) button.WithRole(CommandRole);
            else if (element is Text text) text.WithRole(LabelRole);
        }
        return HStack.Create(elements).WithRole(RowRole);
    }

    private void AdvanceMode()
    {
        if (DataContext is { } model)
            model.Mode = model.Mode == EditorDiagnosticMode.Compiler ? EditorDiagnosticMode.Structured : model.Mode + 1;
    }

    private void AdvanceLevel()
    {
        if (DataContext is { } model)
            model.MinimumLevel = model.MinimumLevel switch
            {
                null => DiagnosticLevel.Trace,
                DiagnosticLevel.Critical => null,
                var level => level + 1
            };
    }

    private void ExportReport()
    {
        if (DataContext is not { } model) return;
        if (_presentationSession is { } ui) ObservePresentation(ui, model);
        model.Export();
    }

    private void Selected(string? id)
    {
        if (!_synchronizing) DataContext?.Select(id);
    }

    private void Changed(object? sender, PropertyChangedEventArgs change)
    {
        if (string.IsNullOrEmpty(change.PropertyName))
        {
            SynchronizeFromModel();
            return;
        }

        if (DataContext is not { } model) return;
        if (change.PropertyName == nameof(IEditorDiagnosticsViewModel.CanClear))
            _clear.Refresh();
        if (change.PropertyName == nameof(IEditorDiagnosticsViewModel.CanRefreshDetails))
            _refreshDetails.Refresh();
        if ((change.PropertyName is nameof(IEditorDiagnosticsViewModel.Rows) or nameof(IEditorDiagnosticsViewModel.IsOpen)) && model.IsOpen)
            Synchronize(model);
        if (change.PropertyName is nameof(IEditorDiagnosticsViewModel.FollowTail) or nameof(IEditorDiagnosticsViewModel.GroupRepeats) or nameof(IEditorDiagnosticsViewModel.MinimumLevel) or nameof(IEditorDiagnosticsViewModel.Mode) or nameof(IEditorDiagnosticsViewModel.ExportIncludesText))
            RefreshActions(model, change.PropertyName == nameof(IEditorDiagnosticsViewModel.Mode));
        if (change.PropertyName == nameof(IEditorDiagnosticsViewModel.SelectedId)) SelectVisible(model);
    }

    private void SynchronizeFromModel()
    {
        _clear.Refresh();
        _refreshDetails.Refresh();
        _presentationPending = true;
        if (DataContext is not { } model)
        {
            _followPending = _followReady = false;
            IsVisible = false;
            this.Events.SetItems([]);
            return;
        }

        RefreshActions(model, _appliedMode != model.Mode);
        Synchronize(model);
    }

    private void RefreshActions(IEditorDiagnosticsViewModel model, bool resetScroll)
    {
        this.Follow.Value = model.FollowTail ? "Автопрокрутка: вкл" : "Автопрокрутка: пауза";
        this.Group.Value = model.GroupRepeats ? "Повторы: группы" : "Повторы: отдельно";
        this.Level.Value = "Уровень: " + (model.MinimumLevel?.ToString() ?? "все");
        this.Export.Value = model.ExportIncludesText ? "Экспорт с текстом" : "Экспорт без текста";
        _appliedMode = model.Mode;
        this.Mode.Value = _appliedMode switch
        {
            EditorDiagnosticMode.Structured => "Structured",
            EditorDiagnosticMode.StandardOutput => "RAW stdout",
            EditorDiagnosticMode.StandardError => "RAW stderr",
            EditorDiagnosticMode.Compiler => "Compiler",
            _ => "Health"
        };

        if (resetScroll) this.Scroll.Offset = 0;
        _followPending = model.FollowTail && _appliedMode != EditorDiagnosticMode.Health;
        _followReady = false;
        if (_followPending) Invalidate();
    }

    private void ResetPresentation()
    {
        _presentationSession = null;
        _presentationPending = true;
    }

    private void Synchronize(IEditorDiagnosticsViewModel model)
    {
        _synchronizing = true;
        try
        {
            this.Events.SetItems(model.Rows.Select(entry => new TreeItem(entry.Id, null, entry.Label, entry.Warning ? UiSymbol.Warning : UiSymbol.Info)));
            SelectVisible(model);
            if (model.FollowTail && model.Mode != EditorDiagnosticMode.Health) { _followPending = true; _followReady = false; Invalidate(); }
        }
        finally { _synchronizing = false; }
    }

    private void SelectVisible(IEditorDiagnosticsViewModel model)
    {
        var previous = _synchronizing;
        _synchronizing = true;
        try
        {
            var events = this.Events;
            var target = model.SelectedId is { } id && events.Items.Any(item => item.Id == id) ? id : null;
            if (events.SelectedId != target) events.Select(target);
        }
        finally { _synchronizing = previous; }
    }

    public bool NeedsFrame => (_followPending || _presentationPending) && IsVisible;

    public void AdvanceFrame(TimeSpan elapsed, IInputContext context)
    {
        if (DataContext is not { } model) return;
        if (context is UiSession ui)
        {
            _presentationSession = ui;
            _presentationPending = false;
            ObservePresentation(ui, model);
        }
        if (!_followPending || !IsVisible) return;
        // Participants run before layout. Allow the new row extent to be measured before following it.
        if (!_followReady) { _followReady = true; Invalidate(Invalidation.Layout); return; }
        _followPending = false;
        var scroll = this.Scroll;
        if (model.FollowTail) scroll.Offset = model.Mode == EditorDiagnosticMode.Structured ? Math.Max(0, scroll.Extent - scroll.Bounds.Height) : 0;
    }

    private static void ObservePresentation(UiSession ui, IEditorDiagnosticsViewModel model)
    {
        var device = (ui.Rendering as IUiRenderDiagnostics)?.DeviceInfo;
        model.ObservePresentation(ui.Rendering.GetType().FullName ?? ui.Rendering.GetType().Name, ui.Viewport.Width, ui.Viewport.Height, ui.Viewport.Scale,
            device == null ? null : $"{device.Api} / {device.Name} / {device.Vendor} / {device.Version}");
    }

    public override bool HandleInput(UiInput input, IInputContext context)
    {
        if (input.Kind == InputKind.KeyDown && input.Control && input.Key == UiKey.C && this.Events.IsFocused && DataContext is { } model)
        {
            context.SetClipboardText(model.Details);
            return true;
        }
        return false;
    }

    private sealed class ConsoleLayout : ElementList
    {
        private readonly int _headers;
        public ConsoleLayout(Element[] headers, HStack columns, Element export, Element status)
        { _headers = headers.Length; foreach (var child in headers.Concat([columns, export, status])) Add(child); }
        protected override Size MeasureCore(Size available, ITextLayoutService text)
        {
            for (var i = 0; i < Children.Count; i++) Children[i].Measure(i == _headers
                ? new(available.Width, Math.Max(0, available.Height - 170)) : available, text);
            return new(available.Width, Math.Min(360, available.Height));
        }
        protected override void ArrangeCore(Rect area)
        {
            var y = area.Y; var remaining = area.Height;
            for (var i = 0; i < _headers; i++)
            {
                var height = Math.Min(remaining, Children[i].DesiredSize.Height + 4);
                Children[i].Arrange(new(area.X, y, area.Width, height)); y += height; remaining -= height;
            }
            var exportHeight = Math.Min(remaining, Children[^2].DesiredSize.Height + Children[^1].DesiredSize.Height + 8);
            var middle = Math.Max(0, remaining - exportHeight);
            Children[_headers].Arrange(new(area.X, y, area.Width, middle));
            y += middle;
            var row = Math.Min(exportHeight, Children[^2].DesiredSize.Height + 4);
            Children[^2].Arrange(new(area.X, y, area.Width, row));
            Children[^1].Arrange(new(area.X, y + row, area.Width, Math.Max(0, exportHeight - row)));
        }
    }
}
