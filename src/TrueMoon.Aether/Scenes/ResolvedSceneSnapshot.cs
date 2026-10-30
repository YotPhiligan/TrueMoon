using System.Collections.Immutable;

namespace TrueMoon.Aether.Scenes;

/// <summary>Atomic immutable authoring snapshot; does not contain live simulation/checkpoint state.</summary>
public sealed class ResolvedSceneSnapshot
{
    internal ResolvedSceneSnapshot(SceneDocument source, ImmutableDictionary<EntityId, ResolvedEntity> entities,
        ImmutableArray<EntityId> roots, ImmutableDictionary<EntityId, ImmutableArray<EntityId>> children,
        ImmutableArray<EntityId> hierarchyOrder, ImmutableDictionary<EntityDefinitionId, long> definitionRevisions,
        ImmutableArray<ComponentSchema> componentSchemas)
    {
        Id = source.Id; Revision = source.Revision; Entities = entities; Roots = roots; Children = children;
        HierarchyOrder = hierarchyOrder; DefinitionRevisions = definitionRevisions; ComponentSchemas = componentSchemas;
    }
    /// <summary>Gets persistent scene identity.</summary>
    public SceneId Id { get; }
    /// <summary>Gets the exact source document revision.</summary>
    public long Revision { get; }
    /// <summary>Gets all effective entities, keyed by stable instance IDs.</summary>
    public ImmutableDictionary<EntityId, ResolvedEntity> Entities { get; }
    /// <summary>Gets roots sorted by sibling order (missing means zero), then stable Guid comparison.</summary>
    public ImmutableArray<EntityId> Roots { get; }
    /// <summary>Gets children sorted by sibling order then Guid for every entity, including leaves.</summary>
    public ImmutableDictionary<EntityId, ImmutableArray<EntityId>> Children { get; }
    /// <summary>Gets deterministic preorder: parents before children, siblings sorted by order then Guid.</summary>
    public ImmutableArray<EntityId> HierarchyOrder { get; }
    /// <summary>Gets exact revisions of definitions referenced by resolved inheritance chains.</summary>
    public ImmutableDictionary<EntityDefinitionId, long> DefinitionRevisions { get; }
    /// <summary>Gets exact schemas used by visible components, sorted by type ID and version.</summary>
    public ImmutableArray<ComponentSchema> ComponentSchemas { get; }
}
