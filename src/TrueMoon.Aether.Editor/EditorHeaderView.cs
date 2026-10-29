using System.ComponentModel;
using System.Globalization;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Diagnostics;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor;

/// <summary>Window-owned creation arguments; services are borrowed from the App graph.</summary>
public sealed record EditorHeaderArguments(OverlayHost Overlays, string Title, Action RequestClose);

/// <summary>Retained compact title content, borrowing presentation models and owning controls/subscriptions.</summary>
public sealed class EditorHeaderView : View<EditorViewModel>, IUiFrameParticipant, IUiInputObserver, IWindowHeaderActivity
{
    private readonly OverlayHost _overlays;
    private readonly EditorDocumentViewModel _documentModel;
    private readonly EditorInspectorViewModel _transformModel;
    private readonly IUiCommand _undo, _redo;
    private readonly string _baseTitle;
    private readonly List<DelegateUiCommand> _commands = [];
    private Element? _hint;
    private TimeSpan _hintRemaining;
    private readonly Dictionary<EditorCommand, IUiCommand> _runtime = [];
    private readonly Dictionary<DocumentCommand, IUiCommand> _documents = [];
    /// <summary>Gets the compact disclosure and application menus.</summary>
    public MenuBar Menu { get; }
    /// <summary>Gets the fixed, non-interactive TrueMoon mark preceding the menu.</summary>
    public Element BrandIcon { get; }
    /// <summary>Gets the retained, clipped window/project caption.</summary>
    public Text Caption { get; }
    /// <summary>Gets the fixed simulation start/resume action.</summary>
    public IconButton RunButton { get; }
    /// <summary>Gets the fixed pause action.</summary>
    public IconButton PauseButton { get; }
    /// <summary>Gets the fixed stop action.</summary>
    public IconButton StopButton { get; }
    /// <summary>Gets the command shared by the Run icon and simulation menu.</summary>
    public IUiCommand RunCommand { get; }
    /// <summary>Gets shared runtime commands by existing action identity.</summary>
    public IReadOnlyDictionary<EditorCommand, IUiCommand> RuntimeCommands => _runtime;
    /// <summary>Gets shared document commands by existing action identity.</summary>
    public IReadOnlyDictionary<DocumentCommand, IUiCommand> DocumentCommands => _documents;
    /// <inheritdoc />
    public bool NeedsFrame => _hintRemaining > TimeSpan.Zero;
    /// <inheritdoc />
    public bool KeepsTitleBarActive => Menu.IsExpanded || Menu.IsMenuOpen || ReferenceEquals(_overlays.PopupOwner, this);

    /// <summary>Creates controls on the UI owner. Window owns this view; injected models/style remain borrowed.</summary>
    /// <param name="model">Runtime presentation.</param><param name="documents">Document presentation.</param>
    /// <param name="transform">Shared editing/history presentation.</param><param name="style">Scoped appearance.</param>
    /// <param name="arguments">Window-specific popup host, caption and coordinated close callback.</param>
    public EditorHeaderView(EditorViewModel model, EditorDocumentViewModel documents,
        EditorInspectorViewModel transform, IViewStyle<EditorHeaderView> style, EditorHeaderArguments arguments)
        : this(model, documents, transform, style, arguments, null) { }

