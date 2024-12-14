using System.Collections;
using TrueMoon.Aluminum;

namespace TrueMoon.Alloy;

public class VisualTree : IVisualTree
{
    private IVisual? _root;

    public VisualTree(IVisual? root)
    {
        _root = root;
    }

    public IEnumerator<IVisual> GetEnumerator()
    {
        if (_root == null)
        {
            yield break;
        }
        
        yield return _root;
        foreach (var v in GetVisuals(_root))
        {
            yield return v;
        }
    }

    private static IEnumerable<IVisual> GetVisuals(IVisual? visual)
    {
        if (visual == null)
        {
            yield break;
        }

        foreach (var v in visual)
        {
            yield return v;
            foreach (var n in GetVisuals(v))
            {
                yield return n;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}