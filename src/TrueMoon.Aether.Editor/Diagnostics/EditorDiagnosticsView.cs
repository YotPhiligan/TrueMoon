using System.ComponentModel;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.Diagnostics;

/// <summary>Parent-owned retained console. The App model/collector are borrowed; all input and updates run on the UI owner.</summary>
public sealed class EditorDiagnosticsView : View<EditorDiagnosticsViewModel>, IUiFrameParticipant
{
    public static ViewStyleRole<TextBox> FilterRole { get; } = new("DiagnosticFilter");
    public static ViewStyleRole<HStack> RowRole { get; } = new("DiagnosticRow");
    public static ViewStyleRole<Button> CommandRole { get; } = new("DiagnosticCommand");
    public static ViewStyleRole<Text> LabelRole { get; } = new("DiagnosticLabel");
    public static ViewStyleRole<CheckBox> ExportChoiceRole { get; } = new("DiagnosticExportChoice");
    private readonly EditorDiagnosticsViewModel _model;
    private bool _synchronizing, _followPending, _followReady;
    private bool _presentationPending = true;
    private TrueMoon.Alloy.Runtime.UiSession? _presentationSession;
    public TreeView Events { get; } = new() { AccessibleName = "Диагностические события" };
    public ScrollViewer Scroll { get; } = new();
    public RichTextEditor Details { get; } = new(new RichTextEditorOptions { IsReadOnly = true, WordWrap = true });
    public TextBox Search { get; }
    public TextBox Source { get; }
    public TextBox EventName { get; }
    public TextBox Session { get; }
    public TextBox Generation { get; }
    public TextBox RuntimeRole { get; }
    public TextBox ExportPath { get; }
    public Button Follow { get; }
    public Button Group { get; }
    public Button Level { get; }
    public Button Clear { get; }
    public Button Close { get; }
    public Button Export { get; }
    public Button Mode { get; }
    public Button RefreshDetails { get; }
    public Button OpenSource { get; }
    public Text NavigationStatus { get; }
    public Text Summary { get; }
    public Text ExportStatus { get; }
    public CheckBox IncludeText { get; }
    public CheckBox IncludeHistory { get; }
    public Button CancelExport { get; }
    public Text ExportPrivacy { get; }
    public HStack Actions { get; }
    public bool NeedsFrame => (_followPending || _presentationPending) && IsVisible;

