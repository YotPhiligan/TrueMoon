using TrueMoon.Kul.Native;

namespace TrueMoon.Kul;

/// <summary>Prepared sound PCM. Voices share its storage; disposing the clip prevents new plays.</summary>
public sealed class AudioClip : IDisposable
{
    internal AudioClip(AudioSession session, NativeHandle handle, ulong frames, long bytes)
    { Session = session; Handle = handle; Frames = frames; Bytes = bytes; }
    internal AudioSession Session { get; }
    internal NativeHandle Handle { get; }
    internal long Bytes { get; }
    internal int Borrowers;
    internal bool Released;
    internal bool Freed;
    /// <summary>Decoded frame length at the App sample rate.</summary>
    public ulong Frames { get; }
    /// <summary>Whether the caller has released the clip. Existing voices may still retain its PCM.</summary>
    public bool IsDisposed { get { lock (Session.Host.Gate) return Released; } }
    /// <summary>Rejects future plays; existing voices retain storage until their own disposal.</summary>
    public void Dispose() => Session.Host.Release(this);
}
