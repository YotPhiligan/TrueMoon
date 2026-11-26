using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Menus;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Views;
using TrueMoon.Argentis.Windows;
using System.Globalization;
using TrueMoon.Aether.Editor.UI.Documents;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.UI;

/// <summary>Window-owned creation arguments; services are borrowed from the App graph.</summary>
public sealed record EditorHeaderArguments(OverlayHost Overlays, string Title, Action RequestClose);

/// <summary>Retained compact title content, borrowing presentation models and owning controls/subscriptions.</summary>
public sealed class EditorHeaderView : View<IEditorViewModel>, IUiFrameParticipant, IUiInputObserver, IWindowHeaderActivity
{
    private static readonly IFactory<Element> Brands = new Factory<Element>(static () => new BrandMark());
    private static readonly IFactory<Text, string> Captions = new Factory<Text, string>(static title => new CaptionText(title));
    private static readonly IFactory<MenuBar, (OverlayHost Host, MenuGroup[] Groups)> Menus =
        new Factory<MenuBar, (OverlayHost Host, MenuGroup[] Groups)>(static args => new MenuBar(args.Host, args.Groups));
    private static readonly IFactory<HeaderLayout> Layouts = new Factory<HeaderLayout>(static () => new HeaderLayout());
    private static readonly IFactory<FileActions> Files = new Factory<FileActions>(static () => new FileActions());

    private readonly IEditorViewModel _model;
    private readonly OverlayHost _overlays;
    private readonly IEditorDocumentViewModel _documentModel;
    private readonly IEditorInspectorViewModel _transformModel;
    private readonly IUiCommand _undo, _redo;
    private readonly string _baseTitle;
    private readonly List<DelegateUiCommand> _commands = [];
    private DelayedHoverHint _hint;
    private readonly Dictionary<EditorCommand, IUiCommand> _runtime = [];
    private readonly Dictionary<DocumentCommand, IUiCommand> _documents = [];
    /// <summary>Gets the command shared by the Run icon and simulation menu.</summary>
    public IUiCommand RunCommand { get; }
    /// <summary>Gets shared runtime commands by existing action identity.</summary>
    public IReadOnlyDictionary<EditorCommand, IUiCommand> RuntimeCommands => _runtime;
    /// <summary>Gets shared document commands by existing action identity.</summary>
    public IReadOnlyDictionary<DocumentCommand, IUiCommand> DocumentCommands => _documents;
    /// <inheritdoc />
    public bool NeedsFrame => _hint.IsPending ||
        _hint.Target != null && ReferenceEquals(_overlays.PopupOwner, this) && !HintIsCurrent();
    /// <inheritdoc />
    public bool KeepsTitleBarActive => IsAttached &&
        (this.Menu.IsExpanded || this.Menu.IsMenuOpen || ReferenceEquals(_overlays.PopupOwner, this));

    /// <summary>Declares the window-owned header. Injected models and style remain borrowed.</summary>
    /// <param name="model">Runtime presentation.</param><param name="documents">Document presentation.</param>
    /// <param name="transform">Shared editing/history presentation.</param><param name="style">Scoped appearance.</param>
    /// <param name="arguments">Window-specific popup host, caption and coordinated close callback.</param>
    public EditorHeaderView(IEditorViewModel model, IEditorDocumentViewModel documents,
        IEditorInspectorViewModel transform, IViewStyle<EditorHeaderView> style, EditorHeaderArguments arguments)
        : this(model, documents, transform, style, arguments, null) { }

