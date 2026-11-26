using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Menus;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Views;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.UI.Hierarchy;

public sealed partial class EditorHierarchyView
{
    private readonly Dictionary<string, DelegateUiCommand> _entityCommands = new(StringComparer.Ordinal);
    private IReadOnlyList<MenuAction> _menuActions = [];
    private OverlayHost? _contextHost;
    private Func<bool>? _contextIsCurrent;
    private bool _pendingContext, _pendingRenameFocus;
    /// <summary>Semantic role for compact hierarchy action icons.</summary>
    public static ViewStyleRole<IconButton> ActionRole { get; } = new("HierarchyAction");
    /// <summary>Shared commands used by action icons, shortcuts and context-menu adapters.</summary>
    public IReadOnlyDictionary<string, DelegateUiCommand> EntityCommands => _entityCommands;
    /// <inheritdoc />
    public bool NeedsFrame => _pendingContext || _pendingRenameFocus;
    private void InitializeEntityCommands()
    {
        Add("create.empty", model => model.CreateEntity(EditorEntityKind.Empty), model => model.CanCreate);
        Add("create.cube", model => model.CreateEntity(EditorEntityKind.Cube), model => model.CanCreate);
        Add("create.child", model => model.CreateEntity(EditorEntityKind.Empty, true), model => model.CanCreate && model.CanEditSelected);
        Add("create.childCube", model => model.CreateEntity(EditorEntityKind.Cube, true), model => model.CanCreate && model.CanEditSelected);
        Add("duplicate", model => model.DuplicateSelected(), model => model.CanEditSelected);
        Add("delete", model => model.DeleteSelected(), model => model.CanEditSelected);
        Add("rename", model => { if (model.BeginRename()) _pendingRenameFocus = true; }, model => model.CanRename);
        Add("root", model => model.MoveToRoot(), model => model.CanMoveToRoot);
        Add("find", model => model.FindNext(), model => model.CanFindNext);
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
        void Add(string id, Action<IEditorHierarchyViewModel> execute, Func<IEditorHierarchyViewModel, bool> available) =>
            _entityCommands.Add(id, new(
                () => { if (DataContext is { } model) execute(model); },
                () => DataContext is { } model && available(model)));
    }

    private static UiControl<IconButton, IEditorHierarchyViewModel> CreateActionIcon(
        IUiBuildContext<IEditorHierarchyViewModel> ui, UiSymbol symbol, string label, string idSuffix)
    {
        var icon = ui.IconButton(symbol, label);
        icon.Element.WithRole(ActionRole);
        icon.Element.Clear(UiProperties.Width);
        icon.Element.Clear(UiProperties.Height);
        return icon.SemanticId("editor.hierarchy." + idSuffix);
    }

    private void RequestContext()
    {
        _pendingContext = true;
        Invalidate();
    }

