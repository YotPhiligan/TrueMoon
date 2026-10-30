using System.Collections.Immutable;

namespace TrueMoon.Aether.Scenes;

/// <summary>Stateless whole-scene validation/resolution over one frozen document revision.</summary>
public static class SceneResolver
{
    /// <summary>Validates scene links and resolves every instance before publishing a snapshot.</summary>
    /// <param name="scene">Frozen scene document; inputs and earlier snapshots remain unchanged on failure.</param>
    /// <param name="cancellationToken">Cooperative cancellation between graph/entity operations.</param>
    /// <returns>Complete immutable authoring snapshot.</returns>
    /// <exception cref="SceneResolutionException">A catalog, parent graph or entity is invalid.</exception>
    public static ResolvedSceneSnapshot Resolve(SceneDocument scene, CancellationToken cancellationToken = default)
        => Resolve(scene, new Factory<EntityResolver, SceneDocument>(document =>
            new(document.Definitions.OrderBy(d => d.Id.Value), document.Schemas.OrderBy(s => s.TypeId.Value).ThenBy(s => s.Version))), cancellationToken);

    internal static ResolvedSceneSnapshot Resolve(SceneDocument scene, IFactory<EntityResolver, SceneDocument> resolvers, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scene); cancellationToken.ThrowIfCancellationRequested();
        var path = ScenePath(scene.Id);
        var ordered = scene.Entities.OrderBy(e => e.Id.Value).ToArray();
        var entities = new Dictionary<EntityId, EntityInstance>();
        foreach (var entity in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!entities.TryAdd(entity.Id, entity)) throw new SceneResolutionException(SceneResolutionError.DuplicateEntity,
                $"{path}/entities/{entity.Id.Value:D}", "Duplicate entity identity.", entity.Id);
        }
        EntityResolver resolver;
        try
        {
            resolver = resolvers.Create(scene);
        }
        catch (EntityResolutionException error)
        {
            throw new SceneResolutionException(SceneResolutionError.InvalidCatalog, $"{path}/{error.Path}", "Invalid authoring catalog.", innerException: error);
        }
        foreach (var entity in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entity.ParentId is { } parent && !entities.ContainsKey(parent))
                throw new SceneResolutionException(SceneResolutionError.MissingParent, $"{path}/entities/{entity.Id.Value:D}/parent",
                    $"Missing parent {parent.Value:D}.", entity.Id);
        }
        ValidateParentGraph(entities, path, cancellationToken);
        var roots = ImmutableArray.CreateBuilder<EntityId>();
        var children = entities.Keys.ToDictionary(id => id, _ => new List<EntityId>());
        foreach (var entity in ordered.OrderBy(entity => entity.SiblingOrder ?? 0))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entity.ParentId is { } parent) children[parent].Add(entity.Id); else roots.Add(entity.Id);
        }
        var order = ImmutableArray.CreateBuilder<EntityId>();
        var pending = new Stack<EntityId>(Enumerable.Reverse(roots));
        while (pending.TryPop(out var id))
        {
            cancellationToken.ThrowIfCancellationRequested(); order.Add(id);
            var descendants = children[id];
            for (var index = descendants.Count - 1; index >= 0; index--) pending.Push(descendants[index]);
        }
        var resolved = ImmutableDictionary.CreateBuilder<EntityId, ResolvedEntity>();
        foreach (var id in order)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { resolved.Add(id, resolver.Resolve(entities[id])); }
            catch (EntityResolutionException error)
            {
                throw new SceneResolutionException(SceneResolutionError.InvalidEntity, $"{path}/{error.Path}", "Entity resolution failed.", id, error);
            }
        }
        var definitions = scene.Definitions.ToDictionary(d => d.Id);
        var revisions = resolved.Values.SelectMany(e => e.DefinitionChain).Distinct().ToImmutableDictionary(id => id, id => definitions[id].Revision);
        var schemas = scene.Schemas.ToDictionary(s => (s.TypeId, s.Version));
        var usedSchemas = resolved.Values.SelectMany(e => e.Components.Values).Select(c => (c.TypeId, c.SchemaVersion)).Distinct()
            .OrderBy(key => key.TypeId.Value).ThenBy(key => key.SchemaVersion).Select(key => schemas[key]).ToImmutableArray();
        cancellationToken.ThrowIfCancellationRequested();
        return new(scene, resolved.ToImmutable(), roots.ToImmutable(), children.ToImmutableDictionary(p => p.Key, p => p.Value.ToImmutableArray()),
            order.ToImmutable(), revisions, usedSchemas);
    }

    private static void ValidateParentGraph(Dictionary<EntityId, EntityInstance> entities, string path, CancellationToken token)
    {
        var completed = new HashSet<EntityId>();
        foreach (var start in entities.Keys.OrderBy(id => id.Value))
        {
            token.ThrowIfCancellationRequested();
            if (completed.Contains(start)) continue;
            var current = start; var visiting = new HashSet<EntityId>(); var chain = new List<EntityId>();
            while (!completed.Contains(current))
            {
                token.ThrowIfCancellationRequested();
                if (!visiting.Add(current))
                {
                    var last = chain[^1];
                    throw new SceneResolutionException(SceneResolutionError.ParentCycle, $"{path}/entities/{last.Value:D}/parent",
                        $"Parent cycle reaches {current.Value:D}.", last);
                }
                chain.Add(current);
                if (entities[current].ParentId is not { } parent) break;
                current = parent;
            }
            completed.UnionWith(chain);
        }
    }

    internal static string ScenePath(SceneId id) => $"scenes/{id.Value:D}";
}
