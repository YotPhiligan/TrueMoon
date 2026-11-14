using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using MiniAudioEx.Native;
using TrueMoon.Configuration;
using TrueMoon.Kul;

namespace TrueMoon.KulModuleProbe;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length < 2) throw new ArgumentException("fixtures-directory report.json [common --Audio:Key=value options]");
        string directory = Path.GetFullPath(args[0]), report = Path.GetFullPath(args[1]);
        var checks = new List<string>();
        var observations = new Dictionary<string, object?>();
        var result = new ProbeResult(checks, observations);
        try
        {
            ProbeFixtures.Generate(directory);
            var profile = ConfigurationProfiles.Create(b =>
            {
                b.AddSchema(AudioConfigurationSchema.Default);
                b.AddArguments("args", args.Skip(2).ToArray());
            });
            await using var app = App.Build(c => c.UseConfiguration(profile).UseConfiguredAudio().Services(s =>
                s.Singleton<ProbeConsumer>(r => new ProbeConsumer(r.Resolve<IAudioService>(), r.Resolve<IFactory<AudioSession>>(), directory, checks, observations))));
            var consumer = app.Services.GetService(typeof(ProbeConsumer)) as ProbeConsumer ?? throw new InvalidOperationException("DI consumer missing.");
            consumer.BeforeStart();
            await app.StartAsync();
            await consumer.Run();
            await app.StopAsync();
            consumer.AfterStop();
            await app.DisposeAsync();
            observations["Assemblies"] = new[] { typeof(App).Assembly, typeof(IAudioService).Assembly, typeof(IApp).Assembly, typeof(MiniAudioNative).Assembly }
                .Select(a => new { Name = a.GetName().Name, Path = a.Location, Sha256 = Hash(a.Location) }).ToArray();
            using var process = Process.GetCurrentProcess();
            var native = process.Modules.Cast<ProcessModule>().Single(m => m.ModuleName.Equals("miniaudioex.dll", StringComparison.OrdinalIgnoreCase));
            observations["Native"] = new { Path = native.FileName, Sha256 = Hash(native.FileName) };
            result.Completed = true;
        }
        catch (Exception error) { result.Failure = error.ToString(); Console.Error.WriteLine(error); }
        finally { await File.WriteAllTextAsync(report, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })); }
        Console.WriteLine($"Audio module: completed={result.Completed}, checks={checks.Count}");
        return result.Completed ? 0 : 1;
    }
    private static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    private sealed class ProbeResult(List<string> checks, Dictionary<string, object?> observations)
    {
        public bool Completed { get; set; }
        public List<string> Checks { get; } = checks;
        public Dictionary<string, object?> Observations { get; } = observations;
        public string? Failure { get; set; }
    }
}

