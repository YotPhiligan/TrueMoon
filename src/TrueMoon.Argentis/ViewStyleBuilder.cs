namespace TrueMoon.Argentis;

internal sealed class ViewStyleBuilder<TView>(TView view) : IViewStyleBuilder<TView> where TView : View
{
    private readonly List<ViewStyleRule> _rules = [];
    private bool _completed;
    public ViewStyleRule Root() => Add(new(view, null));

    public ViewStyleRule Part<TElement>(Func<TView, TElement> selector) where TElement : Element
    {
        ArgumentNullException.ThrowIfNull(selector);
        var part = selector(view);
        if (part == null || part.IsDisposed || !ReferenceEquals(part.FindStyleOwner(), view))
            throw new ArgumentException("A style part must be live and belong to this view's boundary.",
                nameof(selector));
        return Add(new(part, null));
    }

    public ViewStyleRule Role<TElement>(ViewStyleRole<TElement> role) where TElement : Element
    {
        ArgumentNullException.ThrowIfNull(role);
        return Add(new(null, role));
    }

    private ViewStyleRule Add(ViewStyleRule rule)
    {
        if (_completed) throw new InvalidOperationException("A view style declaration is already complete.");
        _rules.Add(rule);
        return rule;
    }

    internal ViewStyleScope Build()
    {
        _completed = true;
        foreach (var rule in _rules) rule.Freeze();
        return new(_rules.ToArray());
    }
}