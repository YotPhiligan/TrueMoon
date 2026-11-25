using System.Security.Cryptography;
using System.Text;
using TrueMoon.Aether.Kul.Runtime;
using TrueMoon.Aether.Kul.Runtime.Content;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Loading;
using TrueMoon.Aether.Runtime.Ecs.Rendering;
using TrueMoon.Aether.Runtime.Ecs.Runtime;
using TrueMoon.Aether.Scenes;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Kul;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace TrueMoon.Aether.Kul.Tests;

internal sealed class AudioFixture : IAsyncDisposable
{
    internal static readonly TimeSpan Step = TimeSpan.FromSeconds(1d / 60);
    internal static readonly AudioAssetId SoundId = new(new Guid("ce5aba7a-4542-440d-9cab-f05b03208201"));
    internal static readonly AudioAssetId OtherId = new(new Guid("ce5aba7a-4542-440d-9cab-f05b03208202"));
    internal static readonly AudioAssetId MusicId = new(new Guid("ce5aba7a-4542-440d-9cab-f05b03208203"));
    internal string Root { get; } = Path.Combine(Path.GetTempPath(), "TrueMoon-KulEcs-" + Guid.NewGuid().ToString("N"));
    internal AudioContentReference Content { get; }
    internal AudioContentManifest Manifest { get; }
    internal IApp App { get; }
    internal IAudioService Audio => App.Services.Resolve<IAudioService>()!;
    internal string PathFor(AudioAssetId id) => Path.Combine(Content.DirectoryPath, Manifest.Assets.Single(asset => asset.Id == id).FileName);
    internal static EntityId Entity(int index) => new(new Guid($"cb23e647-8944-4fba-91f7-{index + 1:000000000000}"));

    internal AudioFixture(bool microsoft = false, int voices = 2, bool music = false, int soundFrames = 48000, int? otherFrames = null,
        Action<AudioOptions>? audio = null, Action<AudioBridgeOptions>? bridge = null,
        Action<IAppConfigurationContext>? beforeBridge = null, Action<IAppConfigurationContext>? afterBridge = null)
    {
        Directory.CreateDirectory(Root);
        var staging = Path.Combine(Root, "staging"); Directory.CreateDirectory(staging);
        WriteWave(Path.Combine(staging, "sound.wav"), soundFrames, .25f);
        WriteWave(Path.Combine(staging, "other.wav"), otherFrames ?? soundFrames, .125f);
        var assets = new List<AudioContentAsset>
        {
            Asset(SoundId, staging, "sound.wav", AudioAssetKind.Sound, voices),
            Asset(OtherId, staging, "other.wav", AudioAssetKind.Sound, 1)
        };
        if (music)
        {
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Assets/music.ogg"), Path.Combine(staging, "music.ogg"));
            assets.Add(Asset(MusicId, staging, "music.ogg", AudioAssetKind.Music, 1));
        }
        Manifest = new(assets); var bytes = Manifest.Encode(); var hash = Convert.ToHexString(SHA256.HashData(bytes));
        File.WriteAllBytes(Path.Combine(staging, AudioContentManifest.FileName), bytes);
        var target = Path.Combine(Root, hash); Directory.Move(staging, target); Content = new(target, hash);
        App = Build(microsoft, Content, audio, bridge, beforeBridge, afterBridge);
    }

    internal static IApp Build(bool microsoft, AudioContentReference reference, Action<AudioOptions>? audio = null,
        Action<AudioBridgeOptions>? bridge = null, Action<IAppConfigurationContext>? beforeBridge = null, Action<IAppConfigurationContext>? afterBridge = null)
        => TrueMoon.App.Builder(context => { if (microsoft) context.UseDI(); }).Setup(context =>
        {
            context.UseAudio(options => { options.Mode = AudioMode.Offline; options.Volume = 1; audio?.Invoke(options); });
            context.UseAetherEcs().UseAetherPrimitiveEcs();
            context.UseAetherSceneLoading(_ => AudioComponentRegistry.Register(PrimitiveEcsComponents.Register(new())).Build());
            beforeBridge?.Invoke(context);
            context.UseAetherKul(reference, bridge); context.UseAetherSceneSubsystems(builder => builder.AddKulAudio());
            RegisterGame(context); afterBridge?.Invoke(context);
        }).Build();

