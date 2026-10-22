using System.Collections.Immutable;

namespace TrueMoon.Aether.Scenes;

/// <summary>Immutable scene revision with frozen instance, definition and exact schema catalogs.</summary>
/// <remarks>Cross-reference validation occurs in SceneResolver. Documents can retain invalid references for diagnostics.</remarks>
public sealed class SceneDocument
{
    /// <summary>Snapshots all catalogs. No game assemblies, resources or simulation state are loaded.</summary>
    /// <param name="id">Persistent scene identity.</param>
    /// <param name="revision">Nonnegative authoring revision; not a simulation tick.</param>
    /// <param name="entities">Instances and their scene parent links.</param>
    /// <param name="definitions">Definitions/variants used for inheritance resolution.</param>
    /// <param name="schemas">Exact component schema catalog.</param>
    public SceneDocument(SceneId id, long revision, IEnumerable<EntityInstance> entities,
        IEnumerable<EntityDefinition> definitions, IEnumerable<ComponentSchema> schemas)
    {
        AuthoringValidation.Id(id.Value, nameof(id));
        if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
        Id = id; Revision = revision;
        Entities = Freeze(entities, nameof(entities)); Definitions = Freeze(definitions, nameof(definitions)); Schemas = Freeze(schemas, nameof(schemas));
    }
    /// <summary>Gets scene identity.</summary>
    public SceneId Id { get; }
    /// <summary>Gets the exact document revision.</summary>
    public long Revision { get; }
    /// <summary>Gets frozen authoring instances; their declaration order is retained.</summary>
    public ImmutableArray<EntityInstance> Entities { get; }
    /// <summary>Gets the frozen definition catalog.</summary>
    public ImmutableArray<EntityDefinition> Definitions { get; }
    /// <summary>Gets the frozen exact schema catalog.</summary>
    public ImmutableArray<ComponentSchema> Schemas { get; }

    private static ImmutableArray<T> Freeze<T>(IEnumerable<T> values, string parameter) where T : class
    {
        ArgumentNullException.ThrowIfNull(values, parameter);
        var result = values.ToImmutableArray();
        foreach (var value in result) ArgumentNullException.ThrowIfNull(value, parameter);
        return result;
    }
}