    public EditorDiagnosticsView(EditorDiagnosticsViewModel model, IViewStyle<EditorDiagnosticsView> style)
    {
        ArgumentNullException.ThrowIfNull(model); ArgumentNullException.ThrowIfNull(style); _model = model;
        try
        {
            Search = Input("Поиск по событию", x => x.Search); Source = Input("Source", x => x.Source);
            EventName = Input("EventName", x => x.EventName); Session = Input("SessionId (точно)", x => x.Session);
            Generation = Input("Generation (точно)", x => x.Generation); RuntimeRole = Input("RuntimeRole (точно)", x => x.RuntimeRole);
            foreach (var filter in new[] { Search, Source, Session, Generation, RuntimeRole }) filter.Bind(UiProperties.Enabled, model, x => x.CanFilter);
            EventName.Bind(UiProperties.Enabled, model, x => x.CanClear);
            Follow = Button.Simple().OnClick(() => model.FollowTail = !model.FollowTail);
            Group = Button.Simple().OnClick(() => model.GroupRepeats = !model.GroupRepeats);
            Level = Button.Simple().OnClick(() => model.MinimumLevel = model.MinimumLevel switch
            { null => TrueMoon.Diagnostics.DiagnosticLevel.Trace, TrueMoon.Diagnostics.DiagnosticLevel.Critical => null, var level => level + 1 });
            Clear = Button.Simple("Очистить вид").Bind(UiProperties.Enabled, model, x => x.CanClear).OnClick(model.Clear);
            Group.Bind(UiProperties.Enabled, model, x => x.CanClear); Level.Bind(UiProperties.Enabled, model, x => x.CanClear);
            Mode = Button.Simple().OnClick(() => model.Mode = model.Mode == EditorDiagnosticMode.Compiler ? EditorDiagnosticMode.Structured : model.Mode + 1);
            RefreshDetails = Button.Simple("Обновить детали").Bind(UiProperties.Enabled, model, x => x.CanRefreshDetails).OnClick(model.RefreshDetails);
            Close = Button.Simple("Закрыть").OnClick(() => model.IsOpen = false);
            Summary = Text.Plain().BindText(model, x => x.Summary);
            Details.AccessibleName = "Детали события · выделение и Ctrl+C"; Details.Bind(RichTextEditor.TextProperty, model, x => x.Details);
            ExportPath = TextBox.Create().BindTwoWay(TextBox.ValueProperty, model, x => x.ExportPath).SetValue(UiProperties.AccessibleName, "Путь нового отчёта JSON");
            Export = Button.Simple("Экспорт без текста").Bind(UiProperties.Enabled, model, x => x.CanExport).OnClick(() =>
            {
                if (_presentationSession is { } ui) ObservePresentation(ui);
                model.Export();
            });
            IncludeText = CheckBox.Create("Включить текст").BindTwoWay(CheckBox.IsCheckedProperty, model, x => x.ExportIncludesText)
                .Bind(UiProperties.Enabled, model, x => x.CanChangeExportOptions);
            IncludeHistory = CheckBox.Create("История запусков").BindTwoWay(CheckBox.IsCheckedProperty, model, x => x.ExportIncludesHistory)
                .Bind(UiProperties.Enabled, model, x => x.CanChangeExportOptions);
            CancelExport = Button.Simple("Отменить экспорт").Bind(UiProperties.Enabled, model, x => x.CanCancelExport).OnClick(model.CancelExport);
            ExportPrivacy = Text.Plain().WithRole(LabelRole).BindText(model, x => x.ExportPrivacyNotice);
            ExportPath.Bind(UiProperties.Enabled, model, x => x.CanChangeExportOptions);
            ExportStatus = Text.Plain().BindText(model, x => x.ExportStatus);
            OpenSource = Button.Simple("Открыть исходник").Bind(UiProperties.Enabled, model, x => x.CanOpenSource).OnClick(model.OpenSource);
            NavigationStatus = Text.Plain().WithRole(LabelRole).BindText(model, x => x.NavigationStatus);
            Actions = Row(Text.Plain("ДИАГНОСТИКА"), Mode, Level, Follow, Group, Clear, RefreshDetails, Close);
            var filters = Row(Text.Plain("Поиск"), Search, Text.Plain("Source"), Source, Text.Plain("Event"), EventName);
            var context = Row(Text.Plain("Session"), Session, Text.Plain("Generation"), Generation, Text.Plain("Role"), RuntimeRole);
            Scroll.SetContent(Events);
            Content(model, _ => new ConsoleLayout([Actions, filters, context, Summary], Scroll, Details,
                VStack.Create().WithChildren(Row(IncludeText, IncludeHistory, CancelExport, ExportPrivacy), Row(OpenSource, ExportPath, Export)),
                VStack.Create().WithChildren(NavigationStatus, ExportStatus.WithRole(LabelRole))));
            this.Bind(UiProperties.Visible, model, x => x.IsOpen); this.WithViewStyle(style);
            Events.SelectionChanged += Selected; model.PropertyChanged += Changed;
            Own(new Subscription(() => { model.PropertyChanged -= Changed; Events.SelectionChanged -= Selected; _presentationSession = null; }));
            UpdateActions(); Synchronize();

            TextBox Input(string name, System.Linq.Expressions.Expression<Func<EditorDiagnosticsViewModel, string>> property) =>
                TextBox.Create().WithRole(FilterRole).BindTwoWay(TextBox.ValueProperty, model, property).SetValue(UiProperties.AccessibleName, name);
        }
        catch (Exception failure)
        { try { Dispose(); } catch (Exception cleanup) { throw new AggregateException(failure, cleanup); } throw; }
    }

