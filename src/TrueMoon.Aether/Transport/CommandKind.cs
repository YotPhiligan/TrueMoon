namespace TrueMoon.Aether.Transport;

public enum CommandKind
{
    Hello = 1, LoadScene, SetMode, Step, GetStatus, Stop, ConfigureSurface,
    /// <summary>Observational capture at a paused tick boundary.</summary>
    CaptureCheckpoint,
    /// <summary>Restore on a fresh runtime before mode/surface readiness.</summary>
    RestoreCheckpoint,
    /// <summary>Stages bounded transient gameplay input for the next fixed tick.</summary>
    SetInput,
    /// <summary>Admits one bounded full-scene transfer on a fresh ECS-capable runtime.</summary>
    BeginSceneWorld,
    /// <summary>Appends one contiguous full-scene chunk within the existing control limit.</summary>
    AppendSceneWorld,
    /// <summary>Validates and atomically activates the completely staged initial World.</summary>
    ActivateSceneWorld,
    /// <summary>Abandons initial staging; a new transfer requires a new runtime generation.</summary>
    AbortSceneWorld,
    /// <summary>Updates the explicit external view on the same EditPreview runtime, without simulation or surface replacement.</summary>
    SetPreviewView
}
