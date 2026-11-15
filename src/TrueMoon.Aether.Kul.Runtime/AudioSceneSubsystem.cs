using TrueMoon.Aether.Abstractions.Ecs;
using TrueMoon.Aether.Kul.Runtime.Content;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Loading;
using TrueMoon.Aether.Runtime.Ecs.Runtime;
using TrueMoon.Kul;
using A = TrueMoon.Aether.Abstractions.Ecs;

namespace TrueMoon.Aether.Kul.Runtime;

/// <summary>Scene-owned prepared audio bank for a borrowed World and App audio device.</summary>
/// <remarks>Calls are externally serialized. Dispose the scheduler first, then this owner, then World/App.
/// Native session cleanup precedes content-pin release. Failed cleanup remains owned for retry.
/// Controls have audible side effects; a failed tick is terminal and cannot roll those effects back.</remarks>
public sealed class AudioSceneSubsystem : ISceneSubsystem
{
    private readonly SceneWorld _scene;
    private readonly World _world;
    private readonly TimeSpan _step;
    private readonly AudioBridgeOptions _options;
    private IAudioContentLease? _content;
    private IDisposable? _retainedContent;
    private AudioSession? _session;
    private readonly Dictionary<AudioAssetId, Slot[]> _bank = [];
    private readonly Dictionary<EntityHandle, Entry> _entries = [];
    private readonly List<Candidate> _candidates = [];
    private readonly Dictionary<EntityHandle, Candidate> _present = [];
    private bool _playing, _busy, _faulted, _disposing, _disposed;
    internal AudioSceneSubsystem(SceneSubsystemArguments arguments, AudioBridgeOptions options)
    { _scene = arguments.Scene; _world = _scene.World; _step = arguments.FixedStep; _options = options; }
    /// <summary>Last fully accepted audio tick; native device time advances independently.</summary>
    public ulong LastCompletedTick { get; private set; }
    /// <summary>One immutable registration. The scheduler owns its system instance.</summary>
    public IReadOnlyList<EcsSystemRegistration> Systems { get; private set; } = [];
    /// <summary>Whether native resources and capture pins have all been released.</summary>
    public bool IsDisposed => _disposed;
    /// <summary>Samples only a current live source generation. ECS AudioSourceState remains an end-of-tick sample.</summary>
    /// <param name="entity">Exact ECS identity.</param><param name="state">Current observed state.</param><returns>Whether currently attached.</returns>
    public bool TryGetState(EntityHandle entity, out AudioSourceState state)
    {
        state = default;
        if (_disposed || _disposing || _busy || _session is null || _session.IsDisposed || _world.IsDisposed || !_world.IsAlive(entity) ||
            !_world.HasComponent<AudioSource>(entity) || !_entries.TryGetValue(entity, out var entry)) return false;
        state = Sample(entry, LastCompletedTick); return true;
    }
    internal void Prepare(IFactory<AudioSession> sessions, IFactory<IAudioContentLease, AudioContentLoadArguments> content,
        IFactory<AudioBridgeSystem, AudioSceneSubsystem> systems, AudioContentReference reference, CancellationToken token)
    {
        if (!_scene.IsReady || _world.IsDisposed || _world.IsFaulted || _step <= TimeSpan.Zero) throw new InvalidOperationException("A healthy private scene and positive fixed step are required.");
        token.ThrowIfCancellationRequested();
        try { _content = content.Create(new(reference, _options.MaximumAssets, _options.MaximumEncodedBytes, token)) ?? throw new InvalidOperationException("Audio content factory returned null."); }
        catch (SceneWorldPreparationException error) { _retainedContent = error.CleanupOwner; throw; }
        // An injected immutable provider is still subject to the module's copied admission limits.
        var manifest = new AudioContentManifest(_content.Manifest.Assets);
        long bytes = 0; var voices = 0;
        foreach (var asset in manifest.Assets)
        {
            if (asset.Length > _options.MaximumEncodedBytes - bytes) throw new InvalidDataException("Encoded audio limit exceeded.");
            bytes += asset.Length; voices = checked(voices + asset.Voices);
        }
        if (manifest.Assets.Count > _options.MaximumAssets || voices > _options.MaximumPreparedVoices) throw new InvalidDataException("Prepared audio capacity exceeded.");
        token.ThrowIfCancellationRequested();
        _session = sessions.Create() ?? throw new InvalidOperationException("Audio session factory returned null.");
        foreach (var asset in manifest.Assets)
        {
            token.ThrowIfCancellationRequested();
            var path = _content.GetPath(asset.Id);
            AudioClip? clip = asset.Kind == AudioAssetKind.Sound ? _session.LoadSound(path) : null;
            var slots = new Slot[asset.Voices]; _bank.Add(asset.Id, slots);
            for (var index = 0; index < slots.Length; index++)
            {
                token.ThrowIfCancellationRequested();
                var options = new AudioPlayOptions { StartPaused = true, RetainAtEnd = true, Volume = 0 };
                var voice = clip is null ? _session.PlayMusic(path, options) : _session.PlaySound(clip, options);
                slots[index] = new(voice, voice.LengthFrames);
            }
        }
        Capture(initial: true, token); token.ThrowIfCancellationRequested(); Reconcile();
        foreach (var candidate in _candidates)
        {
            if (!_world.HasComponent<AudioSourceState>(candidate.Entity)) _world.AddComponent(candidate.Entity, default(AudioSourceState));
            Apply(candidate); _world.SetComponent(candidate.Entity, Sample(_entries[candidate.Entity], 0));
        }
        token.ThrowIfCancellationRequested();
        var description = new A.EcsSystemDescription("kul.audio", EcsPhase.Extraction).After("builtin.primitive-extraction")
            .Read<AudioSource>().Read<AudioPlaybackCommand>().Write<AudioSourceState>();
        Systems = [new(description, new Factory<A.ISystem, World>(world =>
        {
            if (!ReferenceEquals(world, _world)) throw new InvalidOperationException("Audio system World mismatch.");
            return systems.Create(this) ?? throw new InvalidOperationException("Audio system factory returned null.");
        }))];
    }
    private void Capture(bool initial, CancellationToken token)
    {
        _candidates.Clear(); _present.Clear(); var counts = new Dictionary<AudioAssetId, int>();
        _world.Query<AudioSource>().ForEach((EntityHandle entity, in AudioSource source) =>
        {
            token.ThrowIfCancellationRequested();
            if (_candidates.Count == _options.MaximumSources) throw new InvalidDataException("Audio source limit exceeded.");
            if (!source.Asset.IsValid || !_bank.TryGetValue(source.Asset, out var slots) || !float.IsFinite(source.Volume) || source.Volume is < 0 or > 1)
                throw new InvalidDataException("Unknown audio asset or invalid source gain.");
            counts.TryGetValue(source.Asset, out var count);
            if (count == slots.Length) throw new InvalidDataException("Captured audio voice reservation exceeded.");
            counts[source.Asset] = count + 1;
            if (!initial && !_world.HasComponent<AudioSourceState>(entity)) throw new InvalidDataException("Runtime audio attachment requires complete roles; use AudioEntityCommands.");
            _entries.TryGetValue(entity, out var previous);
            if (previous is not null && previous.Source.Asset == source.Asset && previous.Source.AutoPlay != source.AutoPlay)
                throw new InvalidDataException("AutoPlay is binding-time policy; use playback commands.");
            AudioPlaybackCommand? command = _world.TryGetComponent<AudioPlaybackCommand>(entity, out var request) ? request : null;
            if (command is { } value)
            {
                var last = previous?.LastCommand ?? default;
                if (value.Sequence == 0 || !Enum.IsDefined(value.Kind) || value.Sequence < last.Sequence || value.Sequence == last.Sequence && value != last ||
                    value.Kind != AudioCommandKind.Seek && value.Frame != 0)
                    throw new InvalidDataException("Invalid audio command or sequence replay.");
                if (value.Sequence > last.Sequence && value.Kind == AudioCommandKind.Seek && (slots[0].Length is { } length ? value.Frame > length : value.Frame != 0))
                    throw new NotSupportedException("Seek exceeds prepared length; unknown-length streams support rewind only.");
            }
            var candidate = new Candidate(entity, source, command, previous); _candidates.Add(candidate); _present.Add(entity, candidate);
        });
        foreach (var entity in _entries.Keys)
            if (!_present.ContainsKey(entity) && _world.IsAlive(entity) && (_world.HasComponent<AudioSourceState>(entity) || _world.HasComponent<AudioPlaybackCommand>(entity)))
                throw new InvalidDataException("Audio detachment must remove all owned roles; use AudioEntityCommands.RemoveSource.");
        _candidates.Sort(static (a, b) => a.Entity.Index.CompareTo(b.Entity.Index));
    }
    private void Reconcile()
    {
        foreach (var pair in _entries.ToArray())
        {
            if (_present.TryGetValue(pair.Key, out var candidate) && candidate.Source.Asset == pair.Value.Source.Asset) continue;
            pair.Value.Slot.Voice.Stop(); pair.Value.Slot.InUse = false; _entries.Remove(pair.Key);
        }
        foreach (var candidate in _candidates)
        {
            if (_entries.ContainsKey(candidate.Entity)) continue;
            var slot = _bank[candidate.Source.Asset].First(s => !s.InUse); slot.Voice.Stop(); slot.InUse = true;
            _entries.Add(candidate.Entity, new(candidate.Source, slot)
            { Desired = candidate.Source.AutoPlay ? AudioSourcePhase.Playing : AudioSourcePhase.Stopped, LastCommand = candidate.Previous?.LastCommand ?? default });
        }
    }
    private void Apply(Candidate candidate)
    {
        var entry = _entries[candidate.Entity]; var voice = entry.Slot.Voice;
        ObserveEnd(entry); voice.Volume = candidate.Source.Volume; voice.Loop = candidate.Source.Loop; entry.Source = candidate.Source;
        if (candidate.Command is { } command && command.Sequence > entry.LastCommand.Sequence)
        {
            switch (command.Kind)
            {
                case AudioCommandKind.Play: voice.Stop(); entry.Rewound = true; entry.Desired = AudioSourcePhase.Playing; break;
                case AudioCommandKind.Resume:
                    if (entry.Desired != AudioSourcePhase.Ended) entry.Desired = AudioSourcePhase.Playing;
                    break;
                case AudioCommandKind.Pause: voice.Pause(); entry.Desired = AudioSourcePhase.Paused; break;
                case AudioCommandKind.Stop: voice.Stop(); entry.Rewound = true; entry.Desired = AudioSourcePhase.Stopped; break;
                case AudioCommandKind.Seek: voice.Seek(command.Frame); break;
            }
            entry.LastCommand = command;
        }
        Synchronize(entry);
    }
    private static void ObserveEnd(Entry entry)
    { if (!entry.Rewound && entry.Desired == AudioSourcePhase.Playing && entry.Slot.Voice.State == AudioPlaybackState.Ended) entry.Desired = AudioSourcePhase.Ended; }
    private void Synchronize(Entry entry)
    {
        ObserveEnd(entry); var voice = entry.Slot.Voice;
        if (entry.Desired == AudioSourcePhase.Playing)
        {
            if (_playing && (entry.Rewound || voice.State != AudioPlaybackState.Playing)) { voice.Play(); entry.Rewound = false; }
            else if (!_playing && voice.State == AudioPlaybackState.Playing) voice.Pause();
        }
    }
    private AudioSourceState Sample(Entry entry, ulong tick)
    {
        ObserveEnd(entry);
        var phase = entry.Desired == AudioSourcePhase.Playing && !_playing ? AudioSourcePhase.Paused : entry.Desired;
        return new(tick, entry.LastCommand.Sequence, phase, entry.Slot.Voice.CursorFrames);
    }
    internal void Advance(A.EcsSystemContext context)
    {
        Ready();
        if (context.WorldId != _world.Id || context.DeltaTime != _step || context.TickNumber != LastCompletedTick + 1) throw new InvalidOperationException("Audio scene World/step/tick mismatch.");
        _busy = true;
        try
        {
            Capture(initial: false, context.CancellationToken); context.CancellationToken.ThrowIfCancellationRequested();
            // Commit begins after complete preflight. No file IO, decoding, factory resolution or voice allocation below.
            Reconcile();
            foreach (var candidate in _candidates) { Apply(candidate); _world.SetComponent(candidate.Entity, Sample(_entries[candidate.Entity], context.TickNumber)); }
            LastCompletedTick = context.TickNumber;
        }
        catch { _faulted = true; PauseBestEffort(); throw; }
        finally { _busy = false; }
    }
    /// <summary>Forwards actual game mode. Pause preserves each source's desired transport; paused Step stays silent.</summary>
    /// <param name="playing">Actual runtime mode, not tick admission.</param>
    public void SetPlaying(bool playing)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_busy) throw new InvalidOperationException("Leave audio control before changing mode.");
        if (!playing)
        {
            _playing = false;
            if (_session is null || _session.IsDisposed) return;
            _busy = true;
            try { foreach (var entry in _entries.Values) { ObserveEnd(entry); if (entry.Slot.Voice.State == AudioPlaybackState.Playing) entry.Slot.Voice.Pause(); } }
            catch { _faulted = true; PauseBestEffort(); throw; }
            finally { _busy = false; }
            return;
        }
        Ready(); _busy = true; _playing = true;
        try { foreach (var entry in _entries.Values) Synchronize(entry); }
        catch { _faulted = true; PauseBestEffort(); throw; }
        finally { _busy = false; }
    }
    private void PauseBestEffort()
    {
        _playing = false;
        if (_session is null || _session.IsDisposed) return;
        foreach (var entry in _entries.Values) try { if (entry.Slot.Voice.State == AudioPlaybackState.Playing) entry.Slot.Voice.Pause(); } catch { /* Original control failure remains terminal. */ }
    }
    private void Ready()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_busy || _disposing || _faulted || _session is null || _session.IsDisposed || _world.IsDisposed || _world.IsFaulted)
            throw new InvalidOperationException("Audio subsystem is not available for execution.");
    }
    /// <summary>Releases native streams/voices/clips before immutable file pins. Failed cleanup is retained for explicit retry.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        if (_busy) throw new InvalidOperationException("Leave audio control before cleanup.");
        _busy = true; _disposing = true; _playing = false;
        try
        {
            _session?.Dispose(); _session = null;
            _content?.Dispose(); _content = null;
            _retainedContent?.Dispose(); _retainedContent = null;
            _entries.Clear(); _bank.Clear(); _candidates.Clear(); _present.Clear(); Systems = []; _disposed = true;
        }
        finally { _busy = false; }
    }
    private sealed class Slot(AudioPlayback voice, ulong? length)
    { internal readonly AudioPlayback Voice = voice; internal readonly ulong? Length = length; internal bool InUse; }
    private sealed class Entry(AudioSource source, Slot slot)
    { internal AudioSource Source = source; internal readonly Slot Slot = slot; internal AudioSourcePhase Desired; internal AudioPlaybackCommand LastCommand; internal bool Rewound = true; }
    private sealed record Candidate(EntityHandle Entity, AudioSource Source, AudioPlaybackCommand? Command, Entry? Previous);
}
