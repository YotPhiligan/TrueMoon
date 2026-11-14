using System.Text;
using TrueMoon.Dependencies;
using TrueMoon.Kul.Native;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace TrueMoon.Kul.Tests;

internal sealed class AudioFixture : IAsyncDisposable
{
    internal string Root { get; } = Path.Combine(Path.GetTempPath(), "TrueMoon-Audio-" + Guid.NewGuid().ToString("N"));
    internal string Wave => Path.Combine(Root, "эффект.wav");
    internal string Music => Path.Combine(Root, "музыка.ogg");
    internal IApp App { get; }
    internal IAudioService Audio { get; }
    internal IFactory<AudioSession> Sessions { get; }
    private readonly int baseline = Volatile.Read(ref NativeHandle.LiveCount);

    internal AudioFixture(Action<AudioOptions>? configure = null)
    {
        Directory.CreateDirectory(Root);
        WriteWave(Wave, 48000, .25f);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Assets/music.ogg"), Music);
        App = TrueMoon.App.Build(c => c.UseAudio(o => { o.Mode = AudioMode.Offline; o.Volume = 1; configure?.Invoke(o); }));
        Audio = App.Services.Resolve<IAudioService>()!;
        Sessions = App.Services.Resolve<IFactory<AudioSession>>()!;
    }
    internal async Task Start() => await App.StartAsync();
    internal float[] Render(int frames = 1024)
    {
        float[] output = new float[frames * 2];
        Audio.Render(output);
        return output;
    }
    internal async Task<float[]> RenderUntilSignal(int frames = 4096)
    {
        var deadline = System.Diagnostics.Stopwatch.StartNew();
        while (deadline.Elapsed < TimeSpan.FromSeconds(2))
        {
            var output = Render(frames);
            if (output.Any(x => Math.Abs(x) > .01f)) return output;
            await Task.Delay(1); // Stream seek/refill is queued to native resource workers.
        }
        throw new TimeoutException("Native stream did not refill after seek.");
    }
    internal static void WriteWave(string path, int frames, float amplitude, int rate = 48000)
    {
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + frames * 2);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
        writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(frames * 2);
        for (int i = 0; i < frames; i++) writer.Write((short)(amplitude * 32768));
    }
    internal static void AssertUnlocked(string file)
    {
        using var stream = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(stream.Length > 0);
    }
    public async ValueTask DisposeAsync()
    {
        await App.DisposeAsync();
        Assert.Equal(baseline, Volatile.Read(ref NativeHandle.LiveCount));
        Directory.Delete(Root, true);
    }
}

public sealed class WindowsAudioFactAttribute : FactAttribute
{
    public WindowsAudioFactAttribute()
    {
        if (!OperatingSystem.IsWindows() || System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture != System.Runtime.InteropServices.Architecture.X64)
            Skip = "Requires the Windows x64 audio dependency.";
    }
}

public sealed class WindowsAudioTheoryAttribute : TheoryAttribute
{
    public WindowsAudioTheoryAttribute()
    {
        if (!OperatingSystem.IsWindows() || System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture != System.Runtime.InteropServices.Architecture.X64)
            Skip = "Requires the Windows x64 audio dependency.";
    }
}