// A real consumer with constructor-injected dependencies. No resolver appears in the playback path.
internal sealed class ProbeConsumer(IAudioService audio, IFactory<AudioSession> sessions, string directory,
    List<string> checks, Dictionary<string, object?> observations)
{
    private AudioSession? forgotten;
    private AudioPlayback? forgottenMusic;
    internal void BeforeStart()
    {
        Check(!audio.GetSnapshot().IsRunning, "App build opens no native audio graph");
        Check(audio is not IDisposable && sessions is not IDisposable, "DI service and factory aliases do not own disposal");
    }
    internal async Task Run()
    {
        var snapshot = audio.GetSnapshot();
        observations["Output"] = snapshot;
        Check(snapshot.IsRunning, "App startup opens admission");
        bool device = snapshot.Mode == AudioMode.Device;
        if (device) Check(snapshot.DevicePeriodFrames > 0 && snapshot.DevicePeriods > 0, "Actual WASAPI device negotiated periods");
        else
        {
            Check(snapshot.DevicePeriodFrames == 0, "Offline mode opens no device");
            using var test = sessions.Create()!;
            using var constant = test.LoadSound(Path.Combine(directory, "constant.wav"));
            using var voice = test.PlaySound(constant, new() { Volume = .5f });
            audio.Volume = 1;
            float[] buffer = new float[2048]; audio.Render(buffer); audio.Render(buffer);
            Check(buffer.All(x => Math.Abs(x - .125f) < .0001f), "Known PCM gain and mono-to-stereo routing");
            voice.Pause(); var cursor = voice.CursorFrames; audio.Render(buffer);
            Check(buffer.All(x => x == 0) && voice.CursorFrames == cursor, "Pause freezes cursor and emits silence");
            voice.Play(); audio.Render(buffer); Check(voice.CursorFrames > cursor, "Resume advances native cursor");
            voice.Stop(); audio.Render(buffer); Check(voice.CursorFrames == 0, "Stop rewinds sound");
            audio.Volume = .25f;
        }
        using (var session = sessions.Create()!)
        {
            using var clip = session.LoadSound(Path.Combine(directory, "эффект.wav"));
            using var effect = session.PlaySound(clip);
            using var a = session.PlayMusic(Path.Combine(directory, "музыка.ogg"), new() { Loop = true, Volume = .2f });
            using var b = session.PlayMusic(Path.Combine(directory, "музыка.ogg"), new() { Loop = true, Volume = 0 });
            Check(audio.GetSnapshot().MusicStreams == 2, "Two native music streams admitted");
            Check(a.LengthFrames is null, "Unavailable OGG duration is explicit");
            if (device)
            {
                await Task.Delay(350);
                Check(effect.CursorFrames > 0 && a.CursorFrames > 0, "Actual native device advances SFX and OGG");
                a.Pause(); await Task.Delay(50); var cursor = a.CursorFrames; await Task.Delay(75);
                Check(a.CursorFrames == cursor, "Device music pause freezes cursor after current mix block");
                a.Play(); a.Seek(48000);
                for (int i = 0; i <= 10; i++) { a.Volume = .2f * (10 - i) / 10; b.Volume = .2f * i / 10; await Task.Delay(25); }
                Check(b.CursorFrames > 0 && a.Volume == 0 && b.Volume == .2f, "Device stream crossfade controls both voices");
            }
            else
            {
                float[] output = new float[8192];
                audio.Render(output);
                Check(output.Any(x => Math.Abs(x) > .001f), "Streamed OGG and effect mix actual PCM");
                a.Seek(480000); await Refill(a, output);
                Check(a.CursorFrames >= 480000, "Stream seek refills on workers");
            }
        }
        var music = Path.Combine(directory, "музыка.ogg");
        Unlocked(music); Check(audio.GetSnapshot().Voices == 0, "Early session disposal releases music streams");
        using (var invalidSession = sessions.Create()!)
        {
            foreach (string file in new[] { "corrupt.ogg", "truncated.ogg", "bad-crc.ogg" })
            {
                string path = Path.Combine(directory, file);
                for (int i = 0; i < 8; i++)
                {
                    try { using var unexpected = invalidSession.PlayMusic(path); throw new InvalidOperationException("Malformed OGG was accepted."); }
                    catch (AudioException) { }
                    Unlocked(path);
                }
                Check(audio.GetSnapshot().Voices == 0, "Malformed OGG releases file and voice admission: " + file);
            }
            using var valid = invalidSession.PlayMusic(music);
            if (device) await Task.Delay(150);
            else { float[] output = new float[8192]; await Refill(valid, output); }
            Check(valid.CursorFrames > 0, "Valid OGG plays after malformed failures");
        }
        if (!device)
        {
            using var longSession = sessions.Create()!;
            using var stream = longSession.PlayMusic(Path.Combine(directory, "long.wav"), new() { Loop = true });
            Check(stream.LengthFrames == 28800000, "Ten-minute music duration and streaming path");
            stream.Seek(28800000 - 512); float[] output = new float[8192]; await Refill(stream, output);
            Check(stream.CursorFrames < 8192 && stream.State == AudioPlaybackState.Playing, "Long stream seeks across EOF and loops");
        }
        for (int i = 0; i < 32; i++)
        {
            using var session = sessions.Create()!;
            using var voice = session.PlayMusic(music, new() { Loop = true });
            session.Dispose(); Unlocked(music);
        }
        Check(audio.GetSnapshot().Sessions == 0 && audio.GetSnapshot().Voices == 0 && audio.GetSnapshot().ResidentSoundBytes == 0, "32 session cycles release all logical resources");
        forgotten = sessions.Create()!;
        forgottenMusic = forgotten.PlayMusic(music, new() { Loop = true });
        observations["BeforeStop"] = audio.GetSnapshot();
    }
    internal void AfterStop()
    {
        var snapshot = audio.GetSnapshot();
        Check(forgotten!.IsDisposed && forgottenMusic!.State == AudioPlaybackState.Disposed, "App stop closes forgotten factory products");
        Check(!snapshot.IsRunning && snapshot.Sessions == 0 && snapshot.Clips == 0 && snapshot.Voices == 0 && snapshot.ResidentSoundBytes == 0, "App stop drains all logical audio owners");
        Unlocked(Path.Combine(directory, "музыка.ogg"));
        try { sessions.Create(); throw new InvalidOperationException("Stopped App accepted an audio session."); }
        catch (ObjectDisposedException) { checks.Add("Stopped App rejects new playback"); }
        observations["AfterStop"] = snapshot;
    }
    private async Task Refill(AudioPlayback voice, float[] output)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(3))
        {
            audio.Render(output);
            if (output.Any(x => Math.Abs(x) > .001f) && voice.CursorFrames > 0) return;
            await Task.Delay(1);
        }
        throw new TimeoutException("Native stream refill did not produce PCM.");
    }
    private void Check(bool passed, string name)
    { if (!passed) throw new InvalidOperationException(name); checks.Add(name); }
    private static void Unlocked(string path) { using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None); }
}
