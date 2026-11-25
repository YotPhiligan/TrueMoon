using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Views;

namespace TrueMoon.Argentis;

// A scope indexes its root and descendants, up to (but not including) the next scope root.
internal sealed class ElementScope(Element root)
{
    private Dictionary<string, Element>? _parts;
    private ViewStyleScope? _style;

    internal Element? FindPart(string name)
    {
        root.VerifyAccess();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return string.Equals(root.PartName, name, StringComparison.Ordinal) ? root : _parts?.GetValueOrDefault(name);
    }

    internal void ValidateRootName(string name)
    {
        if (_parts?.ContainsKey(name) == true) throw Duplicate(name);
    }

    internal void ValidatePartChanges(IReadOnlyList<Element> added, IReadOnlyList<Element> removed)
    {
        HashSet<string>? names = null;
        for (var i = 0; i < added.Count; i++) Validate(added[i]);

        void Validate(Element node)
        {
            if (node.OwnedScope != null) return;
            if (node.PartName is { } name)
            {
                if (!(names ??= new(StringComparer.Ordinal)).Add(name) || string.Equals(root.PartName, name, StringComparison.Ordinal))
                    throw Duplicate(name);
                if (_parts?.GetValueOrDefault(name) is { } existing && !WillBeRemoved(existing, removed))
                    throw Duplicate(name);
            }
            var children = node.Children;
            for (var i = 0; i < children.Count; i++) Validate(children[i]);
        }
    }

    private InvalidOperationException Duplicate(string name) =>
        new($"View '{root.GetType().Name}' already contains a part named '{name}'.");

    private static bool WillBeRemoved(Element part, IReadOnlyList<Element> removed)
    {
        if (removed.Count == 0) return false;
        for (Element? node = part; node != null; node = node.Parent)
            for (var i = 0; i < removed.Count; i++)
                if (ReferenceEquals(node, removed[i])) return true;
        return false;
    }

    internal void CommitPartChange(Element node, bool added)
    {
        if (node.OwnedScope != null || (!added && _parts == null)) return;
        if (node.PartName is { } name)
        {
            if (added) (_parts ??= new(StringComparer.Ordinal)).Add(name, node);
            else _parts!.Remove(name);
        }
        var children = node.Children;
        for (var i = 0; i < children.Count; i++) CommitPartChange(children[i], added);
    }

    internal void ValidateTheme(Theme theme) => _style?.PrepareTheme(theme);
    internal Style? ResolveStyle(Element element, Theme theme) => _style?.Resolve(element, theme);

    internal void ReplaceStyle(ViewStyleScope? style)
    {
        root.VerifyAccess();
        var pending = new List<(Element Node, Style? Value)>();
        Collect(root);
        _style = style;
        foreach (var (node, value) in pending) node.SetViewStyleValue(value);
        root.Invalidate(Invalidation.Layout | Invalidation.Render);

        void Collect(Element node)
        {
            pending.Add((node, style?.Resolve(node, node.Theme)));
            var children = node.Children;
            for (var i = 0; i < children.Count; i++)
                if (children[i].OwnedScope == null) Collect(children[i]);
        }
    }
}
