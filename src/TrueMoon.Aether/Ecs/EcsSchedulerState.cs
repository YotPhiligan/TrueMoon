namespace TrueMoon.Aether.Ecs;

/// <summary>Terminal stop/fault requires a new scheduler; no automatic partial-tick retry.</summary>
public enum EcsSchedulerState
{
    /// <summary>Ready for a caller-driven fixed tick.</summary>
    Ready,
    /// <summary>Executing a serialized tick.</summary>
    Running,
    /// <summary>Cooperatively stopped, with pending phase commands discarded.</summary>
    Stopped,
    /// <summary>Execution/cleanup failed. Inspect Failure and dispose.</summary>
    Faulted,
    /// <summary>All owned resources released and World lease returned.</summary>
    Disposed
}
