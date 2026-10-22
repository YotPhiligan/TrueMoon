using TrueMoon.Argentis;

namespace TrueMoon.Editor;

public sealed class EditorView : View
{
    private readonly EditorWorkspace _workspace;
    private readonly Dictionary<EditorCommand, Button> _commands = [];
    private readonly Dictionary<string, Button> _entities = new(StringComparer.Ordinal);
    private readonly ContentControl _properties = new();
    private readonly Text _selection = new("");
    private readonly Text _state = new("");
    private readonly Text _error = new("");
    public EditorViewport Canvas { get; } = new();
    public IReadOnlyDictionary<EditorCommand, Button> Commands => _commands;
    public IReadOnlyDictionary<string, Button> Entities => _entities;
    public event Action<EditorCommand>? CommandRequested;
    public EditorView(EditorWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace); _workspace = workspace;
        var toolbar = new HStack { Padding = new Thickness(10, 8, 10, 8), Spacing = 6 };
        AddCommand(EditorCommand.Build, "Собрать"); AddCommand(EditorCommand.Start, "Запустить", ControlTone.Accent);
        AddCommand(EditorCommand.Play, "Play", ControlTone.Positive); AddCommand(EditorCommand.Pause, "Пауза"); AddCommand(EditorCommand.Step, "Шаг");
        AddCommand(EditorCommand.Stop, "Стоп", ControlTone.Danger); AddCommand(EditorCommand.Replace, "Применить сборку"); AddCommand(EditorCommand.Cancel, "Отмена");
        toolbar.Add(new Button("Тема").OnClick(() => { Element root = this; while (root.Parent != null) root = root.Parent; root.ApplyTheme((Theme.Surface == Theme.Dark.Surface ? Theme.Light : Theme.Dark) with { FontFamily = Theme.FontFamily }); }));
        var entities = new VStack { Spacing = 6 };
        foreach (var id in workspace.EntityIds)
        {
            var button = new Button(EditorScene.Name(id)) { Height = 34, AccessibleName = id }.OnClick(() => workspace.Select(id));
            _entities.Add(id, button); entities.Add(button);
        }
        var hierarchy = Pane("СЦЕНА", new Text(workspace.Scene.Id) { FontSize = 14 }, new ScrollViewer().Content(entities));
        var viewport = new ContentControl { Padding = new Thickness(1), BorderWidth = 1 }.Content(new ViewportLayout(Canvas,
            new Text("VIEWPORT · Предпросмотр сцены") { FontSize = 12, Padding = new Thickness(8) }));
        var inspector = Pane("ИНСПЕКТОР", _selection, new Text("Свойства сцены · только просмотр") { FontSize = 12 }, _properties,
            _error);
        SetContent(new EditorLayout(toolbar, hierarchy, viewport, inspector, _state));
        workspace.SelectionChanged += SelectionChanged; Own(new SelectionSubscription(workspace, SelectionChanged));
        SelectionChanged(); Refresh();
        void AddCommand(EditorCommand command, string text, ControlTone tone = ControlTone.Neutral)
        { var button = new Button(text) { Tone = tone }.OnClick(() => { if (_workspace.CanExecute(command)) CommandRequested?.Invoke(command); }); _commands.Add(command, button); toolbar.Add(button); }
    }
    private static Border Pane(string title, params Element[] children) => new Border { Padding = new Thickness(10) }.Content(
        new VStack { Spacing = 10 }.WithChildren(new Text(title) { FontSize = 13 }, new VStack { Spacing = 6 }.WithChildren(children)));
    private void SelectionChanged()
    {
        VerifyAccess(); _selection.Value = _workspace.SelectedId == null ? "Нет объектов" : EditorScene.Name(_workspace.SelectedId);
        var rows = new VStack { Spacing = 2 }; foreach (var row in _workspace.Properties()) rows.Add(new PropertyRow(row.Key, row.Value));
        var old = _properties.Child; _properties.SetContent(rows); old?.Dispose();
        foreach (var (id, button) in _entities) button.Tone = id == _workspace.SelectedId ? ControlTone.Accent : ControlTone.Neutral;
    }
    public void Refresh()
    {
        VerifyAccess(); foreach (var (command, button) in _commands) button.IsEnabled = _workspace.CanExecute(command);
        var state = _workspace.State switch { Aether.Sessions.SessionState.Stopped => "Остановлен", Aether.Sessions.SessionState.Starting => "Запуск", Aether.Sessions.SessionState.Paused => "Пауза", Aether.Sessions.SessionState.Playing => "Симуляция", Aether.Sessions.SessionState.Stopping => "Остановка", _ => "Ошибка runtime" };
        var operation = _workspace.Operation switch { Aether.Editing.GameEditorOperation.Idle => "Готово", Aether.Editing.GameEditorOperation.Build => "Сборка", Aether.Editing.GameEditorOperation.Replace => "Замена сборки", _ => "Выполняется команда" };
        _state.Value = $"  {state}   ·   {operation}   ·   Tick {_workspace.Tick}   ·   Runtime PID {_workspace.ProcessId?.ToString() ?? "—"}   ·   {_workspace.EntityCount} объектов";
        _error.Value = _workspace.Error == null ? "" : "Ошибка: " + _workspace.Error; _error.Foreground = Theme.Error;
    }
    private sealed class SelectionSubscription(EditorWorkspace workspace, Action callback) : IDisposable { public void Dispose() => workspace.SelectionChanged -= callback; }
    private sealed class ViewportLayout : ElementList
    {
        public ViewportLayout(Element canvas, Element title) { Add(canvas); Add(title); }
        protected override Size MeasureCore(Size available, ITextLayoutService text) { foreach (var child in Children) child.Measure(available, text); return new(600, 420); }
        protected override void ArrangeCore(Rect area) { var h = Math.Min(30, area.Height); Children[1].Arrange(new(area.X, area.Y, area.Width, h)); Children[0].Arrange(new(area.X, area.Y + h, area.Width, Math.Max(0, area.Height - h))); }
    }
}
