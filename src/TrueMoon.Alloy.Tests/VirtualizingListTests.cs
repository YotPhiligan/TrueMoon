using TrueMoon.Argentis;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Painting;
using TrueMoon.Alloy.Runtime;
using Xunit;

namespace TrueMoon.Alloy.Tests;

/// <summary>Viewport virtualization of a bound list: only the rows a <see cref="ScrollViewer"/> can show exist.</summary>
public sealed class VirtualizingListTests
{
    private const int Rows = 2496;
    private const float RowHeight = 20;

    private static UiSession Create(Element root) => new(root, new HeadlessSurface(), new UiViewport(240, 140));

    private sealed record Item(int Id, string Name);

    private sealed class Row : Element
    {
        public Item Item = null!;
        public override bool Focusable => true;
        protected override Size MeasureCore(Size available, ITextLayoutService text) => new(200, RowHeight);
    }

    private static (ScrollViewer Scroll, VirtualizingStack List, List<Item> Items, Func<int> Created) Scene(
        bool rebind = true)
    {
        var items = Enumerable.Range(0, Rows).Select(index => new Item(index, $"row {index}")).ToList();
        var created = 0;
        var list = new VirtualizingStack();
        if (rebind)
            list.BindVirtualizedItems(items,
                item =>
                {
                    created++;
                    return new Row { Item = item };
                },
                item => item.Id,
                (element, item) => ((Row)element).Item = item);
        else
            list.BindVirtualizedItems(items,
                item =>
                {
                    created++;
                    return new Row { Item = item };
                },
                item => item.Id);
        var scroll = new ScrollViewer();
        scroll.SetContent(list);
        return (scroll, list, items, () => created);
    }

    [Fact]
    public void WindowHoldsOnlyViewportRowsWhileExtentCoversTheWholeList()
    {
        var (scroll, list, _, created) = Scene();
        using var ui = Create(scroll);
        ui.Update();
        ui.Update();

        Assert.Equal(Rows, list.ItemCount);
        Assert.True(list.WindowLength is > 0 and < 40, $"window realized {list.WindowLength} rows");
        Assert.Equal(list.WindowLength, list.Items.Count);
        Assert.Equal(list.WindowLength, created());
        Assert.Equal(0, list.WindowStart);
        // Every row was measured at 20, so the estimate converges and the extent becomes exact.
        Assert.Equal(Rows * RowHeight, scroll.Extent, 3);
        Assert.All(list.Items, row => Assert.True(row.Bounds.Height > 0));
    }

    [Fact]
    public void ScrollingMovesWindowAndRebindsPooledRows()
    {
        var (scroll, list, _, created) = Scene();
        using var ui = Create(scroll);
        ui.Update();
        ui.Update();
        var narrow = list.WindowLength;
        var made = created();

        scroll.Offset = 4000;
        ui.Update();

        Assert.True(list.WindowStart > 100, $"window starts at {list.WindowStart}");
        // Only the rows the wider mid-list window needs are new: every row that left was re-bound.
        Assert.Equal(made + list.WindowLength - narrow, created());
        for (var index = 0; index < list.Items.Count; index++)
            Assert.Equal(list.WindowStart + index, ((Row)list.Items[index]).Item.Id);

        var top = (Row)list.Items[0];
        // A buffered window starts above the viewport by up to Buffer rows, so every visible row is realized.
        Assert.True(top.Bounds.Y >= -list.Buffer * RowHeight && top.Bounds.Y <= 0f, $"top row sits at {top.Bounds.Y}");
        Assert.Equal(Rows * RowHeight, scroll.Extent, 3);
    }

    [Fact]
    public void RowThatLeavesWindowIsPooledForReuseAndReleasedWithTheBinding()
    {
        var (scroll, list, _, created) = Scene();
        using var ui = Create(scroll);
        ui.Update();
        ui.Update();

        scroll.Offset = 4000;
        ui.Update();
        var wide = list.Items.ToArray();
        var made = created();

        scroll.Offset = 0;
        ui.Update();

        // The top window is shorter than a mid-list one, so the rows it does not need wait in the reuse pool.
        var live = new HashSet<Element>(list.Items);
        var pooled = wide.Where(row => !live.Contains(row)).ToArray();
        Assert.True(pooled.Length > 0, "the shorter window should leave rows in the pool");
        Assert.All(pooled, row => Assert.False(row.IsDisposed));
        Assert.All(pooled, row => Assert.False(row.IsAttached));
        Assert.Equal(made, created());

        // Pooled rows belong to the binding, not the tree: releasing the container releases them too.
        scroll.Dispose();
        Assert.All(pooled, row => Assert.True(row.IsDisposed));
    }

