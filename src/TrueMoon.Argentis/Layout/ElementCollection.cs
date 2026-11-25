using System.Collections.ObjectModel;

namespace TrueMoon.Argentis.Layout;

/// <summary>A collection which prevents cycles and multiple parents before mutation.</summary>
public sealed class ElementCollection(Element owner) : Collection<Element>, IReadOnlyList<Element>
{
    private IItemsBinding? _binding;

    internal void Reserve(IItemsBinding binding)
    {
        owner.VerifyTreeAccess();
        if (_binding != null) throw new InvalidOperationException("The child collection already has a binding.");
        if (Count != 0) throw new InvalidOperationException("BindItems requires an empty child collection.");
        _binding = binding;
    }

    internal void Release(IItemsBinding binding)
    {
        if (ReferenceEquals(_binding, binding)) _binding = null;
    }

    internal void RefreshBinding()
    {
        owner.VerifyAccess();
        if (_binding == null) throw new InvalidOperationException("The child collection has no binding.");
        _binding.Refresh();
    }

    private void VerifyMutation()
    {
        owner.VerifyTreeAccess();
        if (_binding != null) throw new InvalidOperationException("Bound children are managed by BindItems. Change the source collection instead.");
    }

    internal void AddRange(Element[] children)
    {
        VerifyMutation();
        if (children.Length == 0) return;
        var unique = new HashSet<Element>(ReferenceEqualityComparer.Instance);
        foreach (var child in children)
            if (!unique.Add(child)) throw new InvalidOperationException("The same element cannot be added more than once.");

        owner.ValidateChildChange(children, []);
        using var change = new TreeChange([owner, .. children]);
        foreach (var child in children)
        {
            base.InsertItem(Count, child);
            owner.Adopt(child, change);
        }
        change.Complete();
    }

    // The binding prepares/validates drafts before entering this commit. Observers see only the final tree and mapping.
    internal void Reconcile(IItemsBinding binding, Element[] desired, Action committed)
    {
        owner.VerifyTreeAccess();
        if (!ReferenceEquals(_binding, binding)) throw new InvalidOperationException("The child collection belongs to another binding.");
        if (this.SequenceEqual(desired, ReferenceEqualityComparer.Instance)) { committed(); return; }
        var previous = this.ToArray();
        var oldSet = new HashSet<Element>(previous, ReferenceEqualityComparer.Instance);
        var newSet = new HashSet<Element>(ReferenceEqualityComparer.Instance);
        foreach (var node in desired)
        {
            if (!newSet.Add(node)) throw new InvalidOperationException("A factory cannot return the same element for multiple items.");
        }
        var removed = previous.Where(node => !newSet.Contains(node)).ToArray();
        var added = desired.Where(node => !oldSet.Contains(node)).ToArray();
        owner.ValidateChildChange(added, removed);
        List<Exception>? errors = null;
        using (var change = new TreeChange(previous.Concat(desired).Prepend(owner).ToArray()))
        {
            base.ClearItems();
            foreach (var node in desired) base.InsertItem(Count, node);
            foreach (var node in removed) owner.Orphan(node, change);
            foreach (var node in added) owner.Adopt(node, change);
            committed();
            change.Schedule(() => owner.Invalidate(Invalidation.Tree | Invalidation.Layout));
            try { change.Complete(); }
            catch (Exception error) { (errors ??= []).Add(error); }
        }
        // Disposal must run after the notification lock is released. Removal transferred ownership to the binding.
        foreach (var node in removed)
        {
            try { node.Dispose(); }
            catch (Exception error) { (errors ??= []).Add(error); }
        }
        TreeChange.ThrowErrors(errors);
    }

    /// <inheritdoc />
    protected override void InsertItem(int index, Element item)
    {
        VerifyMutation();
        owner.ValidateChildChange([item], []);
        using var change = new TreeChange(owner, item);
        base.InsertItem(index, item);
        owner.Adopt(item, change);
        change.Complete();
    }
    /// <inheritdoc />
    protected override void SetItem(int index, Element item)
    {
        VerifyMutation();
        if (ReferenceEquals(this[index], item)) return;
        var previous = this[index];
        owner.ValidateChildChange([item], [previous]);
        using var change = new TreeChange(owner, previous, item);
        base.SetItem(index, item);
        owner.Orphan(previous, change);
        owner.Adopt(item, change);
        change.Complete();
    }
    /// <inheritdoc />
    protected override void RemoveItem(int index)
    {
        VerifyMutation();
        var item = this[index];
        using var change = new TreeChange(owner, item);
        base.RemoveItem(index);
        owner.Orphan(item, change);
        change.Complete();
    }
    /// <inheritdoc />
    protected override void ClearItems()
    {
        VerifyMutation();
        var previous = this.ToArray();
        using var change = new TreeChange(previous.Prepend(owner).ToArray());
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
        using var change = new TreeChange(owner);
        base.RemoveItem(oldIndex);
        base.InsertItem(newIndex, item);
        change.Schedule(() => owner.Invalidate(Invalidation.Layout));
        change.Complete();
    }
}