using System.Diagnostics;
using TrueMoon.Kul.Native;
using Xunit;

namespace TrueMoon.Kul.Tests;

public sealed class AudioStreamingTests
{
    [WindowsAudioFact]
    public async Task OggMusicStreamsSeeksPausesAndClosesItsFile()
    {
        await using var f = new AudioFixture(); await f.Start();
        using var session = f.Sessions.Create()!;
        var voice = session.PlayMusic(f.Music, new() { Loop = true, Volume = .5f });
        Assert.True(voice.IsStreaming);
        Assert.Null(voice.LengthFrames); // Explicit decoder capability, not a made-up zero duration.
        Assert.Contains(await f.RenderUntilSignal(), x => Math.Abs(x) > .01f);
        Assert.True(voice.CursorFrames > 0);
        Assert.Throws<IOException>(() => new FileStream(f.Music, FileMode.Open, FileAccess.ReadWrite, FileShare.None));
        voice.Pause(); var paused = voice.CursorFrames;
        Assert.All(f.Render(), x => Assert.Equal(0, x)); Assert.Equal(paused, voice.CursorFrames);
        voice.Seek(480000); voice.Play();
        Assert.Contains(await f.RenderUntilSignal(), x => Math.Abs(x) > .01f);
        Assert.True(voice.CursorFrames >= 480000);
        voice.Stop(); f.Render(); Assert.Equal(AudioPlaybackState.Stopped, voice.State);
        voice.Dispose(); AudioFixture.AssertUnlocked(f.Music);
        Assert.Equal(0, f.Audio.GetSnapshot().MusicStreams);
    }

    [WindowsAudioTheory]
    [InlineData("ogg-header")]
    [InlineData("truncated")]
    [InlineData("bad-crc")]
    [InlineData("wav")]
    public async Task MalformedInputRepeatedlyRejectsWithoutLosingPlaybackOrNativeOwners(string kind)
    {
        await using var f = new AudioFixture(); await f.Start();
        using var session = f.Sessions.Create()!;
        var bad = Path.Combine(f.Root, "damaged." + (kind == "wav" ? "wav" : "ogg"));
        var data = File.ReadAllBytes(f.Music);
        if (kind == "truncated") data = data[..128];
        else if (kind == "bad-crc") data[22] ^= 255;
        else data = kind == "wav" ? [1, 2, 3, 4, 5, 6, 7, 8] : [(byte)'O', (byte)'g', (byte)'g', (byte)'S', 0, 0, 0, 0];
        File.WriteAllBytes(bad, data);
        int baseline = NativeHandle.LiveCount;
        for (int i = 0; i < 16; i++)
        {
            var error = Assert.Throws<AudioException>(() => session.PlayMusic(bad));
            Assert.NotEqual(0, error.NativeResult);
            Assert.Throws<AudioException>(() => session.LoadSound(bad));
            Assert.Equal(baseline, NativeHandle.LiveCount);
            Assert.Equal(0, f.Audio.GetSnapshot().Voices);
            Assert.Equal(0, f.Audio.GetSnapshot().ResidentSoundBytes);
            AudioFixture.AssertUnlocked(bad);
        }
        using var valid = session.PlayMusic(f.Music);
        Assert.Contains(f.Render(4096), x => Math.Abs(x) > .01f);
    }

    [WindowsAudioFact]
    public async Task MusicLimitReturnsCapacityAndRepeatedSessionDisposalClosesStreams()
    {
        await using var f = new AudioFixture(o => o.MaximumMusicStreams = 1); await f.Start();
        var baseline = NativeHandle.LiveCount;
        for (int i = 0; i < 32; i++)
        {
            using var session = f.Sessions.Create()!;
            var music = session.PlayMusic(f.Music, new() { Loop = true });
            Assert.Throws<InvalidOperationException>(() => session.PlayMusic(f.Music));
            Assert.Contains(f.Render(), x => x != 0);
            music.Dispose(); using var replacement = session.PlayMusic(f.Music);
            session.Dispose(); session.Dispose();
            Assert.Equal(AudioPlaybackState.Disposed, replacement.State);
            Assert.Equal(baseline, NativeHandle.LiveCount);
            AudioFixture.AssertUnlocked(f.Music);
        }
    }

    [WindowsAudioFact]
    public async Task TenMinuteMusicUsesStreamingInsteadOfResidentWholeFilePcm()
    {
        await using var f = new AudioFixture(); await f.Start();
        using var session = f.Sessions.Create()!;
        // 57.6 MB mono PCM16 on disk; 230.4 MB decoded stereo float32.
        string path = Path.Combine(f.Root, "long.wav");
        AudioFixture.WriteWave(path, 48000 * 600, .125f);
        using (var warm = session.PlayMusic(f.Music)) f.Render();
        using var process = Process.GetCurrentProcess();
        GC.Collect(); GC.WaitForPendingFinalizers(); process.Refresh(); long before = process.PrivateMemorySize64;
        using var stream = session.PlayMusic(path, new() { Loop = true });
        Assert.Equal(28800000UL, stream.LengthFrames);
        Assert.Contains(f.Render(), x => x > .12f);
        stream.Seek(28800000UL - 512); await f.RenderUntilSignal();
        Assert.Equal(AudioPlaybackState.Playing, stream.State);
        Assert.InRange(stream.CursorFrames, 0UL, 4096UL);
        process.Refresh();
        Assert.InRange(process.PrivateMemorySize64 - before, -32L * 1024 * 1024, 32L * 1024 * 1024);
        Assert.Equal(0, f.Audio.GetSnapshot().ResidentSoundBytes);
        stream.Dispose(); AudioFixture.AssertUnlocked(path);
    }
}
