using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Ecs.Loading;

/// <summary>Reference semantics for a string-valued authoring property.</summary>
public enum ComponentReferenceKind
{
    /// <summary>A persistent entity ID in the same scene.</summary>
    Entity,
    /// <summary>A project asset ID validated by the registry's borrowed asset validator.</summary>
    Asset
}

/// <summary>Declares reference semantics without putting CLR or native handles into authoring data.</summary>
public sealed class ComponentReference
{
    /// <summary>Creates a declaration; nullability comes from the referenced property schema.</summary>
    /// <param name="propertyId">Stable property key.</param><param name="kind">Entity or asset semantics.</param>
    public ComponentReference(string propertyId, ComponentReferenceKind kind)
    {
        PropertyId = AuthoringValidation.Key(propertyId, nameof(propertyId));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        Kind = kind;
    }
    /// <summary>Gets the ordinal property key.</summary>
    public string PropertyId { get; }
    /// <summary>Gets the reference kind.</summary>
    public ComponentReferenceKind Kind { get; }
}