    /// <summary>Creates a header with an optional borrowed diagnostics presentation and its menu command.</summary>
    public EditorHeaderView(EditorViewModel model, EditorDocumentViewModel documents,
        EditorInspectorViewModel transform, IViewStyle<EditorHeaderView> style, EditorHeaderArguments arguments, EditorDiagnosticsViewModel? diagnostics)
    {
        ArgumentNullException.ThrowIfNull(model); ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(transform); ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(arguments); ArgumentNullException.ThrowIfNull(arguments.Overlays);
        ArgumentNullException.ThrowIfNull(arguments.Title); ArgumentNullException.ThrowIfNull(arguments.RequestClose);
        _overlays = arguments.Overlays;
        _baseTitle = arguments.Title;
        _documentModel = documents; _transformModel = transform;
        try
        {
            DataContext = model;
            foreach (var command in Enum.GetValues<EditorCommand>())
            {
                var value = command;
                _runtime.Add(value, Command(() => model.Execute(value), () => value switch
                {
                    EditorCommand.Build => model.CanBuild, EditorCommand.Start => model.CanStart,
                    EditorCommand.Play => model.CanPlay, EditorCommand.Pause => model.CanPause,
                    EditorCommand.Stop => model.CanStop, EditorCommand.Step => model.CanStep,
                    EditorCommand.Replace => model.CanReplace, EditorCommand.Cancel => model.CanCancel, _ => false
                }));
            }
            foreach (var command in new[] { DocumentCommand.New, DocumentCommand.Open, DocumentCommand.Save, DocumentCommand.SaveAs, DocumentCommand.Close })
            {
                var value = command;
                _documents.Add(value, Command(() => documents.Request(value), () => value switch
                {
                    DocumentCommand.New => documents.CanNew, DocumentCommand.Open => documents.CanOpen,
                    DocumentCommand.Save => documents.CanSave, DocumentCommand.SaveAs => documents.CanSaveAs,
                    DocumentCommand.Close => documents.CanClose, _ => false
                }));
            }
            RunCommand = Command(model.RunSimulation, () => model.CanRun);
            var undo = Command(transform.Undo, () => transform.CanUndo);
            var redo = Command(transform.Redo, () => transform.CanRedo);
            _undo = undo; _redo = redo;
            Menu = new(_overlays,
            [
                new("file", "Файл",
                [
                    File(DocumentCommand.New, "Новая сцена", UiSymbol.File),
                    File(DocumentCommand.Open, "Открыть сцену по пути", UiSymbol.Folder),
                    File(DocumentCommand.Save, "Сохранить сцену", UiSymbol.Save, "Ctrl+S"),
                    File(DocumentCommand.SaveAs, "Сохранить сцену как", UiSymbol.Save, "Ctrl+Shift+S"),
                    File(DocumentCommand.Close, "Закрыть сцену", UiSymbol.Close, "Ctrl+W"),
                    new("file.cancel", "Отменить операцию", Command(documents.Cancel, () => documents.CanCancel)),
                    new("file.exit", "Выход", Command(arguments.RequestClose), UiSymbol.Close)
                ]),
                new("edit", "Правка",
                [
                    new("edit.undo", "Отменить изменение", undo, UiSymbol.Undo, "Ctrl+Z"),
                    new("edit.redo", "Повторить изменение", redo, UiSymbol.Redo, "Ctrl+Y")
                ]),
                new("view", "Вид", diagnostics == null
                    ? [new("view.theme", "Сменить тему", Command(ToggleTheme), UiSymbol.Theme)]
                    : [new("view.theme", "Сменить тему", Command(ToggleTheme), UiSymbol.Theme),
                        new("view.diagnostics", "Диагностика", Command(diagnostics.Toggle), UiSymbol.Info)]),
                new("project", "Проект",
                [
                    Runtime(EditorCommand.Build, "Собрать проект", UiSymbol.Build),
                    Runtime(EditorCommand.Replace, "Применить сборку", UiSymbol.Build),
                    Runtime(EditorCommand.Cancel, "Отменить операцию", UiSymbol.Close)
                ]),
                new("simulation", "Симуляция",
                [
                    new("simulation.run", "Запустить / продолжить", RunCommand, UiSymbol.Play),
                    Runtime(EditorCommand.Start, "Запустить на паузе", UiSymbol.Pause),
                    Runtime(EditorCommand.Pause, "Пауза", UiSymbol.Pause),
                    Runtime(EditorCommand.Step, "Шаг", UiSymbol.Step),
                    Runtime(EditorCommand.Stop, "Остановить", UiSymbol.Stop)
                ])
            ]);
            BrandIcon = new BrandMark();
            Caption = new CaptionText(arguments.Title);
            UpdateCaption();
            RunButton = Bind(new(UiSymbol.Play, "Запустить / продолжить симуляцию"), RunCommand);
            PauseButton = Bind(new(UiSymbol.Pause, "Пауза симуляции"), _runtime[EditorCommand.Pause]);
            StopButton = Bind(new(UiSymbol.Stop, "Остановить симуляцию"), _runtime[EditorCommand.Stop]);
            Content(model, _ => new HeaderLayout(BrandIcon, Menu, Caption, RunButton, PauseButton, StopButton));
            this.WithViewStyle(style);
            Watch(model); Watch(documents); Watch(transform);
            AttachmentChanged += Detached;
            Own(new Subscription(() => AttachmentChanged -= Detached));

            MenuAction Runtime(EditorCommand command, string label, UiSymbol icon) =>
                new("runtime." + command, label, _runtime[command], icon);
            MenuAction File(DocumentCommand command, string label, UiSymbol icon, string shortcut = "") =>
                new("document." + command, label, _documents[command], icon, shortcut);
        }
        catch (Exception error)
        {
            try { Dispose(); } catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
            throw;
        }
    }

