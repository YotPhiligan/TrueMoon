using Xunit;

namespace TrueMoon.Aether.Kul.Tests;

public sealed class AudioTransportTests
{
    [WindowsAudioTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task PreparationAndPausedStep_RemainSilentUntilActualSessionPlay(bool microsoft)
    {
        await using var fixture = new AudioFixture(microsoft); await fixture.Start();
        using var graph = AudioGraph.Create(fixture, AudioFixture.Document(new AudioSource(AudioFixture.SoundId, AutoPlay: true)));
        Assert.Equal(1, fixture.Audio.GetSnapshot().Sessions); Assert.Equal(3, fixture.Audio.GetSnapshot().Voices);
        Assert.Equal(AudioSourcePhase.Paused, graph.State().Phase); AudioFixture.Silent(fixture.Render());
        graph.Tick(); Assert.Equal(1UL, graph.State().Tick); Assert.Equal(0UL, graph.State().CursorFrames);
        AudioFixture.Silent(fixture.Render());
        graph.Audio.SetPlaying(true); fixture.Render(); AudioFixture.Constant(fixture.Render(), .25f);
        var cursor = graph.Current().CursorFrames; Assert.True(cursor > 0);
        graph.Audio.SetPlaying(false); AudioFixture.Silent(fixture.Render());
        Assert.Equal(cursor, graph.Current().CursorFrames);
        graph.Command(1, AudioCommandKind.Play); graph.Tick(); AudioFixture.Silent(fixture.Render());
        Assert.Equal(new AudioSourceState(2, 1, AudioSourcePhase.Paused, 0), graph.State());
        graph.Audio.SetPlaying(true); Assert.Contains(fixture.Render(), sample => sample > .24f);
        Assert.True(graph.Current().CursorFrames > 0);
        graph.Dispose(); Assert.Equal(0, fixture.Audio.GetSnapshot().Sessions); Assert.Equal(0, fixture.Audio.GetSnapshot().Voices);
        Assert.False(graph.Audio.TryGetState(graph.Entity(), out _));
    }

    [WindowsAudioTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task GlobalPause_ResumesOnlySourcesWhoseDesiredModeIsPlaying(bool microsoft)
    {
        await using var fixture = new AudioFixture(microsoft); await fixture.Start();
        using var graph = AudioGraph.Create(fixture, AudioFixture.Document(
            new(AudioFixture.SoundId, AutoPlay: true), new(AudioFixture.SoundId, AutoPlay: true)));
        graph.Audio.SetPlaying(true); fixture.Render(); AudioFixture.Constant(fixture.Render(), .5f);
        graph.Command(1, AudioCommandKind.Pause, index: 1); graph.Tick(); fixture.Render(); AudioFixture.Constant(fixture.Render(), .25f);
        var first = graph.Current(0).CursorFrames; var second = graph.Current(1).CursorFrames;
        graph.Audio.SetPlaying(false); AudioFixture.Silent(fixture.Render());
        graph.Audio.SetPlaying(false); Assert.Equal(first, graph.Current(0).CursorFrames); Assert.Equal(second, graph.Current(1).CursorFrames);
        graph.Audio.SetPlaying(true); graph.Audio.SetPlaying(true); fixture.Render(); AudioFixture.Constant(fixture.Render(), .25f);
        Assert.True(graph.Current(0).CursorFrames > first); Assert.Equal(second, graph.Current(1).CursorFrames);
        Assert.Equal(AudioSourcePhase.Paused, graph.Current(1).Phase);
        graph.Command(2, AudioCommandKind.Resume, index: 1); graph.Tick(); fixture.Render(); AudioFixture.Constant(fixture.Render(), .5f);
        Assert.True(graph.Current(1).CursorFrames > second);
    }

    [WindowsAudioFact]
    public async Task Commands_ApplyOnceAndPreserveCursorAcrossPauseSeekResumeAndStop()
    {
        await using var fixture = new AudioFixture(); await fixture.Start();
        using var graph = AudioGraph.Create(fixture, AudioFixture.Document(new AudioSource(AudioFixture.SoundId)));
        graph.Audio.SetPlaying(true); AudioFixture.Silent(fixture.Render());
        graph.Command(1, AudioCommandKind.Play); graph.Tick(); fixture.Render(); AudioFixture.Constant(fixture.Render(), .25f);
        var first = graph.Current().CursorFrames;
        graph.Tick(); fixture.Render(); Assert.True(graph.Current().CursorFrames > first); // identical sequence never rewinds
        graph.Command(2, AudioCommandKind.Pause); graph.Tick(); var paused = graph.Current().CursorFrames;
        AudioFixture.Silent(fixture.Render()); Assert.Equal(paused, graph.Current().CursorFrames);
        graph.Command(3, AudioCommandKind.Seek, 20000); graph.Tick();
        Assert.Equal(20000UL, graph.State().CursorFrames); Assert.Equal(AudioSourcePhase.Paused, graph.State().Phase);
        AudioFixture.Silent(fixture.Render()); Assert.Equal(20000UL, graph.Current().CursorFrames);
        graph.Command(4, AudioCommandKind.Resume); graph.Tick(); Assert.Contains(fixture.Render(), sample => sample > .24f);
        Assert.True(graph.Current().CursorFrames > 20000);
        graph.Command(5, AudioCommandKind.Stop); graph.Tick(); Assert.Equal(new AudioSourceState(6, 5, AudioSourcePhase.Stopped, 0), graph.State());
        AudioFixture.Silent(fixture.Render());
        graph.Command(6, AudioCommandKind.Seek, 1000); graph.Tick(); Assert.Equal(1000UL, graph.State().CursorFrames);
        Assert.Equal(AudioSourcePhase.Stopped, graph.State().Phase); AudioFixture.Silent(fixture.Render());
        graph.Command(7, AudioCommandKind.Play); graph.Tick(); Assert.Equal(0UL, graph.State().CursorFrames);
        Assert.Equal(AudioSourcePhase.Playing, graph.State().Phase); Assert.Contains(fixture.Render(), sample => sample > .24f);
    }

    [WindowsAudioFact]
    public async Task SourceGainAndLoop_ChangeActualPcmWithoutAllocatingOrReplacingVoices()
    {
        await using var fixture = new AudioFixture(soundFrames: 4096); await fixture.Start();
        using var graph = AudioGraph.Create(fixture, AudioFixture.Document(new AudioSource(AudioFixture.SoundId, Loop: true, AutoPlay: true)));
        var before = fixture.Audio.GetSnapshot(); graph.Audio.SetPlaying(true); fixture.Render(10000);
        Assert.Equal(AudioSourcePhase.Playing, graph.Current().Phase); AudioFixture.Constant(fixture.Render(), .25f);
        graph.World.SetComponent(graph.Entity(), new AudioSource(AudioFixture.SoundId, .5f, true, true)); graph.Tick();
        fixture.Render(); AudioFixture.Constant(fixture.Render(), .125f);
        var after = fixture.Audio.GetSnapshot(); Assert.Equal(before.Voices, after.Voices); Assert.Equal(before.Clips, after.Clips);
        Assert.Equal(before.ResidentSoundBytes, after.ResidentSoundBytes);
        graph.World.SetComponent(graph.Entity(), new AudioSource(AudioFixture.SoundId, 0, true, true)); graph.Tick();
        fixture.Render(); AudioFixture.Silent(fixture.Render());
        graph.World.SetComponent(graph.Entity(), new AudioSource(AudioFixture.SoundId, 1, false, true)); graph.Tick(); fixture.Render(5000); graph.Tick();
        Assert.Equal(AudioSourcePhase.Ended, graph.State().Phase); Assert.Equal(4096UL, graph.State().CursorFrames);
        AudioFixture.Silent(fixture.Render());
    }

    [WindowsAudioFact]
    public async Task NaturalEof_IsRetainedNeverReplaysOnResumeOrGlobalPlayAndNeedsNewPlayCommand()
    {
        await using var fixture = new AudioFixture(soundFrames: 4096); await fixture.Start();
        using var graph = AudioGraph.Create(fixture, AudioFixture.Document(new AudioSource(AudioFixture.SoundId, AutoPlay: true)));
        graph.Audio.SetPlaying(true); fixture.Render(5000); graph.Tick(); Assert.Equal(AudioSourcePhase.Ended, graph.State().Phase);
        Assert.Equal(4096UL, graph.State().CursorFrames); graph.Command(1, AudioCommandKind.Resume); graph.Tick();
        AudioFixture.Silent(fixture.Render()); Assert.Equal(AudioSourcePhase.Ended, graph.State().Phase);
        graph.Audio.SetPlaying(false); graph.Audio.SetPlaying(true); AudioFixture.Silent(fixture.Render());
        using var other = fixture.Sessions.Create()!; using var clip = other.LoadSound(fixture.PathFor(AudioFixture.SoundId));
        using var voice = other.PlaySound(clip, new() { StartPaused = true });
        Assert.True(graph.Audio.TryGetState(graph.Entity(), out var ended)); Assert.Equal(AudioSourcePhase.Ended, ended.Phase);
        graph.Command(2, AudioCommandKind.Play); graph.Tick(); Assert.Equal(0UL, graph.State().CursorFrames);
        Assert.Contains(fixture.Render(), sample => sample > .24f); Assert.Equal(AudioSourcePhase.Playing, graph.Current().Phase);
        graph.Tick(); Assert.Equal(2UL, graph.State().Sequence); Assert.True(graph.State().CursorFrames > 0);
    }

    [WindowsAudioFact]
    public async Task EofReplayQueuedDuringPause_StartsOnlyAfterGlobalResumeAndEndedSlotCanBeReused()
    {
        await using var fixture = new AudioFixture(voices: 1, soundFrames: 4096); await fixture.Start();
        using var graph = AudioGraph.Create(fixture, AudioFixture.Document(new AudioSource(AudioFixture.SoundId, AutoPlay: true)));
        graph.Audio.SetPlaying(true); fixture.Render(5000); graph.Tick(); Assert.Equal(AudioSourcePhase.Ended, graph.State().Phase);
        graph.Audio.SetPlaying(false); graph.Command(1, AudioCommandKind.Play); graph.Tick();
        Assert.Equal(new AudioSourceState(2, 1, AudioSourcePhase.Paused, 0), graph.State()); AudioFixture.Silent(fixture.Render());
        graph.Audio.SetPlaying(true); Assert.Contains(fixture.Render(), sample => sample > .24f);
        fixture.Render(5000); graph.Tick(); Assert.Equal(AudioSourcePhase.Ended, graph.State().Phase);
        var retired = graph.Entity(); graph.World.DestroyEntity(retired); graph.Tick();
        var replacement = graph.World.CreateEntity(); graph.World.AddComponent(replacement, new AudioSource(AudioFixture.SoundId, AutoPlay: true));
        graph.World.AddComponent(replacement, default(AudioSourceState)); graph.Tick();
        Assert.Equal(retired.Index, replacement.Index); Assert.NotEqual(retired.Generation, replacement.Generation);
        Assert.True(graph.Audio.TryGetState(replacement, out var ready)); Assert.Equal(AudioSourcePhase.Playing, ready.Phase); Assert.Equal(0UL, ready.CursorFrames);
        Assert.False(graph.Audio.TryGetState(retired, out _)); Assert.Contains(fixture.Render(), sample => sample > .24f);
        Assert.Equal(2, fixture.Audio.GetSnapshot().Voices);
    }

    [WindowsAudioTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task StreamingMusic_UsesPreparedCaptureAndObeysGlobalPauseAndExplicitRewind(bool microsoft)
    {
        await using var fixture = new AudioFixture(microsoft, music: true); await fixture.Start();
        using var graph = AudioGraph.Create(fixture, AudioFixture.Document(new AudioSource(AudioFixture.MusicId, AutoPlay: true)));
        Assert.Equal(1, fixture.Audio.GetSnapshot().MusicStreams); AudioFixture.Silent(fixture.Render());
        graph.Audio.SetPlaying(true); Assert.Contains(await fixture.Signal(), sample => Math.Abs(sample) > .01f);
        var cursor = graph.Current().CursorFrames; Assert.True(cursor > 0);
        graph.Audio.SetPlaying(false); AudioFixture.Silent(fixture.Render()); Assert.Equal(cursor, graph.Current().CursorFrames);
        graph.Command(1, AudioCommandKind.Seek); graph.Tick(); Assert.Equal(0UL, graph.State().CursorFrames);
        AudioFixture.Silent(fixture.Render()); graph.Audio.SetPlaying(true);
        Assert.Contains(await fixture.Signal(), sample => Math.Abs(sample) > .01f);
        graph.Dispose(); Assert.Equal(0, fixture.Audio.GetSnapshot().MusicStreams); Assert.Equal(0, fixture.Audio.GetSnapshot().Clips);
        using var writable = new FileStream(fixture.PathFor(AudioFixture.MusicId), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(writable.Length > 0);
    }

    [WindowsAudioTheory]
    [InlineData("zero")] [InlineData("kind")] [InlineData("frame")] [InlineData("lower")] [InlineData("changed")]
    public async Task InvalidCommands_FaultBeforeExecutingAnotherSourcesPendingPlay(string invalid)
    {
        await using var fixture = new AudioFixture(); await fixture.Start();
        using var graph = AudioGraph.Create(fixture, AudioFixture.Document(new(AudioFixture.SoundId), new(AudioFixture.SoundId)));
        if (invalid is "lower" or "changed") { graph.Command(2, AudioCommandKind.Pause, index: 1); graph.Tick(); }
        graph.Audio.SetPlaying(true); graph.Command(1, AudioCommandKind.Play);
        var request = invalid switch
        {
            "zero" => new AudioPlaybackCommand(0, AudioCommandKind.Play),
            "kind" => new AudioPlaybackCommand(1, 0),
            "frame" => new AudioPlaybackCommand(1, AudioCommandKind.Play, 1),
            "lower" => new AudioPlaybackCommand(1, AudioCommandKind.Play),
            _ => new AudioPlaybackCommand(2, AudioCommandKind.Stop)
        };
        var entity = graph.Entity(1);
        if (graph.World.HasComponent<AudioPlaybackCommand>(entity)) graph.World.SetComponent(entity, request); else graph.World.AddComponent(entity, request);
        var tick = graph.Audio.LastCompletedTick;
        Assert.Throws<InvalidDataException>(() => graph.Tick()); Assert.True(graph.World.IsFaulted);
        Assert.Equal(tick, graph.Audio.LastCompletedTick); Assert.Equal(0UL, graph.World.GetComponent<AudioSourceState>(graph.Entity()).Sequence);
        AudioFixture.Silent(fixture.Render()); Assert.Throws<InvalidOperationException>(() => graph.Audio.SetPlaying(true));
        Assert.Throws<InvalidOperationException>(() => graph.Tick());
    }

    [WindowsAudioTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task UnsupportedSeek_FaultsBeforeNativeMutationForPastEndOrUnknownLength(bool stream)
    {
        await using var fixture = new AudioFixture(music: stream); await fixture.Start();
        using var graph = AudioGraph.Create(fixture, AudioFixture.Document(new AudioSource(stream ? AudioFixture.MusicId : AudioFixture.SoundId)));
        graph.Command(1, AudioCommandKind.Seek, stream ? 1UL : 48001UL);
        Assert.Throws<NotSupportedException>(() => graph.Tick()); Assert.Equal(0UL, graph.Audio.LastCompletedTick);
        AudioFixture.Silent(fixture.Render()); Assert.Equal(0UL, graph.Current().CursorFrames);
    }
}
