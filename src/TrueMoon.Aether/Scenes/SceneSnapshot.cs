namespace TrueMoon.Aether.Scenes;

public sealed class SceneSnapshot
{
    public string Id { get; }
    public long Revision { get; }
    private readonly SceneEntity[] _entities;
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
    public List<SceneEntity> CopyEntities() => _entities.Select(Clone).ToList();
    private static SceneEntity Clone(SceneEntity value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new() { Id = value.Id, X = value.X, Y = value.Y, Z = value.Z, Interactive = value.Interactive };
    }
}