    private DelegateUiCommand Command(Action action, Func<bool>? available = null)
    {
        var command = new DelegateUiCommand(action, available); _commands.Add(command); return command;
    }
    private IconButton Bind(IconButton button, IUiCommand command)
    {
        button.IsEnabled = command.CanExecute;
        void Changed() => Dispatch(() => button.IsEnabled = command.CanExecute);
        void Click() => command.Execute();
        command.CanExecuteChanged += Changed; button.Click += Click;
        Own(new Subscription(() => { command.CanExecuteChanged -= Changed; button.Click -= Click; }));
        return button;
    }
    private void Watch(INotifyPropertyChanged model)
    {
        void Changed(object? sender, PropertyChangedEventArgs args) => Dispatch(() =>
        { foreach (var command in _commands) command.Refresh(); UpdateCaption(); });
        model.PropertyChanged += Changed;
        Own(new Subscription(() => model.PropertyChanged -= Changed));
    }
    private void UpdateCaption()
    {
        Caption.Value = _baseTitle + (_documentModel.Document?.Content == null ? "" : " · " + _documentModel.Title);
        Caption.AccessibleName = Caption.Value + "\n" + (_documentModel.Document?.Path ?? _documentModel.ScenesRoot);
    }
    private void ToggleTheme()
    {
        Element root = this; while (root.Parent != null) root = root.Parent;
        root.ApplyTheme((Theme.Surface == Theme.Dark.Surface ? Theme.Light : Theme.Dark) with { FontFamily = Theme.FontFamily });
    }
    /// <summary>Preserves application shortcuts when focus returns to title controls after menu dismissal.</summary>
    public override bool HandleInput(UiInput input, IInputContext context)
    {
        if (input.Kind != InputKind.KeyDown) return false;
        if (input.Key == UiKey.Escape)
        {
            if (_documentModel.AwaitingDecision) { _documentModel.Choose(DocumentCloseChoice.Cancel); return true; }
            if (_transformModel.CanCancel) { _transformModel.Cancel(); return true; }
        }
        if (!input.Control) return false;
        if (input.Key == UiKey.S)
        {
            if (_documentModel.AwaitingDecision) _documentModel.Choose(DocumentCloseChoice.Save);
            else _documents[input.Shift ? DocumentCommand.SaveAs : DocumentCommand.Save].Execute();
            return true;
        }
        if (input.Key == UiKey.W) { if (!_documentModel.AwaitingDecision) _documents[DocumentCommand.Close].Execute(); return true; }
        if (input.Key == UiKey.Enter) { _transformModel.Apply(); return true; }
        if (input.Key == UiKey.Z) { (input.Shift ? _redo : _undo).Execute(); return true; }
        if (input.Key == UiKey.Y) { _redo.Execute(); return true; }
        return false;
    }
    /// <inheritdoc />
    public bool PreviewInput(UiInput input, IInputContext context)
    {
        if (input.Kind == InputKind.PointerMove)
        {
            var hint = new Element[] { RunButton, PauseButton, StopButton, Caption }.FirstOrDefault(b => b.Bounds.Contains(input.X, input.Y));
            if (!ReferenceEquals(hint, _hint))
            {
                CloseHint(); _hint = hint;
                if (hint != null && !Menu.IsMenuOpen) _hintRemaining = TimeSpan.FromMilliseconds(500);
            }
        }
        else if (input.Kind is InputKind.PointerDown or InputKind.KeyDown or InputKind.FocusLost) CloseHint();
        // Modified Enter is an application action, before title buttons interpret ordinary Enter.
        if (input.Kind == InputKind.KeyDown && input.Control && input.Key == UiKey.Enter && FocusWithin(this))
        { _transformModel.Apply(); return true; }
        return false;
    }
    private static bool FocusWithin(Element element) => element.IsFocused || element.Children.Any(FocusWithin);
    /// <inheritdoc />
    public void AdvanceFrame(TimeSpan elapsed, IInputContext context)
    {
        _hintRemaining -= elapsed;
        if (_hintRemaining > TimeSpan.Zero) return;
        _hintRemaining = TimeSpan.Zero;
        if (_hint == null || Menu.IsMenuOpen || _overlays.Popup != null) return;
        var text = _hint.AccessibleName + (_hint.IsEnabled ? "" : " · сейчас недоступно");
        var popup = new HeaderHintSurface().Content(new HeaderHintText(text));
        try { _overlays.Open(this, popup, _hint.Bounds, context, tooltip: true); }
        catch { if (popup.Parent == null && !popup.IsDisposed) popup.Dispose(); throw; }
    }
    private void CloseHint()
    {
        _hintRemaining = TimeSpan.Zero; _hint = null;
        if (ReferenceEquals(_overlays.PopupOwner, this)) _overlays.Close(false);
    }
    private void Detached() { if (!IsAttached) { _hintRemaining = TimeSpan.Zero; _hint = null; } }
    private sealed class Subscription(Action release) : IDisposable { public void Dispose() => release(); }

