namespace TrueMoon.Aether.Scenes;

/// <summary>Versioned component property's stable ID, scalar type, nullability and default.</summary>
public sealed class ComponentPropertySchema
{
    /// <summary>Creates a property contract; the default must satisfy the declared kind and nullability.</summary>
    /// <param name="id">Persistent ordinal property key.</param>
    /// <param name="kind">Nonnull scalar kind.</param>
    /// <param name="defaultValue">Default satisfying kind/nullability.</param>
    /// <param name="isNullable">Whether explicit null is permitted.</param>
    public ComponentPropertySchema(string id, PropertyValueKind kind, PropertyValue defaultValue, bool isNullable = false)
    {
        Id = AuthoringValidation.Key(id, nameof(id));
        if (kind is < PropertyValueKind.Boolean or > PropertyValueKind.String) throw new ArgumentOutOfRangeException(nameof(kind));
        Kind = kind; IsNullable = isNullable;
        if (!Accepts(defaultValue)) throw new ArgumentException("Default value does not satisfy the property schema.", nameof(defaultValue));
        DefaultValue = defaultValue;
    }
    /// <summary>Gets the case-sensitive persistent property ID.</summary>
    public string Id { get; }
    /// <summary>Gets the scalar kind used for nonnull values.</summary>
    public PropertyValueKind Kind { get; }
    /// <summary>Gets whether an explicit null is accepted.</summary>
    public bool IsNullable { get; }
    /// <summary>Gets the effective value when no data or override supplies one.</summary>
    public PropertyValue DefaultValue { get; }
    internal bool Accepts(PropertyValue value) => value.Kind == Kind || IsNullable && value.Kind == PropertyValueKind.Null;
}
