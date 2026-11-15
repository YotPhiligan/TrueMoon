using Xunit;

namespace TrueMoon.Kul.Tests;

public sealed class PreparedPlaybackTests
{
    [WindowsAudioTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task StartPaused_PreparesSilentSoundAndStreamUntilExplicitPlay(bool stream)
    {
        await using var fixture = new AudioFixture(); await fixture.Start();
        using var session = fixture.Sessions.Create()!;
        using var clip = stream ? null : session.LoadSound(fixture.Wave);
        using var voice = stream
            ? session.PlayMusic(fixture.Music, new() { StartPaused = true, RetainAtEnd = true })
            : session.PlaySound(clip!, new() { StartPaused = true, RetainAtEnd = true });
        Assert.Equal(AudioPlaybackState.Paused, voice.State);
        Assert.Equal(0UL, voice.CursorFrames);
        Assert.All(fixture.Render(4096), sample => Assert.Equal(0f, sample));
        Assert.Equal(0UL, voice.CursorFrames);
        voice.Play();
        Assert.Contains(await fixture.RenderUntilSignal(), sample => Math.Abs(sample) > .01f);
        Assert.True(voice.CursorFrames > 0);
    }

    [WindowsAudioFact]
    public async Task RetainAtEnd_SurvivesCollectionAndUnrelatedAdmissionUntilOwnerDisposes()
    {
        await using var fixture = new AudioFixture(options => options.MaximumVoices = 2); await fixture.Start();
        using var session = fixture.Sessions.Create()!; using var clip = session.LoadSound(fixture.Wave);
        using var retained = session.PlaySound(clip, new() { RetainAtEnd = true });
        fixture.Render(50000);
        Assert.Equal(AudioPlaybackState.Ended, retained.State);
        Assert.Equal(0, session.CollectFinished());
        using var other = fixture.Sessions.Create()!; using var otherClip = other.LoadSound(fixture.Wave);
        using var active = other.PlaySound(otherClip, new() { StartPaused = true });
        Assert.Equal(AudioPlaybackState.Ended, retained.State);
        Assert.Equal(2, fixture.Audio.GetSnapshot().Voices);
        Assert.Throws<InvalidOperationException>(() => other.PlaySound(otherClip));
        retained.Play();
        Assert.Contains(fixture.Render(), sample => sample > .24f);
        retained.Dispose();
        Assert.Equal(1, fixture.Audio.GetSnapshot().Voices);
        using var replacement = other.PlaySound(otherClip, new() { StartPaused = true });
        Assert.Equal(2, fixture.Audio.GetSnapshot().Voices);
    }

    [WindowsAudioFact]
    public async Task Loop_CanBeChangedWithoutReplacingThePreparedVoice()
    {
        await using var fixture = new AudioFixture(); await fixture.Start();
        using var session = fixture.Sessions.Create()!; using var clip = session.LoadSound(fixture.Wave);
        using var voice = session.PlaySound(clip, new() { StartPaused = true, RetainAtEnd = true });
        Assert.False(voice.Loop); voice.Loop = true; Assert.True(voice.Loop);
        voice.Play(); fixture.Render(50000);
        Assert.Equal(AudioPlaybackState.Playing, voice.State);
        Assert.All(fixture.Render(), sample => Assert.InRange(sample, .2499f, .2501f));
        voice.Loop = false; Assert.False(voice.Loop); fixture.Render(50000);
        Assert.Equal(AudioPlaybackState.Ended, voice.State);
        Assert.Equal(0, session.CollectFinished());
        voice.Dispose(); Assert.Throws<ObjectDisposedException>(() => _ = voice.Loop);
        Assert.Throws<ObjectDisposedException>(() => voice.Loop = true);
    }
}
