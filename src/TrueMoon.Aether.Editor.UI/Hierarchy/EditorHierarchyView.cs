using System.ComponentModel;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.UI.Hierarchy;

/// <summary>Parent-owned hierarchy UI over borrowed App presentation and style.</summary>
public sealed partial class EditorHierarchyView : View<IEditorHierarchyViewModel>, IUiFrameParticipant
{
    private long _contextVersion;
    private bool _synchronizingItems;
    private bool _treeSyncPending;
    private IReadOnlyList<HierarchyItem>? _observedItems;
    /// <summary>Viewport retaining scroll position across theme and data refresh.</summary>
    public ScrollViewer Scroll { get; private set; } = null!;
    /// <summary>Current scene caption.</summary>
    public Text Caption { get; private set; } = null!;
    /// <summary>Keyboard-accessible explicit move to scene root.</summary>
    public Button RootButton { get; private set; } = null!;
    /// <summary>Gesture/admission guidance.</summary>
    public Text Hint { get; private set; } = null!;
    /// <summary>Selectable complete mutation diagnostic.</summary>
    public RichTextEditor Error { get; private set; } = null!;
    /// <summary>Panel geometry.</summary>
    public Element Layout { get; private set; } = null!;
    /// <summary>Creates fresh controls on the UI owner. Borrowed model/style remain App-owned.</summary>
    public EditorHierarchyView(IEditorHierarchyViewModel model, IViewStyle<EditorHierarchyView> style)
        : this(model)
    {
        ArgumentNullException.ThrowIfNull(style);
        ViewCreation.Prepare(this, style);
    }

    /// <summary>Creates and prepares a parent-owned hierarchy view on the UI owner thread.</summary>
    public static EditorHierarchyView Create(IEditorHierarchyViewModel model, IViewStyle<EditorHierarchyView> style)
    {
        ArgumentNullException.ThrowIfNull(style);
        return ViewCreation.Prepare(new EditorHierarchyView(model), style);
    }

    private EditorHierarchyView(IEditorHierarchyViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        DataContext = model;
        InitializeEntityCommands();
    }

    /// <inheritdoc />
    protected override Element BuildContent()
    {
        Layout = new HierarchyLayout().WithChildren(
            HStack.Create(
                Text.Plain("ОБОЗРЕВАТЕЛЬ").Named("Title").Fill(),
                RootButton = Button.Simple("В корень").WithCommand(_entityCommands["root"]))
                .Configure(header => header.Spacing = 4),
            Caption = Text.Plain().BindText(this, data => data.Caption),
            HStack.Create(
                CreateButton = ActionIcon(UiSymbol.Add, "Создать объект · Insert").WithCommand(_entityCommands["create.empty"]),
                CubeButton = ActionIcon(UiSymbol.Cube, "Создать куб").WithCommand(_entityCommands["create.cube"]),
                DuplicateButton = ActionIcon(UiSymbol.Restore, "Дублировать ветвь · Ctrl+D").WithCommand(_entityCommands["duplicate"]),
                DeleteButton = ActionIcon(UiSymbol.Delete, "Удалить ветвь с потомками · Delete").WithCommand(_entityCommands["delete"]),
                ContextButton = ActionIcon(UiSymbol.More, "Действия объекта · Shift+F10").OnClick(RequestContext))
                .Named("Actions"),
            HStack.Create(
                TextBox.Create()
                    .Named("Search")
                    .AccessibleName("Поиск по имени или ID · Enter следующее совпадение")
                    .MinWidth(0)
                    .BindValueTwoWay(this, data => data.SearchText),
                FindButton = Button.Simple("Найти").WithCommand(_entityCommands["find"]))
                .Named("SearchRow"),
            Hint = Text.Plain().BindText(this, data => data.Hint),
            Error = new RichTextEditor(new RichTextEditorOptions { IsReadOnly = true, WordWrap = true })
                .Bind(RichTextEditor.TextProperty, this, data => data.ErrorText)
                .Bind(UiProperties.Visible, this, data => data.HasError),
            Scroll = ScrollViewer.Create().Fill().Content(new RenameLayer(
                TreeView.Create()
                    .Named("Tree")
                    .AccessibleName("Обозреватель сущностей")
                    .ReorderEnabled(true)
                    .Bind(TreeView.MoveEnabledProperty, this, data => data.CanMove)
                    .OnSelectionChanged(Select)
                    .OnMoveRequested(Move)
                    .OnPlacementRequested(Place),
                TextBox.Create()
                    .Named("NameEditor")
                    .AccessibleName("Имя сущности")
                    .MinWidth(0)
                    .BindValueTwoWay(this, data => data.RenameText)
                    .Bind(UiProperties.Visible, this, data => data.IsRenaming)
                    .Bind(UiProperties.HasError, this, data => data.HasRenameError)
                    .Bind(UiProperties.ErrorMessage, this, data => data.RenameError))
                .Bind(RenameLayer.RenameIdProperty, this, data => data.RenameId)));

        return Overlays = new(Layout);
    }

    /// <inheritdoc />
    protected override void OnContentPrepared()
    {
        var nameEditor = this.NameEditor;
        Own(new Release(() =>
        {
            _contextVersion++;
            nameEditor.FocusChanged -= NameFocusChanged;
            if (DataContext is { IsRenaming: true } model) model.CancelRename();
            CloseContext();
            _pendingContext = _pendingRenameFocus = false;
        }));
        nameEditor.FocusChanged += NameFocusChanged;
        SynchronizeContext();
        this.ObservePropertyChanged(this, (_, change) => Changed(change), SynchronizeContext);
    }

