namespace TrueMoon.Kul;

/// <summary>Selects actual Windows playback or explicit device-free mixing.</summary>
public enum AudioMode
{
    /// <summary>Opens the default WASAPI playback device during App startup.</summary>
    Device,
    /// <summary>Opens no device; the consumer explicitly renders PCM.</summary>
    Offline
}
