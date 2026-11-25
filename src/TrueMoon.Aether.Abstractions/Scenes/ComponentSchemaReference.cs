namespace TrueMoon.Aether.Scenes;

/// <summary>Persistent schema identity, independent of CLR type names, layouts and assembly versions.</summary>
public readonly record struct ComponentSchemaReference
{
    /// <summary>Requires an explicit exact schema version. Unknown versions require a declared preserve/reject/reset migration policy.</summary>
    /// <param name="typeId">Stable component type ID.</param><param name="version">Positive exact version.</param>
    public ComponentSchemaReference(ComponentTypeId typeId, int version)
    {
        if (typeId.Value == Guid.Empty) throw new ArgumentException("Component type identity is required.", nameof(typeId));
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
        TypeId = typeId; Version = version;
    }
    /// <summary>Persistent type identity.</summary>
    public ComponentTypeId TypeId { get; }
    /// <summary>Exact schema version; not CLR/assembly version.</summary>
    public int Version { get; }
}
