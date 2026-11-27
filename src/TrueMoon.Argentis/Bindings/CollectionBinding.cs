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
    private Dictionary<TKey, Element> _nodes;
    private Dictionary<TKey, Element> _prepared;
    private Dictionary<TKey, TItem> _instances;
    private Dictionary<TKey, TItem> _preparedInstances;
    private HashSet<TKey> _seenKeys;
    private readonly HashSet<Element> _draftNodes = new(ReferenceEqualityComparer.Instance);
    private readonly List<Element> _drafts = [];
    private Element[] _desired = [];
    private ItemsBindingConnection? _connection;
    private bool _disposed;

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
        _nodes = new Dictionary<TKey, Element>(_comparer);
        _prepared = new Dictionary<TKey, Element>(_comparer);
        _instances = new Dictionary<TKey, TItem>(_comparer);
        _preparedInstances = new Dictionary<TKey, TItem>(_comparer);
        _seenKeys = new HashSet<TKey>(_comparer);
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
        _instances.Clear();
        _preparedInstances.Clear();
        _element.Items.Release(this);
        connection?.Dispose();
        // Current nodes remain children: Element.Dispose owns their cleanup.
    }

    // Steady state must not allocate per row: mapping and desired buffers are reused, and only new keys run the factory.
    private void Synchronize()
    {
        var connection = _connection;
        if (connection is null || !connection.Active) return;
        _element.VerifyTreeAccess();
        var version = connection.Version;
        var snapshot = _items.ToArray();
        var count = snapshot.Length;
        if (_desired.Length < count) Array.Resize(ref _desired, count);
        _drafts.Clear();
        _draftNodes.Clear();
        _seenKeys.Clear();
        var prepared = _prepared;
        prepared.Clear();
        var preparedInstances = _preparedInstances;
        preparedInstances.Clear();
        var committed = false;
        try
        {
            CheckPreparation(connection, version);
            // Validate the whole snapshot before any row is created, so a rejected source leaves the tree untouched.
            for (var index = 0; index < count; index++)
            {
                var item = snapshot[index];
                if (item == null) throw new InvalidOperationException("Bound items cannot be null.");
                var key = _keySelector(item);
                if (key is null) throw new InvalidOperationException("Bound item keys cannot be null.");
                if (!_seenKeys.Add(key)) throw new InvalidOperationException("Bound item keys must be unique.");
            }

            for (var index = 0; index < count; index++)
            {
                var item = snapshot[index];
                var key = _keySelector(item);
                if (!_nodes.TryGetValue(key, out var node))
                {
                    node = _factory(item) ?? throw new InvalidOperationException("An item factory must return an element.");
                    // Reject foreign ownership, ancestors and duplicate returns before accepting cleanup responsibility.
                    if (node.Parent != null || node.IsAttached || node.IsDisposed || IsAncestor(node) || !_draftNodes.Add(node))
                        throw new InvalidOperationException("An item factory must return a new live, unowned subtree.");
                    _drafts.Add(node);
                    _element.ValidateChild(node);
                }
                else if (_rebind != null && (!_instances.TryGetValue(key, out var bound) || !ReferenceEquals(bound, item)))
                    _rebind(node, item);

                prepared[key] = node;
                preparedInstances[key] = item;
                _desired[index] = node;
                CheckPreparation(connection, version);
            }

            _element.Items.Reconcile(this, _desired, count, () =>
            {
                var previous = _nodes;
                _nodes = prepared;
                _prepared = previous;
                var previousInstances = _instances;
                _instances = preparedInstances;
                _preparedInstances = previousInstances;
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
