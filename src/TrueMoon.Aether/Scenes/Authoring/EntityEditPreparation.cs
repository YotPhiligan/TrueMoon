namespace TrueMoon.Aether.Scenes.Authoring;

internal static class EntityEditPreparation
{
    internal static (SceneAuthoringError Error, string Path, string Message)? Validate(SceneDocument document, ResolvedSceneSnapshot scene, EntityEditBatch batch, int limit)
    {
        var path = SceneResolver.ScenePath(document.Id) + "/entities";
        (SceneAuthoringError, string, string) Invalid(string message) => (SceneAuthoringError.InvalidScene, path, message);
        (SceneAuthoringError, string, string) Bound() => (SceneAuthoringError.BatchLimitExceeded, path, "The expanded entity operation exceeds its finite batch or scene capacity.");
        if (batch is CreateEntitiesBatch create)
        {
            if (create.Entities.Length + create.Definitions.Length + create.Schemas.Length > limit ||
                document.Entities.Length + create.Entities.Length > SceneFileCodec.MaximumEntities ||
                document.Definitions.Length + create.Definitions.Length > SceneFileCodec.MaximumDefinitions ||
                document.Schemas.Length + create.Schemas.Length > SceneFileCodec.MaximumSchemas) return Bound();
            var ids = document.Entities.Select(entity => entity.Id).ToHashSet();
            foreach (var entity in create.Entities)
            {
                if (!ids.Add(entity.Id)) return (SceneAuthoringError.DuplicateTarget, path, "A created entity ID is already present or repeated.");
                if (entity.SiblingOrder != null) return Invalid("Creation assigns appended sibling order; input SiblingOrder must be null.");
            }
        }
        else if (batch is DeleteEntitiesBatch delete)
        {
            if (!Enum.IsDefined(delete.Policy)) return Invalid("Unknown descendant deletion policy.");
            if (delete.Roots.Distinct().Count() != delete.Roots.Length) return (SceneAuthoringError.DuplicateTarget, path, "Repeated deletion root.");
            if (delete.Roots.Any(id => !scene.Entities.ContainsKey(id))) return (SceneAuthoringError.MissingEntity, path, "Missing deletion root.");
            var affected = Affected(scene, delete.Roots, delete.Policy == EntityDeletionPolicy.Subtree);
            if (affected.Count > limit) return Bound();
            if (affected.Any(id => scene.Children[id].Any(child => !affected.Contains(child)))) return Invalid("Deletion would leave a child without its parent.");
        }
        else if (batch is DuplicateEntitiesBatch duplicate)
        {
            if (!scene.Entities.ContainsKey(duplicate.Source) || duplicate.ParentId is { } parent && !scene.Entities.ContainsKey(parent))
                return (SceneAuthoringError.MissingEntity, path, "Missing duplication source or destination parent.");
            var affected = Affected(scene, [duplicate.Source], duplicate.IncludeDescendants);
            if (affected.Count > limit || scene.Entities.Count + affected.Count > SceneFileCodec.MaximumEntities) return Bound();
            if (duplicate.Copies.Select(pair => pair.Source).Distinct().Count() != duplicate.Copies.Length ||
                duplicate.Copies.Select(pair => pair.Copy).Distinct().Count() != duplicate.Copies.Length)
                return (SceneAuthoringError.DuplicateTarget, path, "Repeated source or copy identity.");
            if (!affected.SetEquals(duplicate.Copies.Select(pair => pair.Source))) return Invalid("The copy ID map must cover exactly the selected source set.");
            if (duplicate.Copies.Any(pair => pair.Copy.Value == Guid.Empty || scene.Entities.ContainsKey(pair.Copy))) return Invalid("Copy IDs must be fresh and nonempty.");
        }
        return null;
    }
    internal static HashSet<EntityId> Affected(ResolvedSceneSnapshot scene, IEnumerable<EntityId> roots, bool descendants)
    {
        var result = new HashSet<EntityId>(); var pending = new Stack<EntityId>(roots);
        while (pending.TryPop(out var id)) { if (!result.Add(id) || !descendants) continue; foreach (var child in scene.Children[id]) pending.Push(child); }
        return result;
    }
    internal static SceneDocument Apply(SceneDocument source, ResolvedSceneSnapshot scene, EntityEditBatch batch, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (batch is CreateEntitiesBatch create)
        {
            var document = new SceneDocument(source.Id, source.Revision, source.Entities.Concat(create.Entities),
                source.Definitions.Concat(create.Definitions), source.Schemas.Concat(create.Schemas));
            return HierarchyEditPreparation.Apply(document, new(batch.Operation, placements: create.Entities.Select(entity => new EntityPlacementEdit(entity.Id, entity.ParentId))), token);
        }
        if (batch is DeleteEntitiesBatch delete)
        {
            var affected = Affected(scene, delete.Roots, delete.Policy == EntityDeletionPolicy.Subtree);
            return new(source.Id, source.Revision, source.Entities.Where(entity => !affected.Contains(entity.Id)), source.Definitions, source.Schemas);
        }
        var duplicate = (DuplicateEntitiesBatch)batch; var copies = duplicate.Copies.ToDictionary(pair => pair.Source, pair => pair.Copy);
        var originals = source.Entities.ToDictionary(entity => entity.Id);
        var additions = new List<EntityInstance>(); var ranks = new Dictionary<EntityId, int>();
        foreach (var id in scene.HierarchyOrder)
        {
            token.ThrowIfCancellationRequested(); if (!copies.TryGetValue(id, out var copy)) continue;
            var entity = originals[id];
            var parent = id == duplicate.Source ? duplicate.ParentId : copies[entity.ParentId!.Value];
            var rank = id == duplicate.Source ? 0 : ranks.GetValueOrDefault(parent!.Value);
            if (id != duplicate.Source) ranks[parent!.Value] = rank + 1;
            additions.Add(new(copy, entity.DefinitionId, entity.Changes, parent, entity.Name, rank));
        }
        var result = new SceneDocument(source.Id, source.Revision, source.Entities.Concat(additions), source.Definitions, source.Schemas);
        var sameParent = scene.Entities[duplicate.Source].ParentId == duplicate.ParentId;
        return HierarchyEditPreparation.Apply(result, new(batch.Operation, placements:
            [new(copies[duplicate.Source], duplicate.ParentId, sameParent ? EntityPlacement.After : EntityPlacement.Last, sameParent ? duplicate.Source : null)]), token);
    }
}
