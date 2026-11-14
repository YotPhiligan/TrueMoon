
namespace TrueMoon.Aether.Abstractions.Scenes;


/// <summary>Frozen legacy scene of at most64distinct finite entity values; no live World or authoring service.</summary>
public sealed class SceneSnapshot
{
    /// <summary>Scene identity,1..128characters.</summary>
    public string Id { get; }
    /// <summary>Nonnegative captured revision.</summary>
    public long Revision { get; }
    private readonly SceneEntity[] _entities;
    /// <summary>Validates and deep-copies all input entities.</summary><param name="id">Scene identity.</param><param name="revision">Source revision.</param><param name="entities">Bounded distinct values.</param>
    public SceneSnapshot(string id, long revision, IEnumerable<SceneEntity> entities)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id); ArgumentNullException.ThrowIfNull(entities);
        if (id.Length > 128 || revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
        _entities = entities.Take(65).Select(Clone).ToArray();
        if (_entities.Length > 64 || _entities.Any(e => string.IsNullOrWhiteSpace(e.Id) || e.Id.Length > 128 ||
            !float.IsFinite(e.X) || !float.IsFinite(e.Y) || !float.IsFinite(e.Z)) ||
            _entities.Select(e => e.Id).Distinct(StringComparer.Ordinal).Count() != _entities.Length)
            throw new ArgumentException("Scene requires up to 64 distinct finite entities.", nameof(entities));
        Id = id; Revision = revision;
    }
    /// <summary>Returns independent mutable transfer values.</summary><returns>Deep entity copies.</returns>
    public List<SceneEntity> CopyEntities() => _entities.Select(Clone).ToList();
    private static SceneEntity Clone(SceneEntity value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new()
        {
            Id = value.Id,
            X = value.X,
            Y = value.Y,
            Z = value.Z,
            Interactive = value.Interactive,
            Primitive = value.Primitive == null ? null : Rendering.ScenePrimitive.FromPrimitive(value.Primitive.ToPrimitive())
        };
    }
}
