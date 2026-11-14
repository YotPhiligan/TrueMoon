using TrueMoon.Kul.Hosting;

namespace TrueMoon.Kul;

/// <summary>Caller-owned playback group. Dispose releases all of its voices and clips; App stop also drains it.</summary>
public sealed class AudioSession : IDisposable
{
    internal AudioSession(AudioHost host) => Host = host;
    internal AudioHost Host { get; }
    internal List<AudioClip> Clips { get; } = [];
    internal List<AudioPlayback> Voices { get; } = [];
    internal bool Closed;

    /// <summary>Whether this session has been released by its caller or the App.</summary>
    public bool IsDisposed { get { lock (Host.Gate) return Closed; } }
    /// <summary>Synchronously prepares decoded sound PCM. Call outside frame and audio callbacks.</summary>
    /// <param name="path">Existing local file, opened with the Windows UTF-16 entry point.</param>
    /// <returns>A clip owned by this session, reusable for independent voices.</returns>
    public AudioClip LoadSound(string path) => Host.LoadSound(this, path);
    /// <summary>Starts an independent instance of a clip belonging to this session.</summary>
    /// <param name="clip">Prepared clip.</param><param name="options">Captured playback settings.</param>
    /// <returns>A caller-owned playback, also tracked by this session until released.</returns>
    public AudioPlayback PlaySound(AudioClip clip, AudioPlayOptions? options = null) => Host.PlaySound(this, clip, options ?? new());
    /// <summary>Opens and starts streamed music using native resource-manager workers.</summary>
    /// <param name="path">Existing local file. Retain its contents until playback disposal.</param>
    /// <param name="options">Captured playback settings.</param><returns>A caller-owned streaming playback.</returns>
    public AudioPlayback PlayMusic(string path, AudioPlayOptions? options = null) => Host.PlayMusic(this, path, options ?? new());
    /// <summary>Disposes voices that have reached EOF, returning admission capacity.</summary>
    /// <returns>Number of collected voices.</returns>
    public int CollectFinished() => Host.CollectFinished(this);
    /// <summary>Stops and releases this session's voices, streams and clips. Repeated calls are harmless.</summary>
    public void Dispose() => Host.Release(this);
}
