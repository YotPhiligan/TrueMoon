namespace TrueMoon.Aether.Scenes;

/// <summary>Immutable entity instance referencing a definition, local changes and a separate scene parent.</summary>
public sealed class EntityInstance
{
    /// <summary>Creates an instance. Cross-entity parent graph validation belongs to the scene aggregate.</summary>
    /// <param name="id">Persistent entity identity.</param>
    /// <param name="definitionId">Template or variant to instantiate.</param>
    /// <param name="changes">Optional local changes.</param>
    /// <param name="parentId">Optional scene parent, independent of inheritance.</param>
    public EntityInstance(EntityId id, EntityDefinitionId definitionId, EntityPatch? changes = null, EntityId? parentId = null)
    {
        AuthoringValidation.Id(id.Value, nameof(id)); AuthoringValidation.Id(definitionId.Value, nameof(definitionId));
        if (parentId is { } parent)
        {
            AuthoringValidation.Id(parent.Value, nameof(parentId));
            if (parent == id) throw new ArgumentException("An entity cannot parent itself.", nameof(parentId));
        }
        Id = id; DefinitionId = definitionId; Changes = changes ?? EntityPatch.Empty; ParentId = parentId;
    }
    /// <summary>Gets persistent instance identity.</summary>
    public EntityId Id { get; }
    /// <summary>Gets the inheritance source.</summary>
    public EntityDefinitionId DefinitionId { get; }
    /// <summary>Gets independent parent/child scene placement.</summary>
    public EntityId? ParentId { get; }
    /// <summary>Gets local instance changes.</summary>
    public EntityPatch Changes { get; }
}
