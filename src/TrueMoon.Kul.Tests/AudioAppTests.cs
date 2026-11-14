using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Kul.Native;
using Xunit;

namespace TrueMoon.Kul.Tests;

public sealed class AudioAppTests
{
    [WindowsAudioFact]
    public async Task AppOwnsOneHostAndDrainsForgottenSessions()
    {
        await using var fixture = new AudioFixture();
        Assert.False(fixture.Audio is IDisposable);
        Assert.False(fixture.Sessions is IDisposable);
        Assert.False(fixture.Audio.GetSnapshot().IsRunning);
        Assert.Throws<InvalidOperationException>(() => fixture.Sessions.Create());
        await fixture.Start();
        var session = fixture.Sessions.Create()!;
        var clip = session.LoadSound(fixture.Wave);
        var sound = session.PlaySound(clip, new() { Loop = true });
        var music = session.PlayMusic(fixture.Music, new() { Loop = true });
        Assert.Equal((1, 1, 2, 1), Counts(fixture.Audio));
        Assert.Contains(fixture.Render(), sample => sample != 0);
        await fixture.App.StopAsync();
        Assert.True(session.IsDisposed);
        Assert.True(clip.IsDisposed);
        Assert.Equal(AudioPlaybackState.Disposed, sound.State);
        Assert.Equal(AudioPlaybackState.Disposed, music.State);
        Assert.Equal((0, 0, 0, 0), Counts(fixture.Audio));
        Assert.Equal(0, fixture.Audio.GetSnapshot().ResidentSoundBytes);
        Assert.Throws<ObjectDisposedException>(() => fixture.Sessions.Create());
        Assert.Throws<ObjectDisposedException>(() => music.Play());
        sound.Dispose(); music.Dispose(); clip.Dispose(); session.Dispose();
        await fixture.App.StopAsync();
        AudioFixture.AssertUnlocked(fixture.Music);
        Assert.Equal(0, NativeHandle.LiveCount);
    }

    [WindowsAudioFact]
    public async Task DisposeBeforeStartCreatesNoNativeGraphAndClosesFactory()
    {
        await using var fixture = new AudioFixture();
        await fixture.App.DisposeAsync();
        Assert.Equal(0, NativeHandle.LiveCount);
        Assert.Throws<ObjectDisposedException>(() => fixture.Sessions.Create());
        Assert.False(fixture.Audio.GetSnapshot().IsRunning);
    }

    [WindowsAudioFact]
    public async Task RegistrationCopiesOptionsAndRejectsDuplicates()
    {
        AudioOptions? captured = null;
        await using var fixture = new AudioFixture(o => { o.MaximumSessions = 1; captured = o; });
        captured!.MaximumSessions = 3;
        await fixture.Start();
        using var first = fixture.Sessions.Create()!;
        Assert.Throws<InvalidOperationException>(() => fixture.Sessions.Create());
        first.Dispose();
        using var second = fixture.Sessions.Create()!;
        Assert.NotSame(first, second);
        Assert.Throws<InvalidOperationException>(() => TrueMoon.App.Build(c => c.UseAudio().UseAudio()));
    }

    [Fact]
    public void InvalidStartupLimitsAreRejectedBeforeNativeInitialization()
    {
        Action<AudioOptions>[] invalid = [
            o => o.Mode = (AudioMode)99, o => o.SampleRate = 7999, o => o.SampleRate = 192001,
            o => o.PeriodFrames = 63, o => o.PeriodFrames = 8193,
            o => o.Volume = float.NaN, o => o.Volume = float.PositiveInfinity, o => o.Volume = -.1f, o => o.Volume = 1.1f,
            o => o.MaximumSessions = 0, o => o.MaximumSessions = 1025, o => o.MaximumClips = 0,
            o => o.MaximumVoices = 0, o => o.MaximumMusicStreams = 0, o => o.MaximumMusicStreams = 65,
            o => o.MaximumSoundFileBytes = 0, o => o.MaximumResidentSoundBytes = 7];
        foreach (var configure in invalid)
            Assert.Throws<ArgumentOutOfRangeException>(() => TrueMoon.App.Build(c => c.UseAudio(configure)));
    }

    [WindowsAudioFact]
    public async Task ConfiguredAudioUsesCommonProfileAndExplicitOverride()
    {
        var profile = ConfigurationProfiles.Create(b =>
        {
            b.AddSchema(AudioConfigurationSchema.Default);
            b.AddArguments("args", ["--Audio:Mode=Offline", "--Audio:MaximumSessions=2", "--Audio:Volume=0.25"]);
        });
        await using var app = TrueMoon.App.Build(c => c.UseConfiguration(profile).UseConfiguredAudio(o => o.MaximumSessions = 1));
        var reader = app.Services.Resolve<IConfigurationReader>()!;
        Assert.Equal(.25f, reader.Current.GetOptions(AudioConfigurationSchema.Default).Volume);
        await app.StartAsync();
        var audio = app.Services.Resolve<IAudioService>()!;
        Assert.Equal(AudioMode.Offline, audio.GetSnapshot().Mode);
        Assert.Equal(.25f, audio.Volume);
        var factory = app.Services.Resolve<IFactory<AudioSession>>()!;
        using var session = factory.Create()!;
        Assert.Throws<InvalidOperationException>(() => factory.Create());
        var detached = reader.Current.GetOptions(AudioConfigurationSchema.Default);
        detached.MaximumSessions = 99;
        Assert.Equal(1, reader.Current.GetOptions(AudioConfigurationSchema.Default).MaximumSessions);
    }

    [WindowsAudioFact]
    public async Task LaterStartupFailureRollsBackAudioAndItsNativeResources()
    {
        var expected = new InvalidOperationException("consumer startup failure");
        AudioSession? session = null;
        await using var app = TrueMoon.App.Build(c => c.UseAudio(o => o.Mode = AudioMode.Offline).Services(s =>
            s.Singleton<IStartable>(r => new FailingConsumer(() => { session = r.Resolve<IFactory<AudioSession>>().Create(); throw expected; }))));
        var error = await Record.ExceptionAsync(() => app.StartAsync());
        Assert.Same(expected, error);
        Assert.NotNull(session);
        Assert.True(session.IsDisposed);
        Assert.Equal(0, NativeHandle.LiveCount);
        Assert.False(app.Services.Resolve<IAudioService>()!.GetSnapshot().IsRunning);
    }

    [WindowsAudioFact]
    public async Task TwoAppsAndEarlySessionDisposalHaveIndependentLifetime()
    {
        await using var first = new AudioFixture();
        // Dispose the second App before the first fixture checks the global native baseline.
        await using (var second = new AudioFixture())
        {
            await first.Start(); await second.Start();
            using var a = first.Sessions.Create()!; using var b = second.Sessions.Create()!;
            using var clip = b.LoadSound(second.Wave); using var voice = b.PlaySound(clip, new() { Loop = true });
            a.Dispose();
            Assert.Equal(0, first.Audio.GetSnapshot().Sessions);
            await first.App.StopAsync();
            Assert.Contains(second.Render(), x => x > .2f);
            Assert.Equal(AudioPlaybackState.Playing, voice.State);
        }
    }

    private static (int, int, int, int) Counts(IAudioService audio)
    { var s = audio.GetSnapshot(); return (s.Sessions, s.Clips, s.Voices, s.MusicStreams); }
    private sealed class FailingConsumer(Action start) : IStartable
    { public Task StartAsync(CancellationToken cancellationToken = default) { start(); return Task.CompletedTask; } }
}
