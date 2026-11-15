using MiniAudioEx.Native;
using TrueMoon.Kul.Native;
using static TrueMoon.Kul.Native.AudioGraph;

namespace TrueMoon.Kul.Hosting;

// Sole DI disposal owner. Aliases and lifecycle adapters borrow this host.
internal sealed class AudioHost(AudioOptions settings) : IDisposable
{
    internal object Gate { get; } = new();
    private readonly List<AudioSession> sessions = [];
    private AudioGraph? graph;
    private bool running, stopping;
    private long residentBytes;
    private float volume = settings.Volume;

    internal Task StartAsync(CancellationToken token)
    {
        lock (Gate)
        {
            token.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(stopping, this);
            if (running) return Task.CompletedTask;
            try
            {
                graph = new(settings);
                token.ThrowIfCancellationRequested();
                running = true;
            }
            catch (Exception error)
            {
                try { Dispose(); }
                catch (Exception cleanup) { throw new AggregateException("Audio startup and rollback failed.", error, cleanup); }
                throw;
            }
        }
        return Task.CompletedTask;
    }

    internal AudioSession CreateSession()
    {
        lock (Gate)
        {
            EnsureRunning();
            if (sessions.Count >= settings.MaximumSessions) throw new InvalidOperationException("Audio session limit reached.");
            var session = new AudioSession(this);
            sessions.Add(session);
            return session;
        }
    }

    internal AudioClip LoadSound(AudioSession session, string path)
    {
        lock (Gate)
        {
            EnsureSession(session);
            var file = FilePath(path);
            if (new FileInfo(file).Length > settings.MaximumSoundFileBytes) throw new InvalidOperationException("Encoded sound file limit exceeded; use music streaming for long audio.");
            if (sessions.Sum(x => x.Clips.Count) >= settings.MaximumClips) throw new InvalidOperationException("Audio clip limit reached.");
            var handle = graph!.Load(file, false);
            try
            {
                Check(MiniAudioNative.ma_sound_get_length_in_pcm_frames(new(handle.Pointer), out var frames), "Read decoded sound length");
                var bytes = checked((long)frames * 2 * sizeof(float));
                if (bytes > settings.MaximumResidentSoundBytes - residentBytes) throw new InvalidOperationException("Retained sound PCM limit exceeded.");
                var clip = new AudioClip(session, handle, frames, bytes);
                session.Clips.Add(clip);
                residentBytes += bytes;
                return clip;
            }
            catch { handle.Dispose(); throw; }
        }
    }

    internal AudioPlayback PlaySound(AudioSession session, AudioClip clip, AudioPlayOptions options)
    {
        ArgumentNullException.ThrowIfNull(clip);
        lock (Gate)
        {
            EnsureSession(session);
            if (!ReferenceEquals(clip.Session, session)) throw new ArgumentException("The clip belongs to another audio session.", nameof(clip));
            ObjectDisposedException.ThrowIf(clip.Released, clip);
            Admit(options, false);
            var handle = graph!.Copy(clip.Handle);
            try
            {
                var voice = StartVoice(session, handle, clip, false, options);
                clip.Borrowers++;
                return voice;
            }
            catch { handle.Dispose(); throw; }
        }
    }

    internal AudioPlayback PlayMusic(AudioSession session, string path, AudioPlayOptions options)
    {
        lock (Gate)
        {
            EnsureSession(session);
            var file = FilePath(path);
            Admit(options, true);
            var handle = graph!.Load(file, true);
            try { return StartVoice(session, handle, null, true, options); }
            catch { handle.Dispose(); throw; }
        }
    }

    private AudioPlayback StartVoice(AudioSession session, NativeHandle handle, AudioClip? clip, bool streaming, AudioPlayOptions options)
    {
        var voice = new AudioPlayback(session, handle, clip, streaming)
        { Paused = options.StartPaused, RetainAtEnd = options.RetainAtEnd, Looping = options.Loop };
        MiniAudioNative.ma_sound_set_volume(voice.Pointer, options.Volume);
        MiniAudioNative.ma_sound_set_looping(voice.Pointer, options.Loop ? 1u : 0u);
        if (!options.StartPaused) Check(MiniAudioNative.ma_sound_start(voice.Pointer), "Start audio voice");
        session.Voices.Add(voice);
        return voice;
    }

