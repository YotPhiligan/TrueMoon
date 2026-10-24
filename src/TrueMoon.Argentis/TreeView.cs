namespace TrueMoon.Argentis;

/// <summary>Immutable display data; IDs and parent links are independent of labels and icons.</summary>
/// <param name="Id">Stable nonblank key.</param><param name="ParentId">Parent key, or null for a root.</param>
/// <param name="Text">Single-line display label.</param><param name="Icon">Geometric row icon.</param>
public sealed record TreeItem(string Id, string? ParentId, string Text, UiSymbol Icon = UiSymbol.Objects);

/// <summary>Requested parent change; the consumer validates and commits its own data.</summary>
/// <param name="Id">Dragged/keyboard-selected key.</param><param name="ParentId">New parent, or null for root.</param>
public readonly record struct TreeMove(string Id, string? ParentId);

/// <summary>Selection, expansion and parent-move capability of a single-selection tree.</summary>
public interface ITreeView
{
    /// <summary>Gets the selected stable key.</summary>
    string? SelectedId { get; }
    /// <summary>Snapshots and validates a complete ordered forest before publication.</summary>
    /// <param name="items">At most 4096 distinct items with valid acyclic parent links.</param>
    void SetItems(IEnumerable<TreeItem> items);
    /// <summary>Selects an item and reveals its ancestors, or clears selection.</summary>
    /// <param name="id">Existing key, or null.</param>
    void Select(string? id);
    /// <summary>Sets branch expansion; collapsing the selected descendant selects this parent.</summary>
    /// <param name="id">Existing key.</param><param name="expanded">Desired expansion.</param>
    void SetExpanded(string id, bool expanded);
    /// <summary>Raised when the selected key changes.</summary>
    event Action<string?>? SelectionChanged;
    /// <summary>Raised for an admitted parent-move request; never mutates the source data.</summary>
    event Action<TreeMove>? MoveRequested;
}

/// <summary>Retained single-selection tree with clipped row drawing, keyboard navigation and parent drag/drop.
/// Use a ScrollViewer for scrolling. Consumers own edits; this control owns only expansion and input state.</summary>
public sealed class TreeView : Element, ITreeView
{
    /// <summary>Whether parent changes can be requested. Selection/navigation remain available when false.</summary>
    public static readonly UiProperty<bool> MoveEnabledProperty = new("MoveEnabled", false);
    /// <summary>Logical row height.</summary>
    public static readonly UiProperty<float> RowHeightProperty = new("RowHeight", 24, Invalidation.Layout, v => float.IsFinite(v) && v > 0 && v <= 128);
    /// <summary>Logical indentation per level.</summary>
    public static readonly UiProperty<float> IndentProperty = new("Indent", 16, Invalidation.Layout, v => float.IsFinite(v) && v >= 0 && v <= 64);
    private IReadOnlyList<TreeItem> _items = Array.Empty<TreeItem>(), _visible = Array.Empty<TreeItem>();
    private Dictionary<string, TreeItem> _byId = new(StringComparer.Ordinal);
    private Dictionary<string, List<TreeItem>> _children = new(StringComparer.Ordinal);
    private readonly HashSet<string> _expanded = new(StringComparer.Ordinal);
    private readonly List<Row> _rows = [];
    private string? _selected, _hover, _pressed, _dropParent;
    private float _downX, _downY;
    private bool _dragging, _dropValid, _reveal;
    private IInputContext? _capture;
    private ITextLayoutService? _text;
    /// <summary>Creates an empty tree. No services, models or caller items are owned.</summary>
    public TreeView()
    {
        PropertyChanged += Changed;
        AttachmentChanged += Attachment;
    }
    /// <summary>Gets immutable item data in declaration order.</summary>
    public IReadOnlyList<TreeItem> Items => _items;
    /// <summary>Gets visible preorder after expansion.</summary>
    public IReadOnlyList<TreeItem> VisibleItems => _visible;
    /// <inheritdoc />
    public string? SelectedId => _selected;
    /// <summary>Gets or sets parent-move admission.</summary>
    public bool MoveEnabled { get => Get(MoveEnabledProperty); set => Set(MoveEnabledProperty, value); }
    /// <summary>Gets or sets logical row height.</summary>
    public float RowHeight { get => Get(RowHeightProperty); set => Set(RowHeightProperty, value); }
    /// <summary>Gets or sets logical level indentation.</summary>
    public float Indent { get => Get(IndentProperty); set => Set(IndentProperty, value); }
    /// <inheritdoc />
    public override bool Focusable => true;
    /// <inheritdoc />
    public event Action<string?>? SelectionChanged;
    /// <summary>Raised when branch expansion changes.</summary>
    public event Action<string, bool>? ExpansionChanged;
    /// <inheritdoc />
    public event Action<TreeMove>? MoveRequested;

