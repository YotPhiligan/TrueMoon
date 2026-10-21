using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Frames;

namespace TrueMoon.Aether.Transport;

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
    /// <summary>Bounded encoded snapshot, used only for startup restore.</summary>
    public byte[]? Checkpoint { get; set; }
    /// <summary>Gameplay capture state; release packets must be neutral.</summary>
    public bool InputCaptured { get; set; }
    /// <summary>Complete held GameButtons bit mask.</summary>
    public int InputButtons { get; set; }
    /// <summary>Bounded yaw delta in radians.</summary>
    public float InputYaw { get; set; }
    /// <summary>Bounded pitch delta in radians.</summary>
    public float InputPitch { get; set; }
    /// <summary>Coalesced interaction press, independent of the held keys.</summary>
    public bool InputInteract { get; set; }

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