    private void Admit(AudioPlayOptions options, bool streaming)
    {
        AudioOptions.ValidateVolume(options.Volume);
        foreach (var session in sessions) CollectFinishedLocked(session);
        if (sessions.Sum(x => x.Voices.Count) >= settings.MaximumVoices) throw new InvalidOperationException("Audio voice limit reached. Dispose paused/stopped voices to return capacity.");
        if (streaming && sessions.Sum(x => x.Voices.Count(v => v.IsStreaming)) >= settings.MaximumMusicStreams)
            throw new InvalidOperationException("Music stream limit reached.");
    }

    internal int CollectFinished(AudioSession session)
    {
        lock (Gate) { EnsureSession(session); return CollectFinishedLocked(session); }
    }
    private int CollectFinishedLocked(AudioSession session)
    {
        int collected = 0;
        foreach (var voice in session.Voices.ToArray())
            if (!voice.RetainAtEnd && MiniAudioNative.ma_sound_at_end(voice.Pointer) != 0) { Release(voice); collected++; }
        return collected;
    }

    internal void Release(AudioPlayback voice)
    {
        lock (Gate)
        {
            if (voice.Released) return;
            // Native node detach/uninit synchronizes with the native mixer; the callback takes no managed lock.
            voice.Handle.Dispose();
            voice.Released = true;
            voice.Session.Voices.Remove(voice);
            if (voice.Clip is { } clip)
            {
                clip.Borrowers--;
                FreeClip(clip);
            }
        }
    }
    internal void Release(AudioClip clip)
    {
        lock (Gate) { clip.Released = true; FreeClip(clip); }
    }
    private void FreeClip(AudioClip clip)
    {
        if (!clip.Released || clip.Borrowers != 0 || clip.Freed) return;
        clip.Handle.Dispose();
        clip.Freed = true;
        clip.Session.Clips.Remove(clip);
        residentBytes -= clip.Bytes;
    }
    internal void Release(AudioSession session)
    {
        lock (Gate)
        {
            if (session.Closed) return;
            foreach (var voice in session.Voices.ToArray()) Release(voice);
            foreach (var clip in session.Clips.ToArray()) Release(clip);
            session.Closed = true;
            sessions.Remove(session);
        }
    }

