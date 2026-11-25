using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Kul;
using TrueMoon.Aether.Kul.Runtime;
using TrueMoon.Aether.Kul.Runtime.Content;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Loading;
using TrueMoon.Aether.Runtime.Ecs.Rendering;
using TrueMoon.Aether.Runtime.Ecs.Runtime;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Kul;
using A = TrueMoon.Aether.Ecs;

namespace TrueMoon.ManualTests.KulEcs;

internal static class Program
{
    private static readonly List<object> Checks = [], Owners = [];
    private static readonly AudioAssetId Sound = new(new Guid("1ae17a03-7abf-49e3-959b-9ae55b613e81")), Music = new(new Guid("1ae17a03-7abf-49e3-959b-9ae55b613e82"));
    private static string _provider = "";
    private static void Check(string name, bool value) { if (!value) throw new InvalidOperationException(_provider + ": " + name); Checks.Add(new { Provider = _provider, Name = name }); }
    private static async Task<int> Main(string[] args)
    {
        if (args.Length is < 1 or > 2 || args.Length == 2 && args[1] != "--device") throw new ArgumentException("KulEcsProbe <report.json> [--device]");
        var root = Path.Combine(Path.GetTempPath(), "TrueMoon-KulEcs-content-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var content = Capture(root);
        try { foreach (var microsoft in new[] { false, true }) await Run(microsoft, content, args.Length == 2); }
        finally { Directory.Delete(root, true); }
        var neutral = typeof(AudioSource).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
        Check("sdk-no-native-runtime-workflow", !neutral.Any(n => n != null && (n is "TrueMoon.Kul" or "TrueMoon.Aether.Runtime" || n.Contains("Tooling") || n.Contains("MiniAudio"))));
        var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && a.GetName().Name is { } n && (n.StartsWith("TrueMoon", StringComparison.Ordinal) || n == "MiniAudioExNET"))
            .Select(a => new { Name = a.GetName().Name, a.Location, Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))), References = a.GetReferencedAssemblies().Select(r => r.Name).ToArray() }).ToArray();
        var nativePath = Path.Combine(AppContext.BaseDirectory, "runtimes/win-x64/native/miniaudioex.dll");
        var output = Path.GetFullPath(args[0]); Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { Completed = true, Stage = "ARCH1h.2a", Device = args.Length == 2, OutputMuted = args.Length == 2,
            CheckCount = Checks.Count, Checks, Owners, Assemblies = assemblies, Native = new { Path = nativePath, Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(nativePath))) } }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    private static AudioContentReference Capture(string root)
    {
        var staging = Path.Combine(root, "staging"); Directory.CreateDirectory(staging);
        using (var writer = new BinaryWriter(File.Create(Path.Combine(staging, "sound.wav"))))
        {
            const int frames = 48000; writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + frames * 2); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(48000); writer.Write(96000); writer.Write((short)2); writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(frames * 2); for (var i = 0; i < frames; i++) writer.Write((short)8192);
        }
        File.Copy(Path.Combine(AppContext.BaseDirectory, "music.ogg"), Path.Combine(staging, "music.ogg"));
        AudioContentAsset Asset(AudioAssetId id, string file, AudioAssetKind kind) { var path = Path.Combine(staging, file); return new(id, file, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), new FileInfo(path).Length, kind); }
        var bytes = new AudioContentManifest([Asset(Sound, "sound.wav", AudioAssetKind.Sound), Asset(Music, "music.ogg", AudioAssetKind.Music)]).Encode();
        File.WriteAllBytes(Path.Combine(staging, AudioContentManifest.FileName), bytes); var hash = Convert.ToHexString(SHA256.HashData(bytes)); var final = Path.Combine(root, hash); Directory.Move(staging, final); return new(final, hash);
    }
    private static async Task Run(bool microsoft, AudioContentReference content, bool device)
    {
        _provider = microsoft ? "Microsoft" : "Cobalt"; var observers = new List<Observer>();
        await using var app = App.Builder(c => { if (microsoft) c.UseDI(); }).Setup(context =>
        {
            context.UseAudio(o => { o.Mode = device ? AudioMode.Device : AudioMode.Offline; o.MuteOutput = device; o.Volume = 1; o.MaximumMusicStreams = 8; });
            context.UseAetherEcs().UseAetherPrimitiveEcs().UseAetherKul(content);
            context.UseAetherSceneLoading(_ => AudioComponentRegistry.Register(PrimitiveEcsComponents.Register(new())).Build());
            context.Services(services =>
            {
                services.Singleton<IFactory<Observer, SceneSubsystemArguments>>(_ => new Factory<Observer, SceneSubsystemArguments>(a => { var owner = new Observer(a.Scene.World); observers.Add(owner); return owner; }));
                services.Singleton<IFactory<PrimitiveGameSession>>(resolver =>
                { var loader = resolver.Resolve<SceneWorldLoader>()!; var simulations = resolver.Resolve<IFactory<PrimitiveSimulation, PrimitiveSimulationRequest>>()!; return new Factory<PrimitiveGameSession>(() => new(loader, simulations)); });
            });
            context.UseAetherSceneSubsystems(b => b.AddKulAudio().Add<Observer>("proof.commands"));
        }).Build();
        var audio = app.Services.Resolve<IAudioService>()!;
        Check("build-no-scene-or-device", !audio.GetSnapshot().IsRunning && observers.Count == 0);
        await app.StartAsync(); Check("start-no-scene-native-sessions", audio.GetSnapshot().Sessions == 0);
        var games = app.Services.Resolve<IFactory<PrimitiveGameSession>>()!;
        using var game = games.Create()!; var document = Document(); game.LoadSceneWorld(new(document)); var observer = observers[0];
        var scene = game.ActiveScene!; var entity = scene.Entities[document.Entities[0].Id]; observer.Target = entity;
        Check("prepared-all-assets-clips-streams", audio.GetSnapshot() is { Sessions: 1, Clips: 1, Voices: 2, MusicStreams: 1 });
        Check("private-scene-starts-at-zero", game.TickNumber == 0 && State().CursorFrames == 0);
        if (device) await Task.Delay(60); else Check("prepared-autoplay-is-silent", Silent(Render()));
        Check("paused-preparation-cursor-stable", State().CursorFrames == 0);
        observer.Command = new(1, AudioCommandKind.Play); game.Tick();
        if (device) await Task.Delay(60); else Check("paused-step-play-is-silent", Silent(Render()));
        Check("paused-step-keeps-cursor", State().Phase == AudioSourcePhase.Paused && State().Sequence == 1 && State().CursorFrames == 0);
        game.SetPlaying(true);
        if (device) await Task.Delay(80); else { Render(); Check("actual-known-pcm", Render().All(x => x is > .2498f and < .2502f)); }
        game.Tick(); Check("playing-advances-cursor", State().CursorFrames > 0 && State().Phase == AudioSourcePhase.Playing);
        game.SetPlaying(false); game.Tick(); var paused = State().CursorFrames;
        if (device) await Task.Delay(60); else Check("session-pause-is-silent", Silent(Render()));
        game.Tick(); Check("pause-preserves-native-cursor", State().CursorFrames == paused);
        observer.Command = new(2, AudioCommandKind.Seek, 12000); game.Tick(); Check("paused-seek-is-applied", State().CursorFrames == 12000);
        game.SetPlaying(true); if (device) await Task.Delay(40); else Render(); game.Tick(); Check("resume-continues-position", State().CursorFrames > 12000);
        observer.Source = new(Sound, .5f, true, true); game.Tick();
        if (!device) { Render(); Check("mutable-gain-affects-pcm", Render().All(x => x is > .1248f and < .1252f)); }
        observer.Command = new(3, AudioCommandKind.Pause); game.Tick(); var sourcePause = State().CursorFrames;
        game.SetPlaying(false); game.SetPlaying(true);
        if (device) await Task.Delay(60); else Check("source-pause-survives-global-mode", Silent(Render()));
        game.Tick(); Check("source-paused-cursor-stable", State().CursorFrames == sourcePause && State().Phase == AudioSourcePhase.Paused);
        observer.Command = new(4, AudioCommandKind.Stop); game.Tick(); Check("source-stop-rewinds", State().CursorFrames == 0 && State().Phase == AudioSourcePhase.Stopped);
        observer.Source = new(Music, .4f, true, true); game.Tick();
        if (device) await Task.Delay(100); else Check("captured-ogg-stream-emits", await Signal(audio));
        game.Tick(); Check("stream-source-playing", State().Phase == AudioSourcePhase.Playing && State().CursorFrames > 0);
        using (var invalid = games.Create()!)
        {
            try { invalid.LoadSceneWorld(new(Document(new(Guid.NewGuid())))); throw new InvalidOperationException("Unknown asset accepted."); } catch (InvalidDataException) { }
            Check("failed-candidate-not-published", invalid.ActiveScene is null && audio.GetSnapshot().Sessions == 1);
        }
        var previous = game.TickNumber; game.Tick(); Check("failed-candidate-preserves-playing-session", game.TickNumber == previous + 1 && !game.IsFaulted);
        observer.Detach = true; game.Tick(); Check("phase-detach-keeps-entity", scene.World.IsAlive(entity) && !scene.World.HasComponent<AudioSource>(entity));
        if (device) await Task.Delay(30); else Check("detached-stream-silent", Silent(Render()));
        Check("pool-remains-prepared", audio.GetSnapshot().Voices == 2);
        game.Dispose(); Check("stop-releases-native-bank-before-scene", scene.IsDisposed && audio.GetSnapshot() is { Sessions: 0, Clips: 0, Voices: 0, MusicStreams: 0, ResidentSoundBytes: 0 } && observer.SystemDisposals == 1 && observer.Disposed);
        using (var replaced = games.Create()!) { replaced.LoadSceneWorld(new(document)); Check("replacement-is-new-paused-world", replaced.ActiveScene!.World.Id != scene.World.Id && replaced.TickNumber == 0); }
        await app.StopAsync(); var resources = audio.GetSnapshot(); Check("app-drains-all-owners", !resources.IsRunning && resources.Sessions == 0 && resources.Voices == 0 && resources.ResidentSoundBytes == 0);
        foreach (var file in Directory.EnumerateFiles(content.DirectoryPath)) using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        Check("captured-files-unlocked-after-native-cleanup", true); Owners.Add(new { Provider = _provider, Resources = resources });
        AudioSourceState State() => scene.World.GetComponent<AudioSourceState>(entity);
        float[] Render() { var output = new float[2048]; audio.Render(output); return output; }
    }
    private static bool Silent(float[] output) => output.All(x => x == 0);
    private static async Task<bool> Signal(IAudioService audio)
    { var timer = System.Diagnostics.Stopwatch.StartNew(); while (timer.Elapsed < TimeSpan.FromSeconds(3)) { var output = new float[8192]; audio.Render(output); if (output.Any(x => Math.Abs(x) > .005f)) return true; await Task.Delay(1); } return false; }
    private static SceneDocument Document(AudioAssetId? asset = null)
    {
        var definition = new EntityDefinitionId(Guid.NewGuid());
        return new(new(Guid.NewGuid()), 1, [new(new(Guid.NewGuid()), definition)], [new(definition, new(new ComponentData[]
        { new(new(Guid.NewGuid()), AudioComponentSchemas.SourceId, 1, [new("asset", PropertyValue.From((asset ?? Sound).Value.ToString("D"))), new("loop", PropertyValue.From(true)), new("autoPlay", PropertyValue.From(true))]) }))], [AudioComponentSchemas.Source]);
    }
    private sealed class Observer : ISceneSubsystem
    {
        internal EntityHandle Target; internal AudioSource? Source; internal AudioPlaybackCommand? Command;
        internal bool Detach, Disposed; internal int SystemDisposals;
        public IReadOnlyList<EcsSystemRegistration> Systems { get; }
        internal Observer(World world) => Systems = [new(AudioEntityCommands.DeclareWrites(new A.EcsSystemDescription("proof.commands", EcsPhase.Gameplay)), new Factory<A.ISystem, World>(_ => new ObserveSystem(this)))];
        public void SetPlaying(bool playing) { if (Disposed) throw new ObjectDisposedException(nameof(Observer)); }
        public void Dispose() { if (SystemDisposals != 1) throw new InvalidOperationException("Scheduler must stop first."); Disposed = true; }
        private sealed class ObserveSystem(Observer owner) : A.ISystem
        {
            private bool _hasCommand;
            public void Execute(A.EcsSystemContext context)
            {
                if (owner.Source is { } source) { context.Commands.SetComponent(owner.Target, source); owner.Source = null; }
                if (owner.Command is { } command)
                { if (_hasCommand) context.Commands.SetComponent(owner.Target, command); else { context.Commands.AddComponent(owner.Target, command); _hasCommand = true; } owner.Command = null; }
                if (owner.Detach) { AudioEntityCommands.RemoveSource(context.Commands, owner.Target); owner.Detach = false; }
            }
            public void Dispose() { if (owner.Disposed) throw new InvalidOperationException("Owner released before system."); owner.SystemDisposals++; }
        }
    }
}
