using System.Collections;
using System.Collections.ObjectModel;

namespace TrueMoon.Argentis;

/// <summary>A container with an ownership-aware child collection.</summary>
public abstract class ElementList : Element, IEnumerable<Element>, IElementsList
{
    /// <summary>Creates the child collection.</summary>
    protected ElementList() => Items = new ElementCollection(this);
    /// <summary>Mutable children; changes invalidate layout automatically.</summary>
    public ElementCollection Items { get; }
    /// <inheritdoc />
    public override IReadOnlyList<Element> Children => Items;
    /// <summary>Adds an element and returns this container.</summary>
    public ElementList Add(Element element) { Items.Add(element); return this; }
    /// <inheritdoc />
    public void Add1<TElement>(TElement element) where TElement : IElement => Items.Add((Element)element);
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
        base.InsertItem(index, item);
        owner.Adopt(item);
    }
    /// <inheritdoc />
    protected override void SetItem(int index, Element item)
    {
        if (ReferenceEquals(this[index], item)) return;
        owner.ValidateChild(item);
        var previous = this[index];
        base.SetItem(index, item);
        owner.Orphan(previous);
        owner.Adopt(item);
    }
    /// <inheritdoc />
    protected override void RemoveItem(int index)
    {
        owner.VerifyAccess();
        var item = this[index];
        base.RemoveItem(index);
        owner.Orphan(item);
    }
    /// <inheritdoc />
    protected override void ClearItems()
    {
        owner.VerifyAccess();
        var previous = this.ToArray();
        base.ClearItems();
        foreach (var item in previous) owner.Orphan(item);
    }
    /// <summary>Moves a child without detaching it or losing focus and subscriptions.</summary>
    public void Move(int oldIndex, int newIndex)
    {
        owner.VerifyAccess();
        if ((uint)newIndex >= Count) throw new ArgumentOutOfRangeException(nameof(newIndex));
        var item = this[oldIndex];
        base.RemoveItem(oldIndex);
        base.InsertItem(newIndex, item);
        owner.Invalidate(Invalidation.Layout);
    }
}