    internal AudioPlaybackState GetState(AudioPlayback voice)
    {
        lock (Gate)
        {
            if (voice.Released) return AudioPlaybackState.Disposed;
            if (MiniAudioNative.ma_sound_at_end(voice.Pointer) != 0) return AudioPlaybackState.Ended;
            if (MiniAudioNative.ma_sound_is_playing(voice.Pointer) != 0) return AudioPlaybackState.Playing;
            return voice.Paused ? AudioPlaybackState.Paused : AudioPlaybackState.Stopped;
        }
    }
    internal void Play(AudioPlayback voice)
    {
        lock (Gate)
        {
            EnsureVoice(voice);
            if (MiniAudioNative.ma_sound_at_end(voice.Pointer) != 0) Check(MiniAudioNative.ma_sound_seek_to_pcm_frame(voice.Pointer, 0), "Rewind ended voice");
            Check(MiniAudioNative.ma_sound_start(voice.Pointer), "Play voice");
            voice.Paused = false;
        }
    }
    internal void Pause(AudioPlayback voice)
    {
        lock (Gate) { EnsureVoice(voice); Check(MiniAudioNative.ma_sound_stop(voice.Pointer), "Pause voice"); voice.Paused = true; }
    }
    internal void Stop(AudioPlayback voice)
    {
        lock (Gate)
        {
            EnsureVoice(voice);
            Check(MiniAudioNative.ma_sound_stop(voice.Pointer), "Stop voice");
            Check(MiniAudioNative.ma_sound_seek_to_pcm_frame(voice.Pointer, 0), "Rewind voice");
            voice.Paused = false;
        }
    }
    internal ulong Cursor(AudioPlayback voice)
    {
        lock (Gate) { EnsureVoice(voice); Check(MiniAudioNative.ma_sound_get_cursor_in_pcm_frames(voice.Pointer, out var cursor), "Read audio cursor"); return cursor; }
    }
    internal ulong? Length(AudioPlayback voice)
    {
        lock (Gate)
        {
            EnsureVoice(voice);
            var result = MiniAudioNative.ma_sound_get_length_in_pcm_frames(voice.Pointer, out var length);
            if (result == ma_result.not_implemented) return null;
            Check(result, "Read voice length");
            return length;
        }
    }
    internal void Seek(AudioPlayback voice, ulong frame)
    {
        lock (Gate)
        {
            EnsureVoice(voice);
            if (Length(voice) is { } length && frame > length) throw new ArgumentOutOfRangeException(nameof(frame));
            Check(MiniAudioNative.ma_sound_seek_to_pcm_frame(voice.Pointer, frame), "Seek voice");
        }
    }
    internal float GetVolume(AudioPlayback voice)
    {
        lock (Gate) { EnsureVoice(voice); return MiniAudioNative.ma_sound_get_volume(voice.Pointer); }
    }
    internal void SetVolume(AudioPlayback voice, float value)
    {
        AudioOptions.ValidateVolume(value);
        lock (Gate) { EnsureVoice(voice); MiniAudioNative.ma_sound_set_volume(voice.Pointer, value); }
    }
    internal bool GetLoop(AudioPlayback voice)
    { lock (Gate) { EnsureVoice(voice); return voice.Looping; } }
    internal void SetLoop(AudioPlayback voice, bool value)
    { lock (Gate) { EnsureVoice(voice); MiniAudioNative.ma_sound_set_looping(voice.Pointer, value ? 1u : 0u); voice.Looping = value; } }
    internal float Volume
    {
        get { lock (Gate) { EnsureRunning(); return volume; } }
        set { AudioOptions.ValidateVolume(value); lock (Gate) { EnsureRunning(); Check(MiniAudioNative.ma_engine_set_volume(graph!.Engine, value), "Set master volume"); volume = value; } }
    }
    internal AudioSnapshot Snapshot()
    {
        lock (Gate) return new(running, settings.Mode, settings.SampleRate, sessions.Count, sessions.Sum(s => s.Clips.Count),
            sessions.Sum(s => s.Voices.Count), sessions.Sum(s => s.Voices.Count(v => v.IsStreaming)), residentBytes,
            running ? graph!.PeriodFrames : 0, running ? graph!.Periods : 0);
    }
    internal unsafe void Render(Span<float> samples)
    {
        lock (Gate)
        {
            EnsureRunning();
            if (settings.Mode != AudioMode.Offline) throw new InvalidOperationException("Explicit rendering requires Offline audio mode.");
            if (samples.IsEmpty || samples.Length % 2 != 0) throw new ArgumentException("Provide nonempty interleaved stereo output.", nameof(samples));
            fixed (float* output = samples)
                Check(MiniAudioNative.ma_engine_read_pcm_frames(graph!.Engine, (nint)output, (ulong)samples.Length / 2), "Render offline audio");
        }
    }
    private void EnsureRunning()
    {
        ObjectDisposedException.ThrowIf(stopping, this);
        if (!running) throw new InvalidOperationException("Start the App before creating audio sessions or using playback.");
    }
    private void EnsureSession(AudioSession session) { EnsureRunning(); ObjectDisposedException.ThrowIf(session.Closed, session); }
    private void EnsureVoice(AudioPlayback voice) { EnsureSession(voice.Session); ObjectDisposedException.ThrowIf(voice.Released, voice); }
    private static string FilePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = Path.GetFullPath(path);
        if (!File.Exists(full)) throw new FileNotFoundException("The audio file does not exist.", full);
        return full;
    }
    internal Task StopAsync(CancellationToken token)
    {
        // Teardown is intentionally non-cancellable: cancellation must not free a live callback's dependencies.
        Dispose();
        return Task.CompletedTask;
    }
    public void Dispose()
    {
        lock (Gate)
        {
            stopping = true;
            running = false;
            graph?.Drain(); // A failure retains the whole graph and all children for a later retry.
            foreach (var session in sessions.ToArray()) Release(session);
            graph?.Dispose();
            graph = null;
        }
    }
}
