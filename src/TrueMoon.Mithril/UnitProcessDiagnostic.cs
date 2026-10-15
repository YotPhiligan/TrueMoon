namespace TrueMoon.Mithril;

/// <summary>Observable transitions of an owned process sequence.</summary>
public enum UnitProcessStage
{
    /// <summary>Preparation of a new launch attempt.</summary>
    LaunchStarting,
    /// <summary>OS start succeeded; application readiness is not implied.</summary>
    Started,
    /// <summary>Launch preparation or OS start failed or was cancelled.</summary>
    LaunchFailed,
    /// <summary>A natural exit was observed by the monitor.</summary>
    Exited,
    /// <summary>The owned Process wrapper was disposed.</summary>
    ProcessReleased,
    /// <summary>Policy and budget permit another attempt after the configured delay.</summary>
    RestartScheduled,
    /// <summary>An automatic attempt consumed one unit of the restart budget.</summary>
    Restarting,
    /// <summary>The exit/restart monitor failed; see Reason and Error.</summary>
    SequenceFailed,
    /// <summary>The sequence ended naturally, exhausted its budget, or failed.</summary>
    Terminal,
    /// <summary>Cleanup entered the lifecycle gate after prohibiting further restarts.</summary>
    StopRequested,
    /// <summary>The still-running owned process tree requires forced termination.</summary>
    Killing,
    /// <summary>Process cleanup succeeded; monitor drained except when Reason is TerminalCallback (self-wait is skipped).</summary>
    Stopped,
    /// <summary>Cleanup failed; the existing ownership/retry contract still applies.</summary>
    StopFailed
}

/// <summary>
/// Snapshot emitted through IEventsSource in category UnitProcess. Correlate by
/// RunId and Generation; generation counts launch attempts, including failed preparation.
/// Arguments, environment values and control endpoints are not included.
/// </summary>
/// <param name="Stage">Observed transition.</param>
/// <param name="RunId">Identity of one explicit StartAsync sequence; empty before the first start.</param>
/// <param name="UnitIndex">Configured unit index.</param>
/// <param name="UnitName">Configured unit name.</param>
/// <param name="Generation">One-based launch attempt; zero before preparation.</param>
/// <param name="ProcessId">Owned process PID, or null when the current attempt has not started.</param>
/// <param name="ParentProcessId">PID of the process that owns the handle and emits the event.</param>
/// <param name="RestartCount">Automatic attempts consumed in this sequence.</param>
/// <param name="RestartPolicy">Effective restart policy.</param>
/// <param name="RestartLimit">Effective automatic-attempt budget.</param>
/// <param name="RestartDelay">Effective delay in milliseconds.</param>
/// <param name="TerminationDelay">Effective graceful shutdown timeout in milliseconds.</param>
/// <param name="Reason">Transition-specific decision or cleanup outcome.</param>
/// <param name="ExitCode">Observed exit code, when available.</param>
/// <param name="Error">Original failure, when available. Its message can contain OS diagnostic details.</param>
public sealed record UnitProcessDiagnostic(UnitProcessStage Stage, Guid RunId, int UnitIndex, string UnitName,
    int Generation, int? ProcessId, int ParentProcessId, int RestartCount, UnitRestartPolicy RestartPolicy, int RestartLimit,
    int RestartDelay, int TerminationDelay, string? Reason = null, int? ExitCode = null, Exception? Error = null);
