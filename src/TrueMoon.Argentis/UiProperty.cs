namespace TrueMoon.Argentis;

/// <summary>A typed property identifier shared by elements and styles.</summary>
public sealed class UiProperty<T>(string name, T defaultValue, Invalidation affects = Invalidation.Render, Func<T, bool>? validate = null)
{
    /// <summary>The diagnostic name.</summary>
    public string Name { get; } = name;
    /// <summary>The value used when neither local nor styled values exist.</summary>
    public T DefaultValue { get; } = defaultValue;
    /// <summary>The invalidated work.</summary>
    public Invalidation Affects { get; } = affects;
    internal void Validate(T value)
    {
        if (validate != null && !validate(value)) throw new ArgumentOutOfRangeException(Name);
    }
}