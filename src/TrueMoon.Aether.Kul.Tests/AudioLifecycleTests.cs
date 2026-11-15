using TrueMoon.Aether.Kul.Runtime;
using TrueMoon.Aether.Kul.Runtime.Content;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Runtime;
using TrueMoon.Kul;
using Xunit;
using A = TrueMoon.Aether.Abstractions.Ecs;

namespace TrueMoon.Aether.Kul.Tests;

public sealed class AudioLifecycleTests
{
    [WindowsAudioTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task GameplayHelpers_AttachDetachReattachAndReuseEntityIndexWithoutRevivingOldCommands(bool microsoft)
    {
        await using var fixture = new AudioFixture(microsoft); await fixture.Start(); A.EntityHandle spawned = default;
        var actions = new EcsSystemRegistration(AudioEntityCommands.DeclareWrites(new A.EcsSystemDescription("test.audio.lifecycle")),
            new Factory<A.ISystem>(() => new AudioGraph.Callback(context =>
            {
                switch (context.TickNumber)
                {
                    case 1: AudioEntityCommands.CreateSource(context.Commands, new(AudioFixture.SoundId), new(8, AudioCommandKind.Play)); break;
                    case 2: AudioEntityCommands.RemoveSource(context.Commands, spawned); break;
                    case 3: AudioEntityCommands.AddSource(context.Commands, spawned, new(AudioFixture.SoundId)); break;
                    case 4: context.Commands.DestroyEntity(spawned); break;
                    case 5: AudioEntityCommands.CreateSource(context.Commands, new(AudioFixture.SoundId)); break;
                }
            })));
        using var graph = AudioGraph.Create(fixture, AudioFixture.Document(), actions); graph.Audio.SetPlaying(true); graph.Tick();
        graph.World.Query<AudioSource>().ForEach((A.EntityHandle entity, in AudioSource source) => spawned = entity);
        Assert.True(graph.Audio.TryGetState(spawned, out var active)); Assert.Equal(8UL, active.Sequence);
        Assert.Contains(fixture.Render(), sample => sample > .24f); Assert.False(graph.World.HasComponent<A.Transforms.LocalTransform>(spawned));
        graph.Tick(); Assert.False(graph.Audio.TryGetState(spawned, out _)); AudioFixture.Silent(fixture.Render());
        Assert.False(graph.World.HasComponent<AudioSourceState>(spawned)); Assert.False(graph.World.HasComponent<AudioPlaybackCommand>(spawned));
        graph.Tick(); Assert.True(graph.Audio.TryGetState(spawned, out var reattached)); Assert.Equal(0UL, reattached.Sequence);
        Assert.Equal(AudioSourcePhase.Stopped, reattached.Phase); AudioFixture.Silent(fixture.Render());
        var retired = spawned; graph.Tick(); Assert.False(graph.World.IsAlive(retired)); Assert.False(graph.Audio.TryGetState(retired, out _));
        graph.Tick(); graph.World.Query<AudioSource>().ForEach((A.EntityHandle entity, in AudioSource source) => spawned = entity);
        Assert.Equal(retired.Index, spawned.Index); Assert.NotEqual(retired.Generation, spawned.Generation);
        Assert.True(graph.Audio.TryGetState(spawned, out var replacement)); Assert.Equal(new AudioSourceState(5, 0, AudioSourcePhase.Stopped, 0), replacement);
        Assert.False(graph.Audio.TryGetState(retired, out _)); AudioFixture.Silent(fixture.Render());
        Assert.Equal(3, fixture.Audio.GetSnapshot().Voices); Assert.Equal(2, fixture.Audio.GetSnapshot().Clips);
    }

    [WindowsAudioFact]
    public async Task AssetRebind_ResetsNewVoiceAndPreservesSequenceWithoutReplayingOldSeekAgainstShorterClip()
    {
        await using var fixture = new AudioFixture(otherFrames: 4096); await fixture.Start();
        using var graph = AudioGraph.Create(fixture, AudioFixture.Document(new AudioSource(AudioFixture.SoundId)));
        graph.Command(4, AudioCommandKind.Seek, 20000); graph.Tick(); Assert.Equal(20000UL, graph.State().CursorFrames);
        graph.World.SetComponent(graph.Entity(), new AudioSource(AudioFixture.OtherId)); graph.Tick();
        Assert.Equal(new AudioSourceState(2, 4, AudioSourcePhase.Stopped, 0), graph.State()); AudioFixture.Silent(fixture.Render());
        graph.Command(5, AudioCommandKind.Play); graph.Audio.SetPlaying(true); graph.Tick(); fixture.Render(); AudioFixture.Constant(fixture.Render(), .125f);
        Assert.Equal(5UL, graph.State().Sequence); Assert.Equal(3, fixture.Audio.GetSnapshot().Voices);
    }

    [WindowsAudioTheory]
    [InlineData("state")] [InlineData("source")] [InlineData("unknown")]
    [InlineData("gain")] [InlineData("autoplay")] [InlineData("pool")] [InlineData("sources")]
    public async Task InvalidAttachmentAndPoolChanges_FaultBeforeAnotherSourcesPendingPlay(string invalid)
    {
        await using var fixture = new AudioFixture(voices: 1, bridge: options => options.MaximumSources = invalid == "sources" ? 1 : 64); await fixture.Start();
        using var graph = AudioGraph.Create(fixture, AudioFixture.Document(new AudioSource(AudioFixture.SoundId)));
        graph.Command(1, AudioCommandKind.Play); graph.Audio.SetPlaying(true);
        switch (invalid)
        {
            case "state": graph.World.RemoveComponent<AudioSourceState>(graph.Entity()); break;
            case "source": graph.World.RemoveComponent<AudioSource>(graph.Entity()); break;
            case "unknown": graph.World.SetComponent(graph.Entity(), new AudioSource(new(Guid.NewGuid()))); break;
            case "gain": graph.World.SetComponent(graph.Entity(), new AudioSource(AudioFixture.SoundId, float.NaN)); break;
            case "autoplay": graph.World.SetComponent(graph.Entity(), new AudioSource(AudioFixture.SoundId, AutoPlay: true)); break;
            default:
                var next = graph.World.CreateEntity();
                graph.World.AddComponent(next, new AudioSource(invalid == "pool" ? AudioFixture.SoundId : AudioFixture.OtherId));
                graph.World.AddComponent(next, default(AudioSourceState)); break;
        }
        Assert.Throws<InvalidDataException>(() => graph.Tick()); Assert.True(graph.World.IsFaulted);
        Assert.Equal(0UL, graph.Audio.LastCompletedTick); AudioFixture.Silent(fixture.Render());
        Assert.Equal(2, fixture.Audio.GetSnapshot().Voices); Assert.Throws<InvalidOperationException>(() => graph.Audio.SetPlaying(true));
    }

    [WindowsAudioTheory]
    [InlineData(-.01f)] [InlineData(1.01f)] [InlineData(float.PositiveInfinity)]
    public async Task ChangedRuntimeGain_RejectsOutsideBothFiniteBoundsBeforeStartingVoice(float volume)
    {
        await using var fixture = new AudioFixture(); await fixture.Start();
        using var graph = AudioGraph.Create(fixture, AudioFixture.Document(new AudioSource(AudioFixture.SoundId)));
        graph.Audio.SetPlaying(true); graph.Command(1, AudioCommandKind.Play);
        graph.World.SetComponent(graph.Entity(), new AudioSource(AudioFixture.SoundId, volume));
        Assert.Throws<InvalidDataException>(() => graph.Tick()); Assert.Equal(0UL, graph.Audio.LastCompletedTick);
        AudioFixture.Silent(fixture.Render());
    }

    [WindowsAudioTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task FailedCandidate_DoesNotInterruptOldSessionAndReplacementOwnsIndependentBank(bool microsoft)
    {
        await using var fixture = new AudioFixture(microsoft); await fixture.Start();
        using var old = fixture.Game(); using var bad = fixture.Game(); using var replacement = fixture.Game();
        old.LoadSceneWorld(new(AudioFixture.Document(new AudioSource(AudioFixture.SoundId, AutoPlay: true)))); old.SetPlaying(true);
        fixture.Render(); AudioFixture.Constant(fixture.Render(), .25f); var oldWorld = old.ActiveScene!.World.Id;
        Assert.Throws<InvalidDataException>(() => bad.LoadSceneWorld(new(AudioFixture.Document(new AudioSource(new(Guid.NewGuid()))))));
        Assert.Null(bad.ActiveScene); Assert.Equal(1, fixture.Audio.GetSnapshot().Sessions); Assert.False(old.IsFaulted);
        old.Tick(); Assert.Contains(fixture.Render(), sample => sample > .24f); Assert.Equal(1UL, old.TickNumber);
        replacement.LoadSceneWorld(new(AudioFixture.Document(new AudioSource(AudioFixture.OtherId, AutoPlay: true))));
        Assert.NotEqual(oldWorld, replacement.ActiveScene!.World.Id); Assert.Equal(2, fixture.Audio.GetSnapshot().Sessions);
        old.Dispose(); Assert.Equal(1, fixture.Audio.GetSnapshot().Sessions); AudioFixture.Silent(fixture.Render());
        replacement.SetPlaying(true); fixture.Render(); AudioFixture.Constant(fixture.Render(), .125f);
        replacement.Dispose(); Assert.Equal(0, fixture.Audio.GetSnapshot().Sessions); Assert.Equal(0, fixture.Audio.GetSnapshot().ResidentSoundBytes);
    }

    [WindowsAudioFact]
    public async Task AppAdmissionFailureDuringCandidatePreparation_CleansOnlyPartialNewBank()
    {
        await using var fixture = new AudioFixture(audio: options => options.MaximumVoices = 4); await fixture.Start();
        using var old = fixture.Game(); old.LoadSceneWorld(new(AudioFixture.Document(new AudioSource(AudioFixture.SoundId, AutoPlay: true)))); old.SetPlaying(true);
        using var candidate = fixture.Game(); Assert.Throws<InvalidOperationException>(() => candidate.LoadSceneWorld(new(AudioFixture.Document())));
        Assert.Null(candidate.ActiveScene); Assert.Equal(1, fixture.Audio.GetSnapshot().Sessions); Assert.Equal(3, fixture.Audio.GetSnapshot().Voices);
        old.Tick(); fixture.Render(); AudioFixture.Constant(fixture.Render(), .25f); Assert.False(old.IsFaulted);
    }

    [WindowsAudioTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task CleanupFailure_ReleasesNativeFirstAndRetainsPinsAndBorrowedWorldUntilRetry(bool microsoft)
    {
        FaultLease? retained = null; IAudioService? audio = null;
        await using var fixture = new AudioFixture(microsoft, beforeBridge: context => context.Services(services =>
            services.Singleton<IFactory<IAudioContentLease, AudioContentLoadArguments>>(resolver =>
            {
                audio = resolver.Resolve<IAudioService>()!;
                return new Factory<IAudioContentLease, AudioContentLoadArguments>(args => retained = new(AudioContentLease.Open(args), () => audio.GetSnapshot()) { Failures = 1 });
            })));
        await fixture.Start(); using var game = fixture.Game();
        game.LoadSceneWorld(new(AudioFixture.Document(new AudioSource(AudioFixture.SoundId, AutoPlay: true)))); game.SetPlaying(true);
        var scene = game.ActiveScene!;
        try
        {
            Assert.Equal("content cleanup", Assert.Throws<IOException>(() => game.Dispose()).Message);
            Assert.Equal(1, retained!.Attempts); Assert.Equal(0, retained.SessionsOnDispose); Assert.Equal(0, retained.VoicesOnDispose);
            Assert.Equal(0, audio!.GetSnapshot().Sessions); Assert.False(scene.World.IsDisposed);
            Assert.Throws<InvalidOperationException>(() => scene.Dispose()); Assert.Throws<InvalidOperationException>(() => game.Tick());
            Assert.Throws<IOException>(() => File.Open(fixture.PathFor(AudioFixture.SoundId), FileMode.Open, FileAccess.Write, FileShare.Read));
            game.Dispose(); game.Dispose(); Assert.Equal(2, retained.Attempts); Assert.True(scene.IsDisposed);
            using var write = new FileStream(fixture.PathFor(AudioFixture.SoundId), FileMode.Open, FileAccess.ReadWrite, FileShare.None); Assert.True(write.Length > 0);
        }
        finally { if (retained is not null) retained.Failures = 0; game.Dispose(); }
    }

    [WindowsAudioFact]
    public async Task CancellationAfterContentPreparation_ReleasesPinsBeforeNativeSessionCreation()
    {
        using var cancelled = new CancellationTokenSource(); FaultLease? retained = null;
        await using var fixture = new AudioFixture(beforeBridge: context => context.Services(services =>
            services.Singleton<IFactory<IAudioContentLease, AudioContentLoadArguments>>(_ => new Factory<IAudioContentLease, AudioContentLoadArguments>(args =>
            { retained = new(AudioContentLease.Open(args), () => new(false, AudioMode.Offline, 48000, 0, 0, 0, 0, 0, 0, 0)); cancelled.Cancel(); return retained; }))));
        await fixture.Start(); using var game = fixture.Game();
        Assert.Equal(cancelled.Token, Assert.Throws<OperationCanceledException>(() => game.LoadSceneWorld(new(AudioFixture.Document()), cancelled.Token)).CancellationToken);
        Assert.Null(game.ActiveScene); Assert.Equal(1, retained!.Attempts); Assert.Equal(0, fixture.Audio.GetSnapshot().Sessions);
        using var write = new FileStream(fixture.PathFor(AudioFixture.SoundId), FileMode.Open, FileAccess.ReadWrite, FileShare.None); Assert.True(write.Length > 0);
    }

    [WindowsAudioTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task FailedUnpublishedPreparation_RetainsCleanupOwnerAndOriginalErrorUntilExplicitRetry(bool cancelAfterOpen)
    {
        using var cancelled = new CancellationTokenSource(); FaultLease? retained = null; var calls = 0;
        await using var fixture = new AudioFixture(beforeBridge: context => context.Services(services =>
            services.Singleton<IFactory<IAudioContentLease, AudioContentLoadArguments>>(_ => new Factory<IAudioContentLease, AudioContentLoadArguments>(args =>
            {
                calls++; retained = new(AudioContentLease.Open(args), () => new(false, AudioMode.Offline, 48000, 0, 0, 0, 0, 0, 0, 0)) { Failures = int.MaxValue };
                if (cancelAfterOpen) { cancelled.Cancel(); return retained; }
                throw new SceneWorldPreparationException(new IOException("content preparation"), retained);
            }))));
        await fixture.Start(); using var game = fixture.Game();
        try
        {
            var error = Assert.Throws<AggregateException>(() => game.LoadSceneWorld(new(AudioFixture.Document()), cancelled.Token));
            Assert.Contains(Flatten(error), item => item is IOException { Message: "content cleanup" });
            if (cancelAfterOpen) Assert.Contains(Flatten(error), item => item is OperationCanceledException value && value.CancellationToken == cancelled.Token);
            else Assert.Contains(Flatten(error), item => item is IOException { Message: "content preparation" });
            Assert.Null(game.ActiveScene); Assert.Equal(0, fixture.Audio.GetSnapshot().Sessions); Assert.Equal(1, calls);
            Assert.True(retained!.Attempts > 0); Assert.Throws<InvalidOperationException>(() => game.LoadSceneWorld(new(AudioFixture.Document())));
            Assert.Equal(1, calls); Assert.Throws<IOException>(() => File.Open(fixture.PathFor(AudioFixture.SoundId), FileMode.Open, FileAccess.Write, FileShare.Read));
            retained.Failures = 0; game.Dispose();
            using var write = new FileStream(fixture.PathFor(AudioFixture.SoundId), FileMode.Open, FileAccess.ReadWrite, FileShare.None); Assert.True(write.Length > 0);
        }
        finally { if (retained is not null) retained.Failures = 0; game.Dispose(); }
    }

    [WindowsAudioFact]
    public async Task AppFallbackDisposal_LeavesContentPinsCallerOwnedAndBridgeCleanupIdempotent()
    {
        await using var fixture = new AudioFixture(); await fixture.Start();
        using var graph = AudioGraph.Create(fixture, AudioFixture.Document(new AudioSource(AudioFixture.SoundId, AutoPlay: true)));
        graph.Audio.SetPlaying(true); await fixture.App.DisposeAsync();
        Assert.False(graph.Audio.IsDisposed); Assert.False(graph.Audio.TryGetState(graph.Entity(), out _));
        Assert.Throws<IOException>(() => File.Open(fixture.PathFor(AudioFixture.SoundId), FileMode.Open, FileAccess.Write, FileShare.Read));
        graph.Audio.SetPlaying(false); Assert.Throws<InvalidOperationException>(() => graph.Audio.SetPlaying(true));
        graph.Dispose(); graph.Dispose(); Assert.True(graph.Audio.IsDisposed); Assert.True(graph.Scene.IsDisposed);
        using var write = new FileStream(fixture.PathFor(AudioFixture.SoundId), FileMode.Open, FileAccess.ReadWrite, FileShare.None); Assert.True(write.Length > 0);
    }

    private sealed class FaultLease(IAudioContentLease inner, Func<AudioSnapshot> snapshot) : IAudioContentLease
    {
        internal int Failures, Attempts, SessionsOnDispose, VoicesOnDispose;
        public AudioContentManifest Manifest => inner.Manifest;
        public string GetPath(AudioAssetId id) => inner.GetPath(id);
        public void Dispose()
        {
            Attempts++; var state = snapshot(); SessionsOnDispose = state.Sessions; VoicesOnDispose = state.Voices;
            if (Failures > 0) { Failures--; throw new IOException("content cleanup"); }
            inner.Dispose();
        }
    }
    private static IEnumerable<Exception> Flatten(Exception error)
    {
        yield return error;
        if (error is AggregateException aggregate) foreach (var nested in aggregate.InnerExceptions) foreach (var item in Flatten(nested)) yield return item;
        else if (error.InnerException is { } inner) foreach (var item in Flatten(inner)) yield return item;
    }
}
