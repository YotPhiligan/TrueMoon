namespace TrueMoon.Aether.Scenes.Authoring;

internal static class HierarchyEditPreparation
{
    internal static (SceneAuthoringError Error, string Path, string Message)? Validate(ResolvedSceneSnapshot scene, HierarchyEditBatch batch)
    {
        var named = new HashSet<EntityId>(); var placed = new HashSet<EntityId>();
        foreach (var edit in batch.Names)
        {
            var path = $"{SceneResolver.ScenePath(scene.Id)}/entities/{edit.EntityId.Value:D}/name";
            if (!named.Add(edit.EntityId)) return (SceneAuthoringError.DuplicateTarget, path, "Duplicate name target.");
            if (!scene.Entities.ContainsKey(edit.EntityId)) return (SceneAuthoringError.MissingEntity, path, "Missing name target.");
            if (!EntityInstance.IsValidName(edit.Name)) return (SceneAuthoringError.InvalidName, path, "Use a nonblank single-line name of at most 128 UTF16 characters.");
        }
        foreach (var edit in batch.Placements)
        {
            var path = $"{SceneResolver.ScenePath(scene.Id)}/entities/{edit.EntityId.Value:D}/placement";
            if (!placed.Add(edit.EntityId)) return (SceneAuthoringError.DuplicateTarget, path, "Duplicate placement target.");
            if (!scene.Entities.ContainsKey(edit.EntityId) || edit.ParentId is { } parent && !scene.Entities.ContainsKey(parent) ||
                edit.AnchorId is { } anchor && !scene.Entities.ContainsKey(anchor)) return (SceneAuthoringError.MissingEntity, path, "Missing placement target, parent or anchor.");
            if (!Enum.IsDefined(edit.Placement) || edit.ParentId == edit.EntityId || edit.AnchorId == edit.EntityId ||
                (edit.Placement == EntityPlacement.Last) != (edit.AnchorId == null)) return (SceneAuthoringError.InvalidScene, path, "Invalid placement relationship.");
        }
        return null;
    }
    internal static SceneDocument Apply(SceneDocument source, HierarchyEditBatch batch, CancellationToken token)
    {
        var entities = source.Entities.ToDictionary(entity => entity.Id);
        foreach (var edit in batch.Names)
        {
            token.ThrowIfCancellationRequested(); var entity = entities[edit.EntityId];
            entities[entity.Id] = new(entity.Id, entity.DefinitionId, entity.Changes, entity.ParentId, edit.Name, entity.SiblingOrder);
        }
        foreach (var edit in batch.Placements)
        {
            token.ThrowIfCancellationRequested();
            var entity = entities[edit.EntityId];
            var siblings = entities.Values.Where(other => other.Id != edit.EntityId && other.ParentId == edit.ParentId)
                .OrderBy(other => other.SiblingOrder ?? 0).ThenBy(other => other.Id.Value).ToList();
            var index = edit.AnchorId is { } anchor ? siblings.FindIndex(other => other.Id == anchor) : siblings.Count;
            if (index < 0) throw new InvalidDataException("The placement anchor is not a destination sibling.");
            if (edit.Placement == EntityPlacement.After) index++;
            siblings.Insert(index, new(entity.Id, entity.DefinitionId, entity.Changes, edit.ParentId, entity.Name, null));
            for (var rank = 0; rank < siblings.Count; rank++)
            {
                token.ThrowIfCancellationRequested(); var sibling = siblings[rank];
                entities[sibling.Id] = new(sibling.Id, sibling.DefinitionId, sibling.Changes, sibling.ParentId, sibling.Name, rank);
            }
        }
        return new(source.Id, source.Revision, source.Entities.Select(entity => entities[entity.Id]), source.Definitions, source.Schemas);
    }
}
