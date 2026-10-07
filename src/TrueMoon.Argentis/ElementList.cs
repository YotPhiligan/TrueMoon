using System.Collections;
using System.Collections.ObjectModel;

namespace TrueMoon.Argentis;

/// <summary>A container with an ownership-aware child collection.</summary>
public abstract class ElementList : Element, IEnumerable<Element>, IElementsList
{
    /// <summary>Creates the child collection.</summary>
    protected ElementList() => Items = new ElementCollection(this);
    /// <summary>Mutable owned children. Removal/replacement transfers the old subtree to the caller without disposal.</summary>
    public ElementCollection Items { get; }
    /// <inheritdoc />
    public override IReadOnlyList<Element> Children => Items;
    /// <summary>Adds an element and returns this container.</summary>
    public ElementList Add(Element element) { Items.Add(element); return this; }
    /// <inheritdoc />
    public void Add1<TElement>(TElement element) where TElement : IElement => Items.Add((Element)(IElement)element);
    /// <inheritdoc />
    public IEnumerator<Element> GetEnumerator() => Items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>A collection which prevents cycles and multiple parents before mutation.</summary>
public sealed class ElementCollection(Element owner) : Collection<Element>, IReadOnlyList<Element>
{
    /// <inheritdoc />
    protected override void InsertItem(int index, Element item)
    {
        owner.ValidateChild(item);
        using var change = new TreeChange(owner, item);
        base.InsertItem(index, item);
        owner.Adopt(item, change);
        change.Complete();
    }
    /// <inheritdoc />
    protected override void SetItem(int index, Element item)
    {
        owner.VerifyTreeAccess();
        if (ReferenceEquals(this[index], item)) return;
        owner.ValidateChild(item);
        var previous = this[index];
        using var change = new TreeChange(owner, previous, item);
        base.SetItem(index, item);
        owner.Orphan(previous, change);
        owner.Adopt(item, change);
        change.Complete();
    }
    /// <inheritdoc />
    protected override void RemoveItem(int index)
    {
        owner.VerifyTreeAccess();
        var item = this[index];
        using var change = new TreeChange(owner, item);
        base.RemoveItem(index);
        owner.Orphan(item, change);
        change.Complete();
    }
    /// <inheritdoc />
    protected override void ClearItems()
    {
        owner.VerifyTreeAccess();
        var previous = this.ToArray();
        using var change = new TreeChange(previous.Prepend(owner).ToArray());
        base.ClearItems();
        foreach (var item in previous) owner.Orphan(item, change);
        change.Complete();
    }
    /// <summary>Moves a child without detaching it or losing focus and subscriptions.</summary>
    public void Move(int oldIndex, int newIndex)
    {
        owner.VerifyTreeAccess();
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
