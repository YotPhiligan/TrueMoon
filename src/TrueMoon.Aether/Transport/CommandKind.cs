namespace TrueMoon.Aether.Transport;

public enum CommandKind
{
    Hello = 1, LoadScene, SetMode, Step, GetStatus, Stop, ConfigureSurface,
    /// <summary>Observational capture at a paused tick boundary.</summary>
    CaptureCheckpoint,
    /// <summary>Restore on a fresh runtime before mode/surface readiness.</summary>
    RestoreCheckpoint,
    /// <summary>Stages bounded transient gameplay input for the next fixed tick.</summary>
    SetInput
}
