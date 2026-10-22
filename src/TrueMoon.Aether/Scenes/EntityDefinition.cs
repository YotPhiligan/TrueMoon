namespace TrueMoon.Aether.Scenes;

/// <summary>Immutable template, or variant with a single base definition and local changes.</summary>
public sealed class EntityDefinition
{
    /// <summary>Creates a definition. Base graph validation occurs in the resolver.</summary>
    /// <param name="id">Persistent template/variant identity.</param>
    /// <param name="changes">Immutable local changes.</param>
    /// <param name="baseDefinitionId">Optional single base definition.</param>
    /// <param name="revision">Nonnegative authoring revision.</param>
    public EntityDefinition(EntityDefinitionId id, EntityPatch changes, EntityDefinitionId? baseDefinitionId = null, long revision = 0)
    {
        AuthoringValidation.Id(id.Value, nameof(id)); ArgumentNullException.ThrowIfNull(changes);
        if (baseDefinitionId is { } baseId) AuthoringValidation.Id(baseId.Value, nameof(baseDefinitionId));
        if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
        Id = id; Changes = changes; BaseDefinitionId = baseDefinitionId; Revision = revision;
    }
    /// <summary>Gets persistent definition identity.</summary>
    public EntityDefinitionId Id { get; }
    /// <summary>Gets the optional inheritance base, independent of scene hierarchy.</summary>
    public EntityDefinitionId? BaseDefinitionId { get; }
    /// <summary>Gets the nonnegative authoring revision of this definition.</summary>
    public long Revision { get; }
    /// <summary>Gets local changes only.</summary>
    public EntityPatch Changes { get; }
}
