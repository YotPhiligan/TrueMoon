using TrueMoon.Argentis.Layout;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;

namespace TrueMoon.Argentis.Bindings;

/// <summary>
/// Keeps a container's children mapped to a source list by a stable key. A row survives whenever its key survives,
/// so replacing an item instance with an equal key reuses the subtree, its focus and its subscriptions.
/// </summary>
internal sealed class CollectionBinding<TItem, TKey> : IItemsBinding where TItem : class where TKey : notnull
{
    private readonly ElementList _element;
    private readonly IReadOnlyList<TItem> _items;
    private readonly INotifyCollectionChanged? _source;
    private readonly Func<TItem, Element> _factory;
    private readonly Func<TItem, TKey> _keySelector;
    private readonly Action<Element, TItem>? _rebind;
    private readonly IEqualityComparer<TKey> _comparer;
    private Dictionary<TKey, BoundRow> _nodes;
    private Dictionary<TKey, BoundRow> _prepared;
    private readonly HashSet<Element> _draftNodes = new(ReferenceEqualityComparer.Instance);
    private readonly List<Element> _drafts = [];
    private Element[] _desired = [];
    private TItem[] _snapshot = [];
    private TKey[] _snapshotKeys = [];
    private ItemsBindingConnection? _connection;
    private bool _disposed;

    // The live subtree of one key plus the source instance its row currently observes.
    private readonly record struct BoundRow(Element Node, TItem Item);

    internal CollectionBinding(
        ElementList element,
        IReadOnlyList<TItem> items,
        INotifyCollectionChanged? source,
        Func<TItem, Element> factory,
        Func<TItem, TKey> keySelector,
        IEqualityComparer<TKey>? comparer,
        Action<Element, TItem>? rebind)
    {
        _element = element; _items = items; _source = source; _factory = factory; _keySelector = keySelector;
        _rebind = rebind;
        _comparer = comparer ?? EqualityComparer<TKey>.Default;
        _nodes = new Dictionary<TKey, BoundRow>(_comparer);
        _prepared = new Dictionary<TKey, BoundRow>(_comparer);
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
        _element.Items.Release(this);
        connection?.Dispose();
        // Current nodes remain children: Element.Dispose owns their cleanup.
    }

    // Steady state must not allocate per row: the two mapping dictionaries and the desired, source-snapshot and
    // key buffers are reused, the source is read by index, and only new keys run the factory.
    private void Synchronize()
    {
        var connection = _connection;
        if (connection is null || !connection.Active) return;
        _element.VerifyTreeAccess();
        var version = connection.Version;
        // The source is copied into a reused buffer instead of re-allocated per refresh. Anything that exposes
        // ICollection<T> — a plain List<T>, an ObservableCollection<T>, a read-only wrapper — is block-copied through
        // that member, which is also the point where a source that fails while snapshotting fails before a row exists.
        var count = 0;
        if (_items is ICollection<TItem> source)
        {
            count = source.Count;
            if (_snapshot.Length < count) Array.Resize(ref _snapshot, count);
            source.CopyTo(_snapshot, 0);
        }
        else
        {
            foreach (var item in _items)
            {
                if (count == _snapshot.Length) Array.Resize(ref _snapshot, Math.Max(4, _snapshot.Length * 2));
                _snapshot[count++] = item;
            }
        }

        if (_desired.Length < count) Array.Resize(ref _desired, count);
        if (_snapshotKeys.Length < count) Array.Resize(ref _snapshotKeys, count);
        _drafts.Clear();
        _draftNodes.Clear();
        var prepared = _prepared;
        prepared.Clear();
        var committed = false;
        try
        {
            CheckPreparation(connection, version);
            // Validate the whole snapshot before any row is created, so a rejected source leaves the tree untouched.
            for (var index = 0; index < count; index++)
            {
                var item = _snapshot[index];
                if (item == null) throw new InvalidOperationException("Bound items cannot be null.");
                var key = _keySelector(item);
                if (key is null) throw new InvalidOperationException("Bound item keys cannot be null.");
                // The prepared dictionary is the seen-key set until the main loop below overwrites every entry,
                // so a rejected snapshot still fails before a single row is created.
                if (!prepared.TryAdd(key, default)) throw new InvalidOperationException("Bound item keys must be unique.");
                _snapshotKeys[index] = key;
            }

            for (var index = 0; index < count; index++)
            {
                var item = _snapshot[index];
                var key = _snapshotKeys[index];
                if (!_nodes.TryGetValue(key, out var row))
                {
                    var node = _factory(item) ?? throw new InvalidOperationException("An item factory must return an element.");
                    // Reject foreign ownership, ancestors and duplicate returns before accepting cleanup responsibility.
                    if (node.Parent != null || node.IsAttached || node.IsDisposed || IsAncestor(node) || !_draftNodes.Add(node))
                        throw new InvalidOperationException("An item factory must return a new live, unowned subtree.");
                    _drafts.Add(node);
                    _element.ValidateChild(node);
                    prepared[key] = new BoundRow(node, item);
                    _desired[index] = node;
                }
                else
                {
                    if (_rebind != null && !ReferenceEquals(row.Item, item)) _rebind(row.Node, item);
                    prepared[key] = new BoundRow(row.Node, item);
                    _desired[index] = row.Node;
                }

                CheckPreparation(connection, version);
            }

            // Neither buffer may keep a source instance that has left the list.
            if (_snapshot.Length > count) Array.Clear(_snapshot, count, _snapshot.Length - count);
            if (_snapshotKeys.Length > count) Array.Clear(_snapshotKeys, count, _snapshotKeys.Length - count);

            _element.Items.Reconcile(this, _desired, count, () =>
            {
                var previous = _nodes;
                _nodes = prepared;
                _prepared = previous;
                committed = true;
            });
        }
        catch (Exception error)
        {
            if (committed) throw;
            var errors = new List<Exception> { error };
            foreach (var node in _drafts)
            {
                try { node.Dispose(); }
                catch (Exception cleanup) { errors.Add(cleanup); }
            }

            _drafts.Clear();
            TreeChange.ThrowErrors(errors);
        }
    }

    private bool IsAncestor(Element candidate)
    {
        for (Element? node = _element; node != null; node = node.Parent)
            if (ReferenceEquals(candidate, node)) return true;
        return false;
    }

    private static void CheckPreparation(ItemsBindingConnection connection, int version)
    {
        if (!connection.Active || version != connection.Version)
            throw new InvalidOperationException("The collection or attachment changed while preparing bound items.");
    }
}

/// <summary>Keys rows by object identity, which is what the parameterless <c>BindItems</c> overloads promise.</summary>
internal sealed class ReferenceKeyComparer<T> : IEqualityComparer<T>
{
    internal static readonly ReferenceKeyComparer<T> Instance = new();

    public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
    public int GetHashCode(T value) => RuntimeHelpers.GetHashCode(value!);
}