    private void SynchronizeContext()
    {
        _contextVersion++;
        _treeSyncPending = _pendingContext = _pendingRenameFocus = false;
        RequestContextClose();
        this.Tree.CancelMove();
        SynchronizeItems();
        RefreshEntityCommands(null);
        if (DataContext == null)
        {
            this.NameEditor.IsVisible = Error.IsVisible = false;
            this.Tree.Set(TreeView.MoveEnabledProperty, false);
        }
    }

    private void Select(string? id) { if (!_synchronizingItems) DataContext?.Select(id); }
    /// <summary>Clears selection with Esc after the tree has had a chance to cancel a drag.</summary>
    public override bool HandleInput(UiInput input, IInputContext context)
    {
        if (input.Kind == InputKind.KeyDown && this.NameEditor.IsFocused && DataContext is { IsRenaming: true } model)
        {
            if (input.Key == UiKey.Enter) { if (model.CommitRename()) context.Focus(this.Tree); return true; }
            if (input.Key == UiKey.Escape) { model.CancelRename(); context.Focus(this.Tree); return true; }
            // TextBox already handled clipboard and caret keys. Do not apply global scene shortcuts to a name draft.
            return input.Control;
        }
        if (input.Kind == InputKind.KeyDown && input.Key == UiKey.F2 && this.Tree.IsFocused)
        {
            _entityCommands["rename"].Execute(); SynchronizeRenameFocus(context);
            return true;
        }
        if (HandleEntityInput(input, context)) return true;
        if (input.Kind != InputKind.KeyDown || input.Key != UiKey.Escape || !this.Tree.IsFocused) return false;
        DataContext?.Select(null); return true;
    }
    private void Move(TreeMove move) => DataContext?.Move(move.Id, move.ParentId);
    private void Place(TreePlacement move) => DataContext?.Place(move.Id, move.ParentId, move.AnchorId,
        move.Position == TreeDropPosition.Before ? EditorEntityPlacement.Before : EditorEntityPlacement.After);
    private void NameFocusChanged(Element element)
    {
        if (!element.IsFocused && DataContext is { IsRenaming: true } model) model.CancelRename();
    }
    private void Changed(PropertyChangedEventArgs args)
    {
        RefreshEntityCommands(args.PropertyName);
        var all = string.IsNullOrEmpty(args.PropertyName);
        if (all || args.PropertyName is nameof(IEditorHierarchyViewModel.SnapshotVersion) or nameof(IEditorHierarchyViewModel.SelectedId) or nameof(IEditorHierarchyViewModel.CanMove)) RequestContextClose();
        if (all || args.PropertyName == nameof(IEditorHierarchyViewModel.SnapshotVersion))
        {
            var version = _contextVersion;
            Dispatch(() => { if (version == _contextVersion) this.Tree.CancelMove(); });
        }
        if (all || args.PropertyName is nameof(IEditorHierarchyViewModel.Items) or nameof(IEditorHierarchyViewModel.SelectedId)) ScheduleTreeSync();
    }
    private void ScheduleTreeSync()
    {
        if (_treeSyncPending) return; _treeSyncPending = true;
        var version = _contextVersion;
        Dispatch(() =>
        {
            if (version != _contextVersion) return;
            _treeSyncPending = false;
            SynchronizeItems();
        });
    }
    private void SynchronizeItems()
    {
        var model = DataContext;
        var items = model?.Items ?? []; var selection = model?.SelectedId;
        var tree = this.Tree;
        _synchronizingItems = true;
        try
        {
            if (!ReferenceEquals(_observedItems, items))
            {
                tree.SetItems(items.Select(item => new TreeItem(item.Id, item.ParentId, item.Name, item.Kind switch
                {
                    HierarchyItemKind.Renderable => UiSymbol.Cube, HierarchyItemKind.Interactive => UiSymbol.Interaction, _ => UiSymbol.Entity
                })));
                _observedItems = items;
            }
            tree.Select(selection);
        }
        finally { _synchronizingItems = false; }
    }
    private sealed class Release(Action release) : IDisposable { public void Dispose() => release(); }
    private sealed class RenameLayer : ElementList
    {
        internal static readonly UiProperty<string?> RenameIdProperty = new("RenameId", null, Invalidation.Layout);
        private readonly TreeView _tree;
        private readonly TextBox _input;
        public RenameLayer(TreeView tree, TextBox input)
        { _tree = tree; _input = input; Add(tree); Add(input); }
        protected override Size MeasureCore(Size available, ITextLayoutService text)
        {
            _tree.Measure(available, text); if (_input.IsVisible) _input.Measure(available, text);
            return _tree.DesiredSize;
        }
        protected override void ArrangeCore(Rect area)
        {
            _tree.Arrange(area);
            if (Get(RenameIdProperty) is not { } id || _tree.GetItemBounds(id) is not { } row) { _input.Arrange(default); return; }
            var depth = 0; var item = _tree.Items.First(value => value.Id == id);
            while (item.ParentId != null) { depth++; item = _tree.Items.First(value => value.Id == item.ParentId); }
            var offset = Math.Min(depth * _tree.Indent, Math.Max(0, row.Width - 36)) + 34;
            _input.Arrange(new(row.X + offset, row.Y, Math.Max(0, row.Width - offset), row.Height));
        }
    }
    private sealed class HierarchyLayout : VStack
    {
        protected override Size MeasureCore(Size available, ITextLayoutService text)
        {
            var desired = base.MeasureCore(available, text);
            return new(Math.Min(240, available.Width), desired.Height);
        }
    }
}
