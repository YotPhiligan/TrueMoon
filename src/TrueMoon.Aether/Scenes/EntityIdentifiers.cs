namespace TrueMoon.Aether.Scenes;

/// <summary>Stable identity of an entity instance, independent of its template and scene parent.</summary>
public readonly record struct EntityId
{
    /// <summary>Creates a nonempty identity.</summary>
    /// <param name="value">Persistent nonempty GUID.</param>
    public EntityId(Guid value) { AuthoringValidation.Id(value, nameof(value)); Value = value; }
    /// <summary>Gets the persistent identifier.</summary>
    public Guid Value { get; }
}

/// <summary>Stable identity of an entity template or variant.</summary>
public readonly record struct EntityDefinitionId
{
    /// <summary>Creates a nonempty identity.</summary>
    /// <param name="value">Persistent nonempty GUID.</param>
    public EntityDefinitionId(Guid value) { AuthoringValidation.Id(value, nameof(value)); Value = value; }
    /// <summary>Gets the persistent identifier.</summary>
    public Guid Value { get; }
}

/// <summary>Stable identity of a component occurrence, retained throughout an inheritance chain.</summary>
public readonly record struct ComponentId
{
    /// <summary>Creates a nonempty identity.</summary>
    /// <param name="value">Persistent nonempty GUID.</param>
    public ComponentId(Guid value) { AuthoringValidation.Id(value, nameof(value)); Value = value; }
    /// <summary>Gets the persistent identifier.</summary>
    public Guid Value { get; }
}

/// <summary>Persistent component type identity; does not name a CLR type or assembly.</summary>
public readonly record struct ComponentTypeId
{
    /// <summary>Creates a nonempty identity.</summary>
    /// <param name="value">Persistent nonempty GUID.</param>
    public ComponentTypeId(Guid value) { AuthoringValidation.Id(value, nameof(value)); Value = value; }
    /// <summary>Gets the persistent identifier.</summary>
    public Guid Value { get; }
}
