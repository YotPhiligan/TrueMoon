using System.Collections.ObjectModel;

namespace TrueMoon.Argentis.Layout;

/// <summary>A collection which prevents cycles and multiple parents before mutation.</summary>
public sealed class ElementCollection(Element owner) : Collection<Element>, IReadOnlyList<Element>
{
    private readonly List<Segment> _segments = [];
    private ReconcilePlan? _plan;
    [ThreadStatic] private static HashSet<Element>? _probe;

    /// <summary>Reserves the next free tail slots for a binding, so a container can host several bindings.</summary>
    /// <remarks>The first binding still requires an empty collection; later bindings append after the registered ones.</remarks>
    internal void Reserve(IItemsBinding binding)
    {
        owner.VerifyTreeAccess();
        for (var index = 0; index < _segments.Count; index++)
            if (ReferenceEquals(_segments[index].Binding, binding))
                throw new InvalidOperationException("The binding is already registered in this child collection.");
        if (_segments.Count == 0 && Count != 0) throw new InvalidOperationException("BindItems requires an empty child collection.");
        _segments.Add(new Segment(binding, Count, 0));
    }

    internal void Release(IItemsBinding binding)
    {
        for (var index = 0; index < _segments.Count; index++)
        {
            if (!ReferenceEquals(_segments[index].Binding, binding)) continue;
            _segments.RemoveAt(index);
            // Released rows stay children and keep their slots: Element.Dispose owns their cleanup.
            return;
        }
    }

    internal void RefreshBindings()
    {
        owner.VerifyAccess();
        if (_segments.Count == 0) throw new InvalidOperationException("The child collection has no binding.");
        for (var index = 0; index < _segments.Count; index++) _segments[index].Binding.Refresh();
    }

    private void VerifyMutation()
    {
        owner.VerifyTreeAccess();
        if (_segments.Count != 0) throw new InvalidOperationException("Bound children are managed by BindItems. Change the source collection instead.");
    }

    private int FindSegment(IItemsBinding binding)
    {
        for (var index = 0; index < _segments.Count; index++)
            if (ReferenceEquals(_segments[index].Binding, binding)) return index;
        throw new InvalidOperationException("The child collection belongs to another binding.");
    }

    internal void AddRange(Element[] children)
    {
        VerifyMutation();
        if (children.Length == 0) return;
        if (children.Length > 1)
        {
            // A pairwise scan costs quadratic time and allocation in a batch: one reused set keeps the check linear.
            var unique = _probe ??= new HashSet<Element>(ReferenceEqualityComparer.Instance);
            try
            {
                foreach (var child in children)
                    if (!unique.Add(child)) throw new InvalidOperationException("The same element cannot be added more than once.");
            }
            finally { unique.Clear(); }
        }

        owner.ValidateChildChange(children, children.Length, null, 0);
        using var change = TreeChange.Acquire(owner, children);
        foreach (var child in children)
        {
            base.InsertItem(Count, child);
            owner.Adopt(child, change);
        }

        change.Complete();
    }

    /// <summary>Applies a binding's desired children to its reserved slots with the fewest structural operations.</summary>
    /// <remarks>
    /// The binding prepares and validates drafts before entering this commit, so observers see only the final tree and
    /// mapping. Surviving rows keep attachment, focus and subscriptions: only added and removed rows change ownership.
    /// </remarks>
    internal void Reconcile(IItemsBinding binding, Element[] desired, int desiredCount, Action committed, Action<Element>? recycle = null)
    {
        owner.VerifyTreeAccess();
        var slot = FindSegment(binding);
        var start = _segments[slot].Start;
        var length = _segments[slot].Length;
        if (length == desiredCount && Matches(start, desired, desiredCount))
        {
            committed();
            return;
        }

        var plan = _plan ??= new ReconcilePlan();
        plan.Build(this, start, length, desired, desiredCount);
        var added = plan.Added;
        var addedCount = plan.AddedCount;
        var removed = plan.Removed;
        var removedCount = plan.RemovedCount;
        owner.ValidateChildChange(added, addedCount, removed, removedCount);
        List<Exception>? errors = null;
        using (var change = TreeChange.Acquire(plan.Locked(owner, addedCount, removedCount), plan.LockedCount))
        {
            Apply(plan, start, change);
            var delta = desiredCount - length;
            _segments[slot].Length = desiredCount;
            if (delta != 0)
                for (var index = slot + 1; index < _segments.Count; index++) _segments[index].Start += delta;
            committed();
            change.ScheduleInvalidation(owner, Invalidation.Tree | Invalidation.Layout);
            try { change.Complete(); }
            catch (Exception error) { (errors ??= []).Add(error); }
        }

        // Removal transferred ownership to the binding: a pooling binding recycles the row, otherwise it is disposed
        // after the notification lock is released.
        for (var index = 0; index < removedCount; index++)
        {
            var node = removed[index];
            removed[index] = null!;
            if (node == null) continue;
            if (recycle != null) { recycle(node); continue; }
            try { node.Dispose(); }
            catch (Exception error) { (errors ??= []).Add(error); }
        }

        plan.Reset();
        TreeChange.ThrowErrors(errors);
    }

