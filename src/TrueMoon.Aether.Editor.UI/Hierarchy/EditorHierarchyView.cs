using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Menus;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Views;
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
    /// <summary>Current scene caption.</summary>
    public Text Caption => this.CaptionPart;
    /// <summary>Declares the hierarchy for its first attachment. Model and style remain App-owned.</summary>
    public EditorHierarchyView(IEditorHierarchyViewModel model, IViewStyle<EditorHierarchyView> style)
        : this(model)
    {
        ArgumentNullException.ThrowIfNull(style);
        ViewCreation.Prepare(this, style);
    }

    /// <summary>Creates and prepares a parent-owned hierarchy view on the UI owner thread.</summary>
    public static EditorHierarchyView Create(IEditorHierarchyViewModel model, IViewStyle<EditorHierarchyView> style) => new(model, style);

    internal EditorHierarchyView(IEditorHierarchyViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        SemanticId = "editor.hierarchy";
        InitializeEntityCommands();
        Content(model, ui =>
        {
            var layout = new HierarchyLayout();
            var overlays = ui.OverlayHost(layout.Named<Element>("Layout")).Named("Overlays");
            layout.WithChildren(
                ui.HStack(
                    ui.Text("ОБОЗРЕВАТЕЛЬ").Named("Title").Fill(),
                    ui.Button("В корень").Named("RootButton").SemanticId("editor.hierarchy.move-to-root").WithCommand(_entityCommands["root"]))
                    .Configure(header => header.Spacing = 4),
                ui.Text().Named("CaptionPart").BindText(data => data.Caption),
                ui.HStack(
                    CreateActionIcon(ui, UiSymbol.Add, "Создать объект · Insert", "create")
                        .Named("CreateButton").WithCommand(_entityCommands["create.empty"]),
                    CreateActionIcon(ui, UiSymbol.Cube, "Создать куб", "create-cube")
                        .Named("CubeButton").WithCommand(_entityCommands["create.cube"]),
                    CreateActionIcon(ui, UiSymbol.Restore, "Дублировать ветвь · Ctrl+D", "duplicate")
                        .Named("DuplicateButton").WithCommand(_entityCommands["duplicate"]),
                    CreateActionIcon(ui, UiSymbol.Delete, "Удалить ветвь с потомками · Delete", "delete")
                        .Named("DeleteButton").WithCommand(_entityCommands["delete"]),
                    CreateActionIcon(ui, UiSymbol.More, "Действия объекта · Shift+F10", "context")
                        .Named("ContextButton").OnClick(RequestContext))
                    .Named("Actions"),
                ui.HStack(
                    ui.TextBox()
                        .Named("Search")
                        .BindValueTwoWay(data => data.SearchText)
                        .SemanticId("editor.hierarchy.search")
                        .AccessibleName("Поиск по имени или ID · Enter следующее совпадение")
                        .MinWidth(0),
                    ui.Button("Найти").Named("FindButton").SemanticId("editor.hierarchy.find").WithCommand(_entityCommands["find"]))
                    .Named("SearchRow"),
                ui.Text().Named("Hint").BindText(data => data.Hint),
                ui.RichTextEditor(new() { IsReadOnly = true, WordWrap = true }).Named("Error")
                    .Bind(RichTextEditor.TextProperty, data => data.ErrorText)
                    .Bind(UiProperties.Visible, data => data.HasError),
                ui.ScrollViewer().Named("Scroll").Fill().Content(new RenameLayer(
                    ui.TreeView()
                        .Named("Tree")
                        .SemanticId("editor.hierarchy.tree")
                        .AccessibleName("Обозреватель сущностей")
                        .ReorderEnabled(true)
                        .Bind(TreeView.MoveEnabledProperty, data => data.CanMove)
                        .OnSelectionChanged(Select)
                        .OnMoveRequested(Move)
                        .OnPlacementRequested(Place),
                    ui.TextBox()
                        .Named("NameEditor")
                        .BindValueTwoWay(data => data.RenameText)
                        .SemanticId("editor.hierarchy.name")
                        .AccessibleName("Имя сущности")
                        .MinWidth(0)
                        .Bind(UiProperties.Visible, data => data.IsRenaming)
                        .Bind(UiProperties.HasError, data => data.HasRenameError)
                        .Bind(UiProperties.ErrorMessage, data => data.RenameError))
                    .Bind(RenameLayer.RenameIdProperty, this, data => data.RenameId)));

            return overlays;
        });
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
            this.NameEditor.IsVisible = this.Error.IsVisible = false;
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
