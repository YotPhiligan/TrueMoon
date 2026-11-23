using System.Diagnostics.CodeAnalysis;

namespace TrueMoon.Argentis;

/// <summary>A reusable view authored in a C# class.</summary>
public abstract class View : ContentControl, IView
{
    private Dictionary<string, Element>? _namedParts;
    private bool _contentPreparationStarted;
    private bool _contentPrepared;

    /// <summary>Builds the unowned root to install during factory preparation after construction.</summary>
    /// <returns>A fresh root, or null for a view with no factory-built content.</returns>
    protected virtual Element? BuildContent() => null;

    /// <summary>Initializes behavior after factory-built content and its named parts are installed.</summary>
    protected virtual void OnContentPrepared() { }

    internal void PrepareContent()
    {
        VerifyAccess();
        if (_contentPrepared) return;
        if (_contentPreparationStarted) throw new InvalidOperationException("View content preparation has already started.");

        _contentPreparationStarted = true;
        if (Child == null && BuildContent() is { } content)
        {
            try
            {
                SetContent(content);
            }
            catch (Exception original)
            {
                if (content.Parent == null && !content.IsAttached && !ReferenceEquals(content, this))
                    ViewCreation.DisposeAfterFailure(content, original);
                throw;
            }

            OnContentPrepared();
        }

        _contentPrepared = true;
    }

    /// <summary>Gets the live named part of the expected type in this view's scope.</summary>
    /// <exception cref="KeyNotFoundException">The part is missing.</exception>
    /// <exception cref="InvalidOperationException">The part has an incompatible type.</exception>
    public T GetRequiredPart<T>(string name) where T : Element
    {
        var part = FindPart(name) ?? throw new KeyNotFoundException($"View '{GetType().Name}' has no part named '{name}'.");
        return part as T ?? throw new InvalidOperationException(
            $"Part '{name}' in view '{GetType().Name}' is '{part.GetType().Name}', expected '{typeof(T).Name}'.");
    }

    /// <summary>Gets an optional named part. Returns false for a missing part or a type mismatch.</summary>
    public bool TryGetPart<T>(string name, [NotNullWhen(true)] out T? part) where T : Element
    {
        part = FindPart(name) as T;
        return part != null;
    }

    private Element? FindPart(string name)
    {
        VerifyAccess();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (string.Equals(PartName, name, StringComparison.Ordinal)) return this;
        return _namedParts?.GetValueOrDefault(name);
    }

    internal void ValidateRootPartName(string name)
    {
        if (_namedParts?.ContainsKey(name) == true) throw DuplicatePart(name);
    }

    internal void ValidatePartChanges(IReadOnlyList<Element> added, IReadOnlyList<Element> removed)
    {
        HashSet<string>? names = null;
        foreach (var subtree in added)
        {
            foreach (var (name, _) in NamedParts(subtree))
            {
                names ??= new(StringComparer.Ordinal);
                if (!names.Add(name) || string.Equals(PartName, name, StringComparison.Ordinal))
                    throw DuplicatePart(name);
                if (_namedParts?.GetValueOrDefault(name) is { } existing && !WillBeRemoved(existing, removed))
                    throw DuplicatePart(name);
            }
        }
    }

    internal void RegisterParts(Element subtree)
    {
        foreach (var (name, part) in NamedParts(subtree))
            (_namedParts ??= new(StringComparer.Ordinal)).Add(name, part);
    }

    internal void UnregisterParts(Element subtree)
    {
        if (_namedParts == null) return;
        foreach (var (name, _) in NamedParts(subtree)) _namedParts.Remove(name);
    }

    private InvalidOperationException DuplicatePart(string name) =>
        new($"View '{GetType().Name}' already contains a part named '{name}'.");

    private static bool WillBeRemoved(Element part, IReadOnlyList<Element> removed)
    {
        if (removed.Count == 0) return false;
        for (Element? node = part; node != null; node = node.Parent)
            foreach (var subtree in removed)
                if (ReferenceEquals(node, subtree)) return true;
        return false;
    }

    private static IEnumerable<(string Name, Element Part)> NamedParts(Element subtree)
    {
        if (subtree is View) yield break;
        if (subtree.PartName is { } name) yield return (name, subtree);
        foreach (var child in subtree.Children)
            foreach (var part in NamedParts(child)) yield return part;
    }

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
    private readonly DataContextState<TData> _data = new();

    /// <inheritdoc />
    public TData? DataContext { get => _data.Value; set => _data.Set(this, value); }
    /// <inheritdoc />
    public event Action? DataContextChanged { add => _data.Changed += value; remove => _data.Changed -= value; }

    /// <inheritdoc />
    public void Content(TData? data, Func<TData?, object> factory) { DataContext = data; Content(() => factory(DataContext)); }
}