    /// <summary>Creates a header with an optional borrowed diagnostics presentation and its menu command.</summary>
    public EditorHeaderView(IEditorViewModel model, IEditorDocumentViewModel documents,
        IEditorInspectorViewModel transform, IViewStyle<EditorHeaderView> style, EditorHeaderArguments arguments, IEditorDiagnosticsViewModel? diagnostics)
    {
        ArgumentNullException.ThrowIfNull(model); ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(transform); ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(arguments); ArgumentNullException.ThrowIfNull(arguments.Overlays);
        ArgumentNullException.ThrowIfNull(arguments.Title); ArgumentNullException.ThrowIfNull(arguments.RequestClose);
        _overlays = arguments.Overlays;
        _baseTitle = arguments.Title;
        _model = model;
        _documentModel = documents; _transformModel = transform;
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
        foreach (var command in new[] { DocumentCommand.New, DocumentCommand.Open, DocumentCommand.Save, DocumentCommand.SaveAs, DocumentCommand.Close, DocumentCommand.OpenProject, DocumentCommand.CloseProject, DocumentCommand.OpenBuild, DocumentCommand.ImportModel })
        {
            var value = command;
            _documents.Add(value, Command(() => documents.Request(value), () => value switch
            {
                DocumentCommand.New => documents.CanNew, DocumentCommand.Open => documents.CanOpen,
                DocumentCommand.Save => documents.CanSave, DocumentCommand.SaveAs => documents.CanSaveAs,
                DocumentCommand.Close => documents.CanClose, DocumentCommand.OpenProject => documents.CanOpenProject,
                DocumentCommand.CloseProject => documents.CanCloseProject, DocumentCommand.OpenBuild => documents.CanOpenBuild,
                DocumentCommand.ImportModel => documents.CanImportModel, _ => false
            }));
        }
        RunCommand = Command(model.RunSimulation, () => model.CanRun);
        _undo = Command(transform.Undo, () => transform.CanUndo);
        _redo = Command(transform.Redo, () => transform.CanRedo);

        Content(model, ui => ui.Create(Layouts).WithChildren(
            ui.Create(Brands).Named("BrandIcon"),
            ui.Create(Menus, (_overlays, CreateGroups(arguments.RequestClose, diagnostics))).Named("Menu"),
            ui.Create(Captions, _baseTitle).Named("Caption"),
            ui.Create(Files).WithChildren(
                Action(ui, UiSymbol.Folder, "Открыть сцену по пути", _documents[DocumentCommand.Open])
                    .SemanticId("editor.header.open-scene").Named("OpenButton"),
                Action(ui, UiSymbol.Save, "Сохранить сцену · Ctrl+S", _documents[DocumentCommand.Save])
                    .SemanticId("editor.header.save-scene").Named("SaveButton"),
                Action(ui, UiSymbol.Undo, "Отменить изменение · Ctrl+Z", _undo).Named("UndoButton"),
                Action(ui, UiSymbol.Redo, "Повторить изменение · Ctrl+Y", _redo).Named("RedoButton")),
            Action(ui, UiSymbol.Play, "Запустить / продолжить симуляцию", RunCommand).Named("RunButton"),
            Action(ui, UiSymbol.Pause, "Пауза симуляции", _runtime[EditorCommand.Pause]).Named("PauseButton"),
            Action(ui, UiSymbol.Stop, "Остановить симуляцию", _runtime[EditorCommand.Stop]).Named("StopButton"))
            .Named("Layout"));
        this.WithViewStyle(style);
    }