    private static HStack Row(params Element[] elements)
    {
        foreach (var element in elements)
        {
            if (element is CheckBox choice) choice.WithRole(ExportChoiceRole);
            else if (element is Button button) button.WithRole(CommandRole);
            else if (element is Text text) text.WithRole(LabelRole);
        }
        return HStack.Create().WithRole(RowRole).WithChildren(elements);
    }
    private void Selected(string? id) { if (!_synchronizing) _model.Select(id); }
    private void Changed(object? sender, PropertyChangedEventArgs change)
    {
        if (change.PropertyName is nameof(EditorDiagnosticsViewModel.Rows) or nameof(EditorDiagnosticsViewModel.IsOpen))
            Dispatch(() => { if (_model.IsOpen) Synchronize(); });
        if (change.PropertyName is nameof(EditorDiagnosticsViewModel.FollowTail) or nameof(EditorDiagnosticsViewModel.GroupRepeats) or nameof(EditorDiagnosticsViewModel.MinimumLevel) or nameof(EditorDiagnosticsViewModel.Mode) or nameof(EditorDiagnosticsViewModel.ExportIncludesText))
            Dispatch(() =>
            {
                UpdateActions(); _followPending = _model.FollowTail && _model.Mode != EditorDiagnosticMode.Health;
                _followReady = false; if (change.PropertyName == nameof(EditorDiagnosticsViewModel.Mode)) Scroll.Offset = 0;
                if (_followPending) Invalidate();
            });
        if (change.PropertyName == nameof(EditorDiagnosticsViewModel.SelectedId)) Dispatch(SelectVisible);
    }
    private void Synchronize()
    {
        _synchronizing = true;
        try
        {
            Events.SetItems(_model.Rows.Select(entry => new TreeItem(entry.Id, null, entry.Label, entry.Warning ? UiSymbol.Warning : UiSymbol.Info)));
            SelectVisible();
            if (_model.FollowTail && _model.Mode != EditorDiagnosticMode.Health) { _followPending = true; _followReady = false; Invalidate(); }
        }
        finally { _synchronizing = false; }
    }
    private void SelectVisible()
    {
        var previous = _synchronizing; _synchronizing = true;
        try
        {
            var target = _model.SelectedId is { } id && Events.Items.Any(item => item.Id == id) ? id : null;
            if (Events.SelectedId != target) Events.Select(target);
        }
        finally { _synchronizing = previous; }
    }
    private void UpdateActions()
    {
        Follow.Value = _model.FollowTail ? "Автопрокрутка: вкл" : "Автопрокрутка: пауза";
        Group.Value = _model.GroupRepeats ? "Повторы: группы" : "Повторы: отдельно";
        Level.Value = "Уровень: " + (_model.MinimumLevel?.ToString() ?? "все");
        Export.Value = _model.ExportIncludesText ? "Экспорт с текстом" : "Экспорт без текста";
        Mode.Value = _model.Mode switch { EditorDiagnosticMode.Structured => "Structured", EditorDiagnosticMode.StandardOutput => "RAW stdout", EditorDiagnosticMode.StandardError => "RAW stderr", EditorDiagnosticMode.Compiler => "Compiler", _ => "Health" };
    }
    public void AdvanceFrame(TimeSpan elapsed, IInputContext context)
    {
        if (context is TrueMoon.Alloy.Runtime.UiSession ui)
        {
            _presentationSession = ui; _presentationPending = false; ObservePresentation(ui);
        }
        if (!_followPending || !IsVisible) return;
        // Participants run before layout. Allow the new row extent to be measured before following it.
        if (!_followReady) { _followReady = true; Invalidate(Invalidation.Layout); return; }
        _followPending = false;
        if (_model.FollowTail) Scroll.Offset = _model.Mode == EditorDiagnosticMode.Structured ? Math.Max(0, Scroll.Extent - Scroll.Bounds.Height) : 0;
    }
    private void ObservePresentation(TrueMoon.Alloy.Runtime.UiSession ui)
    {
        var device = (ui.Rendering as TrueMoon.Alloy.Runtime.IUiRenderDiagnostics)?.DeviceInfo;
        _model.ObservePresentation(new(ui.Rendering.GetType().FullName ?? ui.Rendering.GetType().Name, ui.Viewport.Width, ui.Viewport.Height, ui.Viewport.Scale,
            device == null ? null : $"{device.Api} / {device.Name} / {device.Vendor} / {device.Version}"));
    }
    public override bool HandleInput(UiInput input, IInputContext context)
    {
        if (input.Kind == InputKind.KeyDown && input.Control && input.Key == UiKey.C && Events.IsFocused)
        { context.SetClipboardText(_model.Details); return true; }
        return false;
    }
    private sealed class Subscription(Action release) : IDisposable { public void Dispose() => release(); }
    private sealed class ConsoleLayout : ElementList
    {
        private readonly int _headers;
        public ConsoleLayout(Element[] headers, Element list, Element details, Element export, Element status)
        { _headers = headers.Length; foreach (var child in headers.Concat([list, details, export, status])) Add(child); }
        protected override Size MeasureCore(Size available, ITextLayoutService text)
        {
            for (var i = 0; i < Children.Count; i++) Children[i].Measure(i == _headers || i == _headers + 1
                ? new(Math.Max(0, (available.Width - 8) / 2), Math.Max(0, available.Height - 170)) : available, text);
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
            var middle = Math.Max(0, remaining - exportHeight); var width = Math.Max(0, (area.Width - 8) / 2);
            Children[_headers].Arrange(new(area.X, y, width, middle)); Children[_headers + 1].Arrange(new(area.X + width + 8, y, width, middle));
            y += middle;
            var row = Math.Min(exportHeight, Children[^2].DesiredSize.Height + 4);
            Children[^2].Arrange(new(area.X, y, area.Width, row));
            Children[^1].Arrange(new(area.X, y + row, area.Width, Math.Max(0, exportHeight - row)));
        }
    }
}
