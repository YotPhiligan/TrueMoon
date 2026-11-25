using System.Collections;

namespace TrueMoon.Argentis.Layout;

/// <summary>A container with an ownership-aware child collection.</summary>
public abstract class ElementList : Element, IEnumerable<Element>
{
    /// <summary>Creates the child collection.</summary>
    protected ElementList() => Items = new ElementCollection(this);
    /// <summary>Mutable owned children. Removal/replacement transfers the old subtree to the caller without disposal.</summary>
    /// <remarks>After BindItems registration, structural changes are exclusively managed by the binding.</remarks>
    public ElementCollection Items { get; }
    /// <inheritdoc />
    public override IReadOnlyList<Element> Children => Items;
    /// <summary>Adds an element and returns this container.</summary>
    public ElementList Add(Element element) { Items.Add(element); return this; }
    /// <inheritdoc />
    public IEnumerator<Element> GetEnumerator() => Items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}