    private MenuGroup[] CreateGroups(Action requestClose, IEditorDiagnosticsViewModel? diagnostics)
    {
        return
        [
            new("file", "Файл",
            [
                File(DocumentCommand.New, "Новая сцена", UiSymbol.File),
                File(DocumentCommand.Open, "Открыть сцену по пути", UiSymbol.Folder),
                File(DocumentCommand.Save, "Сохранить сцену", UiSymbol.Save, "Ctrl+S"),
                File(DocumentCommand.SaveAs, "Сохранить сцену как", UiSymbol.Save, "Ctrl+Shift+S"),
                File(DocumentCommand.Close, "Закрыть сцену", UiSymbol.Close, "Ctrl+W"),
                new("file.cancel", "Отменить операцию", Command(_documentModel.Cancel, () => _documentModel.CanCancel)),
                new("file.exit", "Выход", Command(requestClose), UiSymbol.Close)
            ]),
            new("edit", "Правка",
            [
                new("edit.undo", "Отменить изменение", _undo, UiSymbol.Undo, "Ctrl+Z"),
                new("edit.redo", "Повторить изменение", _redo, UiSymbol.Redo, "Ctrl+Y")
            ]),
            new("view", "Вид", diagnostics == null
                ? [new("view.theme", "Сменить тему", Command(ToggleTheme), UiSymbol.Theme)]
                : [new("view.theme", "Сменить тему", Command(ToggleTheme), UiSymbol.Theme),
                    new("view.diagnostics", "Диагностика", Command(diagnostics.Toggle), UiSymbol.Info)]),
            new("project", "Проект",
            [
                File(DocumentCommand.OpenProject, "Открыть проект по пути", UiSymbol.Folder),
                File(DocumentCommand.CloseProject, "Закрыть проект", UiSymbol.Close),
                File(DocumentCommand.ImportModel, "Импортировать модель", UiSymbol.File),
                Runtime(EditorCommand.Build, "Собрать проект", UiSymbol.Build),
                ..(_documentModel.SupportsPublishedBuilds ? new[] { File(DocumentCommand.OpenBuild, "Выбрать опубликованную сборку", UiSymbol.Folder) } : []),
                Runtime(EditorCommand.Replace, "Применить сборку", UiSymbol.Build),
                Runtime(EditorCommand.Cancel, "Отменить операцию", UiSymbol.Close)
            ]),
            new("simulation", "Симуляция",
            [
                new("simulation.run", "Запустить / продолжить", RunCommand, UiSymbol.Play),
                Runtime(EditorCommand.Start, "Запустить на паузе", UiSymbol.Pause),
                Runtime(EditorCommand.Pause, "Пауза симуляции", UiSymbol.Pause),
                Runtime(EditorCommand.Step, "Шаг", UiSymbol.Step),
                Runtime(EditorCommand.Stop, "Остановить симуляцию", UiSymbol.Stop)
            ])
        ];

        MenuAction Runtime(EditorCommand command, string label, UiSymbol icon) =>
            new("runtime." + command, label, _runtime[command], icon);
        MenuAction File(DocumentCommand command, string label, UiSymbol icon, string shortcut = "") =>
            new("document." + command, label, _documents[command], icon, shortcut);
    }

    private DelegateUiCommand Command(Action action, Func<bool>? available = null)
    {
        var command = new DelegateUiCommand(action, available); _commands.Add(command); return command;
    }

    private static UiControl<IconButton, IEditorViewModel> Action(IUiBuildContext<IEditorViewModel> ui,
        UiSymbol symbol, string name, IUiCommand command) =>
        ui.IconButton(symbol, name).Height(float.NaN).WithRole(EditorHeaderViewStyle.EditorHeaderAction).WithCommand(command);

    /// <inheritdoc />
    protected override void OnContentPrepared()
    {
        UpdateCaption();
        this.ObserveState(Refresh, _model, _documentModel, _transformModel);
        AttachmentChanged += ResetDetachedHint;
        Own(new Subscription(() => AttachmentChanged -= ResetDetachedHint));
    }

    private void ResetDetachedHint()
    {
        if (!IsAttached) _hint.Reset();
    }

