using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.Hierarchy;

public sealed partial class EditorHierarchyView
{
    private readonly Dictionary<string, DelegateUiCommand> _entityCommands = new(StringComparer.Ordinal);
    private IReadOnlyList<MenuAction> _menuActions = [];
    private OverlayHost? _contextHost;
    private bool _pendingContext, _pendingRenameFocus;
    /// <summary>Semantic role for compact hierarchy action icons.</summary>
    public static ViewStyleRole<IconButton> ActionRole { get; } = new("HierarchyAction");
    /// <summary>Shared commands used by action icons, shortcuts and context-menu adapters.</summary>
    public IReadOnlyDictionary<string, DelegateUiCommand> EntityCommands => _entityCommands;
    /// <summary>Owned fallback popup host; a surrounding editor overlay is preferred when available.</summary>
    public OverlayHost Overlays { get; private set; } = null!;
    /// <summary>Retained object-action row.</summary>
    public HStack Actions { get; } = HStack.Create();
    /// <summary>Retained search row.</summary>
    public HStack SearchRow { get; } = HStack.Create();
    /// <summary>Object creation, also available with Insert.</summary>
    public IconButton CreateButton { get; private set; } = null!;
    /// <summary>Visible cube creation.</summary>
    public IconButton CubeButton { get; private set; } = null!;
    /// <summary>Subtree duplication, also available with Ctrl+D.</summary>
    public IconButton DuplicateButton { get; private set; } = null!;
    /// <summary>Explicit subtree deletion, also available with Delete.</summary>
    public IconButton DeleteButton { get; private set; } = null!;
    /// <summary>Keyboard-accessible context actions.</summary>
    public IconButton ContextButton { get; private set; } = null!;
    /// <summary>Single-line name/ID query; Enter navigates to the next match.</summary>
    public TextBox Search { get; } = new();
    /// <summary>Next-match action.</summary>
    public Button FindButton { get; private set; } = null!;
    /// <inheritdoc />
    public bool NeedsFrame => _pendingContext || _pendingRenameFocus;
    private void InitializeEntityControls()
    {
        Add("create.empty", () => _model.CreateEntity(EditorEntityKind.Empty), () => _model.CanCreate);
        Add("create.cube", () => _model.CreateEntity(EditorEntityKind.Cube), () => _model.CanCreate);
        Add("create.child", () => _model.CreateEntity(EditorEntityKind.Empty, true), () => _model.CanCreate && _model.CanEditSelected);
        Add("create.childCube", () => _model.CreateEntity(EditorEntityKind.Cube, true), () => _model.CanCreate && _model.CanEditSelected);
        Add("duplicate", () => _model.DuplicateSelected(), () => _model.CanEditSelected);
        Add("delete", () => _model.DeleteSelected(), () => _model.CanEditSelected);
        Add("rename", () => { if (_model.BeginRename()) _pendingRenameFocus = true; }, () => _model.CanRename);
        Add("root", _model.MoveToRoot, () => _model.CanMoveToRoot);
        Add("find", _model.FindNext, () => _model.CanFindNext);
        _menuActions = Array.AsReadOnly(new MenuAction[]
        {
            new("create.empty", "Создать объект", _entityCommands["create.empty"], UiSymbol.Add, "Insert"),
            new("create.cube", "Создать куб", _entityCommands["create.cube"], UiSymbol.Cube),
            new("create.child", "Создать дочерний объект", _entityCommands["create.child"], UiSymbol.Add, "Ctrl+Insert"),
            new("create.childCube", "Создать дочерний куб", _entityCommands["create.childCube"], UiSymbol.Cube),
            new("rename", "Переименовать", _entityCommands["rename"], null, "F2"),
            new("duplicate", "Дублировать ветвь", _entityCommands["duplicate"], UiSymbol.Restore, "Ctrl+D"),
            new("delete", "Удалить ветвь", _entityCommands["delete"], UiSymbol.Delete, "Delete"),
            new("root", "Перенести в корень", _entityCommands["root"])
        });
        CreateButton = BindCommand(new IconButton(UiSymbol.Add, "Создать объект · Insert").WithRole(ActionRole), "create.empty");
        CubeButton = BindCommand(new IconButton(UiSymbol.Cube, "Создать куб").WithRole(ActionRole), "create.cube");
        DuplicateButton = BindCommand(new IconButton(UiSymbol.Restore, "Дублировать ветвь · Ctrl+D").WithRole(ActionRole), "duplicate");
        DeleteButton = BindCommand(new IconButton(UiSymbol.Delete, "Удалить ветвь с потомками · Delete").WithRole(ActionRole), "delete");
        ContextButton = new IconButton(UiSymbol.More, "Действия объекта · Shift+F10").WithRole(ActionRole);
        foreach (var button in new[] { CreateButton, CubeButton, DuplicateButton, DeleteButton, ContextButton })
        { button.Clear(UiProperties.Width); button.Clear(UiProperties.Height); }
        ContextButton.OnClick(() => { _pendingContext = true; Invalidate(); });
        Actions.WithChildren(CreateButton, CubeButton, DuplicateButton, DeleteButton, ContextButton);
        Search.AccessibleName = "Поиск по имени или ID · Enter следующее совпадение"; Search.MinWidth = 0;
        Search.BindTwoWay(TextBox.ValueProperty, _model, model => model.SearchText);
        FindButton = BindCommand(Button.Simple("Найти"), "find");
        SearchRow.WithChildren(Search, FindButton);

        void Add(string id, Action execute, Func<bool> available) => _entityCommands.Add(id, new(execute, available));
    }
    private T BindCommand<T>(T button, string id) where T : Button
    {
        var command = _entityCommands[id]; button.IsEnabled = command.CanExecute;
        void Changed() => Dispatch(() => button.IsEnabled = command.CanExecute);
        void Execute() => command.Execute();
        command.CanExecuteChanged += Changed; button.Click += Execute;
        Own(new Release(() => { command.CanExecuteChanged -= Changed; button.Click -= Execute; }));
        return button;
    }
    private void RefreshEntityCommands(string? property)
    {
        string[] ids = property switch
        {
            nameof(EditorHierarchyViewModel.CanCreate) => ["create.empty", "create.cube", "create.child", "create.childCube"],
            nameof(EditorHierarchyViewModel.CanEditSelected) => ["create.child", "create.childCube", "duplicate", "delete"],
            nameof(EditorHierarchyViewModel.CanRename) => ["rename"],
            nameof(EditorHierarchyViewModel.CanMoveToRoot) => ["root"],
            nameof(EditorHierarchyViewModel.CanFindNext) => ["find"], _ => []
        };
        foreach (var id in ids) _entityCommands[id].Refresh();
    }
    /// <inheritdoc />
    public void AdvanceFrame(TimeSpan elapsed, IInputContext context)
    {
        if (_pendingContext) { _pendingContext = false; OpenContext(context, ContextButton.Bounds); }
        SynchronizeRenameFocus(context);
    }
    private void SynchronizeRenameFocus(IInputContext context)
    {
        if (!_pendingRenameFocus) return; _pendingRenameFocus = false;
        if (!_model.IsRenaming) return;
        // Input may arrive before queued bindings. Install the current draft before transferring focus.
        NameEditor.Value = _model.RenameText; NameEditor.IsVisible = true;
        context.Focus(NameEditor); NameEditor.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true), context);
    }
    private bool HandleEntityInput(UiInput input, IInputContext context)
    {
        if (input.Kind == InputKind.PointerDown && input.Button == 1 && Scroll.ChildClipBounds.Contains(input.X, input.Y) && Tree.Bounds.Contains(input.X, input.Y))
        {
            var item = Tree.VisibleItems.FirstOrDefault(item => Tree.GetItemBounds(item.Id)?.Contains(input.X, input.Y) == true);
            context.Focus(Tree); _model.Select(item?.Id); Tree.Select(_model.SelectedId);
            OpenContext(context, new(input.X, input.Y, 0, 0)); return true;
        }
        if (input.Kind != InputKind.KeyDown) return false;
        if (Search.IsFocused)
        {
            if (input.Key == UiKey.Enter) { _entityCommands["find"].Execute(); return true; }
            if (input.Key == UiKey.Escape) { _model.SearchText = ""; context.Focus(Tree); return true; }
        }
        if (!Tree.IsFocused) return false;
        if (input.Control && input.Key == UiKey.F) { context.Focus(Search); return true; }
        if (input.Control && input.Key == UiKey.D) { _entityCommands["duplicate"].Execute(); return true; }
        if (input.Key == UiKey.Delete) { _entityCommands["delete"].Execute(); return true; }
        if (input.Key == UiKey.Insert) { _entityCommands[input.Control ? "create.child" : "create.empty"].Execute(); return true; }
        if (input.Shift && input.Key == UiKey.F10) { OpenContext(context, Tree.GetItemBounds(_model.SelectedId ?? "") ?? Tree.Bounds); return true; }
        return false;
    }
    private void OpenContext(IInputContext context, Rect anchor)
    {
        CloseContext(); var host = Overlays;
        for (var parent = Parent; parent != null; parent = parent.Parent) if (parent is OverlayHost outer) host = outer;
        var version = _model.SnapshotVersion; var selection = _model.SelectedId;
        bool Current() => version == _model.SnapshotVersion && selection == _model.SelectedId;
        var actions = _menuActions.Select(action => action with { Command = new DelegateUiCommand(() => { if (Current()) action.Command.Execute(); }, () => Current() && action.Command.CanExecute) });
        var popup = new MenuPopup(actions, () => host.Close());
        try { host.Open(this, popup, anchor, context, Tree); _contextHost = host; popup.FocusFirst(context); }
        catch { if (popup.Parent == null && !popup.IsDisposed) popup.Dispose(); throw; }
    }
    private void CloseContext()
    {
        var host = _contextHost; _contextHost = null;
        if (host is { IsDisposed: false, IsAttached: true } && ReferenceEquals(host.PopupOwner, this)) host.Close(false);
    }
    private void RequestContextClose()
    {
        var host = _contextHost; var popup = host?.Popup;
        if (host == null || popup == null || !ReferenceEquals(host.PopupOwner, this)) return;
        Dispatch(() => { if (ReferenceEquals(_contextHost, host) && ReferenceEquals(host.Popup, popup)) CloseContext(); });
    }
}