    private sealed class HeaderHintSurface : Border
    {
        public HeaderHintSurface() { Padding = new(7); BorderWidth = 1; CornerRadius = 4; }
        protected override Color ThemeBackground => Theme.MenuBackdrop == null ? Theme.Surface : Color.Transparent;
        protected override BackdropMaterial? ThemeBackdrop => Theme.MenuBackdrop;
    }
    private sealed class HeaderHintText(string value) : Text(value)
    {
        private readonly List<string> _lines = [];
        private float _lineHeight;
        protected override Size MeasureCore(Size available, ITextLayoutService text)
        {
            var width = Math.Min(560, available.Width); var widest = 0f;
            _lineHeight = Math.Max(FontSize, text.Measure("Mg", FontSize, FontFamily).Height) + 2;
            _lines.Clear();
            foreach (var paragraph in Value.Replace("\r", "").Split('\n'))
            {
                var line = ""; var elements = StringInfo.GetTextElementEnumerator(paragraph);
                while (elements.MoveNext())
                {
                    var next = elements.GetTextElement();
                    if (line.Length != 0 && text.Measure(line + next, FontSize, FontFamily).Width > width)
                    { _lines.Add(line); widest = Math.Max(widest, text.Measure(line, FontSize, FontFamily).Width); line = next; }
                    else line += next;
                }
                _lines.Add(line); widest = Math.Max(widest, text.Measure(line, FontSize, FontFamily).Width);
            }
            return new(Math.Min(width, widest), _lines.Count * _lineHeight);
        }
        protected override void DrawCore(IDrawingContext context)
        {
            var y = Bounds.Y;
            foreach (var line in _lines) { context.Text(line, Bounds.X, y, Foreground, FontSize, FontFamily); y += _lineHeight; }
        }
    }

    private sealed class BrandMark : Element
    {
        public BrandMark() { AccessibleName = "TrueMoon"; WindowRegion = WindowRegionRole.Caption; }
        protected override Size MeasureCore(Size available, ITextLayoutService text) => new(28, 32);
        protected override void DrawCore(IDrawingContext context)
        {
            var size = Math.Min(20, Math.Min(Bounds.Width, Bounds.Height));
            UiSymbols.Draw(context, UiSymbol.TrueMoon, new(Bounds.X + (Bounds.Width - size) / 2,
                Bounds.Y + (Bounds.Height - size) / 2, size, size), Foreground);
        }
    }

    private sealed class HeaderLayout : ElementList
    {
        public HeaderLayout(params Element[] elements) { WindowRegion = WindowRegionRole.Caption; foreach (var element in elements) Add(element); }
        protected override Size MeasureCore(Size available, ITextLayoutService text)
        { foreach (var child in Children) child.Measure(available, text); return new(500, 36); }
        protected override void ArrangeCore(Rect area)
        {
            var controls = Math.Min(96, area.Width);
            var left = Math.Max(0, area.Width - controls);
            var brandWidth = Math.Min(Children[0].DesiredSize.Width, left);
            var menuWidth = Math.Min(Children[1].DesiredSize.Width, left - brandWidth);
            Children[0].Arrange(new(area.X, area.Y, brandWidth, area.Height));
            Children[1].Arrange(new(area.X + brandWidth, area.Y + (area.Height - 32) / 2, menuWidth, Math.Min(32, area.Height)));
            Children[2].Arrange(new(area.X + brandWidth + menuWidth + 10, area.Y, Math.Max(0, left - brandWidth - menuWidth - 20), area.Height));
            var size = controls / 3;
            for (var i = 0; i < 3; i++) Children[i + 3].Arrange(new(area.X + left + size * i, area.Y + (area.Height - size) / 2, size, size));
        }
    }
    private sealed class CaptionText(string value) : Text(value)
    {
        private ITextLayoutService? _text;
        private string _display = value;
        protected override Size MeasureCore(Size available, ITextLayoutService text) { _text = text; return base.MeasureCore(available, text); }
        protected override void ArrangeCore(Rect area)
        {
            _display = Value;
            if (_text == null || _text.Measure(_display, FontSize, FontFamily).Width <= area.Width) return;
            const string ellipsis = "…";
            if (_text.Measure(ellipsis, FontSize, FontFamily).Width > area.Width) { _display = ""; return; }
            var count = Value.Length;
            while (count > 0 && _text.Measure(Value[..count] + ellipsis, FontSize, FontFamily).Width > area.Width) count--;
            if (count > 0 && char.IsHighSurrogate(Value[count - 1])) count--;
            _display = Value[..count] + ellipsis;
        }
        protected override void DrawCore(IDrawingContext context) =>
            context.Text(_display, Bounds.X, Bounds.Y + (Bounds.Height - FontSize) / 2, Foreground, FontSize, FontFamily);
    }
}