    private void Refresh()
    {
        foreach (var command in _commands) command.Refresh();
        UpdateCaption();
    }
    private void UpdateCaption()
    {
        var caption = this.Caption;
        caption.Value = _documentModel.GetWindowCaption(_baseTitle);
        caption.AccessibleName = _documentModel.GetWindowCaptionDescription(_baseTitle);
    }
    private void ToggleTheme()
    {
        Element root = this; while (root.Parent != null) root = root.Parent;
        root.ApplyTheme((Theme.Surface == Theme.Dark.Surface ? Theme.Light : Theme.Dark) with
        { FontFamily = Theme.FontFamily, FontSize = Theme.FontSize, FontWeight = Theme.FontWeight, FontItalic = Theme.FontItalic });
    }
    /// <summary>Preserves application shortcuts when focus returns to title controls after menu dismissal.</summary>
    public override bool HandleInput(UiInput input, IInputContext context)
    {
        if (input.Kind != InputKind.KeyDown) return false;
        if (input.Key == UiKey.Escape)
        {
            if (_documentModel.AwaitingDecision) { _documentModel.Choose(EditorDocumentCloseChoice.Cancel); return true; }
            if (_transformModel.CanCancel) { _transformModel.Cancel(); return true; }
        }
        if (!input.Control) return false;
        if (input.Key == UiKey.S)
        {
            if (_documentModel.AwaitingDecision) _documentModel.Choose(EditorDocumentCloseChoice.Save);
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
            var hint = this.Layout.HintAt(input.X, input.Y);
            if (!ReferenceEquals(hint, _hint.Target))
            {
                CloseHint();
                if (hint != null) _hint.Start(hint, input.X, input.Y,
                    this.Menu.IsMenuOpen ? TimeSpan.Zero : TimeSpan.FromMilliseconds(500));
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
        if (_hint.Target is not { } target) return;
        var layoutPending = context.HasPendingLayout;
        if (!layoutPending && !HintIsCurrent()) { CloseHint(); return; }
        if (!_hint.Advance(elapsed, layoutPending) || this.Menu.IsMenuOpen ||
            !_overlays.IsAttached || _overlays.Popup != null) return;
        var text = target.AccessibleName + (target.IsEnabled ? "" : " · сейчас недоступно");
        var popup = new HeaderHintSurface().Content(new HeaderHintText(text));
        try { _overlays.Open(this, popup, target.Bounds, context, tooltip: true); }
        catch { if (popup.Parent == null && !popup.IsDisposed) popup.Dispose(); throw; }
    }
    private bool HintIsCurrent() => _hint.IsCurrent(this.Layout.HintAt(_hint.X, _hint.Y));
    private void CloseHint()
    {
        _hint.Reset();
        if (ReferenceEquals(_overlays.PopupOwner, this)) _overlays.Close(false);
    }
    private sealed class Subscription(Action release) : IDisposable { public void Dispose() => release(); }

    private sealed class HeaderHintSurface : Border
    {
        public HeaderHintSurface() { Padding = new(7); BorderWidth = 1; CornerRadius = 4; }
        protected override Color ThemeBackground => Theme.MenuBackdrop == null ? Theme.Surface : Color.Transparent;
        protected override BackdropMaterial? ThemeBackdrop => Theme.MenuBackdrop;
    }
    internal sealed class HeaderHintText(string value) : Text(value)
    {
        private readonly List<string> _lines = [];
        private float _lineHeight;
        protected override Size MeasureCore(Size available, ITextLayoutService text)
        {
            var width = Math.Min(560, available.Width); var widest = 0f;
            _lineHeight = Math.Max(FontSize, text.Measure("Mg", FontSize, FontFamily, FontWeight, FontItalic).Height) + 2;
            _lines.Clear();
            foreach (var paragraph in Value.Replace("\r", "").Split('\n'))
            {
                var line = ""; var elements = StringInfo.GetTextElementEnumerator(paragraph);
                while (elements.MoveNext())
                {
                    var next = elements.GetTextElement();
                    if (line.Length != 0 && text.Measure(line + next, FontSize, FontFamily, FontWeight, FontItalic).Width > width)
                    { _lines.Add(line); widest = Math.Max(widest, text.Measure(line, FontSize, FontFamily, FontWeight, FontItalic).Width); line = next; }
                    else line += next;
                }
                _lines.Add(line); widest = Math.Max(widest, text.Measure(line, FontSize, FontFamily, FontWeight, FontItalic).Width);
            }
            return new(Math.Min(width, widest), _lines.Count * _lineHeight);
        }
        internal ReadOnlySpan<string> Lines => System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_lines);
        internal float LineHeight => _lineHeight;
    }

    internal sealed class BrandMark : Element
    {
        public BrandMark() { AccessibleName = "TrueMoon"; WindowRegion = WindowRegionRole.Caption; }
        protected override Size MeasureCore(Size available, ITextLayoutService text) => new(28, 32);
    }

    internal sealed class HeaderLayout : ElementList
    {
        public HeaderLayout() => WindowRegion = WindowRegionRole.Caption;

        internal Element? HintAt(float x, float y)
        {
            var files = Children[3];
            if (files.IsVisible)
                for (var i = 0; i < files.Children.Count; i++)
                    if (files.Children[i].Bounds.Contains(x, y)) return files.Children[i];
            for (var i = 4; i < Children.Count; i++)
                if (Children[i].Bounds.Contains(x, y)) return Children[i];
            var caption = Children[2];
            return caption.Bounds.Contains(x, y) ? caption : null;
        }
        protected override Size MeasureCore(Size available, ITextLayoutService text)
        { foreach (var child in Children) child.Measure(available, text); return new(500, 36); }
        protected override void ArrangeCore(Rect area)
        {
            var controls = Math.Min(96, area.Width);
            var simulationStart = area.Width - controls;
            var files = Children[3];
            // Keep room for the collapsed menu and caption. Narrow headers keep the same commands in the menu.
            files.IsVisible = area.Width >= 368;
            var left = simulationStart - (files.IsVisible ? FileActions.WidthWithGap : 0);
            var brandWidth = Math.Min(Children[0].DesiredSize.Width, left);
            var menuWidth = Math.Min(Children[1].DesiredSize.Width, left - brandWidth);
            Children[0].Arrange(new(area.X, area.Y, brandWidth, area.Height));
            Children[1].Arrange(new(area.X + brandWidth, area.Y + (area.Height - 32) / 2, menuWidth, Math.Min(32, area.Height)));
            Children[2].Arrange(new(area.X + brandWidth + menuWidth + 10, area.Y, Math.Max(0, left - brandWidth - menuWidth - 20), area.Height));
            files.Arrange(new(area.X + left, area.Y, FileActions.WidthWithGap, area.Height));
            var buttonWidth = controls / 3;
            for (var i = 0; i < 3; i++)
                Children[i + 4].Arrange(new(area.X + simulationStart + buttonWidth * i, area.Y, buttonWidth, area.Height));
        }
    }

    private sealed class FileActions : ElementList
    {
        internal const float WidthWithGap = 136;
        protected override Size MeasureCore(Size available, ITextLayoutService text)
        { foreach (var button in Children) button.Measure(available, text); return new(WidthWithGap, 32); }
        protected override void ArrangeCore(Rect area)
        {
            for (var i = 0; i < Children.Count; i++)
                Children[i].Arrange(new(area.X + i * 32, area.Y, 32, area.Height));
        }
    }
    internal sealed class CaptionText(string value) : Text(value)
    {
        private ITextLayoutService? _text;
        private string _display = value;
        private float? _inkCenter;
        protected override Size MeasureCore(Size available, ITextLayoutService text)
        {
            _text = text;
            // Use a capital and a descender so editing or eliding the caption leaves its baseline stable.
            var ink = text.MeasureInkBounds("Hg", FontSize, FontFamily, FontWeight, FontItalic);
            _inkCenter = ink.Y + ink.Height / 2;
            return base.MeasureCore(available, text);
        }
        protected override void ArrangeCore(Rect area) => _display = FitSingleLine(_text, area.Width);
        internal string DisplayText => _display;
        internal float InkCenter => _inkCenter ?? FontSize / 2;
    }
}
