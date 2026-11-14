using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Kul.Native;
using Xunit;

namespace TrueMoon.Kul.Tests;

public sealed class NativeLifetimeTests
{
    [WindowsAudioTheory]
    [InlineData("resources")]
    [InlineData("engine")]
    public void NativeConstructionFailureRollsBackInitializedParents(string stage)
    {
        int baseline = NativeHandle.LiveCount;
        var expected = new InvalidOperationException("injected initialization failure");
        for (int i = 0; i < 16; i++)
        {
            var error = Assert.Throws<InvalidOperationException>(() => new AudioGraph(new AudioOptions { Mode = AudioMode.Offline },
                reached => { if (reached == stage) throw expected; }));
            Assert.Same(expected, error);
            Assert.Equal(baseline, NativeHandle.LiveCount);
        }
    }

    [WindowsAudioFact]
    public async Task MicrosoftAdapterBorrowsAliasesAndDrainsOneNativeOwner()
    {
        var baseline = NativeHandle.LiveCount;
        var builder = TrueMoon.App.Builder(c => c.UseDI()).Setup(c => c.UseAudio(o => o.Mode = AudioMode.Offline));
        await using var app = builder.Build();
        var audio = app.Services.Resolve<IAudioService>()!;
        await app.StartAsync();
        var factory = app.Services.Resolve<IFactory<AudioSession>>()!;
        var session = factory.Create()!;
        Assert.Equal(1, audio.GetSnapshot().Sessions);
        Assert.False(audio is IDisposable);
        await app.StopAsync(); await app.DisposeAsync();
        Assert.True(session.IsDisposed);
        Assert.Equal(baseline, NativeHandle.LiveCount);
        Assert.False(audio.GetSnapshot().IsRunning);
        Assert.Throws<ObjectDisposedException>(() => factory.Create());
    }

    [WindowsAudioFact]
    public async Task ConcurrentControlsSessionDisposeAndAppStopDoNotUseFreedNativeMemory()
    {
        await using var f = new AudioFixture(); await f.Start();
        var session = f.Sessions.Create()!; var clip = session.LoadSound(f.Wave);
        var voice = session.PlaySound(clip, new() { Loop = true });
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var control = Task.Run(async () =>
        {
            await ready.Task;
            for (int i = 0; i < 256; i++)
            {
                try { voice.Volume = .5f; voice.Pause(); voice.Play(); f.Render(256); }
                catch (ObjectDisposedException) { return; }
            }
        });
        ready.SetResult();
        session.Dispose(); await f.App.StopAsync(); await control;
        Assert.Equal(AudioPlaybackState.Disposed, voice.State);
        Assert.True(clip.IsDisposed);
        Assert.Equal(0, NativeHandle.LiveCount);
    }
}
