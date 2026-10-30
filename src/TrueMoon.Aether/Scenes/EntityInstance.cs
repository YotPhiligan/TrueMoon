namespace TrueMoon.Aether.Scenes;

/// <summary>Immutable entity instance referencing a definition, local changes and a separate scene parent.</summary>
public sealed class EntityInstance
{
    /// <summary>Maximum instance display-name length in UTF16 code units.</summary>
    public const int MaximumNameLength = 128;
    /// <summary>Creates an instance. Cross-entity parent graph validation belongs to the scene aggregate.</summary>
    /// <param name="id">Persistent entity identity.</param>
    /// <param name="definitionId">Template or variant to instantiate.</param>
    /// <param name="changes">Optional local changes.</param>
    /// <param name="parentId">Optional scene parent, independent of inheritance.</param>
    public EntityInstance(EntityId id, EntityDefinitionId definitionId, EntityPatch? changes = null, EntityId? parentId = null)
        : this(id, definitionId, changes, parentId, null, null) { }

    /// <summary>Creates an instance with optional display metadata. Names never replace persistent identity.</summary>
    /// <param name="id">Persistent occurrence.</param><param name="definitionId">Inheritance source.</param>
    /// <param name="changes">Local component changes.</param><param name="parentId">Scene parent.</param>
    /// <param name="name">Optional nonblank single-line name; duplicate names are allowed.</param>
    /// <param name="siblingOrder">Optional nonnegative sibling rank. Missing/tied ranks use GUID order.</param>
    public EntityInstance(EntityId id, EntityDefinitionId definitionId, EntityPatch? changes, EntityId? parentId, string? name, int? siblingOrder)
    {
        AuthoringValidation.Id(id.Value, nameof(id)); AuthoringValidation.Id(definitionId.Value, nameof(definitionId));
        if (parentId is { } parent)
        {
            AuthoringValidation.Id(parent.Value, nameof(parentId));
            if (parent == id) throw new ArgumentException("An entity cannot parent itself.", nameof(parentId));
        }
        if (!IsValidName(name)) throw new ArgumentException("An instance name must be nonblank, single-line, valid UTF16 and at most 128 characters.", nameof(name));
        if (siblingOrder < 0) throw new ArgumentOutOfRangeException(nameof(siblingOrder));
        Id = id; DefinitionId = definitionId; Changes = changes ?? EntityPatch.Empty; ParentId = parentId; Name = name; SiblingOrder = siblingOrder;
    }
    /// <summary>Gets persistent instance identity.</summary>
    public EntityId Id { get; }
    /// <summary>Gets the inheritance source.</summary>
    public EntityDefinitionId DefinitionId { get; }
    /// <summary>Gets independent parent/child scene placement.</summary>
    public EntityId? ParentId { get; }
    /// <summary>Gets local instance changes.</summary>
    public EntityPatch Changes { get; }
    /// <summary>Optional instance label, independent of inheritance and stable ID.</summary>
    public string? Name { get; }
    /// <summary>Optional sibling rank; null behaves as zero and ties resolve by GUID.</summary>
    public int? SiblingOrder { get; }
    /// <summary>Checks a name without normalization. Null resets to the consumer's ID-based fallback.</summary>
    /// <param name="name">Candidate label.</param><returns>Whether the metadata is valid.</returns>
    public static bool IsValidName(string? name)
    {
        if (name == null) return true;
        if (name.Length > MaximumNameLength || string.IsNullOrWhiteSpace(name)) return false;
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i]; if (char.IsControl(c) || c is '\u2028' or '\u2029') return false;
            if (char.IsHighSurrogate(c)) { if (++i >= name.Length || !char.IsLowSurrogate(name[i])) return false; }
            else if (char.IsLowSurrogate(c)) return false;
        }
        return true;
    }
}