    private bool Matches(int start, Element[] desired, int desiredCount)
    {
        for (var index = 0; index < desiredCount; index++)
            if (!ReferenceEquals(this[start + index], desired[index])) return false;
        return true;
    }

    private void Apply(ReconcilePlan plan, int start, TreeChange change)
    {
        // Displaced and unwanted rows leave first, descending, so earlier slot indices stay valid.
        for (var entry = plan.RemoveCount - 1; entry >= 0; entry--)
        {
            var index = plan.RemoveAt[entry];
            var node = this[index];
            base.RemoveItem(index);
            if (plan.Disowned[entry]) owner.Orphan(node, change);
        }

        // Kept rows already sit in the desired relative order; moved and new rows fill the slots in front of them.
        var desired = plan.Desired!;
        var cursor = start + plan.Prefix;
        for (var offset = 0; offset < plan.WindowLength; offset++)
        {
            var kind = plan.Kinds[offset];
            if (kind != ReconcilePlan.Keep)
            {
                var node = desired[plan.Prefix + offset];
                base.InsertItem(cursor, node);
                if (kind == ReconcilePlan.NewNode) owner.Adopt(node, change);
            }
            cursor++;
        }
    }

    /// <inheritdoc />
    protected override void InsertItem(int index, Element item)
    {
        VerifyMutation();
        ArgumentNullException.ThrowIfNull(item, "child");
        ChangeItem(index, null, item);
    }
    /// <inheritdoc />
    protected override void SetItem(int index, Element item)
    {
        VerifyMutation();
        var previous = this[index];
        if (ReferenceEquals(previous, item)) return;
        ArgumentNullException.ThrowIfNull(item, "child");
        ChangeItem(index, previous, item);
    }
    /// <inheritdoc />
    protected override void RemoveItem(int index)
    {
        VerifyMutation();
        ChangeItem(index, this[index], null);
    }

    internal void SetSlot(Element? previous, Element? item, Action<Element?> committed)
    {
        VerifyMutation();
        var index = Count;
        if (previous != null)
        {
            for (index = 0; index < Count && !ReferenceEquals(this[index], previous); index++) { }
            if (index == Count) throw new InvalidOperationException("The current slot element is not in the child collection.");
        }
        ChangeItem(index, previous, item, committed);
    }

    private void ChangeItem(int index, Element? previous, Element? item, Action<Element?>? committed = null)
    {
        if (item != null) owner.ValidateChildChange(item, previous);
        using var change = previous == null || item == null
            ? TreeChange.Acquire(owner, previous ?? item)
            : TreeChange.Acquire(owner, previous, item);
        if (previous == null) base.InsertItem(index, item!);
        else if (item == null) base.RemoveItem(index);
        else base.SetItem(index, item);
        if (previous != null) owner.Orphan(previous, change);
        if (item != null) owner.Adopt(item, change);
        // Publish container state after updating ownership and before delivering queued observers.
        committed?.Invoke(item);
        change.Complete();
    }

    /// <inheritdoc />
    protected override void ClearItems()
    {
        VerifyMutation();
        var previous = this.ToArray();
        using var change = TreeChange.Acquire(owner, previous);
        base.ClearItems();
        foreach (var item in previous) owner.Orphan(item, change);
        change.Complete();
    }

    /// <summary>Moves a child without detaching it or losing focus and subscriptions.</summary>
    public void Move(int oldIndex, int newIndex)
    {
        VerifyMutation();
        if ((uint)newIndex >= Count) throw new ArgumentOutOfRangeException(nameof(newIndex));
        var item = this[oldIndex];
        if (oldIndex == newIndex) return;
        using var change = TreeChange.Acquire(owner);
        base.RemoveItem(oldIndex);
        base.InsertItem(newIndex, item);
        change.ScheduleInvalidation(owner, Invalidation.Layout);
        change.Complete();
    }

    private sealed class Segment(IItemsBinding binding, int start, int length)
    {
        internal readonly IItemsBinding Binding = binding;
        internal int Start = start;
        internal int Length = length;
    }

    /// <summary>
    /// Reusable diff buffers for one reconcile. Structural edits are local, so a single row change must not allocate
    /// per row: the plan grows its arrays once and classifies the changed window between the common prefix and suffix.
    /// </summary>
    private sealed class ReconcilePlan
    {
        internal const byte Keep = 0, Moved = 1, NewNode = 2;

        private readonly Dictionary<Element, int> _currentIndex = new(ReferenceEqualityComparer.Instance);
        private int[] _tails = [], _tailSlots = [], _previous = [], _removeAt = [];
        private bool[] _kept = [], _survivor = [], _disowned = [];
        private byte[] _kinds = [];
        private Element[] _added = [], _removed = [], _locked = [];

