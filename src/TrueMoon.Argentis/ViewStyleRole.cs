namespace TrueMoon.Argentis;

/// <summary>A typed semantic role shared by a view and its styles.</summary>
/// <typeparam name="TElement">Controls permitted to carry this role.</typeparam>
public sealed class ViewStyleRole<TElement> where TElement : Element
{
    /// <summary>Creates an independent role token.</summary>
    /// <param name="name">A diagnostic name, not a global selector.</param>
    public ViewStyleRole(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>The diagnostic name.</summary>
    public string Name { get; }
}