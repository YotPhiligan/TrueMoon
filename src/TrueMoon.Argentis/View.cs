namespace TrueMoon.Argentis;

/// <summary>A reusable view authored in a C# class.</summary>
public abstract class View : ContentControl, IView
{
    internal ViewStyleScope? ViewStyleScope { get; private set; }
    internal void ReplaceViewStyle(ViewStyleScope? scope)
    {
        VerifyAccess();
        var nodes = StyleElements().ToArray();
        var values = nodes.Select(node => scope?.Resolve(node, node.Theme)).ToArray();
        ViewStyleScope = scope;
        for (var i = 0; i < nodes.Length; i++) nodes[i].SetViewStyleValue(values[i]);
        Invalidate(Invalidation.Layout | Invalidation.Render);
    }
    private IEnumerable<Element> StyleElements()
    {
        yield return this;
        foreach (var child in Children)
            foreach (var node in Visit(child)) yield return node;
        static IEnumerable<Element> Visit(Element element)
        {
            if (element is View) yield break;
            yield return element;
            foreach (var child in element.Children)
                foreach (var node in Visit(child)) yield return node;
        }
    }
    /// <summary>Creates content once. Use SetContent to replace it dynamically.</summary>
    public void Content(Func<object> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        SetContent(factory() as Element ?? throw new ArgumentException("View content must be an Element.", nameof(factory)));
    }
    /// <inheritdoc />
    public virtual object? GetContent() => Child;
}
/// <summary>A view with explicitly bound typed data. Data may be a plain model or an observable view model.</summary>
public abstract class View<TData> : View, IView<TData>
{
    /// <inheritdoc />
    public TData? DataContext { get; set; }
    /// <inheritdoc />
    public void Content(TData? data, Func<TData?, object> factory) { DataContext = data; Content(() => factory(DataContext)); }
}
