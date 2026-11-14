using Xunit;

namespace TrueMoon.Kul.Tests;

public sealed class AudioPlaybackTests
{
    [WindowsAudioFact]
    public async Task SoundVoicesMixIndependentKnownPcmAndRetainClipUntilLastRelease()
    {
        await using var f = new AudioFixture(); await f.Start();
        using var session = f.Sessions.Create()!;
        var clip = session.LoadSound(f.Wave);
        Assert.Equal(48000UL, clip.Frames);
        Assert.Equal(48000 * 8L, f.Audio.GetSnapshot().ResidentSoundBytes);
        using var first = session.PlaySound(clip, new() { Volume = .5f, Loop = true });
        using var second = session.PlaySound(clip, new() { Volume = .25f, Loop = true });
        f.Render();
        Assert.All(f.Render(), x => Assert.InRange(x, .1874f, .1876f));
        first.Pause(); f.Render();
        Assert.All(f.Render(), x => Assert.InRange(x, .0624f, .0626f));
        Assert.NotEqual(first.CursorFrames, second.CursorFrames);
        clip.Dispose();
        Assert.Throws<ObjectDisposedException>(() => session.PlaySound(clip));
        Assert.Equal(1, f.Audio.GetSnapshot().Clips);
        Assert.Contains(f.Render(), x => x > .06f);
        first.Dispose();
        Assert.Equal(1, f.Audio.GetSnapshot().Clips);
        second.Dispose();
        Assert.Equal(0, f.Audio.GetSnapshot().Clips);
        Assert.Equal(0, f.Audio.GetSnapshot().ResidentSoundBytes);
    }

    [WindowsAudioFact]
    public async Task PlaybackPauseResumeSeekStopAndGainAffectActualPcm()
    {
        await using var f = new AudioFixture(); await f.Start();
        using var session = f.Sessions.Create()!; using var clip = session.LoadSound(f.Wave);
        using var voice = session.PlaySound(clip);
        f.Render();
        voice.Pause(); var cursor = voice.CursorFrames;
        Assert.All(f.Render(), x => Assert.Equal(0, x));
        Assert.Equal(cursor, voice.CursorFrames); Assert.Equal(AudioPlaybackState.Paused, voice.State);
        voice.Play(); Assert.Contains(f.Render(), x => x > .24f); Assert.True(voice.CursorFrames > cursor);
        voice.Seek(20000); f.Render(); Assert.InRange(voice.CursorFrames, 20000UL, 22048UL);
        voice.Volume = .5f; f.Audio.Volume = .5f; f.Render();
        Assert.Equal(.5f, voice.Volume); Assert.Equal(.5f, f.Audio.Volume);
        Assert.All(f.Render(), x => Assert.InRange(x, .0624f, .0626f));
        Assert.Throws<ArgumentOutOfRangeException>(() => voice.Seek(48001));
        Assert.Throws<ArgumentOutOfRangeException>(() => voice.Volume = float.NaN);
        Assert.Throws<ArgumentOutOfRangeException>(() => f.Audio.Volume = 1.01f);
        voice.Stop(); f.Render(); Assert.Equal(AudioPlaybackState.Stopped, voice.State);
        Assert.Equal(0UL, voice.CursorFrames); Assert.All(f.Render(), x => Assert.Equal(0, x));
        voice.Play(); Assert.Contains(f.Render(), x => x > .06f);
    }

    [WindowsAudioFact]
    public async Task EofCollectionAndLoopingUseRealFramesAndReturnCapacity()
    {
        await using var f = new AudioFixture(o => { o.MaximumVoices = 1; o.MaximumMusicStreams = 1; }); await f.Start();
        using var s = f.Sessions.Create()!; using var clip = s.LoadSound(f.Wave);
        var voice = s.PlaySound(clip);
        for (int i = 0; i < 50; i++) f.Render();
        Assert.Equal(AudioPlaybackState.Ended, voice.State);
        Assert.Equal(1, s.CollectFinished()); Assert.Equal(AudioPlaybackState.Disposed, voice.State);
        using var loop = s.PlaySound(clip, new() { Loop = true });
        for (int i = 0; i < 50; i++) f.Render();
        Assert.Equal(AudioPlaybackState.Playing, loop.State);
        Assert.All(f.Render(), x => Assert.InRange(x, .2499f, .2501f));
        Assert.Equal(0, s.CollectFinished());
    }

    [WindowsAudioFact]
    public async Task AdmissionLimitsAndSessionIdentityAreEnforcedAndReusable()
    {
        await using var f = new AudioFixture(o => { o.MaximumClips = 1; o.MaximumVoices = 1; o.MaximumMusicStreams = 1; }); await f.Start();
        using var s = f.Sessions.Create()!; using var other = f.Sessions.Create()!;
        using var clip = s.LoadSound(f.Wave);
        Assert.Throws<InvalidOperationException>(() => s.LoadSound(f.Wave));
        Assert.Throws<ArgumentException>(() => other.PlaySound(clip));
        Assert.Throws<ArgumentOutOfRangeException>(() => s.PlaySound(clip, new() { Volume = -1 }));
        using var voice = s.PlaySound(clip, new() { Loop = true });
        voice.Pause(); Assert.Throws<InvalidOperationException>(() => s.PlaySound(clip));
        voice.Stop(); Assert.Throws<InvalidOperationException>(() => s.PlayMusic(f.Music));
        voice.Dispose(); using var music = s.PlayMusic(f.Music);
        Assert.Equal(1, f.Audio.GetSnapshot().Voices);
    }

    [WindowsAudioFact]
    public async Task FileAndResidentLimitsRejectAndReleaseFailedPreparation()
    {
        await using var fileLimited = new AudioFixture(o => o.MaximumSoundFileBytes = 44);
        await fileLimited.Start(); using var a = fileLimited.Sessions.Create()!;
        Assert.Throws<InvalidOperationException>(() => a.LoadSound(fileLimited.Wave));
        Assert.Equal(0, fileLimited.Audio.GetSnapshot().Clips);
        await fileLimited.App.StopAsync();
        await using var memoryLimited = new AudioFixture(o => o.MaximumResidentSoundBytes = 383999);
        await memoryLimited.Start(); using var b = memoryLimited.Sessions.Create()!;
        Assert.Throws<InvalidOperationException>(() => b.LoadSound(memoryLimited.Wave));
        Assert.Equal(0, memoryLimited.Audio.GetSnapshot().ResidentSoundBytes);
        Assert.Throws<FileNotFoundException>(() => b.LoadSound(Path.Combine(memoryLimited.Root, "missing.wav")));
        Assert.Throws<ArgumentException>(() => b.LoadSound("  "));
        Assert.Throws<ArgumentException>(() => memoryLimited.Audio.Render(new float[3]));
        AudioFixture.AssertUnlocked(memoryLimited.Wave);
    }
}
