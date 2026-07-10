using System.Collections;

namespace TrueMoon.Argentis;

public abstract class ElementList : Element, IEnumerable<IElement>
{
    public IEnumerator<IElement> GetEnumerator()
    {
        yield return default;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}