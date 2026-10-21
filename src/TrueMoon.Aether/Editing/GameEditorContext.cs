using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Editing;

/// <summary>Explicit authoring inputs. Setting a context snapshots inputs and invalidates the previous candidate.</summary>
public sealed record GameEditorContext(GameBuildRequest Build, SceneSnapshot Scene, string Selection);

/// <summary>One serialized Editor command; Stop can interrupt any other command.</summary>
public enum GameEditorOperation
{
    /// <summary>No pending command.</summary>
    Idle,
    /// <summary>Compiling a candidate.</summary>
    Build,
    /// <summary>Validating an explicitly selected artifact.</summary>
    Select,
    /// <summary>Starting a fresh runtime and waiting for presentation.</summary>
    Start,
    /// <summary>Capturing/replacing/restoring or recovering a runtime.</summary>
    Replace,
    /// <summary>Resuming simulation.</summary>
    Play,
    /// <summary>Pausing simulation.</summary>
    Pause,
    /// <summary>Advancing a paused tick.</summary>
    Step,
    /// <summary>Interrupting commands and draining owned resources.</summary>
    Stop
}

/// <summary>A build selected for one authoring context and one originating runtime identity.</summary>
public sealed record GameEditorCandidate(GameBuildArtifact Build, long ContextVersion, SessionIdentity? Origin);

/// <summary>Immutable command status. Error retains the complete original and recovery exceptions.</summary>
public sealed record GameEditorStatus(long ContextVersion, GameEditorContext? Context, GameEditorOperation Operation,
    GameEditorCandidate? Candidate, Exception? Error, bool Closed);
