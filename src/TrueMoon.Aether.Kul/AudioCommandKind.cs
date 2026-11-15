namespace TrueMoon.Aether.Kul;

/// <summary>Explicit source transport commands.</summary>
public enum AudioCommandKind
{
    /// <summary>Rewind and play, including after EOF.</summary>
    Play = 1,
    /// <summary>Continue without replaying natural EOF.</summary>
    Resume = 2,
    /// <summary>Pause, preserving position.</summary>
    Pause = 3,
    /// <summary>Stop and rewind.</summary>
    Stop = 4,
    /// <summary>Seek, preserving desired transport mode. Unknown-length streams support rewind only.</summary>
    Seek = 5
}
