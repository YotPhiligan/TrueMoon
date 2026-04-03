using System.Collections;

namespace TrueMoon.Aluminum;

public abstract class ElementList : Element, IEnumerable<IElement>
{
    public IEnumerator<IElement> GetEnumerator()
    {
        yield return default;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}