        internal Element[]? Desired { get; private set; }
        internal int Prefix { get; private set; }
        internal int WindowLength { get; private set; }
        internal int AddedCount { get; private set; }
        internal int RemovedCount { get; private set; }
        internal int RemoveCount { get; private set; }
        internal int LockedCount { get; private set; }
        internal Element[] Added => _added;
        internal Element[] Removed => _removed;
        internal int[] RemoveAt => _removeAt;
        internal bool[] Disowned => _disowned;
        internal byte[] Kinds => _kinds;

        internal void Build(ElementCollection items, int start, int length, Element[] desired, int desiredCount)
        {
            Reset();
            Desired = desired;

            var prefix = 0;
            while (prefix < length && prefix < desiredCount && ReferenceEquals(items[start + prefix], desired[prefix])) prefix++;
            var suffix = 0;
            while (suffix < length - prefix && suffix < desiredCount - prefix &&
                   ReferenceEquals(items[start + length - 1 - suffix], desired[desiredCount - 1 - suffix])) suffix++;

            Prefix = prefix;
            WindowLength = desiredCount - prefix - suffix;
            var windowEnd = length - suffix;
            Ensure(ref _kinds, WindowLength);
            Ensure(ref _kept, windowEnd - prefix);
            Ensure(ref _survivor, windowEnd - prefix);

            for (var offset = prefix; offset < windowEnd; offset++)
                _currentIndex[items[start + offset]] = offset;

            // The longest increasing run of surviving rows keeps its slots; every other row moves or is created.
            Ensure(ref _previous, WindowLength);
            Ensure(ref _tails, WindowLength);
            Ensure(ref _tailSlots, WindowLength);
            var tails = 0;
            for (var offset = 0; offset < WindowLength; offset++)
            {
                if (!_currentIndex.TryGetValue(desired[prefix + offset], out var at))
                {
                    _kinds[offset] = NewNode;
                    continue;
                }

                _survivor[at - prefix] = true;
                var slot = 0;
                while (slot < tails && _tails[slot] < at) slot++;
                _previous[offset] = slot > 0 ? _tailSlots[slot - 1] : -1;
                _tails[slot] = at;
                _tailSlots[slot] = offset;
                if (slot == tails) tails++;
                _kinds[offset] = Moved;
            }

            if (tails > 0)
                for (var offset = _tailSlots[tails - 1]; offset >= 0; offset = _previous[offset])
                {
                    _kept[_currentIndex[desired[prefix + offset]] - prefix] = true;
                    _kinds[offset] = Keep;
                }

            var newCount = 0;
            for (var offset = 0; offset < WindowLength; offset++)
                if (_kinds[offset] == NewNode) newCount++;
            Ensure(ref _added, newCount);
            for (var offset = 0; offset < WindowLength; offset++)
                if (_kinds[offset] == NewNode) _added[AddedCount++] = desired[prefix + offset];

            for (var offset = prefix; offset < windowEnd; offset++)
            {
                var relative = offset - prefix;
                if (_survivor[relative] && _kept[relative]) continue;
                Ensure(ref _removeAt, RemoveCount + 1);
                Ensure(ref _disowned, RemoveCount + 1);
                Ensure(ref _removed, RemovedCount + 1);
                _removeAt[RemoveCount] = start + offset;
                _disowned[RemoveCount] = !_survivor[relative];
                // A surviving row leaving its slot keeps ownership: it is re-inserted in the same commit.
                if (!_survivor[relative]) _removed[RemovedCount] = items[start + offset];
                RemoveCount++;
                if (!_survivor[relative]) RemovedCount++;
            }
        }

        internal Element[] Locked(Element owner, int addedCount, int removedCount)
        {
            Ensure(ref _locked, 1 + addedCount + removedCount);
            _locked[0] = owner;
            var cursor = 1;
            for (var index = 0; index < addedCount; index++) _locked[cursor++] = _added[index];
            for (var index = 0; index < RemovedCount; index++)
                if (_removed[index] != null) _locked[cursor++] = _removed[index]!;
            LockedCount = cursor;
            return _locked;
        }

        internal void Reset()
        {
            _currentIndex.Clear();
            Prefix = 0;
            WindowLength = 0;
            AddedCount = 0;
            RemovedCount = 0;
            RemoveCount = 0;
            LockedCount = 0;
            Array.Clear(_added);
            Array.Clear(_removed);
            Array.Clear(_locked);
            Array.Clear(_kept);
            Array.Clear(_survivor);
            Array.Clear(_kinds);
        }

        private static void Ensure<T>(ref T[] buffer, int needed)
        {
            if (buffer.Length >= needed) return;
            var size = buffer.Length == 0 ? 8 : buffer.Length;
            while (size < needed) size *= 2;
            Array.Resize(ref buffer, size);
        }
    }
}
