using System.ComponentModel;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.Hierarchy;

/// <summary>Parent-owned hierarchy UI over borrowed App presentation and style.</summary>
public sealed class EditorHierarchyView : View<EditorHierarchyViewModel>
{
    private readonly EditorHierarchyViewModel _model;
    /// <summary>General UI tree; owns only display/selection/expansion/gesture state.</summary>
    public TreeView Tree { get; } = new();
    /// <summary>Viewport retaining scroll position across theme and data refresh.</summary>
    public ScrollViewer Scroll { get; } = new();
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
            Caption = Text.Plain().BindText(model, value => value.Caption);
            RootButton = Button.Simple("В корень").Bind(UiProperties.Enabled, model, value => value.CanMoveToRoot).OnClick(model.MoveToRoot);
            Hint = Text.Plain().BindText(model, value => value.Hint);
            Error = new(new RichTextEditorOptions { IsReadOnly = true, WordWrap = true });
            Error.Bind(RichTextEditor.TextProperty, model, value => value.ErrorText).Bind(UiProperties.Visible, model, value => value.HasError);
            Tree.AccessibleName = "Обозреватель сущностей"; Tree.Bind(TreeView.MoveEnabledProperty, model, value => value.CanMove);
            Scroll.SetContent(Tree); Layout = new HierarchyLayout(Title, RootButton, Caption, Hint, Error, Scroll);
            Content(model, _ => Layout);
            SynchronizeItems();
            Tree.SelectionChanged += Select; Tree.MoveRequested += Move;
            model.PropertyChanged += Changed;
            Own(new Release(() => { model.PropertyChanged -= Changed; Tree.SelectionChanged -= Select; Tree.MoveRequested -= Move; }));
            this.WithViewStyle(style);
        }
        catch (Exception original)
        {
            try { Dispose(); } catch (Exception cleanup) { throw new AggregateException(original, cleanup); }
            throw;
        }
    }
    private void Select(string? id) => _model.Select(id);
    /// <summary>Clears selection with Esc after the tree has had a chance to cancel a drag.</summary>
    public override bool HandleInput(UiInput input, IInputContext context)
    {
        if (input.Kind != InputKind.KeyDown || input.Key != UiKey.Escape || !Tree.IsFocused) return false;
        _model.Select(null); return true;
    }
    private void Move(TreeMove move) => _model.Move(move.Id, move.ParentId);
    private void Changed(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(EditorHierarchyViewModel.SnapshotVersion)) Dispatch(Tree.CancelMove);
        if (args.PropertyName == nameof(EditorHierarchyViewModel.Items)) Dispatch(SynchronizeItems);
        if (args.PropertyName == nameof(EditorHierarchyViewModel.SelectedId)) Dispatch(() => Tree.Select(_model.SelectedId));
    }
    private void SynchronizeItems()
    {
        Tree.SetItems(_model.Items.Select(item => new TreeItem(item.Id, item.ParentId, item.Name, item.Kind switch
        {
            HierarchyItemKind.Renderable => UiSymbol.Cube, HierarchyItemKind.Interactive => UiSymbol.Interaction, _ => UiSymbol.Entity
        })));
        Tree.Select(_model.SelectedId);
    }
    private sealed class Release(Action release) : IDisposable { public void Dispose() => release(); }
    private sealed class HierarchyLayout : ElementList
    {
        public HierarchyLayout(params Element[] children) { foreach (var child in children) Add(child); }
        protected override Size MeasureCore(Size available, ITextLayoutService text)
        {
            for (var i = 0; i < 5; i++) Children[i].Measure(available, text);
            var header = Math.Max(Children[0].DesiredSize.Height, Children[1].DesiredSize.Height);
            var fixedHeight = header + Children.Skip(2).Take(3).Sum(child => child.IsVisible ? child.DesiredSize.Height + Get(UiProperties.Spacing) : 0)
                + Get(UiProperties.Spacing);
            Children[5].Measure(new(available.Width, Math.Max(0, available.Height - fixedHeight)), text);
            return new(Math.Min(240, available.Width), Math.Min(available.Height, fixedHeight + Children[5].DesiredSize.Height));
        }
        protected override void ArrangeCore(Rect area)
        {
            var remaining = area.Height; var y = area.Y;
            var header = Math.Min(remaining, Math.Max(Children[0].DesiredSize.Height, Children[1].DesiredSize.Height));
            var button = Math.Min(area.Width, Children[1].DesiredSize.Width);
            Children[0].Arrange(new(area.X, y, Math.Max(0, area.Width - button - 4), header));
            Children[1].Arrange(new(area.X + area.Width - button, y, button, header));
            y += header; remaining -= header;
            for (var i = 2; i < 5; i++)
            {
                var child = Children[i]; if (!child.IsVisible) { child.Arrange(default); continue; }
                var gap = Math.Min(remaining, Get(UiProperties.Spacing)); y += gap; remaining -= gap;
                var height = Math.Min(remaining, child.DesiredSize.Height);
                child.Arrange(new(area.X, y, area.Width, height)); y += height; remaining -= height;
            }
            var lastGap = Math.Min(remaining, Get(UiProperties.Spacing)); y += lastGap; remaining -= lastGap;
            Children[5].Arrange(new(area.X, y, area.Width, remaining));
        }
    }
}
