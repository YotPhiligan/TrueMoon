namespace TrueMoon.Kul;

/// <summary>Current state of one playback instance.</summary>
public enum AudioPlaybackState
{
    /// <summary>Actively advancing.</summary>
    Playing,
    /// <summary>Paused, retaining its cursor and resources.</summary>
    Paused,
    /// <summary>Stopped and rewound.</summary>
    Stopped,
    /// <summary>Reached EOF; collect or dispose to return admission capacity.</summary>
    Ended,
    /// <summary>Released by its caller, session, collection or App.</summary>
    Disposed
}
