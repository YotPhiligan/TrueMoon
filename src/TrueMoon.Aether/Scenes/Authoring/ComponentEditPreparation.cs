namespace TrueMoon.Aether.Scenes.Authoring;

// Pure preparation. The session owns validation, receipt publication and the single commit point.
internal static class ComponentEditPreparation
{
    internal static (SceneAuthoringError Error, string Path, string Message)? Validate(
        SceneDocument document, ResolvedSceneSnapshot scene, ComponentEditBatch batch)
    {
        var structural = new Dictionary<(EntityId, ComponentId), ComponentEdit>();
        foreach (var edit in batch.Components)
        {
            var path = Path(scene.Id, edit.EntityId, edit.ComponentId);
            if (!scene.Entities.ContainsKey(edit.EntityId)) return (SceneAuthoringError.MissingEntity, path, "The entity does not exist.");
            if (!structural.TryAdd((edit.EntityId, edit.ComponentId), edit))
                return (SceneAuthoringError.DuplicateTarget, path, "A component can have one presence edit per batch.");
            if (edit.ComponentId.Value == Guid.Empty || !Enum.IsDefined(edit.Kind) ||
                (edit.Kind == ComponentChangeKind.Add ? edit.Addition == null || edit.Addition.Id != edit.ComponentId : edit.Addition != null))
                return (SceneAuthoringError.InvalidScene, path, "Invalid component presence intent.");
            var entity = scene.Entities[edit.EntityId];
            if (edit.Kind == ComponentChangeKind.Remove && !entity.Components.ContainsKey(edit.ComponentId))
                return (SceneAuthoringError.MissingComponent, path, "Only a visible component can be removed.");
            if (edit.Kind == ComponentChangeKind.ResetRemoval &&
                !document.Entities.Single(e => e.Id == edit.EntityId).Changes.RemovedComponents.Contains(edit.ComponentId))
                return (SceneAuthoringError.InvalidScene, path, "No instance removal mask exists.");
        }
        var addresses = new HashSet<(EntityId, ComponentId, string)>();
        foreach (var edit in batch.Properties)
        {
            var path = Path(scene.Id, edit.EntityId, edit.ComponentId) + "/properties/" + edit.PropertyId;
            if (!addresses.Add((edit.EntityId, edit.ComponentId, edit.PropertyId)))
                return (SceneAuthoringError.DuplicateTarget, path, "A property can be edited once per batch.");
            if (!scene.Entities.TryGetValue(edit.EntityId, out var entity))
                return (SceneAuthoringError.MissingEntity, path, "The entity does not exist.");
            if (edit.PropertyId == null || edit.PropertyId.Length is < 1 or > 128 ||
                edit.PropertyId.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '.' and not '_' and not '-'))
                return (SceneAuthoringError.InvalidScene, path, "Invalid property key.");
            structural.TryGetValue((edit.EntityId, edit.ComponentId), out var presence);
            if (structural.ContainsKey((edit.EntityId, edit.ComponentId)) &&
                presence.Kind is ComponentChangeKind.Remove or ComponentChangeKind.ResetRemoval)
                return (SceneAuthoringError.InvalidScene, path, "A hidden component cannot receive property edits.");
            if (!entity.Components.ContainsKey(edit.ComponentId) &&
                (!structural.ContainsKey((edit.EntityId, edit.ComponentId)) ||
                    presence.Kind is not (ComponentChangeKind.Add or ComponentChangeKind.Restore)))
                return (SceneAuthoringError.MissingComponent, path, "The component is not visible.");
            // Sets are checked by the final resolver. Resets must also validate the addressed schema key.
            if (edit.Reset)
            {
                var data = presence.Addition;
                var effective = entity.Components.GetValueOrDefault(edit.ComponentId);
                if (effective == null && data == null && presence.Kind == ComponentChangeKind.Restore)
                    data = entity.DefinitionChain.SelectMany(id => document.Definitions.Single(d => d.Id == id).Changes.AddedComponents)
                        .FirstOrDefault(c => c.Id == edit.ComponentId);
                if (effective != null || data != null)
                {
                    var type = effective?.TypeId ?? data!.TypeId; var version = effective?.SchemaVersion ?? data!.SchemaVersion;
                    var schema = document.Schemas.FirstOrDefault(s => s.TypeId == type && s.Version == version);
                    if (schema == null || !schema.Properties.ContainsKey(edit.PropertyId))
                        return (SceneAuthoringError.InvalidScene, path, "The reset property is not declared by the exact schema.");
                }
                else return (SceneAuthoringError.InvalidScene, path, "Restore the component before resetting a property.");
            }
        }
        return null;
    }

    internal static SceneDocument Apply(SceneDocument source, ComponentEditBatch batch, CancellationToken token)
    {
        var properties = batch.Properties.ToLookup(e => e.EntityId);
        var components = batch.Components.ToLookup(e => e.EntityId);
        var entities = source.Entities.Select(entity =>
        {
            token.ThrowIfCancellationRequested();
            if (!properties.Contains(entity.Id) && !components.Contains(entity.Id)) return entity;
            var patch = entity.Changes;
            var added = patch.AddedComponents.ToDictionary(c => c.Id);
            var removed = patch.RemovedComponents.ToHashSet(); var restored = patch.RestoredComponents.ToHashSet();
            var overrides = patch.PropertyOverrides.ToDictionary(p => (p.ComponentId, p.PropertyId));
            foreach (var edit in components[entity.Id])
            {
                token.ThrowIfCancellationRequested();
                switch (edit.Kind)
                {
                    case ComponentChangeKind.Add:
                        if (!added.TryAdd(edit.ComponentId, edit.Addition!))
                            throw new InvalidDataException("Component occurrence already exists.");
                        break;
                    case ComponentChangeKind.Remove:
                        if (!added.Remove(edit.ComponentId) && !restored.Remove(edit.ComponentId)) removed.Add(edit.ComponentId);
                        foreach (var key in overrides.Keys.Where(k => k.ComponentId == edit.ComponentId).ToArray()) overrides.Remove(key);
                        break;
                    case ComponentChangeKind.Restore:
                        if (!removed.Remove(edit.ComponentId)) restored.Add(edit.ComponentId);
                        break;
                    case ComponentChangeKind.ResetRemoval: removed.Remove(edit.ComponentId); break;
                }
            }
            foreach (var edit in properties[entity.Id])
            {
                token.ThrowIfCancellationRequested();
                var address = (edit.ComponentId, edit.PropertyId);
                if (edit.Reset)
                {
                    overrides.Remove(address);
                    if (added.TryGetValue(edit.ComponentId, out var data))
                        added[edit.ComponentId] = new(data.Id, data.TypeId, data.SchemaVersion, data.Properties.Remove(edit.PropertyId));
                }
                else overrides[address] = new(edit.ComponentId, edit.PropertyId, edit.Value);
            }
            return new EntityInstance(entity.Id, entity.DefinitionId,
                new EntityPatch(added.Values, removed, overrides.Values, restored), entity.ParentId, entity.Name, entity.SiblingOrder);
        });
        return new(source.Id, source.Revision, entities, source.Definitions, source.Schemas);
    }

    private static string Path(SceneId scene, EntityId entity, ComponentId component) =>
        $"{SceneResolver.ScenePath(scene)}/entities/{entity.Value:D}/components/{component.Value:D}";
}
