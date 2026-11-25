namespace TrueMoon.Argentis.Styling;

/// <summary>Declares a generated role token on a partial view style class.</summary>
/// <typeparam name="TElement">The element type accepted by the role.</typeparam>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class ViewRoleAttribute<TElement> : Attribute where TElement : Element
{
    /// <summary>Declares a typed role named by a C# identifier unique within the style class.</summary>
    /// <param name="name">The generated member name and the role's diagnostic label.</param>
    public ViewRoleAttribute(string name) => Name = name;

    /// <summary>The generated role member name.</summary>
    public string Name { get; }
}