    /// <inheritdoc />
    public void SetItems(IEnumerable<TreeItem> items)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(items);
        var values = new List<TreeItem>();
        foreach (var item in items)
        {
            if (values.Count == 4096) throw new ArgumentOutOfRangeException(nameof(items), "At most 4096 tree items are supported.");
            if (item == null || string.IsNullOrWhiteSpace(item.Id) || item.Text == null ||
                item.Text.Contains('\r') || item.Text.Contains('\n') || !Enum.IsDefined(item.Icon))
                throw new ArgumentException("Tree items require a key, single-line label and valid icon.", nameof(items));
            values.Add(item);
        }
        var byId = new Dictionary<string, TreeItem>(StringComparer.Ordinal);
        foreach (var item in values)
            if (!byId.TryAdd(item.Id, item)) throw new ArgumentException("Duplicate tree key.", nameof(items));
        var complete = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in values)
        {
            var chain = new HashSet<string>(StringComparer.Ordinal); string? current = item.Id;
            while (current != null && !complete.Contains(current))
            {
                if (!byId.TryGetValue(current, out var node)) throw new ArgumentException("Missing tree parent.", nameof(items));
                if (!chain.Add(current)) throw new ArgumentException("Cyclic tree parent.", nameof(items));
                current = node.ParentId;
            }
            complete.UnionWith(chain);
        }
        CancelMove();
        if (_items.SequenceEqual(values)) return;
        _items = Array.AsReadOnly(values.ToArray()); _byId = byId;
        _children = byId.Keys.ToDictionary(id => id, _ => new List<TreeItem>(), StringComparer.Ordinal);
        foreach (var item in values) if (item.ParentId != null) _children[item.ParentId].Add(item);
        _expanded.IntersectWith(byId.Keys);
        var removed = _selected != null && !byId.ContainsKey(_selected);
        if (removed) _selected = null;
        _hover = null; Rebuild();
        if (removed) SelectionChanged?.Invoke(null);
    }
    /// <summary>Gets whether a branch is expanded.</summary>
    /// <param name="id">Existing key.</param><returns>Stored expansion state.</returns>
    public bool IsExpanded(string id) { VerifyAccess(); RequireItem(id); return _expanded.Contains(id); }
    /// <inheritdoc />
    public void SetExpanded(string id, bool expanded)
    {
        VerifyAccess(); RequireItem(id);
        if (_children[id].Count == 0 || (expanded ? !_expanded.Add(id) : !_expanded.Remove(id))) return;
        CancelMove(); Rebuild();
        if (!expanded && _selected != null && !_visible.Any(item => item.Id == _selected)) Select(id);
        ExpansionChanged?.Invoke(id, expanded);
    }
    /// <inheritdoc />
    public void Select(string? id)
    {
        VerifyAccess();
        if (id != null)
        {
            var item = RequireItem(id); var changed = false;
            for (var parent = item.ParentId; parent != null; parent = _byId[parent].ParentId) changed |= _expanded.Add(parent);
            if (changed) Rebuild();
        }
        var previous = _selected; _selected = id;
        if (id != null) { _reveal = true; Invalidate(Invalidation.Layout); }
        else if (previous != null) Invalidate();
        if (previous != id) SelectionChanged?.Invoke(id);
    }
    /// <summary>Gets current logical row bounds, or null for an absent/collapsed item.</summary>
    /// <param name="id">Stable key.</param><returns>Row bounds before ancestor clipping.</returns>
    public Rect? GetItemBounds(string id)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(id);
        var index = _rows.FindIndex(row => row.Item.Id == id);
        return index < 0 ? null : RowBounds(index);
    }
    private TreeItem RequireItem(string id) => id != null && _byId.TryGetValue(id, out var item) ? item :
        throw new ArgumentException("The tree item does not exist.", nameof(id));
    private void Rebuild()
    {
        _rows.Clear();
        var pending = new Stack<Row>(_items.Where(item => item.ParentId == null).Reverse().Select(item => new Row(item, 0)));
        while (pending.TryPop(out var row))
        {
            _rows.Add(row);
            if (!_expanded.Contains(row.Item.Id)) continue;
            var children = _children[row.Item.Id];
            for (var i = children.Count - 1; i >= 0; i--) pending.Push(new(children[i], row.Depth + 1));
        }
        _visible = Array.AsReadOnly(_rows.Select(row => row.Item).ToArray());
        Invalidate(Invalidation.Layout);
    }
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text)
    {
        _text = text;
        var width = _rows.Select(row => text.Measure(row.Item.Text, FontSize, FontFamily).Width + row.Depth * Indent + 36).DefaultIfEmpty(0).Max();
        return new(Math.Min(available.Width, width), _rows.Count * RowHeight);
    }
    /// <inheritdoc />
    protected override void ArrangeCore(Rect area)
    {
        if (!_reveal || _selected == null || GetItemBounds(_selected) is not { } row) return;
        _reveal = false;
        for (var parent = Parent; parent != null; parent = parent.Parent)
            if (parent is ScrollViewer scroll) { scroll.BringIntoView(row); break; }
    }
    private Rect RowBounds(int index)
    {
        var area = Bounds.Deflate(Padding);
        return new(area.X, area.Y + index * RowHeight, area.Width, RowHeight);
    }
    private Rect VisibleArea()
    {
        var area = Bounds.Deflate(Padding);
        for (var parent = Parent; parent != null; parent = parent.Parent)
        {
            var clip = parent.ChildClipBounds; var x = Math.Max(area.X, clip.X); var y = Math.Max(area.Y, clip.Y);
            area = new(x, y, Math.Max(0, Math.Min(area.X + area.Width, clip.X + clip.Width) - x),
                Math.Max(0, Math.Min(area.Y + area.Height, clip.Y + clip.Height) - y));
        }
        return area;
    }
    private int HitRow(float x, float y)
    {
        if (!VisibleArea().Contains(x, y)) return -1;
        var index = (int)MathF.Floor((y - Bounds.Y - Padding.Top) / RowHeight);
        return index >= 0 && index < _rows.Count ? index : -1;
    }
    /// <inheritdoc />
    protected override void DrawCore(IDrawingContext context)
    {
        var visible = VisibleArea(); if (visible.Width <= 0 || visible.Height <= 0) return;
        var first = Math.Max(0, (int)MathF.Floor((visible.Y - Bounds.Y - Padding.Top) / RowHeight));
        var end = Math.Min(_rows.Count, (int)MathF.Ceiling((visible.Y + visible.Height - Bounds.Y - Padding.Top) / RowHeight));
        for (var i = first; i < end; i++)
        {
            var row = _rows[i]; var bounds = RowBounds(i); var selected = row.Item.Id == _selected;
            if (selected) context.Fill(bounds, IsEffectivelyEnabled ? Theme.Accent : Theme.ControlDisabled);
            else if (IsHovered && row.Item.Id == _hover) context.Fill(bounds, Theme.ControlHover);
            var color = !IsEffectivelyEnabled ? Theme.Disabled : selected ? Theme.OnAccent : Foreground;
            var x = bounds.X + Math.Min(row.Depth * Indent, Math.Max(0, bounds.Width - 36));
            var y = bounds.Y + (bounds.Height - 14) / 2;
            if (_children[row.Item.Id].Count != 0)
                UiSymbols.Draw(context, _expanded.Contains(row.Item.Id) ? UiSymbol.ChevronDown : UiSymbol.ChevronRight, new(x, y, 14, 14), color);
            UiSymbols.Draw(context, row.Item.Icon, new(x + 16, y, 14, 14), color);
            context.Text(Ellipsize(row.Item.Text, Math.Max(0, bounds.X + bounds.Width - x - 36)), x + 34,
                bounds.Y + (bounds.Height - FontSize) / 2, color, FontSize, FontFamily);
            if (_dragging && _dropValid && _dropParent == row.Item.Id) context.Stroke(bounds.Deflate(new Thickness(1)), Theme.Focus, 2);
        }
        if (_dragging && _dropValid && _dropParent == null) context.Stroke(visible.Deflate(new Thickness(1)), Theme.Focus, 2);
    }
    private string Ellipsize(string value, float width)
    {
        if (_text == null || _text.Measure(value, FontSize, FontFamily).Width <= width) return value;
        var low = 0; var high = value.Length;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (_text.Measure(value[..middle] + "…", FontSize, FontFamily).Width <= width) low = middle; else high = middle - 1;
        }
        if (low > 0 && char.IsHighSurrogate(value[low - 1])) low--;
        return low == 0 ? "" : value[..low] + "…";
    }
    /// <inheritdoc />
    public override bool HandleInput(UiInput input, IInputContext context)
    {
        if (!IsEffectivelyEnabled) { CancelMove(); return false; }
        if (input.Kind is InputKind.PointerDown or InputKind.PointerUp && input.Button != 0) return false;
        if (input.Kind == InputKind.PointerDown)
        {
            context.Focus(this);
            var index = HitRow(input.X, input.Y); if (index < 0) return true;
            var row = _rows[index]; var bounds = RowBounds(index);
            var x = bounds.X + Math.Min(row.Depth * Indent, Math.Max(0, bounds.Width - 36));
            if (_children[row.Item.Id].Count != 0 && input.X >= x && input.X < x + 16)
            { SetExpanded(row.Item.Id, !_expanded.Contains(row.Item.Id)); return true; }
            Select(row.Item.Id);
            if (MoveEnabled)
            { _pressed = row.Item.Id; _downX = input.X; _downY = input.Y; _capture = context; context.Capture(this); }
            return true;
        }
        if (input.Kind == InputKind.PointerMove)
        {
            var index = HitRow(input.X, input.Y); var hover = index < 0 ? null : _rows[index].Item.Id;
            if (_hover != hover) { _hover = hover; Invalidate(); }
            if (_pressed == null) return false;
            if (!_dragging && MathF.Pow(input.X - _downX, 2) + MathF.Pow(input.Y - _downY, 2) >= 16) _dragging = true;
            if (_dragging) { UpdateDrop(input.X, input.Y); Invalidate(); }
            return true;
        }
        if (input.Kind == InputKind.PointerUp && _pressed != null)
        {
            if (_dragging) UpdateDrop(input.X, input.Y);
            var move = _dragging && _dropValid && MoveEnabled ? new TreeMove(_pressed, _dropParent) : (TreeMove?)null;
            CancelMove(); if (move is { } request) MoveRequested?.Invoke(request); return true;
        }
        if (input.Kind != InputKind.KeyDown) return false;
        if (input.Key == UiKey.Escape && _pressed != null) { CancelMove(); return true; }
        if (_rows.Count == 0) return false;
        var selectedIndex = _rows.FindIndex(row => row.Item.Id == _selected);
        var selectedItem = selectedIndex < 0 ? null : _rows[selectedIndex].Item;
        if (input.Control && input.Key is UiKey.Left or UiKey.Right)
        {
            if (!MoveEnabled || selectedItem == null) return false;
            string? parent = null;
            if (input.Key == UiKey.Left)
            { if (selectedItem.ParentId == null) return true; parent = _byId[selectedItem.ParentId].ParentId; }
            else
            {
                var siblings = _items.Where(item => item.ParentId == selectedItem.ParentId).ToList();
                var index = siblings.FindIndex(item => item.Id == selectedItem.Id); if (index <= 0) return true;
                parent = siblings[index - 1].Id;
            }
            if (CanMove(selectedItem.Id, parent)) MoveRequested?.Invoke(new(selectedItem.Id, parent));
            return true;
        }
        switch (input.Key)
        {
            case UiKey.Up: Select(_rows[Math.Max(0, selectedIndex - 1)].Item.Id); return true;
            case UiKey.Down: Select(_rows[Math.Min(_rows.Count - 1, selectedIndex + 1)].Item.Id); return true;
            case UiKey.Home: Select(_rows[0].Item.Id); return true;
            case UiKey.End: Select(_rows[^1].Item.Id); return true;
            case UiKey.Left when selectedItem != null:
                if (_expanded.Contains(selectedItem.Id)) SetExpanded(selectedItem.Id, false);
                else if (selectedItem.ParentId != null) Select(selectedItem.ParentId);
                return true;
            case UiKey.Right when selectedItem != null && _children[selectedItem.Id].Count != 0:
                if (_expanded.Contains(selectedItem.Id)) Select(_children[selectedItem.Id][0].Id); else SetExpanded(selectedItem.Id, true);
                return true;
            case UiKey.Enter or UiKey.Space when selectedItem != null:
                SetExpanded(selectedItem.Id, !_expanded.Contains(selectedItem.Id)); return true;
            default: return false;
        }
    }
    private bool CanMove(string id, string? parent)
    {
        if (_byId[id].ParentId == parent) return false;
        for (var current = parent; current != null; current = _byId[current].ParentId) if (current == id) return false;
        return true;
    }
    private void UpdateDrop(float x, float y)
    {
        var index = HitRow(x, y); _dropParent = index < 0 ? null : _rows[index].Item.Id;
        _dropValid = VisibleArea().Contains(x, y) && _pressed != null && CanMove(_pressed, _dropParent);
    }
    /// <summary>Cancels a pending gesture and releases its owned capture without requesting an edit.</summary>
    public void CancelMove()
    {
        VerifyAccess(); var context = _capture; var changed = _pressed != null;
        _capture = null; _pressed = null; _dragging = _dropValid = false; _dropParent = null;
        if (context != null) context.ReleaseCapture(this);
        if (changed) Invalidate();
    }
    private void Changed(Element sender, object property)
    {
        if (ReferenceEquals(property, MoveEnabledProperty) && !MoveEnabled ||
            ReferenceEquals(property, RowHeightProperty) || ReferenceEquals(property, IndentProperty)) CancelMove();
    }
    private void Attachment() { if (!IsAttached) CancelMove(); }
    /// <inheritdoc />
    public override void OnInputCancelled() => CancelMove();
    /// <inheritdoc />
    public override void Dispose()
    {
        try { base.Dispose(); }
        finally
        {
            if (IsDisposed)
            { _capture = null; _pressed = null; SelectionChanged = null; ExpansionChanged = null; MoveRequested = null; _text = null; }
        }
    }
    private readonly record struct Row(TreeItem Item, int Depth);
}
