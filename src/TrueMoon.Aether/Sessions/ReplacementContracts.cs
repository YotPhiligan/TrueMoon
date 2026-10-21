using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Sessions;

/// <summary>Monotonic transaction limits; defaults reserve 27 seconds for cleanup and one recovery attempt.</summary>
public sealed record ReplacementDeadlines
{
    /// <summary>Gets the total budget, greater than zero and at most 45 seconds.</summary>
    public TimeSpan Total { get; init; } = TimeSpan.FromSeconds(45);
    /// <summary>Gets the initial candidate window, at most 18 seconds, including preflight, capture and old-child reap.</summary>
    public TimeSpan Forward { get; init; } = TimeSpan.FromSeconds(18);
    /// <summary>Gets the wait for one owned cleanup, at most 10 seconds and also capped by the total budget.</summary>
    public TimeSpan Cleanup { get; init; } = TimeSpan.FromSeconds(10);
    internal void Validate()
    {
        if (Total <= TimeSpan.Zero || Total > TimeSpan.FromSeconds(45) || Forward <= TimeSpan.Zero || Forward > TimeSpan.FromSeconds(18) || Forward >= Total ||
            Cleanup <= TimeSpan.Zero || Cleanup > TimeSpan.FromSeconds(10) || Cleanup >= Total || Forward + Cleanup >= Total) throw new ArgumentOutOfRangeException(nameof(Total));
    }
}

/// <summary>Frozen caller selection. Source revision is checked against current editor input, independently of scene revision.</summary>
/// <param name="Candidate">Completed immutable artifact.</param><param name="ExpectedIdentity">Session this selection targets.</param>
/// <param name="Scene">Current authoring snapshot, compatible with the running world.</param>
/// <param name="ExpectedSourceRevision">Current compiler input revision; stale completion must not be applied.</param>
public sealed record GameReplacementRequest(GameBuildArtifact Candidate, SessionIdentity ExpectedIdentity, SceneSnapshot Scene, long ExpectedSourceRevision);

/// <summary>A candidate became ready with restored state and the previous mode.</summary>
/// <param name="PreviousIdentity">Reaped original generation.</param><param name="Identity">Ready candidate generation.</param>
/// <param name="Checkpoint">Exact paused state used for startup restore.</param><param name="Playing">Restored mode.</param>
public sealed record GameReplacementResult(SessionIdentity PreviousIdentity, SessionIdentity Identity, GameCheckpoint Checkpoint, bool Playing);

/// <summary>Retains the original transaction error even when recovery also fails.</summary>
public sealed class GameReplacementException : Exception
{
    /// <summary>Gets the phase that produced the original error.</summary>
    public string Phase { get; }
    /// <summary>Gets whether one old-build startup was attempted.</summary>
    public bool RollbackAttempted { get; }
    /// <summary>Gets whether the previous build, state, presentation and mode were recovered.</summary>
    public bool RollbackSucceeded { get; }
    /// <summary>Gets any separate recovery/cleanup error.</summary>
    public Exception? RecoveryError { get; }
    /// <summary>Creates an observable transaction failure.</summary>
    /// <param name="phase">Original failure phase.</param><param name="cause">Original failure, never replaced by a recovery error.</param>
    /// <param name="attempted">Whether rollback startup was attempted.</param><param name="succeeded">Whether recovery completed.</param>
    /// <param name="recoveryError">Separate recovery or cleanup error.</param>
    public GameReplacementException(string phase, Exception cause, bool attempted, bool succeeded, Exception? recoveryError)
        : base("Game replacement failed during " + phase + ".", cause)
    { Phase = phase; RollbackAttempted = attempted; RollbackSucceeded = succeeded; RecoveryError = recoveryError; }
}
