using System.Collections;

namespace TrueMoon.Aluminum;

public abstract class ElementList : Element, IElementList
{
    public IEnumerator<IElement> GetEnumerator()
    {
        throw new NotImplementedException();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Add(IElement item)
    {
        throw new NotImplementedException();
    }

    public void Clear()
    {
        throw new NotImplementedException();
    }

    public bool Contains(IElement item)
    {
        throw new NotImplementedException();
    }

    public void CopyTo(IElement[] array, int arrayIndex)
    {
        throw new NotImplementedException();
    }

    public bool Remove(IElement item)
    {
        throw new NotImplementedException();
    }

    public int Count { get; }
    public bool IsReadOnly { get; }
    public int IndexOf(IElement item)
    {
        throw new NotImplementedException();
    }

    public void Insert(int index, IElement item)
    {
        throw new NotImplementedException();
    }

    public void RemoveAt(int index)
    {
        throw new NotImplementedException();
    }

    public IElement this[int index]
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }
}