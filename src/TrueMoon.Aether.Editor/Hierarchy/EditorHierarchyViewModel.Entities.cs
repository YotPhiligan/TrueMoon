using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;

namespace TrueMoon.Aether.Editor.Hierarchy;

public sealed partial class EditorHierarchyViewModel
{
    private bool _canCreate, _canEditSelected;
    private string _search = "";
    private int _matches;
    /// <summary>Whether a new object can be created under the current admission/capacity.</summary>
    public bool CanCreate => _canCreate;
    /// <summary>Whether the selected branch may be deleted, duplicated or used as a creation parent.</summary>
    public bool CanEditSelected => _canEditSelected;
    /// <summary>Name/ID search text. Searching does not filter or rebuild the hierarchy.</summary>
    public string SearchText
    {
        get => _search;
        set { VerifyOwner(); ArgumentNullException.ThrowIfNull(value); Set(ref _search, value, nameof(SearchText)); UpdateMatches(); }
    }
    /// <summary>Number of current case-insensitive name/ID matches.</summary>
    public int MatchCount => _matches;
    /// <summary>Whether next-match navigation is available.</summary>
    public bool CanFindNext => _matches != 0;
    private IEnumerable<HierarchyItem> Matches() => _search.Length == 0 ? [] : _items.Where(item =>
        item.Name.Contains(_search, StringComparison.OrdinalIgnoreCase) || item.Id.Contains(_search, StringComparison.OrdinalIgnoreCase));
    private void UpdateMatches()
    {
        var canFind = CanFindNext; Set(ref _matches, Matches().Count(), nameof(MatchCount));
        if (canFind != CanFindNext) OnPropertyChanged(nameof(CanFindNext));
    }
    private void RefreshEntityActions(SceneDocument? document)
    {
        Set(ref _canCreate, CanMove && document!.Entities.Length < SceneFileCodec.MaximumEntities && document.Definitions.Length < SceneFileCodec.MaximumDefinitions, nameof(CanCreate));
        Set(ref _canEditSelected, CanMove && _items.Any(item => item.Id == _selected), nameof(CanEditSelected));
        UpdateMatches();
    }
    /// <summary>Selects/reveals the next matching occurrence, cycling in scene hierarchy order.</summary>
    public void FindNext()
    {
        VerifyOwner(); Refresh(); var matches = Matches().ToArray(); if (matches.Length == 0) return;
        var index = Array.FindIndex(matches, item => item.Id == _selected);
        Select(matches[(index + 1) % matches.Length].Id);
    }
    private SceneDocument? CaptureEntityEdit()
    {
        var expected = _baseline; Refresh();
        if (!ReferenceEquals(expected, _baseline))
        { Set(ref _error, "Сцена изменилась. Повторите действие на актуальном дереве.", nameof(ErrorText)); return null; }
        return CanMove ? expected : null;
    }
    /// <summary>Creates a root or selected child and selects it for inspection. One action is one transaction.</summary>
    /// <param name="kind">Transform-only or cube preset.</param><param name="asChild">Use the selected occurrence as parent.</param><returns>Whether committed.</returns>
    public bool CreateEntity(EditorEntityKind kind, bool asChild = false)
    {
        VerifyOwner(); var document = CaptureEntityEdit();
        if (document == null || !CanCreate || !Enum.IsDefined(kind) || asChild && !CanEditSelected) return false;
        try
        {
            var schemas = BuiltInComponents.Schemas.Where(schema => schema.TypeId == BuiltInComponents.TransformId || kind == EditorEntityKind.Cube && schema.TypeId == BuiltInComponents.AppearanceId).ToArray();
            foreach (var schema in schemas)
            {
                var existing = document.Schemas.FirstOrDefault(value => value.TypeId == schema.TypeId && value.Version == schema.Version);
                if (existing != null && (existing.AllowMultiple != schema.AllowMultiple || !existing.RequiredTypes.SetEquals(schema.RequiredTypes) ||
                    existing.Properties.Count != schema.Properties.Count || schema.Properties.Any(pair => !existing.Properties.TryGetValue(pair.Key, out var value) ||
                        value.Kind != pair.Value.Kind || value.DefaultValue != pair.Value.DefaultValue || value.IsNullable != pair.Value.IsNullable)))
                    throw new InvalidDataException("Схема встроенного компонента несовместима с создаваемым объектом.");
            }
            var id = new EntityId(Guid.NewGuid()); var definitionId = new EntityDefinitionId(Guid.NewGuid());
            var definition = new EntityDefinition(definitionId, new(addedComponents: schemas.Select(schema => new ComponentData(new(Guid.NewGuid()), schema.TypeId, schema.Version))));
            var entity = new EntityInstance(id, definitionId, null, asChild ? new EntityId(Guid.Parse(_selected!)) : null,
                kind == EditorEntityKind.Cube ? "Куб" : "Объект", null);
            return CommitEntities(document, new CreateEntitiesBatch(NewEntityOperation(document), [entity], [definition],
                schemas.Where(schema => !document.Schemas.Any(value => value.TypeId == schema.TypeId && value.Version == schema.Version))), id.Value.ToString("D"));
        }
        catch (Exception error) { Set(ref _error, error.Message.ReplaceLineEndings("\n"), nameof(ErrorText)); return false; }
    }
    /// <summary>Removes the selected occurrence and all descendants, retaining shared catalogs.</summary>
    /// <returns>Whether committed.</returns>
    public bool DeleteSelected()
    {
        VerifyOwner(); var document = CaptureEntityEdit(); if (document == null || !CanEditSelected) return false;
        var item = _items.Single(item => item.Id == _selected);
        return CommitEntities(document, new DeleteEntitiesBatch(NewEntityOperation(document), [new(Guid.Parse(_selected!))], EntityDeletionPolicy.Subtree), item.ParentId);
    }
    /// <summary>Copies the selected subtree with fresh entity IDs, selects its root and preserves source data.</summary>
    /// <returns>Whether committed.</returns>
    public bool DuplicateSelected()
    {
        VerifyOwner(); var document = CaptureEntityEdit(); if (document == null || !CanEditSelected) return false;
        var content = _commands.Read().Document!.Content!; var source = new EntityId(Guid.Parse(_selected!));
        var selected = new HashSet<EntityId>(); var pending = new Stack<EntityId>(); pending.Push(source);
        while (pending.TryPop(out var id)) { selected.Add(id); foreach (var child in content.Resolved.Children[id]) pending.Push(child); }
        var copies = content.Resolved.HierarchyOrder.Where(selected.Contains).Select(id => new EntityCopyId(id, new(Guid.NewGuid()))).ToArray();
        return CommitEntities(document, new DuplicateEntitiesBatch(NewEntityOperation(document), source, copies, content.Resolved.Entities[source].ParentId),
            copies.Single(copy => copy.Source == source).Copy.Value.ToString("D"));
    }
    private static AuthoringOperation NewEntityOperation(SceneDocument document) => new(Guid.NewGuid(), document.Revision, "editor");
    private bool CommitEntities(SceneDocument expected, EntityEditBatch batch, string? selection)
    {
        try
        {
            var result = _commands.EditEntities(expected, batch);
            if (!result.Succeeded) { Set(ref _error, (result.Error + ": " + result.Message).ReplaceLineEndings("\n"), nameof(ErrorText)); return false; }
            _workspace.SetDocument(_commands.Read().Document!.Content);
            if (selection == null) _workspace.ClearSelection(); else _workspace.Select(selection);
            _transform.Refresh(_workspace.SelectedId); Refresh(); return true;
        }
        catch (Exception error) { Set(ref _error, error.Message.ReplaceLineEndings("\n"), nameof(ErrorText)); return false; }
    }
}
