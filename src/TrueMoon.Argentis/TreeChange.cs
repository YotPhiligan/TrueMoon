using System.Runtime.ExceptionServices;

namespace TrueMoon.Argentis;

// Keeps structural changes coherent even when user notifications or cleanup throw.
// Locking counts depth on each locked element, so a nested change cannot release an outer lock and
// the common one- and two-element change needs no set, no copy and no notification buffer.
internal sealed class TreeChange : IDisposable
{
    private readonly Element? _first, _firstRoot, _second, _secondRoot, _third, _thirdRoot;
    private readonly Element?[]? _batch;
    private readonly int _batchCount, _invalidationCapacity;
    private List<Exception>? _errors;
    private Queue<Action>? _notifications;
    private List<(Element Node, Invalidation Reason)>? _invalidations;
    // One change per thread slot: a nested change never sees the buffers of an outer one, it only reuses what an
    // earlier change finished with. Disposing a subtree creates one change per node, so without this every node pays
    // a fresh queue and its default backing array.
    [ThreadStatic] private static Queue<Action>? _pooledNotifications;
    [ThreadStatic] private static List<(Element Node, Invalidation Reason)>? _pooledInvalidations;
    private const int PooledCapacityLimit = 4096;

    internal TreeChange(Element? first = null, Element? second = null, Element? third = null)
    {
        Verify(first); Verify(second); Verify(third);
        (_first, _firstRoot) = Lock(first);
        (_second, _secondRoot) = Lock(second);
        (_third, _thirdRoot) = Lock(third);
        _invalidationCapacity = 4;
    }

    /// <summary>Locks a prepared batch of elements and their current roots; the buffer is read, never copied.</summary>
    internal TreeChange(Element?[] elements, int count)
    {
        ArgumentNullException.ThrowIfNull(elements);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        for (var index = 0; index < count; index++) Verify(elements[index]);
        var batch = new Element?[count * 2];
        for (var index = 0; index < count; index++)
        {
            var (element, root) = Lock(elements[index]);
            batch[index * 2] = element;
            batch[index * 2 + 1] = root;
        }

        _batch = batch;
        _batchCount = count * 2;
        _invalidationCapacity = count * 2;
    }

    /// <summary>Locks a container plus the children one batch adopts: the caller's array is read, never copied or re-spread.</summary>
    internal TreeChange(Element owner, Element[] children)
    {
        Verify(owner);
        for (var index = 0; index < children.Length; index++) Verify(children[index]);
        (_first, _firstRoot) = Lock(owner);
        var batch = new Element?[children.Length * 2];
        for (var index = 0; index < children.Length; index++)
        {
            var (element, root) = Lock(children[index]);
            batch[index * 2] = element;
            batch[index * 2 + 1] = root;
        }

        _batch = batch;
        _batchCount = children.Length * 2;
        // Adopt and theme application queue one invalidation per node, plus one for the container.
        _invalidationCapacity = (children.Length + 1) * 2;
    }

    // Reentrancy is judged before this change locks anything: a container and the child it adopts are locked by the same
    // change, and Dispose unlocks exactly the slots Lock filled, so the root is only returned when locked separately.
    private static void Verify(Element? element) => element?.VerifyTreeAccess();

    private static (Element? Element, Element? Root) Lock(Element? element)
    {
        if (element == null) return (null, null);
        element.BeginTreeLock();
        var root = element;
        while (root.Parent != null) root = root.Parent;
        if (ReferenceEquals(root, element)) return (element, null);
        root.BeginTreeLock();
        return (element, root);
    }

    internal void Run(Action action)
    {
        try { action(); }
        catch (Exception error) { (_errors ??= []).Add(error); }
    }

    internal void Notify(Action? handlers)
    {
        if (handlers == null) return;
        foreach (Action handler in handlers.GetInvocationList()) Schedule(handler);
    }

    internal void Schedule(Action action)
    {
        _notifications ??= _pooledNotifications ?? new Queue<Action>();
        // The slot never hands the same buffer to two live changes: only this change may hold it from now on.
        _pooledNotifications = null;
        _notifications.Enqueue(action);
    }

    /// <summary>Queues an invalidation for a later flush without allocating a closure.</summary>
    internal void ScheduleInvalidation(Element node, Invalidation reason)
    {
        _invalidations ??= _pooledInvalidations ?? new List<(Element Node, Invalidation Reason)>(_invalidationCapacity);
        _pooledInvalidations = null;
        _invalidations.Add((node, reason));
    }

    /// <summary>Delivers queued invalidations first, then queued notifications: an observer never sees a subtree whose
    /// layout dirtiness was not recorded first, and one failing entry does not stop the rest.</summary>
    internal void Flush()
    {
        if (_invalidations != null)
        {
            for (var index = 0; index < _invalidations.Count; index++)
            {
                var (node, reason) = _invalidations[index];
                try { node.Invalidate(reason); }
                catch (Exception error) { (_errors ??= []).Add(error); }
            }

            _invalidations.Clear();
        }

        while (_notifications?.TryDequeue(out var action) == true) Run(action);
    }

    internal void Complete()
    {
        Flush();
        ThrowErrors(_errors);
    }

    internal static void ThrowErrors(IReadOnlyCollection<Exception>? errors)
    {
        if (errors == null || errors.Count == 0) return;
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors.First()).Throw();
        throw new AggregateException("UI tree notifications or cleanup failed after the change committed.", errors);
    }

    public void Dispose()
    {
        Unlock(_first); Unlock(_firstRoot);
        Unlock(_second); Unlock(_secondRoot);
        Unlock(_third); Unlock(_thirdRoot);
        if (_batch != null)
            for (var index = 0; index < _batchCount; index++) Unlock(_batch[index]);
        // Only an exhausted buffer returns: a change that threw or never flushed still holds pending entries, and those
        // must not be delivered by an unrelated change. Capacity is capped so one huge batch is not kept forever.
        if (_notifications is { Count: 0 } notifications && notifications.Capacity <= PooledCapacityLimit)
            _pooledNotifications = notifications;
        if (_invalidations is { Count: 0 } invalidations && invalidations.Capacity <= PooledCapacityLimit)
            _pooledInvalidations = invalidations;
    }

    private static void Unlock(Element? element)
    {
        if (element != null) element.EndTreeLock();
    }
}
