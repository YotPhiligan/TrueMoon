namespace Engine.Probe.Contracts;

// Probe-only contracts. Mutable wire DTOs are decoded into validated snapshots
// before application; no transport, process, UI or graphics types cross this boundary.
public interface IEngineControl
{
    Task<ControlReply> SendAsync(ControlRequest request, CancellationToken cancellationToken = default);
}

public enum CommandKind { Hello = 1, LoadScene, SetMode, Step, GetStatus, Stop, ConfigureSurface }

public sealed record SessionIdentity(Guid SessionId, ulong Generation, string HostContractId,
    string GameBuildId, string Nonce, int ControlVersion = 1, int FrameVersion = 1, int SnapshotVersion = 1)
{
    public void Validate()
    {
        if (SessionId == Guid.Empty || Generation == 0 || ControlVersion != 1 || FrameVersion != 1 || SnapshotVersion != 1 ||
            !IsHash(HostContractId) || !IsHash(GameBuildId) || !IsHash(Nonce))
            throw new InvalidDataException("Invalid engine session identity/version.");
    }
    private static bool IsHash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}

public sealed class ControlRequest
{
    public string SessionId { get; set; } = "";
    // Enerit's current scalar codec supports Int64; these fields carry the exact
    // UInt64 bit pattern. Domain identity/sequence remain unsigned and checked.
    public long Generation { get; set; }
    public string HostContractId { get; set; } = "";
    public string GameBuildId { get; set; } = "";
    public string Nonce { get; set; } = "";
    public int ControlVersion { get; set; }
    public int FrameVersion { get; set; }
    public int SnapshotVersion { get; set; }
    public long Sequence { get; set; }
    public int Kind { get; set; }
    public string SceneId { get; set; } = "";
    public long Revision { get; set; }
    public List<SceneEntity>? Entities { get; set; }
    public bool Playing { get; set; }
    public SurfaceConfiguration? Surface { get; set; }

    public SessionIdentity Identity() => new(Guid.Parse(SessionId), unchecked((ulong)Generation), HostContractId, GameBuildId,
        Nonce, ControlVersion, FrameVersion, SnapshotVersion);

    public static ControlRequest Create(SessionIdentity identity, ulong sequence, CommandKind kind) => new()
    {
        SessionId = identity.SessionId.ToString("D"), Generation = unchecked((long)identity.Generation),
        HostContractId = identity.HostContractId, GameBuildId = identity.GameBuildId, Nonce = identity.Nonce,
        ControlVersion = identity.ControlVersion, FrameVersion = identity.FrameVersion,
        SnapshotVersion = identity.SnapshotVersion, Sequence = unchecked((long)sequence), Kind = (int)kind
    };
}

public sealed class ControlReply
{
    public ControlRequest Stamp { get; set; } = new();
    public string Phase { get; set; } = "";
    public long Revision { get; set; }
    public long Tick { get; set; }
    public int ProcessId { get; set; }
    public long SurfaceGeneration { get; set; }
    public long DroppedFrames { get; set; }
}

public sealed class SceneEntity
{
    public string Id { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public bool Interactive { get; set; }
}

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

public interface IGameSession : IDisposable
{
    void Load(SceneSnapshot scene);
    void Tick();
    ulong TickNumber { get; }
    long Revision { get; }
    uint PatternColor { get; }
}
