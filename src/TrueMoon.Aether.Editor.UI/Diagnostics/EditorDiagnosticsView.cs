using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Views;
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

    /// <summary>Declares the console and its style for first attachment.</summary>
    public EditorDiagnosticsView(IEditorDiagnosticsViewModel model, IViewStyle<EditorDiagnosticsView> style)
        : this(model)
    {
        ArgumentNullException.ThrowIfNull(style);
        ViewCreation.Prepare(this, style);
    }

    /// <summary>Creates a console with a borrowed style to apply at attachment.</summary>
    public static EditorDiagnosticsView Create(IEditorDiagnosticsViewModel model, IViewStyle<EditorDiagnosticsView> style)
    {
        ArgumentNullException.ThrowIfNull(style);
        return ViewCreation.Prepare(new EditorDiagnosticsView(model), style);
    }

    private EditorDiagnosticsView(IEditorDiagnosticsViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _clear = new(() => DataContext?.Clear(), () => DataContext?.CanClear == true);
        _refreshDetails = new(() => DataContext?.RefreshDetails(), () => DataContext?.CanRefreshDetails == true);
        Content(model, ui =>
        {
            var layout = new ConsoleLayout();
            var root = ui.Create<ContentControl>().Content(layout);
            layout.SetSections(
                [
                    Row(ui,
                        ui.Text("ДИАГНОСТИКА"),
                        ui.Button().Named("Mode").OnClick(AdvanceMode),
                        ui.Button().Named("Level").Bind(UiProperties.Enabled, data => data.CanClear).OnClick(AdvanceLevel),
                        ui.Button().Named("Follow").OnClick(() =>
                        {
                            if (DataContext is { } data) data.FollowTail = !data.FollowTail;
                        }),
                        ui.Button().Named("Group").Bind(UiProperties.Enabled, data => data.CanClear).OnClick(() =>
                        {
                            if (DataContext is { } data) data.GroupRepeats = !data.GroupRepeats;
                        }),
                        ui.Button("Очистить вид").Named("ClearButton").WithCommand(_clear),
                        ui.Button("Обновить детали").Named("RefreshDetails").WithCommand(_refreshDetails),
                        ui.Button("Закрыть").Named("Close").OnClick(() =>
                        {
                            if (DataContext is { } data) data.IsOpen = false;
                        })).Named("Actions"),
                    Row(ui,
                        ui.Text("Поиск"), Filter(ui, "Поиск по событию", data => data.Search).Named("Search"),
                        ui.Text("Source"), Filter(ui, "Source", data => data.Source).Named("Source"),
                        ui.Text("Event"), Filter(ui, "EventName", data => data.EventName, data => data.CanClear).Named("EventName")),
                    Row(ui,
                        ui.Text("Session"), Filter(ui, "SessionId (точно)", data => data.Session).Named("Session"),
                        ui.Text("Generation"), Filter(ui, "Generation (точно)", data => data.Generation).Named("Generation"),
                        ui.Text("Role"), Filter(ui, "RuntimeRole (точно)", data => data.RuntimeRole).Named("RuntimeRole")),
                    ui.Text().Named("Summary").BindText(data => data.Summary)
                ],
                ui.HStack(
                    ui.ScrollViewer().Named("Scroll").Fill().Content(
                        ui.TreeView().Named("Events").AccessibleName("Диагностические события").OnSelectionChanged(Selected)),
                    ui.RichTextEditor(new() { IsReadOnly = true, WordWrap = true })
                        .Named("Details")
                        .AccessibleName("Детали события · выделение и Ctrl+C")
                        .Bind(RichTextEditor.TextProperty, data => data.Details)
                        .Fill())
                    .SetValue(UiProperties.Spacing, 8f),
                ui.VStack(
                    Row(ui,
                        ui.CheckBox("Включить текст").Named("IncludeText")
                            .BindTwoWay(CheckBox.IsCheckedProperty, data => data.ExportIncludesText)
                            .Bind(UiProperties.Enabled, data => data.CanChangeExportOptions),
                        ui.CheckBox("История запусков").Named("IncludeHistory")
                            .BindTwoWay(CheckBox.IsCheckedProperty, data => data.ExportIncludesHistory)
                            .Bind(UiProperties.Enabled, data => data.CanChangeExportOptions),
                        ui.Button("Отменить экспорт").Named("CancelExport")
                            .Bind(UiProperties.Enabled, data => data.CanCancelExport)
                            .OnClick(() => DataContext?.CancelExport()),
                        ui.Text().Named("ExportPrivacy").BindText(data => data.ExportPrivacyNotice)),
                    Row(ui,
                        ui.Button("Открыть исходник").Named("OpenSource")
                            .Bind(UiProperties.Enabled, data => data.CanOpenSource)
                            .OnClick(() => DataContext?.OpenSource()),
                        ui.TextBox().Named("ExportPath").AccessibleName("Путь нового отчёта JSON")
                            .BindValueTwoWay(data => data.ExportPath)
                            .Bind(UiProperties.Enabled, data => data.CanChangeExportOptions),
                        ui.Button("Экспорт без текста").Named("Export")
                            .Bind(UiProperties.Enabled, data => data.CanExport)
                            .OnClick(ExportReport))),
                ui.VStack(
                    ui.Text().Named("NavigationStatus").WithRole(LabelRole).BindText(data => data.NavigationStatus),
                    ui.Text().Named("ExportStatus").WithRole(LabelRole).BindText(data => data.ExportStatus)));
            return root;
        });
    }

    protected override void OnContentPrepared()
    {
        this.Bind(UiProperties.Visible, this, data => data.IsOpen)
            .ObservePropertyChanged(this, Changed, SynchronizeFromModel);
        AttachmentChanged += ResetPresentation;
        SynchronizeFromModel();
    }

    private static UiControl<TextBox, IEditorDiagnosticsViewModel> Filter(IUiBuildContext<IEditorDiagnosticsViewModel> ui,
        string name, Expression<Func<IEditorDiagnosticsViewModel, string>> value,
        Expression<Func<IEditorDiagnosticsViewModel, bool>>? available = null) =>
        ui.TextBox().WithRole(FilterRole).AccessibleName(name)
            .BindValueTwoWay(value)
            .Bind(UiProperties.Enabled, available ?? (data => data.CanFilter));

    private static UiControl<HStack, IEditorDiagnosticsViewModel> Row(IUiBuildContext<IEditorDiagnosticsViewModel> ui, params Element[] elements)
    {
        foreach (var element in elements)
        {
            if (element is CheckBox choice) choice.WithRole(ExportChoiceRole);
            else if (element is Button button) button.WithRole(CommandRole);
            else if (element is Text text) text.WithRole(LabelRole);
        }
        return ui.HStack(elements).WithRole(RowRole);
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
        private int _headers;
        public void SetSections(Element[] headers, HStack columns, Element export, Element status)
        {
            _headers = headers.Length;
            foreach (var child in headers) Add(child);
            Add(columns); Add(export); Add(status);
        }
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
