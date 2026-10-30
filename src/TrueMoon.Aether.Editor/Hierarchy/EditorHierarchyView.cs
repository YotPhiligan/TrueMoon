using System.ComponentModel;
using TrueMoon.Argentis;
using TrueMoon.Aether.Scenes.Authoring;

namespace TrueMoon.Aether.Editor.Hierarchy;

/// <summary>Parent-owned hierarchy UI over borrowed App presentation and style.</summary>
public sealed partial class EditorHierarchyView : View<EditorHierarchyViewModel>, IUiFrameParticipant
{
    private readonly EditorHierarchyViewModel _model;
    private bool _synchronizingItems;
    private bool _treeSyncPending;
    private IReadOnlyList<HierarchyItem>? _observedItems;
    /// <summary>General UI tree; owns only display/selection/expansion/gesture state.</summary>
    public TreeView Tree { get; } = new();
    /// <summary>Viewport retaining scroll position across theme and data refresh.</summary>
    public ScrollViewer Scroll { get; } = new();
    /// <summary>Retained single-line editor over the selected row; its draft belongs to the model.</summary>
    public TextBox NameEditor { get; } = new();
    /// <summary>Panel heading.</summary>
    public Text Title { get; } = new("ОБОЗРЕВАТЕЛЬ");
    /// <summary>Current scene caption.</summary>
    public Text Caption { get; }
    /// <summary>Keyboard-accessible explicit move to scene root.</summary>
    public Button RootButton { get; }
    /// <summary>Gesture/admission guidance.</summary>
    public Text Hint { get; }
    /// <summary>Selectable complete mutation diagnostic.</summary>
    public RichTextEditor Error { get; }
    /// <summary>Panel geometry.</summary>
    public Element Layout { get; }
    /// <summary>Creates fresh controls on the UI owner. Borrowed model/style remain App-owned.</summary>
    public EditorHierarchyView(EditorHierarchyViewModel model, IViewStyle<EditorHierarchyView> style)
    {
        ArgumentNullException.ThrowIfNull(model); ArgumentNullException.ThrowIfNull(style); _model = model;
        try
        {
            InitializeEntityControls();
            Caption = Text.Plain().BindText(model, value => value.Caption);
            RootButton = BindCommand(Button.Simple("В корень"), "root");
            Hint = Text.Plain().BindText(model, value => value.Hint);
            Error = new(new RichTextEditorOptions { IsReadOnly = true, WordWrap = true });
            Error.Bind(RichTextEditor.TextProperty, model, value => value.ErrorText).Bind(UiProperties.Visible, model, value => value.HasError);
            Tree.AccessibleName = "Обозреватель сущностей"; Tree.Bind(TreeView.MoveEnabledProperty, model, value => value.CanMove);
            Tree.ReorderEnabled = true;
            NameEditor.AccessibleName = "Имя сущности"; NameEditor.MinWidth = 0;
            NameEditor.BindTwoWay(TextBox.ValueProperty, model, value => value.RenameText).Bind(UiProperties.Visible, model, value => value.IsRenaming);
            Scroll.SetContent(new RenameLayer(Tree, NameEditor, model)); Layout = new HierarchyLayout(Title, RootButton, Caption, Actions, SearchRow, Hint, Error, Scroll);
            Overlays = new(Layout); Content(model, _ => Overlays);
            SynchronizeItems();
            Tree.SelectionChanged += Select; Tree.MoveRequested += Move; Tree.PlacementRequested += Place;
            NameEditor.FocusChanged += NameFocusChanged;
            model.PropertyChanged += Changed;
            AttachmentChanged += TreeAttachmentChanged;
            Own(new Release(() =>
            {
                model.PropertyChanged -= Changed; Tree.SelectionChanged -= Select; Tree.MoveRequested -= Move; Tree.PlacementRequested -= Place;
                AttachmentChanged -= TreeAttachmentChanged;
                NameEditor.FocusChanged -= NameFocusChanged; if (model.IsRenaming) model.CancelRename();
                CloseContext(); _pendingContext = _pendingRenameFocus = false;
            }));
            this.WithViewStyle(style);
        }
        catch (Exception original)
        {
            try { Dispose(); } catch (Exception cleanup) { throw new AggregateException(original, cleanup); }
            throw;
        }
    }
    private void Select(string? id) { if (!_synchronizingItems) _model.Select(id); }
    /// <summary>Clears selection with Esc after the tree has had a chance to cancel a drag.</summary>
    public override bool HandleInput(UiInput input, IInputContext context)
    {
        if (input.Kind == InputKind.KeyDown && NameEditor.IsFocused && _model.IsRenaming)
        {
            if (input.Key == UiKey.Enter) { if (_model.CommitRename()) context.Focus(Tree); return true; }
            if (input.Key == UiKey.Escape) { _model.CancelRename(); context.Focus(Tree); return true; }
            // TextBox already handled clipboard and caret keys. Do not apply global scene shortcuts to a name draft.
            return input.Control;
        }
        if (input.Kind == InputKind.KeyDown && input.Key == UiKey.F2 && Tree.IsFocused)
        {
            _entityCommands["rename"].Execute(); SynchronizeRenameFocus(context);
            return true;
        }
        if (HandleEntityInput(input, context)) return true;
        if (input.Kind != InputKind.KeyDown || input.Key != UiKey.Escape || !Tree.IsFocused) return false;
        _model.Select(null); return true;
    }
    private void Move(TreeMove move) => _model.Move(move.Id, move.ParentId);
    private void Place(TreePlacement move) => _model.Place(move.Id, move.ParentId, move.AnchorId,
        move.Position == TreeDropPosition.Before ? EntityPlacement.Before : EntityPlacement.After);
    private void NameFocusChanged(Element element) { if (!element.IsFocused && _model.IsRenaming) _model.CancelRename(); }
    private void Changed(object? sender, PropertyChangedEventArgs args)
    {
        RefreshEntityCommands(args.PropertyName);
        if (args.PropertyName is nameof(EditorHierarchyViewModel.SnapshotVersion) or nameof(EditorHierarchyViewModel.SelectedId) or nameof(EditorHierarchyViewModel.CanMove)) RequestContextClose();
        if (args.PropertyName == nameof(EditorHierarchyViewModel.SnapshotVersion)) Dispatch(Tree.CancelMove);
        if (args.PropertyName is nameof(EditorHierarchyViewModel.Items) or nameof(EditorHierarchyViewModel.SelectedId)) ScheduleTreeSync();
    }
    private void TreeAttachmentChanged() { _treeSyncPending = false; if (IsAttached) ScheduleTreeSync(); }
    private void ScheduleTreeSync()
    {
        if (_treeSyncPending) return; _treeSyncPending = true;
        Dispatch(() => { _treeSyncPending = false; SynchronizeItems(); });
    }
    private void SynchronizeItems()
    {
        var items = _model.Items; var selection = _model.SelectedId;
        _synchronizingItems = true;
        try
        {
            if (!ReferenceEquals(_observedItems, items))
            {
                Tree.SetItems(items.Select(item => new TreeItem(item.Id, item.ParentId, item.Name, item.Kind switch
                {
                    HierarchyItemKind.Renderable => UiSymbol.Cube, HierarchyItemKind.Interactive => UiSymbol.Interaction, _ => UiSymbol.Entity
                })));
                _observedItems = items;
            }
            Tree.Select(selection);
        }
        finally { _synchronizingItems = false; }
    }
    private sealed class Release(Action release) : IDisposable { public void Dispose() => release(); }
    private sealed class RenameLayer : ElementList
    {
        private readonly TreeView _tree;
        private readonly TextBox _input;
        private readonly EditorHierarchyViewModel _model;
        public RenameLayer(TreeView tree, TextBox input, EditorHierarchyViewModel model)
        { _tree = tree; _input = input; _model = model; Add(tree); Add(input); }
        protected override Size MeasureCore(Size available, ITextLayoutService text)
        {
            _tree.Measure(available, text); if (_input.IsVisible) _input.Measure(available, text);
            return _tree.DesiredSize;
        }
        protected override void ArrangeCore(Rect area)
        {
            _tree.Arrange(area);
            if (_model.RenameId is not { } id || _tree.GetItemBounds(id) is not { } row) { _input.Arrange(default); return; }
            var depth = 0; var item = _tree.Items.First(value => value.Id == id);
            while (item.ParentId != null) { depth++; item = _tree.Items.First(value => value.Id == item.ParentId); }
            var offset = Math.Min(depth * _tree.Indent, Math.Max(0, row.Width - 36)) + 34;
            _input.Arrange(new(row.X + offset, row.Y, Math.Max(0, row.Width - offset), row.Height));
        }
    }
    private sealed class HierarchyLayout : ElementList
    {
        public HierarchyLayout(params Element[] children) { foreach (var child in children) Add(child); }
        protected override Size MeasureCore(Size available, ITextLayoutService text)
        {
            for (var i = 0; i < 7; i++) Children[i].Measure(available, text);
            var header = Math.Max(Children[0].DesiredSize.Height, Children[1].DesiredSize.Height);
            var fixedHeight = header + Children.Skip(2).Take(5).Sum(child => child.IsVisible ? child.DesiredSize.Height + Get(UiProperties.Spacing) : 0)
                + Get(UiProperties.Spacing);
            Children[7].Measure(new(available.Width, Math.Max(0, available.Height - fixedHeight)), text);
            return new(Math.Min(240, available.Width), Math.Min(available.Height, fixedHeight + Children[7].DesiredSize.Height));
        }
        protected override void ArrangeCore(Rect area)
        {
            var remaining = area.Height; var y = area.Y;
            var header = Math.Min(remaining, Math.Max(Children[0].DesiredSize.Height, Children[1].DesiredSize.Height));
            var button = Math.Min(area.Width, Children[1].DesiredSize.Width);
            Children[0].Arrange(new(area.X, y, Math.Max(0, area.Width - button - 4), header));
            Children[1].Arrange(new(area.X + area.Width - button, y, button, header));
            y += header; remaining -= header;
            for (var i = 2; i < 7; i++)
            {
                var child = Children[i]; if (!child.IsVisible) { child.Arrange(default); continue; }
                var gap = Math.Min(remaining, Get(UiProperties.Spacing)); y += gap; remaining -= gap;
                var height = Math.Min(remaining, child.DesiredSize.Height);
                child.Arrange(new(area.X, y, area.Width, height)); y += height; remaining -= height;
            }
            var lastGap = Math.Min(remaining, Get(UiProperties.Spacing)); y += lastGap; remaining -= lastGap;
            Children[7].Arrange(new(area.X, y, area.Width, remaining));
        }
    }
}
