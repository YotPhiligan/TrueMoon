using System.Collections.Specialized;

namespace TrueMoon.Argentis;

internal sealed class CollectionBinding<TItem> : IItemsBinding where TItem : class
{
    private readonly ElementList _element;
    private readonly IReadOnlyList<TItem> _items;
    private readonly INotifyCollectionChanged _source;
    private readonly Func<TItem, Element> _factory;
    private Dictionary<TItem, Element> _nodes = new(ReferenceEqualityComparer.Instance);
    private Connection? _connection;
    private bool _disposed;

    internal CollectionBinding(ElementList element, IReadOnlyList<TItem> items, INotifyCollectionChanged source, Func<TItem, Element> factory)
    {
        _element = element; _items = items; _source = source; _factory = factory;
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
        var connection = new Connection(this);
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
        _element.Items.Release(this);
        connection?.Dispose();
        // Current nodes remain children: Element.Dispose owns their cleanup.
    }

    private void Synchronize(Connection connection)
    {
        if (!connection.Active) return;
        _element.VerifyTreeAccess();
        var version = connection.Version;
        var snapshot = _items.ToArray();
        var identities = new HashSet<TItem>(ReferenceEqualityComparer.Instance);
        foreach (var item in snapshot)
        {
            if (item == null) throw new InvalidOperationException("Bound items cannot be null.");
            if (!identities.Add(item)) throw new InvalidOperationException("Bound items must have unique object references.");
        }
        var drafts = new List<Element>();
        var draftSet = new HashSet<Element>(ReferenceEqualityComparer.Instance);
        var next = new Dictionary<TItem, Element>(ReferenceEqualityComparer.Instance);
        var desired = new Element[snapshot.Length];
        var committed = false;
        try
        {
            CheckPreparation(connection, version);
            for (var index = 0; index < snapshot.Length; index++)
            {
                var item = snapshot[index];
                if (!_nodes.TryGetValue(item, out var node))
                {
                    node = _factory(item) ?? throw new InvalidOperationException("An item factory must return an element.");
                    // Reject foreign ownership, ancestors and duplicate returns before accepting cleanup responsibility.
                    if (node.Parent != null || node.IsAttached || node.IsDisposed || IsAncestor(node) || !draftSet.Add(node))
                        throw new InvalidOperationException("An item factory must return a new live, unowned subtree.");
                    drafts.Add(node);
                    _element.ValidateChild(node);
                }
                next.Add(item, node);
                desired[index] = node;
                CheckPreparation(connection, version);
            }
            _element.Items.Reconcile(this, desired, () => { _nodes = next; committed = true; });
        }
        catch (Exception error)
        {
            if (committed) throw;
            var errors = new List<Exception> { error };
            foreach (var node in drafts)
            {
                try { node.Dispose(); }
                catch (Exception cleanup) { errors.Add(cleanup); }
            }
            TreeChange.ThrowErrors(errors);
        }
    }

    private bool IsAncestor(Element candidate)
    {
        for (Element? node = _element; node != null; node = node.Parent)
            if (ReferenceEquals(candidate, node)) return true;
        return false;
    }

    private static void CheckPreparation(Connection connection, int version)
    {
        if (!connection.Active || version != connection.Version)
            throw new InvalidOperationException("The collection or attachment changed while preparing bound items.");
    }

    private sealed class Connection(CollectionBinding<TItem> binding) : IDisposable
    {
        private int _active = 1;
        private int _pending;
        private int _version;
        private bool _subscribed;
        private readonly Action<Action> _dispatch = binding._element.CaptureAttachmentDispatcher();
        internal bool Active => Volatile.Read(ref _active) != 0 && binding._element.IsAttached;
        internal int Version => Volatile.Read(ref _version);

        internal void Connect()
        {
            _subscribed = true;
            binding._source.CollectionChanged += Changed;
            Request();
        }

        private void Changed(object? sender, NotifyCollectionChangedEventArgs args)
        {
            if (!Active) return;
            Interlocked.Increment(ref _version);
            Request();
        }

        internal void Request()
        {
            if (!Active || Interlocked.Exchange(ref _pending, 1) != 0) return;
            try
            {
                _dispatch(() =>
                {
                    Interlocked.Exchange(ref _pending, 0);
                    if (Active) binding.Synchronize(this);
                });
            }
            catch (ObjectDisposedException) when (!Active || binding._element.IsDisposed)
            {
                Interlocked.Exchange(ref _pending, 0);
            }
            catch
            {
                Interlocked.Exchange(ref _pending, 0);
                throw;
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _active, 0) == 0) return;
            if (_subscribed) binding._source.CollectionChanged -= Changed;
        }
    }
}
