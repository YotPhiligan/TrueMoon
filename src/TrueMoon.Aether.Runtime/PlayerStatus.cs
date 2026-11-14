namespace TrueMoon.Aether.Runtime;

/// <summary>Local standalone session lifetime, separate from process launch role.</summary>
public enum PlayerState
{
    /// <summary>No game instance exists.</summary>
    Created,
    /// <summary>Selected game activation is in progress.</summary>
    Loading,
    /// <summary>CPU scene active; Playing may still require presentation.</summary>
    Paused,
    /// <summary>Simulation is admitted.</summary>
    Playing,
    /// <summary>Admission closed, owned work draining.</summary>
    Stopping,
    /// <summary>All owned game/render resources released.</summary>
    Stopped,
    /// <summary>Work failed; cleanup remains explicit and owned.</summary>
    Faulted,
    /// <summary>Cleanup failed and retains its dependent owners for retry.</summary>
    CleanupFailed
}

/// <summary>Detached local session observation; never contains native handles or the mutable World.</summary>
public sealed record PlayerStatus(PlayerState State,ulong Tick,bool RenderViewReady,Exception? Error=null);