    private void RefreshEntityCommands(string? property)
    {
        if (string.IsNullOrEmpty(property))
        {
            foreach (var command in _entityCommands.Values) command.Refresh();
            return;
        }
        string[] ids = property switch
        {
            nameof(IEditorHierarchyViewModel.CanCreate) => ["create.empty", "create.cube", "create.child", "create.childCube"],
            nameof(IEditorHierarchyViewModel.CanEditSelected) => ["create.child", "create.childCube", "duplicate", "delete"],
            nameof(IEditorHierarchyViewModel.CanRename) => ["rename"],
            nameof(IEditorHierarchyViewModel.CanMoveToRoot) => ["root"],
            nameof(IEditorHierarchyViewModel.CanFindNext) => ["find"], _ => []
        };
        foreach (var id in ids) _entityCommands[id].Refresh();
    }
    /// <inheritdoc />
    public void AdvanceFrame(TimeSpan elapsed, IInputContext context)
    {
        if (_pendingContext) { _pendingContext = false; OpenContext(context, this.ContextButton.Bounds); }
        SynchronizeRenameFocus(context);
    }
    private void SynchronizeRenameFocus(IInputContext context)
    {
        if (!_pendingRenameFocus) return; _pendingRenameFocus = false;
        if (DataContext is not { IsRenaming: true } model) return;
        // Input may arrive before queued bindings. Install the current draft before transferring focus.
        var nameEditor = this.NameEditor;
        nameEditor.Value = model.RenameText; nameEditor.IsVisible = true;
        context.Focus(nameEditor); nameEditor.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true), context);
    }
    private bool HandleEntityInput(UiInput input, IInputContext context)
    {
        if (DataContext is not { } model) return false;
        var tree = this.Tree;
        if (input.Kind == InputKind.PointerDown && input.Button == 1 && this.Scroll.ChildClipBounds.Contains(input.X, input.Y) && tree.Bounds.Contains(input.X, input.Y))
        {
            var item = tree.VisibleItems.FirstOrDefault(item => tree.GetItemBounds(item.Id)?.Contains(input.X, input.Y) == true);
            context.Focus(tree); model.Select(item?.Id); tree.Select(model.SelectedId);
            OpenContext(context, new(input.X, input.Y, 0, 0)); return true;
        }
        if (input.Kind != InputKind.KeyDown) return false;
        var search = this.Search;
        if (search.IsFocused)
        {
            if (input.Key == UiKey.Enter) { _entityCommands["find"].Execute(); return true; }
            if (input.Key == UiKey.Escape) { model.SearchText = ""; context.Focus(tree); return true; }
        }
        if (!tree.IsFocused) return false;
        if (input.Control && input.Key == UiKey.F) { context.Focus(search); return true; }
        if (input.Control && input.Key == UiKey.D) { _entityCommands["duplicate"].Execute(); return true; }
        if (input.Key == UiKey.Delete) { _entityCommands["delete"].Execute(); return true; }
        if (input.Key == UiKey.Insert) { _entityCommands[input.Control ? "create.child" : "create.empty"].Execute(); return true; }
        if (input.Shift && input.Key == UiKey.F10) { OpenContext(context, tree.GetItemBounds(model.SelectedId ?? "") ?? tree.Bounds); return true; }
        return false;
    }
    private void OpenContext(IInputContext context, Rect anchor)
    {
        CloseContext();
        if (DataContext is not { } model) return;
        var host = this.Overlays;
        for (var parent = Parent; parent != null; parent = parent.Parent) if (parent is OverlayHost outer) host = outer;
        var contextVersion = _contextVersion;
        var version = model.SnapshotVersion; var selection = model.SelectedId; var canMove = model.CanMove;
        bool Current() => contextVersion == _contextVersion && version == model.SnapshotVersion &&
            selection == model.SelectedId && canMove == model.CanMove;
        var actions = _menuActions.Select(action => action with { Command = new DelegateUiCommand(() => { if (Current()) action.Command.Execute(); }, () => Current() && action.Command.CanExecute) });
        var popup = new MenuPopup(actions, () => host.Close());
        try
        {
            host.Open(this, popup, anchor, context, this.Tree);
            _contextHost = host;
            _contextIsCurrent = Current;
            popup.FocusFirst(context);
        }
        catch { if (popup.Parent == null && !popup.IsDisposed) popup.Dispose(); throw; }
    }
    private void CloseContext()
    {
        var host = _contextHost; _contextHost = null;
        _contextIsCurrent = null;
        if (host is { IsDisposed: false, IsAttached: true } && ReferenceEquals(host.PopupOwner, this)) host.Close(false);
    }
    private void RequestContextClose()
    {
        var host = _contextHost; var popup = host?.Popup;
        if (host == null || popup == null || !ReferenceEquals(host.PopupOwner, this) || _contextIsCurrent?.Invoke() == true) return;
        Dispatch(() => { if (ReferenceEquals(_contextHost, host) && ReferenceEquals(host.Popup, popup)) CloseContext(); });
    }
}
