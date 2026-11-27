using TrueMoon.Argentis.Painting;

namespace TrueMoon.Argentis.Layout;

/// <summary>A vertical stack that realizes, measures and arranges only the rows a scrolling ancestor can show.</summary>
/// <remarks>
/// Rows outside the window and its buffer are not children at all, so they cost no construction, measure, arrange or
/// memory. The container still reports the full extent: a row keeps the height it was measured with, and a row that
/// was never measured is assumed to have the average of the measured ones. Without a <see cref="ScrollViewer"/>
/// ancestor nothing defines a window, so every row is realized. Rows cannot use <see cref="StackBase.FillProperty"/>.
/// </remarks>
public class VirtualizingStack : StackBase
{
    /// <summary>Rows kept realized on each side of the visible window.</summary>
    public static readonly UiProperty<int> BufferProperty = new("VirtualizationBuffer", 4, Invalidation.Layout, v => v >= 0 && v <= 256);
    /// <summary>Height assumed for a row before any row has been measured.</summary>
    public static readonly UiProperty<float> EstimatedRowHeightProperty = new("EstimatedRowHeight", 24, Invalidation.Layout, v => float.IsFinite(v) && v > 0f);
    /// <inheritdoc />
    internal override bool RealizesScrollWindow => true;

    private IVirtualizingItemsBinding? _source;
    private float[] _heights = [];
    private float _measuredHeight;
    private int _measuredRows;
    private float _windowTop;

    /// <summary>Rows kept realized on each side of the visible window; zero keeps only the window itself.</summary>
    public int Buffer { get => Get(BufferProperty); set => Set(BufferProperty, value); }
    /// <summary>Height assumed for rows that were never measured, until measured rows provide an average.</summary>
    public float EstimatedRowHeight { get => Get(EstimatedRowHeightProperty); set => Set(EstimatedRowHeightProperty, value); }
    /// <summary>First source row held as a child after the last measure.</summary>
    public int WindowStart { get; private set; }
    /// <summary>Number of rows held as children after the last measure.</summary>
    public int WindowLength { get; private set; }
    /// <summary>Row count of the bound source, or the current child count while unbound.</summary>
    public int ItemCount => _source?.ItemCount ?? Items.Count;

    internal void SetItemsSource(IVirtualizingItemsBinding source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (_source != null) throw new InvalidOperationException("A virtualizing container already has an item source.");
        _source = source;
    }

    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text)
    {
        var source = _source;
        if (source is null) return base.MeasureCore(available, text);

        var count = source.ItemCount;
        WindowStart = 0;
        WindowLength = 0;
        _windowTop = 0;
        if (count == 0)
        {
            source.PrepareWindow(0, 0);
            return default;
        }

        if (_heights.Length < count) Array.Resize(ref _heights, count);
        var spacing = Spacing;
        var estimate = Estimate();
        var viewport = FindViewport();
        var offset = viewport?.Offset ?? 0f;
        var bottom = viewport is null ? float.PositiveInfinity : offset + viewport.ViewportHeight;

        // One pass over every row: an unrealized row only costs an addition, and the pass is what keeps the
        // scrollbar extent honest while the container itself holds just the window.
        var first = count;
        var last = count - 1;
        var top = 0f;
        var cursor = 0f;
        var opened = false;
        var closed = false;
        for (var index = 0; index < count; index++)
        {
            var height = RowHeight(index, estimate);
            if (!opened && cursor + height > offset)
            {
                opened = true;
                first = index;
                top = cursor;
            }

            if (opened && !closed && cursor >= bottom)
            {
                closed = true;
                last = index - 1;
            }

            cursor += height + spacing;
        }

        float total;
        if (!opened) top = cursor;
        var buffered = Math.Max(0, first - Buffer);
        for (var index = first - 1; index >= buffered; index--) top -= RowHeight(index, estimate) + (index > 0 ? spacing : 0f);
        first = buffered;
        last = Math.Min(count - 1, last + Buffer);
        top = Math.Max(0, top);

        var length = Math.Max(0, Math.Min(last, count - 1) - first + 1);
        source.PrepareWindow(first, length);
        WindowStart = first;
        WindowLength = length;
        _windowTop = top;

        var width = 0f;
        var children = Children;
        for (var index = 0; index < children.Count; index++)
        {
            var child = children[index];
            child.Measure(available, text);
            var row = first + index;
            var height = child.DesiredSize.Height;
            if (_heights[row] <= 0f && height > 0f)
            {
                _measuredHeight += height;
                _measuredRows++;
            }

            _heights[row] = height;
            width = Math.Max(width, child.DesiredSize.Width);
        }

        // Measured rows can differ from the estimate that located the window, so the extent is summed again with the
        // heights this pass actually knows: the scrollbar stays exact while only the window is realized.
        var known = Estimate();
        total = (count - 1) * spacing;
        for (var index = 0; index < count; index++) total += RowHeight(index, known);

        return new Size(Math.Min(available.Width, width), total);
    }

    /// <inheritdoc />
    protected override void ArrangeCore(Rect area)
    {
        var source = _source;
        if (source is null)
        {
            base.ArrangeCore(area);
            return;
        }

        var spacing = Spacing;
        var y = area.Y + _windowTop;
        var children = Children;
        for (var index = 0; index < children.Count; index++)
        {
            var child = children[index];
            var height = _heights[WindowStart + index];
            var width = child.HorizontalAlignment == LayoutAlignment.Stretch ? area.Width : child.DesiredSize.Width;
            var left = child.HorizontalAlignment switch
            {
                LayoutAlignment.Center => area.X + (area.Width - width) / 2,
                LayoutAlignment.End => area.X + area.Width - width,
                _ => area.X,
            };
            child.Arrange(new Rect(left, y, width, height));
            y += height + spacing;
        }
    }

    private float Estimate() => _measuredRows != 0 ? _measuredHeight / _measuredRows : EstimatedRowHeight;

    private float RowHeight(int index, float estimate) => _heights[index] > 0f ? _heights[index] : estimate;

    private ScrollViewer? FindViewport()
    {
        for (var node = Parent; node != null; node = node.Parent)
            if (node is ScrollViewer scroll) return scroll;
        return null;
    }
}
