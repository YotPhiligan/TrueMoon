using MiniAudioEx.Native;
using TrueMoon.Kul.Native;

namespace TrueMoon.Kul;

/// <summary>Caller-owned voice. Pause/Stop retain resources; Dispose or session collection releases them.</summary>
public sealed class AudioPlayback : IDisposable
{
    internal AudioPlayback(AudioSession session, NativeHandle handle, AudioClip? clip, bool streaming)
    { Session = session; Handle = handle; Clip = clip; IsStreaming = streaming; }
    internal AudioSession Session { get; }
    internal NativeHandle Handle { get; }
    internal AudioClip? Clip { get; }
    internal bool Released;
    internal bool Paused;
    internal bool RetainAtEnd;
    internal bool Looping;
    /// <summary>Whether this voice uses worker-fed music streaming.</summary>
    public bool IsStreaming { get; }
    /// <summary>Current state, including Disposed after App stop.</summary>
    public AudioPlaybackState State => Session.Host.GetState(this);
    /// <summary>Decoded cursor at the App sample rate; unknown-length OGG loops have a monotonic cursor.</summary>
    public ulong CursorFrames => Session.Host.Cursor(this);
    /// <summary>Decoded duration in frames, or null when the decoder cannot report it.</summary>
    public ulong? LengthFrames => Session.Host.Length(this);
    /// <summary>Current voice gain, between zero and one.</summary>
    public float Volume { get => Session.Host.GetVolume(this); set => Session.Host.SetVolume(this, value); }
    /// <summary>Whether playback repeats at EOF. Changes affect this voice only.</summary>
    public bool Loop { get => Session.Host.GetLoop(this); set => Session.Host.SetLoop(this, value); }
    /// <summary>Resumes a paused/stopped voice; an ended voice is rewound before starting.</summary>
    public void Play() => Session.Host.Play(this);
    /// <summary>Pauses at the current cursor.</summary>
    public void Pause() => Session.Host.Pause(this);
    /// <summary>Stops and rewinds, retaining resources for replay.</summary>
    public void Stop() => Session.Host.Stop(this);
    /// <summary>Requests a seek to an output PCM frame; streamed refill completes on native workers and may briefly render silence.</summary>
    /// <param name="frame">Decoded frame position.</param>
    public void Seek(ulong frame) => Session.Host.Seek(this, frame);
    /// <summary>Stops and releases this instance. Repeated calls are harmless.</summary>
    public void Dispose() => Session.Host.Release(this);
    internal ma_sound_ptr Pointer => new(Handle.Pointer);
}