    [Fact]
    public void WithoutARebindRowsAreCreatedAndDisposedPerScrollStep()
    {
        var (scroll, list, _, created) = Scene(rebind: false);
        using var ui = Create(scroll);
        ui.Update();
        ui.Update();
        var first = created();
        var old = (Row)list.Items[^1];

        scroll.Offset = 8000;
        ui.Update();

        // Without a re-bind a row cannot point at another item, so scrolling costs a fresh subtree per row.
        Assert.Equal(first + list.WindowLength, created());
        Assert.True(old.IsDisposed);
        Assert.All(list.Items, row => Assert.Equal(((Row)row).Item.Name, $"row {((Row)row).Item.Id}"));
    }

    [Fact]
    public void SourceChangeOutsideWindowRealizesNoRows()
    {
        var (scroll, list, items, created) = Scene();
        using var ui = Create(scroll);
        ui.Update();
        scroll.Offset = 4000;
        ui.Update();
        var narrow = list.WindowLength;
        var first = created();

        items[^1] = new Item(9000, "far");
        list.RefreshItems();
        ui.Update();

        Assert.Equal(narrow, list.WindowLength);
        Assert.Equal(first, created());
        Assert.DoesNotContain(list.Items, row => ((Row)row).Item.Id == 9000);
        Assert.Equal(Rows * RowHeight, scroll.Extent, 3);
    }

    [Fact]
    public void SourceChangeInsideWindowRebindsTheRowOnNextLayout()
    {
        var (scroll, list, items, _) = Scene();
        using var ui = Create(scroll);
        ui.Update();
        ui.Update();
        var row = (Row)list.Items[1];

        items[1] = new Item(1, "replaced");
        list.RefreshItems();
        ui.Update();

        Assert.Same(row, list.Items[1]);
        Assert.Equal("replaced", row.Item.Name);
    }

    [Fact]
    public void ContainerOutsideScrollViewportRealizesEveryRow()
    {
        var items = Enumerable.Range(0, 12).Select(index => new Item(index, $"row {index}")).ToList();
        var list = new VirtualizingStack();
        list.BindVirtualizedItems(items, item => new Row { Item = item }, item => item.Id,
            (element, item) => ((Row)element).Item = item);
        using var ui = Create(list);
        ui.Update();

        Assert.Equal(12, list.Items.Count);
        Assert.Equal(12, list.WindowLength);
        Assert.Equal(0, list.WindowStart);
    }

    [Fact]
    public void ScrollToEndRealizesTheLastRows()
    {
        var (scroll, list, _, _) = Scene();
        using var ui = Create(scroll);
        ui.Update();

        scroll.ScrollToEnd();
        ui.Update();

        Assert.Equal(Rows, list.WindowStart + list.WindowLength);
        Assert.Equal(Rows - 1, ((Row)list.Items[^1]).Item.Id);
        Assert.True(list.Items[^1].Bounds.Y > 0);
    }

    [Fact]
    public void BufferExpandsTheRealizedWindow()
    {
        var (scroll, list, _, created) = Scene();
        using var ui = Create(scroll);
        ui.Update();
        ui.Update();
        var narrow = list.WindowLength;
        var made = created();

        list.Buffer = 0;
        ui.Update();

        // Without the buffer only the rows the viewport crosses stay realized.
        Assert.True(list.WindowLength < narrow, $"{list.WindowLength} of {narrow}");
        Assert.Equal(list.WindowLength, list.Items.Count);

        scroll.Offset = 4000;
        ui.Update();

        // The buffer only decides how many rows stay alive: a bufferless window still reuses what the pool holds.
        Assert.True(created() - made <= narrow, $"built {created() - made} rows for a {list.WindowLength}-row window");
        Assert.True(list.WindowLength < narrow, $"{list.WindowLength} of {narrow}");
    }

    [Fact]
    public void FocusedRowReleasesFocusWhenTheWindowMovesPastIt()
    {
        var (scroll, list, _, _) = Scene();
        using var ui = Create(scroll);
        ui.Update();
        ui.Update();

        var row = list.Items[1];
        ui.Focus(row);
        Assert.Same(row, ui.FocusedElement);

        scroll.Offset = 4_000;
        ui.Update();

        // The row left the window and went to the pool. It keeps its attachment, because a later window must be able
        // to reuse it without re-attaching the subtree, but focus leaves it together with its position in the tree:
        // the stale flag would otherwise travel to the next item the pooled row is re-bound to.
        Assert.True(row.IsAttached, "a pooled row keeps its attachment so reuse does not pay for re-attachment");
        Assert.False(row.IsFocused, $"window={list.WindowStart}+{list.WindowLength} offset={scroll.Offset}");
        // The window really moved: no realized row carries the old item, and the session has no focused element.
        Assert.DoesNotContain(1, list.Items.Select(item => ((Row)item).Item.Id));
        Assert.Null(ui.FocusedElement);
        Assert.NotEmpty(list.Items);
    }
    private sealed class HeadlessSurface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new FixedTextLayout();
        public void VerifyAvailable() { }
        public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport, UiPresenters? presenters = null) { }
        public void Dispose() { }
    }
}
