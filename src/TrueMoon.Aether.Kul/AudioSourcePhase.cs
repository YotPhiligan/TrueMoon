namespace TrueMoon.Aether.Kul;

/// <summary>Observed source transport; actual playback is also gated by the session mode.</summary>
public enum AudioSourcePhase
{
    /// <summary>Stopped and rewound.</summary>
    Stopped = 1,
    /// <summary>Playing.</summary>
    Playing = 2,
    /// <summary>Paused by source command or session mode.</summary>
    Paused = 3,
    /// <summary>Natural EOF, retained for explicit replay.</summary>
    Ended = 4
}
