using TrueMoon.Argentis.Layout;
using System.Collections.Specialized;

namespace TrueMoon.Argentis.Bindings;

/// <summary>
/// Keeps a <see cref="VirtualizingStack"/> realizing only the rows its scrolling ancestor can show. A row that leaves
/// the window returns to a reuse pool instead of being disposed, so scrolling back re-binds the existing subtree.
/// </summary>
/// <remarks>
/// Unlike the full keyed binding this one never walks the whole source: a source notification only marks the window
/// dirty, and the next measure reads the rows it actually needs. Recycling requires <c>rebind</c>; without it a row
/// keeps closures over its old item, so leaving rows are disposed and entering rows come from the factory.
/// </remarks>
internal sealed class VirtualizingCollectionBinding<TItem, TKey> : IVirtualizingItemsBinding
    where TItem : class where TKey : notnull
{
    /// <summary>Cap on pooled rows, so a fast scroll does not retain the whole list.</summary>
    private const int MaxPooledRows = 128;

    private readonly VirtualizingStack _element;
    private readonly IReadOnlyList<TItem> _items;
    private readonly INotifyCollectionChanged? _source;
    private readonly Func<TItem, Element> _factory;
    private readonly Func<TItem, TKey> _keySelector;
    private readonly Action<Element, TItem>? _rebind;
    private readonly IEqualityComparer<TKey> _comparer;
    private Dictionary<TKey, Element> _nodes;
    private Dictionary<TKey, Element> _prepared;
    private Dictionary<TKey, TItem> _instances;
    private Dictionary<TKey, TItem> _preparedInstances;
    private readonly HashSet<TKey> _seenKeys;
    private readonly HashSet<Element> _draftNodes = new(ReferenceEqualityComparer.Instance);
    private readonly List<Element> _pool = [];
    private Element[] _desired = [];
    private Element[] _keep = [];
    private ItemsBindingConnection? _connection;
    private bool _disposed;

    internal VirtualizingCollectionBinding(
        VirtualizingStack element,
        IReadOnlyList<TItem> items,
        INotifyCollectionChanged? source,
        Func<TItem, Element> factory,
        Func<TItem, TKey> keySelector,
        IEqualityComparer<TKey>? comparer,
        Action<Element, TItem>? rebind)
    {
        _element = element;
        _items = items;
        _source = source;
        _factory = factory;
        _keySelector = keySelector;
        _rebind = rebind;
        _comparer = comparer ?? EqualityComparer<TKey>.Default;
        _nodes = new Dictionary<TKey, Element>(_comparer);
        _prepared = new Dictionary<TKey, Element>(_comparer);
        _instances = new Dictionary<TKey, TItem>(_comparer);
        _preparedInstances = new Dictionary<TKey, TItem>(_comparer);
        _seenKeys = new HashSet<TKey>(_comparer);
        element.SetItemsSource(this);
        element.Items.Reserve(this);
        element.AttachmentChanged += Reconnect;
        try { Reconnect(); }
        catch (Exception error)
        {
            try { Dispose(); }
            catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
            throw;
        }
    }

    public int ItemCount => _items.Count;

    private void Reconnect()
    {
        var previous = _connection;
        _connection = null;
        previous?.Dispose();
        if (_disposed || !_element.IsAttached) return;
        var connection = new ItemsBindingConnection(_element, _source, Synchronize);
        _connection = connection;
        try
        {
            _element.OwnAttachment(connection);
            connection.Connect();
        }
        catch (Exception error)
        {
            _connection = null;
            try { connection.Dispose(); }
            catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
            throw;
        }
    }

    public void Refresh() => _connection?.Request();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _element.AttachmentChanged -= Reconnect;
        var connection = _connection;
        _connection = null;
        _nodes.Clear();
        _prepared.Clear();
        _instances.Clear();
        _preparedInstances.Clear();
        _element.Items.Release(this);
        // Pooled rows are not children, so this binding owns them.
        var errors = new List<Exception>();
        foreach (var node in _pool)
        {
            try { node.Dispose(); }
            catch (Exception error) { errors.Add(error); }
        }

        _pool.Clear();
        connection?.Dispose();
        TreeChange.ThrowErrors(errors);
    }

    /// <summary>Marks the window stale; the next measure re-reads the source rows it needs.</summary>
    private void Synchronize()
    {
        var connection = _connection;
        if (connection is null || !connection.Active) return;
        _draftNodes.Clear();
        _seenKeys.Clear();
        // Rows waiting in the pool are not children, so this binding releases them itself.
        foreach (var node in _pool) node.Dispose();
        _pool.Clear();
        // A source replacement can shrink the list below the current window, so the container re-runs its pass.
        _element.Invalidate(Invalidation.Layout);
    }

    public void PrepareWindow(int start, int length)
    {
        var connection = _connection;
        // A detached container keeps the rows it had: nothing defines a window until it is attached again.
        if (connection is null || !connection.Active) return;

        var version = connection.Version;
        if (start < 0 || length < 0 || start + length > _items.Count)
            throw new InvalidOperationException("The virtualizing window exceeds the bound item list.");
        if (_desired.Length < length) Array.Resize(ref _desired, length);
        if (_keep.Length < length) Array.Resize(ref _keep, length);
        _draftNodes.Clear();
        var committed = false;
        try
        {
            var keep = CollectSurvivors(start, length);
            if (keep != _nodes.Count)
            {
                // Rows whose key left the window return to the reuse pool first, so the pass that realises the
                // window re-binds those subtrees instead of building them again.
                Commit(_keep, keep, connection, version);
                committed = true;
            }

            Commit(_desired, Fill(start, length), connection, version);
            committed = true;
        }
        catch (Exception error)
        {
            if (committed) throw;
            var errors = new List<Exception> { error };
            foreach (var node in _draftNodes)
            {
                try { node.Dispose(); }
                catch (Exception cleanup) { errors.Add(cleanup); }
            }

            _draftNodes.Clear();
            TreeChange.ThrowErrors(errors);
        }
    }

    /// <summary>Window rows that already exist, in window order, plus the draft key maps for that shrink pass.</summary>
    private int CollectSurvivors(int start, int length)
    {
        _seenKeys.Clear();
        _prepared.Clear();
        _preparedInstances.Clear();
        var keep = 0;
        for (var index = 0; index < length; index++)
        {
            var item = _items[start + index] ?? throw new InvalidOperationException("Bound items cannot be null.");
            var key = _keySelector(item) ?? throw new InvalidOperationException("Bound item keys cannot be null.");
            if (!_seenKeys.Add(key)) throw new InvalidOperationException("Bound item keys must be unique.");
            if (_nodes.TryGetValue(key, out var node))
            {
                _keep[keep++] = node;
                _prepared[key] = node;
                _preparedInstances[key] = item;
            }
        }

        return keep;
    }

    /// <summary>Builds the full window: existing rows first, then pooled subtrees, then the factory.</summary>
    private int Fill(int start, int length)
    {
        _seenKeys.Clear();
        _prepared.Clear();
        _preparedInstances.Clear();
        var total = 0;
        for (var index = 0; index < length; index++)
        {
            var item = _items[start + index] ?? throw new InvalidOperationException("Bound items cannot be null.");
            var key = _keySelector(item) ?? throw new InvalidOperationException("Bound item keys cannot be null.");
            if (!_seenKeys.Add(key)) throw new InvalidOperationException("Bound item keys must be unique.");
            if (_nodes.TryGetValue(key, out var node))
            {
                // A surviving key whose item instance was replaced keeps its row: only the row data moves.
                if (_rebind != null && (!_instances.TryGetValue(key, out var bound) || !ReferenceEquals(bound, item)))
                    _rebind(node, item);
            }
            else
            {
                if (_rebind != null && _pool.Count != 0)
                {
                    node = _pool[^1];
                    _pool.RemoveAt(_pool.Count - 1);
                    _rebind(node, item);
                }
                else node = _factory(item) ?? throw new InvalidOperationException("An item factory must return an element.");

                // Reject foreign ownership, ancestors and duplicate returns before accepting cleanup responsibility.
                if (node.Parent != null || node.IsAttached || node.IsDisposed || IsAncestor(node) || !_draftNodes.Add(node))
                    throw new InvalidOperationException("An item factory must return a new live, unowned subtree.");
                _element.ValidateChild(node);
            }

            _prepared[key] = node;
            _preparedInstances[key] = item;
            _desired[total++] = node;
        }

        return total;
    }

    /// <summary>Applies one window buffer to the tree and swaps the key maps in only after the change succeeds.</summary>
    private void Commit(Element[] buffer, int count, ItemsBindingConnection connection, int version)
    {
        if (!connection.Active || version != connection.Version)
            throw new InvalidOperationException("The collection or attachment changed while preparing bound items.");

        var previousNodes = _nodes;
        var previousInstances = _instances;
        _element.Items.Reconcile(this, buffer, count, () =>
        {
            _nodes = _prepared;
            _prepared = previousNodes;
            _instances = _preparedInstances;
            _preparedInstances = previousInstances;
        }, Recycle);
    }

    /// <summary>Takes a row out of the window: reuse it when it can be re-bound, otherwise release it.</summary>
    private void Recycle(Element node)
    {
        if (_rebind is null || _pool.Count >= MaxPooledRows) { node.Dispose(); return; }
        // A pooled row keeps its attachment so a later window does not pay for re-attachment, but it is out of the
        // tree: focus and hover leave it here, otherwise the stale flags travel to the next item it is re-bound to.
        node.IsFocused = false;
        node.IsHovered = false;
        _pool.Add(node);
    }

    private bool IsAncestor(Element candidate)
    {
        for (Element? node = _element; node != null; node = node.Parent)
            if (ReferenceEquals(candidate, node)) return true;
        return false;
    }
}
