using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class TreeViewTests
{
    private static TreeItem[] Graph() =>
    [
        new("leaf", "branch", "Leaf", UiSymbol.Interaction),
        new("root", null, "Root", UiSymbol.Entity),
        new("branch", "root", "Branch", UiSymbol.Cube),
        new("sibling", "root", "Sibling"),
        new("other", null, "Other", UiSymbol.Entity)
    ];

    [Fact]
    public void SetItems_FreezesCallerDataAndFlattensUnorderedHierarchyBySiblingOrder()
    {
        using var tree = new TreeView();
        var items = Graph().ToList(); tree.SetItems(items);
        tree.SetExpanded("root", true); tree.SetExpanded("branch", true);
        items.Clear();

        Assert.Equal(new[] { "leaf", "root", "branch", "sibling", "other" }, tree.Items.Select(item => item.Id));
        Assert.Equal(new[] { "root", "branch", "leaf", "sibling", "other" }, tree.VisibleItems.Select(item => item.Id));
        Assert.Equal(UiSymbol.Interaction, tree.Items.Single(item => item.Id == "leaf").Icon);
        tree.SetExpanded("branch", false);
        Assert.Equal(new[] { "root", "branch", "sibling", "other" }, tree.VisibleItems.Select(item => item.Id));
        Assert.Null(tree.GetItemBounds("leaf"));
        tree.SetExpanded("root", false);
        Assert.Equal(new[] { "root", "other" }, tree.VisibleItems.Select(item => item.Id));
        Assert.False(tree.IsExpanded("root")); Assert.False(tree.IsExpanded("branch"));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("empty")]
    [InlineData("missing-parent")]
    [InlineData("self-parent")]
    [InlineData("cycle")]
    [InlineData("invalid-icon")]
    [InlineData("blank-id")]
    [InlineData("multiline")]
    [InlineData("null-item")]
    public void SetItems_InvalidGraphPreservesItemsSelectionExpansionAndNotifications(string invalid)
    {
        using var tree = new TreeView(); tree.SetItems(Graph()); tree.Select("leaf");
        var before = tree.Items.ToArray(); var visible = tree.VisibleItems.ToArray();
        var changes = 0; tree.SelectionChanged += _ => changes++; tree.ExpansionChanged += (_, _) => changes++;
        TreeItem[] candidate = invalid switch
        {
            "duplicate" => [new("same", null, "First"), new("same", null, "Second")],
            "empty" => [new("", null, "Empty")],
            "missing-parent" => [new("a", "missing", "A")],
            "self-parent" => [new("a", "a", "A")],
            "cycle" => [new("a", "b", "A"), new("b", "a", "B")],
            "blank-id" => [new("  ", null, "Blank")],
            "multiline" => [new("a", null, "Line\nbreak")],
            "null-item" => [null!],
            _ => [new("a", null, "A", (UiSymbol)int.MaxValue)]
        };

        Assert.ThrowsAny<ArgumentException>(() => tree.SetItems(candidate));
        Assert.Equal(before, tree.Items); Assert.Equal(visible, tree.VisibleItems);
        Assert.Equal("leaf", tree.SelectedId); Assert.True(tree.IsExpanded("root")); Assert.True(tree.IsExpanded("branch"));
        Assert.Equal(0, changes);
    }

    [Fact]
    public void Collapse_SelectedDescendantSelectsBranchAndRemovedSelectionClearsExactlyOnce()
    {
        using var tree = new TreeView(); tree.SetItems(Graph()); tree.Select("leaf");
        var selected = new List<string?>(); var expanded = new List<(string, bool)>();
        tree.SelectionChanged += selected.Add; tree.ExpansionChanged += (id, value) => expanded.Add((id, value));
        tree.SetExpanded("branch", false);
        Assert.Equal("branch", tree.SelectedId); Assert.Equal(new[] { "branch" }, selected);
        Assert.Equal(("branch", false), Assert.Single(expanded)); Assert.Null(tree.GetItemBounds("leaf"));
        tree.SetExpanded("branch", false); Assert.Single(selected); Assert.Single(expanded);
        tree.SetItems([new("other", null, "Other")]);
        Assert.Null(tree.SelectedId); Assert.Equal(new string?[] { "branch", null }, selected);
        Assert.Equal("other", Assert.Single(tree.VisibleItems).Id);
        Assert.Throws<ArgumentException>(() => tree.Select("missing"));
        Assert.Throws<ArgumentException>(() => tree.SetExpanded("missing", true));
        Assert.Null(tree.SelectedId); Assert.Equal(2, selected.Count);
    }

    [Fact]
    public void SetItems_ExactCapacityAcceptedAndOverflowOrEnumerationFailurePreservesPreviousSnapshot()
    {
        using var tree = new TreeView();
        tree.SetItems(Enumerable.Range(0, 4096).Select(index => new TreeItem(index.ToString(), null, "Item " + index)));
        tree.Select("4095"); var before = tree.Items.ToArray();
        Assert.Equal(4096, tree.Items.Count); Assert.Equal(4096, tree.VisibleItems.Count);
        Assert.ThrowsAny<ArgumentException>(() => tree.SetItems(Enumerable.Range(0, 4097).Select(index => new TreeItem(index.ToString(), null, "Item"))));
        var failure = new IOException("item source failed");
        Assert.Same(failure, Assert.Throws<IOException>(() => tree.SetItems(Broken())));
        Assert.Equal(before, tree.Items); Assert.Equal("4095", tree.SelectedId);
        tree.SetItems([]); Assert.Empty(tree.Items); Assert.Empty(tree.VisibleItems); Assert.Null(tree.SelectedId);
        Assert.Null(tree.GetItemBounds("4095"));

        IEnumerable<TreeItem> Broken() { yield return new("new", null, "New"); throw failure; }
    }

    [Fact]
    public void Select_ExpandsAncestorsRevealsDeepRowAndRefreshRetainsSelectionExpansionAndFocusById()
    {
        var tree = new TreeView(); tree.SetItems(Enumerable.Range(0, 20).Select(index =>
            new TreeItem(index.ToString(), index == 0 ? null : (index - 1).ToString(), "Node " + index)));
        var scroll = new ScrollViewer().Content(tree); using var ui = Create(scroll, 240, 96); ui.Update(); ui.Focus(tree);
        var selected = new List<string?>(); tree.SelectionChanged += selected.Add;
        tree.Select("19"); ui.Update(); ui.Update();
        Assert.Equal("19", tree.SelectedId); Assert.Equal(new[] { "19" }, selected);
        Assert.Equal(20, tree.VisibleItems.Count); Assert.True(tree.IsExpanded("0")); Assert.True(tree.IsExpanded("18"));
        var row = tree.GetItemBounds("19")!.Value;
        Assert.True(scroll.Offset > 0); Assert.True(row.Y >= scroll.ChildClipBounds.Y);
        Assert.True(row.Y + row.Height <= scroll.ChildClipBounds.Y + scroll.ChildClipBounds.Height);
        var offset = scroll.Offset;
        tree.SetItems(tree.Items.Select(item => item with { Text = item.Text + " updated" })); ui.SetTheme(Theme.Light); ui.Update();
        Assert.Equal("19", tree.SelectedId); Assert.True(tree.IsExpanded("18")); Assert.True(tree.IsFocused);
        Assert.Equal(offset, scroll.Offset); Assert.Single(selected);
        tree.Select("19"); Assert.Single(selected);
        tree.Select(null); Assert.Null(tree.SelectedId); Assert.Equal(new string?[] { "19", null }, selected);
    }

    [Fact]
    public void Keyboard_VisibleNavigationExpandsCollapsesAndSelectsParentWithoutMovingGraph()
    {
        var tree = new TreeView(); tree.SetItems(Graph()); tree.SetExpanded("root", false);
        using var ui = Create(tree); ui.Update(); ui.Focus(tree);
        Key(ui, UiKey.Home); Assert.Equal("root", tree.SelectedId);
        Key(ui, UiKey.Right); Assert.True(tree.IsExpanded("root")); Assert.Equal("root", tree.SelectedId);
        Key(ui, UiKey.Right); Assert.Equal("branch", tree.SelectedId);
        Key(ui, UiKey.Right); Assert.True(tree.IsExpanded("branch")); Assert.Equal("branch", tree.SelectedId);
        Key(ui, UiKey.Right); Assert.Equal("leaf", tree.SelectedId);
        Key(ui, UiKey.Down); Assert.Equal("sibling", tree.SelectedId);
        Key(ui, UiKey.Up); Assert.Equal("leaf", tree.SelectedId);
        Key(ui, UiKey.Left); Assert.Equal("branch", tree.SelectedId);
        Key(ui, UiKey.Left); Assert.False(tree.IsExpanded("branch")); Assert.Equal("branch", tree.SelectedId);
        Key(ui, UiKey.Left); Assert.Equal("root", tree.SelectedId);
        Key(ui, UiKey.End); Assert.Equal("other", tree.SelectedId);
        Key(ui, UiKey.Down); Assert.Equal("other", tree.SelectedId);
        Key(ui, UiKey.Home); Key(ui, UiKey.Up); Assert.Equal("root", tree.SelectedId);
        Assert.Equal("branch", tree.Items.Single(item => item.Id == "leaf").ParentId);
    }

    [Fact]
    public void Keyboard_IndentAndOutdentPublishTypedRequestsWhileKeepingModelImmutable()
    {
        var tree = new TreeView { MoveEnabled = true }; tree.SetItems(Graph()); tree.Select("sibling");
        using var ui = Create(tree); ui.Update(); ui.Focus(tree);
        var moves = new List<TreeMove>(); tree.MoveRequested += moves.Add;
        Key(ui, UiKey.Right, control: true);
        Assert.Equal(new TreeMove("sibling", "branch"), Assert.Single(moves));
        Assert.Equal("root", tree.Items.Single(item => item.Id == "sibling").ParentId);
        tree.Select("leaf"); Key(ui, UiKey.Left, control: true);
        Assert.Equal(new TreeMove("leaf", "root"), moves[1]);
        tree.Select("root"); Key(ui, UiKey.Left, control: true); Key(ui, UiKey.Right, control: true);
        Assert.Equal(2, moves.Count);
        tree.MoveEnabled = false; tree.Select("sibling"); Key(ui, UiKey.Right, control: true);
        Assert.Equal(2, moves.Count); Assert.Equal("sibling", tree.SelectedId);
    }

    [Fact]
    public void Pointer_ClickSelectsAndDisclosureTogglesWithoutRequestingMove()
    {
        var tree = new TreeView { MoveEnabled = true }; tree.SetItems(Graph()); tree.SetExpanded("root", false);
        using var ui = Create(tree); ui.Update(); var moves = new List<TreeMove>(); tree.MoveRequested += moves.Add;
        var root = Row(tree, "root"); Click(ui, root.X + 8, root.Y + root.Height / 2); ui.Update();
        Assert.True(tree.IsExpanded("root"));
        var leaf = Row(tree, "branch"); Click(ui, leaf.X + leaf.Width - 8, leaf.Y + leaf.Height / 2);
        Assert.Equal("branch", tree.SelectedId); Assert.True(tree.IsFocused); Assert.Empty(moves);
        Assert.False(ui.HandleInput(new(InputKind.PointerUp, leaf.X + 30, leaf.Y + 5)).PointerCaptured);
    }

    [Fact]
    public void PointerDrag_ParentAndBlankRootDropsPublishRequestsWithoutChangingItems()
    {
        var tree = new TreeView { MoveEnabled = true }; tree.SetItems(Graph()); tree.SetExpanded("root", true);
        using var ui = Create(tree, 240, 240); ui.Update(); var moves = new List<TreeMove>(); tree.MoveRequested += moves.Add;
        Drag(ui, tree, "sibling", Row(tree, "other"));
        Assert.Equal(new TreeMove("sibling", "other"), Assert.Single(moves));
        Assert.Equal("root", tree.Items.Single(item => item.Id == "sibling").ParentId);
        var branch = Row(tree, "branch");
        var capture = ui.HandleInput(new(InputKind.PointerDown, branch.X + 80, branch.Y + 12)); Assert.True(capture.PointerCaptured);
        ui.HandleInput(new(InputKind.PointerMove, branch.X + 80, 220));
        Assert.False(ui.HandleInput(new(InputKind.PointerUp, branch.X + 80, 220)).PointerCaptured);
        Assert.Equal(new TreeMove("branch", null), moves[1]);
        Assert.Equal("root", tree.Items.Single(item => item.Id == "branch").ParentId);
        Assert.Equal("branch", tree.SelectedId);
    }

    [Fact]
    public void PointerDrag_ThresholdRejectsClicksSelfDescendantSameParentAndOutsideBounds()
    {
        var tree = new TreeView { MoveEnabled = true }; tree.SetItems(Graph()); tree.Select("leaf");
        using var ui = Create(tree, 240, 240); ui.Update(); var moves = new List<TreeMove>(); tree.MoveRequested += moves.Add;
        var other = Row(tree, "other");
        ui.HandleInput(new(InputKind.PointerDown, other.X + 80, other.Y + 12));
        ui.HandleInput(new(InputKind.PointerMove, other.X + 83.9f, other.Y + 12));
        ui.HandleInput(new(InputKind.PointerUp, other.X + 83.9f, other.Y + 12)); Assert.Empty(moves);
        Drag(ui, tree, "root", Row(tree, "leaf")); Drag(ui, tree, "leaf", Row(tree, "leaf"));
        Drag(ui, tree, "leaf", Row(tree, "branch")); Drag(ui, tree, "root", new(-20, -20, 1, 1));
        Assert.Empty(moves); Assert.Equal(Graph(), tree.Items);
    }

    [Theory]
    [InlineData(3.99f, false)]
    [InlineData(4f, true)]
    [InlineData(4.01f, true)]
    public void PointerDrag_FourLogicalPixelBoundaryDeterminesWhetherCrossedRowRequestsMove(float distance, bool expected)
    {
        var tree = new TreeView { MoveEnabled = true }; tree.SetItems([new("a", null, "A"), new("b", null, "B")]);
        using var ui = Create(tree); ui.Update(); var moves = new List<TreeMove>(); tree.MoveRequested += moves.Add;
        ui.HandleInput(new(InputKind.PointerDown, 80, 22));
        ui.HandleInput(new(InputKind.PointerMove, 80, 22 + distance));
        Assert.False(ui.HandleInput(new(InputKind.PointerUp, 80, 22 + distance)).PointerCaptured);
        if (expected) Assert.Equal(new TreeMove("a", "b"), Assert.Single(moves)); else Assert.Empty(moves);
        Assert.Null(tree.Items[0].ParentId); Assert.Equal("a", tree.SelectedId);
    }

    [Fact]
    public void DisabledMovingAndNonprimaryPointerKeepSelectionAvailableWithoutCaptureOrRequests()
    {
        var tree = new TreeView(); tree.SetItems([new("a", null, "A"), new("b", null, "B")]);
        using var ui = Create(tree); ui.Update(); var moves = new List<TreeMove>(); tree.MoveRequested += moves.Add;
        Assert.False(ui.HandleInput(new(InputKind.PointerDown, 80, 12, Button: 1)).PointerCaptured); Assert.Null(tree.SelectedId);
        Assert.False(ui.HandleInput(new(InputKind.PointerDown, 80, 12)).PointerCaptured); Assert.Equal("a", tree.SelectedId);
        ui.HandleInput(new(InputKind.PointerMove, 80, 36)); ui.HandleInput(new(InputKind.PointerUp, 80, 36)); Assert.Empty(moves);
        tree.IsEnabled = false; ui.Update(); Click(ui, 80, 36); Assert.Equal("a", tree.SelectedId); Assert.Empty(moves);
        tree.IsEnabled = true; tree.SetItems([]); ui.Update(); ui.Focus(tree);
        foreach (var key in new[] { UiKey.Up, UiKey.Down, UiKey.Left, UiKey.Right, UiKey.Home, UiKey.End }) Key(ui, key);
        Assert.Null(tree.SelectedId); Assert.Empty(moves);
    }

    [Theory]
    [InlineData("escape")]
    [InlineData("focus-lost")]
    [InlineData("disable")]
    [InlineData("move-disabled")]
    [InlineData("replace-items")]
    [InlineData("explicit")]
    [InlineData("detach")]
    public void PointerDrag_CancellationFencesLateReleaseAndReleasesCapture(string cancel)
    {
        var tree = new TreeView { MoveEnabled = true }; tree.SetItems(Graph()); tree.SetExpanded("root", true);
        var panel = new Panel().WithChildren(tree); using var ui = Create(panel, 240, 240); ui.Update();
        var moves = new List<TreeMove>(); tree.MoveRequested += moves.Add; var source = Row(tree, "sibling"); var target = Row(tree, "other");
        ui.HandleInput(new(InputKind.PointerDown, source.X + 80, source.Y + 12));
        Assert.True(ui.HandleInput(new(InputKind.PointerMove, target.X + 80, target.Y + 12)).PointerCaptured);
        switch (cancel)
        {
            case "escape": Key(ui, UiKey.Escape); break;
            case "focus-lost": ui.HandleInput(new(InputKind.FocusLost)); break;
            case "disable": tree.IsEnabled = false; break;
            case "move-disabled": tree.MoveEnabled = false; break;
            case "replace-items": tree.SetItems(Graph()); break;
            case "explicit": tree.CancelMove(); break;
            case "detach": panel.Items.Remove(tree); break;
        }
        ui.Update();
        var released = ui.HandleInput(new(InputKind.PointerUp, target.X + 80, target.Y + 12));
        Assert.False(released.PointerCaptured); Assert.Empty(moves);
        Assert.Equal("root", tree.Items.Single(item => item.Id == "sibling").ParentId);
        if (cancel == "detach") tree.Dispose();
    }

    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(128.01f)] [InlineData(float.NaN)] [InlineData(float.PositiveInfinity)]
    public void RowHeight_InvalidValuePreservesLayoutAndProperty(float value)
    {
        using var tree = new TreeView(); tree.SetItems([new("a", null, "A"), new("b", null, "B")]);
        tree.Measure(new(240, 240), new FixedTextLayout()); tree.Arrange(new(0, 0, 240, 240));
        var before = Row(tree, "b");
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.RowHeight = value);
        Assert.Equal(24, tree.RowHeight); Assert.Equal(before, Row(tree, "b"));
    }

    [Theory]
    [InlineData(-1)] [InlineData(64.01f)] [InlineData(float.NaN)] [InlineData(float.PositiveInfinity)]
    public void Indent_InvalidValuePreservesProperty(float value)
    {
        using var tree = new TreeView();
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.Indent = value); Assert.Equal(16, tree.Indent);
    }

    [Fact]
    public void Layout_ValidBoundaryValuesAndEmptyTreeHaveFiniteRowGeometry()
    {
        using var tree = new TreeView { RowHeight = 128, Indent = 64 };
        tree.SetItems([new("a", null, "A"), new("b", "a", "B")]); tree.SetExpanded("a", true);
        tree.Measure(new(240, 400), new FixedTextLayout()); tree.Arrange(new(10, 20, 240, 400));
        Assert.Equal(128, Row(tree, "a").Height); Assert.Equal(148, Row(tree, "b").Y);
        tree.RowHeight = 1; tree.Indent = 0;
        tree.Measure(new(240, 400), new FixedTextLayout()); tree.Arrange(new(10, 20, 240, 400));
        Assert.Equal(1, Row(tree, "a").Height); Assert.Equal(21, Row(tree, "b").Y);
        tree.SetItems([]); tree.Measure(new(240, 400), new FixedTextLayout()); tree.Arrange(new(10, 20, 240, 400));
        Assert.Empty(tree.VisibleItems); Assert.Null(tree.GetItemBounds("a")); Assert.True(float.IsFinite(tree.DesiredSize.Height));
    }

    [Fact]
    public void Draw_DisclosureAndPerItemIconsUseDeclaredSymbolsAndHierarchyDepth()
    {
        using var tree = new TreeView(); tree.SetItems([new("root", null, "Root", UiSymbol.Cube), new("child", "root", "Child", UiSymbol.Interaction)]);
        tree.Measure(new(240, 100), new FixedTextLayout()); tree.Arrange(new(0, 0, 240, 100));
        var collapsed = new Drawing(); tree.Draw(collapsed); var expectedCollapsed = new Drawing();
        UiSymbols.Draw(expectedCollapsed, UiSymbol.ChevronRight, new(0, 5, 14, 14), tree.Foreground);
        UiSymbols.Draw(expectedCollapsed, UiSymbol.Cube, new(16, 5, 14, 14), tree.Foreground);
        Assert.Equal(expectedCollapsed.Shapes, collapsed.Shapes); Assert.Equal(new[] { "Root" }, collapsed.Texts);
        tree.SetExpanded("root", true); tree.Measure(new(240, 100), new FixedTextLayout()); tree.Arrange(new(0, 0, 240, 100));
        var expanded = new Drawing(); tree.Draw(expanded); var expectedExpanded = new Drawing();
        UiSymbols.Draw(expectedExpanded, UiSymbol.ChevronDown, new(0, 5, 14, 14), tree.Foreground);
        UiSymbols.Draw(expectedExpanded, UiSymbol.Cube, new(16, 5, 14, 14), tree.Foreground);
        UiSymbols.Draw(expectedExpanded, UiSymbol.Interaction, new(32, 29, 14, 14), tree.Foreground);
        Assert.Equal(expectedExpanded.Shapes, expanded.Shapes); Assert.Equal(new[] { "Root", "Child" }, expanded.Texts);
        Assert.Equal(expanded.Saves, expanded.Restores);
    }

    [Fact]
    public void Draw_ScrolledRowsAreCulledAndSymbolsAreDistinctVectorGeometry()
    {
        var tree = new TreeView(); tree.SetItems(Enumerable.Range(0, 100).Select(index => new TreeItem(index.ToString(), null, "Node " + index, UiSymbol.Entity)));
        var scroll = new ScrollViewer().Content(tree); using var ui = Create(scroll, 240, 72); ui.Update(); scroll.Offset = 24 * 50; ui.Update();
        var drawing = new Drawing(); scroll.Draw(drawing);
        Assert.Equal(new[] { "Node 50", "Node 51", "Node 52" }, drawing.Texts);
        Assert.Equal(drawing.Saves, drawing.Restores); Assert.NotEmpty(drawing.Clips);
        var icons = new[] { UiSymbol.Cube, UiSymbol.Entity, UiSymbol.Interaction, UiSymbol.ChevronRight, UiSymbol.ChevronDown };
        var shapes = icons.Select(icon => { var canvas = new Drawing(); UiSymbols.Draw(canvas, icon, new(0, 0, 14, 14), Theme.Dark.Foreground); Assert.NotEmpty(canvas.Shapes); return string.Join("|", canvas.Shapes); }).ToArray();
        Assert.Equal(icons.Length, shapes.Distinct().Count());
    }

    private static Rect Row(TreeView tree, string id) => tree.GetItemBounds(id) ?? throw new InvalidOperationException("Row is not visible: " + id);
    private static void Key(UiSession ui, UiKey key, bool control = false) => ui.HandleInput(new(InputKind.KeyDown, Key: key, Control: control));
    private static void Click(UiSession ui, float x, float y) { ui.HandleInput(new(InputKind.PointerDown, x, y)); ui.HandleInput(new(InputKind.PointerUp, x, y)); }
    private static void Drag(UiSession ui, TreeView tree, string id, Rect target)
    {
        var source = Row(tree, id); ui.HandleInput(new(InputKind.PointerDown, source.X + 80, source.Y + source.Height / 2));
        ui.HandleInput(new(InputKind.PointerMove, target.X + Math.Min(80, target.Width / 2), target.Y + target.Height / 2));
        Assert.False(ui.HandleInput(new(InputKind.PointerUp, target.X + Math.Min(80, target.Width / 2), target.Y + target.Height / 2)).PointerCaptured);
    }
    private static UiSession Create(Element root, int width = 240, int height = 240) => new(root, new Surface(), new(width, height));
    private sealed class Surface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new FixedTextLayout();
        public void VerifyAvailable() { }
        public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport) { }
        public void Dispose() { }
    }
    private sealed class Drawing : IDrawingContext
    {
        public int Saves, Restores;
        public List<string> Texts { get; } = [];
        public List<Rect> Clips { get; } = [];
        public List<string> Shapes { get; } = [];
        public void Save() => Saves++;
        public void Restore() => Restores++;
        public void Clip(Rect rectangle) => Clips.Add(rectangle);
        public void Translate(float x, float y) { }
        public void Fill(Rect rectangle, Color color, float radius = 0) => Shapes.Add("fill:" + rectangle);
        public void Stroke(Rect rectangle, Color color, float width = 1, float radius = 0) => Shapes.Add("stroke:" + rectangle);
        public void Text(string text, float x, float y, Color color, float fontSize, string fontFamily) => Texts.Add(text);
        public void Image(IImageSource source, Rect destination) { }
    }
}