    internal static void RegisterGame(IAppConfigurationContext context) => context.Services(services => services.Singleton<IFactory<PrimitiveGameSession>>(resolver =>
    {
        var loader = resolver.Resolve<SceneWorldLoader>()!;
        var simulations = resolver.Resolve<IFactory<PrimitiveSimulation, PrimitiveSimulationRequest>>()!;
        return new Factory<PrimitiveGameSession>(() => new(loader, simulations));
    }));
    internal Task Start() => App.StartAsync();
    internal PrimitiveGameSession Game() => App.Services.Resolve<IFactory<PrimitiveGameSession>>()!.Create()!;
    internal SceneWorldLoader Loader => App.Services.Resolve<SceneWorldLoader>()!;
    internal IFactory<AudioSceneSubsystem, SceneSubsystemArguments> Subsystems => App.Services.Resolve<IFactory<AudioSceneSubsystem, SceneSubsystemArguments>>()!;
    internal IFactory<AudioSession> Sessions => App.Services.Resolve<IFactory<AudioSession>>()!;
    internal float[] Render(int frames = 1024) { float[] output = new float[frames * 2]; Audio.Render(output); return output; }
    internal async Task<float[]> Signal()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (watch.Elapsed < TimeSpan.FromSeconds(3))
        {
            var output = Render(4096);
            if (output.Any(value => Math.Abs(value) > .01f)) return output;
            await Task.Delay(1);
        }
        throw new TimeoutException("Native streaming worker did not produce PCM.");
    }
    internal static void Silent(IEnumerable<float> samples) => Assert.All(samples, sample => Assert.Equal(0f, sample));
    internal static void Constant(IEnumerable<float> samples, float expected) => Assert.All(samples, sample => Assert.InRange(sample, expected - .0001f, expected + .0001f));
    internal static AudioContentAsset Asset(AudioAssetId id, string root, string name, AudioAssetKind kind, int voices)
    { var bytes = File.ReadAllBytes(Path.Combine(root, name)); return new(id, name, Convert.ToHexString(SHA256.HashData(bytes)), bytes.Length, kind, voices); }
    internal static void WriteWave(string path, int frames, float amplitude)
    {
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + frames * 2);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
        writer.Write(48000); writer.Write(96000); writer.Write((short)2); writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(frames * 2);
        for (var i = 0; i < frames; i++) writer.Write((short)(amplitude * 32768));
    }
    internal static SceneDocument Document(params AudioSource[] sources)
    {
        var entities = new List<EntityInstance>(); var definitions = new List<EntityDefinition>();
        for (var i = 0; i < sources.Length; i++)
        {
            var source = sources[i]; var definition = new EntityDefinitionId(Guid.NewGuid());
            entities.Add(new(Entity(i), definition));
            definitions.Add(new(definition, new([new ComponentData(new(Guid.NewGuid()), AudioComponentSchemas.SourceId, 1,
                [new("asset",PropertyValue.From(source.Asset.Value.ToString("D"))),new("volume",PropertyValue.From((double)source.Volume)),
                 new("loop",PropertyValue.From(source.Loop)),new("autoPlay",PropertyValue.From(source.AutoPlay))])])));
        }
        return new(new(Guid.NewGuid()), 17, entities, definitions, BuiltInComponents.Schemas.Concat([AudioComponentSchemas.Source]));
    }
    public async ValueTask DisposeAsync()
    {
        await App.DisposeAsync();
        Directory.Delete(Root, true);
    }
}

public sealed class WindowsAudioFactAttribute : FactAttribute
{
    public WindowsAudioFactAttribute()
    { if (!OperatingSystem.IsWindows() || System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture != System.Runtime.InteropServices.Architecture.X64) Skip = "Requires Windows x64 native Kul."; }
}
public sealed class WindowsAudioTheoryAttribute : TheoryAttribute
{
    public WindowsAudioTheoryAttribute()
    { if (!OperatingSystem.IsWindows() || System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture != System.Runtime.InteropServices.Architecture.X64) Skip = "Requires Windows x64 native Kul."; }
}
