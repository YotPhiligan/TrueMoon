using System.Text;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Checkpoints;


/// <summary>Finite checkpoint camera position and Euler rotation in radians.</summary>
/// <param name="X">World X.</param><param name="Y">World Y.</param><param name="Z">World Z.</param>
/// <param name="Pitch">Pitch radians.</param><param name="Yaw">Yaw radians.</param><param name="Roll">Roll radians.</param>
public readonly record struct CameraTransform(float X, float Y, float Z, float Pitch, float Yaw, float Roll)
{
    /// <summary>Rejects non-finite transforms before any game mutation.</summary>
    public void Validate()
    {
        if (!float.IsFinite(X) || !float.IsFinite(Y) || !float.IsFinite(Z) || !float.IsFinite(Pitch) || !float.IsFinite(Yaw) || !float.IsFinite(Roll))
            throw new InvalidDataException("Checkpoint camera must be finite.");
    }
}


/// <summary>Immutable game state captured at a paused tick boundary.</summary>
/// <param name="Tick">Exact UInt64 simulation tick.</param><param name="Camera">Camera pose.</param>
/// <param name="Interaction">Demo interaction flag.</param>
public sealed record GameCheckpointState(ulong Tick, CameraTransform Camera, bool Interaction)
{
    /// <summary>Maximum UTF-8 size, in bytes, of the optional game payload.</summary>
    public const int MaxPayloadUtf8Bytes = 24 * 1024;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    /// <summary>Complete game-defined per-entity switch states; empty for games without switches.</summary>
    public GameObjectStates Objects { get; init; } = GameObjectStates.Empty;
    /// <summary>Optional game-defined checkpoint data. Null means absent; an empty string is invalid.</summary>
    public string? Payload { get; init; }
    /// <summary>Validates all state before restore.</summary>
    public void Validate()
    {
        Camera.Validate();
        if (Objects == null) throw new InvalidDataException("Missing game object states.");
        if (Payload is not { } payload) return;
        if (payload.Length is 0 or > MaxPayloadUtf8Bytes)
            throw new InvalidDataException("Invalid checkpoint game payload length.");
        try
        {
            if (Utf8.GetByteCount(payload) > MaxPayloadUtf8Bytes)
                throw new InvalidDataException("Invalid checkpoint game payload length.");
        }
        catch (EncoderFallbackException error) { throw new InvalidDataException("Invalid checkpoint game payload UTF-8.", error); }
    }
}


/// <summary>Optional checkpoint capability. Existing IGameSession implementations remain valid without it.</summary>
/// <remarks>Capture must be observational. Restore validates first and commits atomically, or throws
/// CheckpointRejectedException without mutation, including when the game does not support the supplied payload.
/// Unexpected callback failures have an unknown outcome.</remarks>
public interface ICheckpointGameSession : IGameSession
{
    /// <summary>Captures state without advancing or changing the paused game.</summary>
    GameCheckpointState CaptureCheckpoint();
    /// <summary>Atomically applies previously validated state to an already loaded scene.</summary>
    void RestoreCheckpoint(GameCheckpointState state);
}


/// <summary>Version-three portable envelope. Source build is provenance; a compatible new build may restore it.</summary>
public sealed class GameCheckpoint
{
    /// <summary>The current snapshot schema.</summary>
    public const int CurrentVersion = 3;
    /// <summary>Gets the snapshot schema.</summary>
    public int Version => CurrentVersion;
    /// <summary>Gets the compatible host contract SHA256.</summary>
    public string HostContractId { get; }
    /// <summary>Gets the build that produced the snapshot, not a restriction on the next build.</summary>
    public string SourceGameBuildId { get; }
    /// <summary>Gets the loaded scene identifier.</summary>
    public string SceneId { get; }
    /// <summary>Gets the exact compatible authoring scene revision.</summary>
    public long SceneRevision { get; }
    /// <summary>Gets immutable game state.</summary>
    public GameCheckpointState State { get; }
    /// <summary>Validates and freezes a checkpoint envelope.</summary>
    public GameCheckpoint(string hostContractId, string sourceGameBuildId, string sceneId, long sceneRevision, GameCheckpointState state)
    {
        if (!IsHash(hostContractId) || !IsHash(sourceGameBuildId)) throw new InvalidDataException("Checkpoint requires SHA256 host/build identity.");
        if (string.IsNullOrWhiteSpace(sceneId) || sceneId.Length > 128 || sceneRevision < 0) throw new InvalidDataException("Invalid checkpoint scene/revision.");
        ArgumentNullException.ThrowIfNull(state); state.Validate();
        HostContractId = hostContractId.ToUpperInvariant(); SourceGameBuildId = sourceGameBuildId.ToUpperInvariant();
        SceneId = sceneId; SceneRevision = sceneRevision; State = state;
    }
    /// <summary>Preflights compatibility before stopping the old game or creating a new generation.</summary>
    public void ValidateFor(string hostContractId, SceneSnapshot scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ValidateFor(hostContractId, scene.Id, scene.Revision);
    }

    /// <summary>Preflights the exact scene identity and revision for a loaded scene of either format.</summary>
    public void ValidateFor(string hostContractId, string sceneId, long sceneRevision)
    {
        if (!string.Equals(HostContractId, hostContractId, StringComparison.OrdinalIgnoreCase) || SceneId != sceneId || SceneRevision != sceneRevision)
            throw new InvalidDataException("Checkpoint host/scene/revision is incompatible.");
        State.Validate();
    }
    private static bool IsHash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}


/// <summary>Explicit checkpoint acknowledgement, independent of the current runtime mode.</summary>
public enum CheckpointOutcome
{
    /// <summary>Not a checkpoint response.</summary>
    None,
    /// <summary>A validated snapshot was captured.</summary>
    Captured,
    /// <summary>State was applied before frame readiness.</summary>
    Restored,
    /// <summary>The game does not provide checkpoint capability.</summary>
    Unsupported,
    /// <summary>Capture was declined without changing the paused game.</summary>
    CaptureRejected,
    /// <summary>Restore was declined without applying state.</summary>
    RestoreRejected
}


/// <summary>A confirmed, sequenced checkpoint rejection. Transport/timeout errors are never converted to this result.</summary>
public sealed class CheckpointRejectedException : InvalidOperationException
{
    /// <summary>Gets the confirmed rejection outcome.</summary>
    public CheckpointOutcome Outcome { get; }
    /// <summary>Declares an expected observational/atomic rejection.</summary>
    public CheckpointRejectedException(CheckpointOutcome outcome = CheckpointOutcome.CaptureRejected)
        : base("Checkpoint operation rejected: " + outcome)
    {
        if (outcome is not (CheckpointOutcome.Unsupported or CheckpointOutcome.CaptureRejected or CheckpointOutcome.RestoreRejected))
            throw new ArgumentOutOfRangeException(nameof(outcome));
        Outcome = outcome;
    }
}
