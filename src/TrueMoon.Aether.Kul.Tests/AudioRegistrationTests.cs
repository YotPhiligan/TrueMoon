using System.Runtime.CompilerServices;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Kul.Runtime;
using TrueMoon.Aether.Kul.Runtime.Content;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Loading;
using TrueMoon.Aether.Runtime.Ecs.Runtime;
using TrueMoon.Aether.Scenes;
using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Kul;
using Xunit;
using Bridge = TrueMoon.Aether.Kul.Runtime.AppConfigurationExtensions;

namespace TrueMoon.Aether.Kul.Tests;

public sealed class AudioRegistrationTests
{
    private static AudioContentReference MissingCapture() { var hash = new string('A', 64); return new(Path.Combine(Path.GetTempPath(), hash), hash); }

    [Fact]
    public void NeutralSdk_ContainsReferenceFreeComponentsAndExactNonspatialSchemaWithoutRuntimeOrNativeDependency()
    {
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<AudioAssetId>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<AudioSource>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<AudioPlaybackCommand>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<AudioSourceState>());
        var dependencies = typeof(AudioSource).Assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
        Assert.Contains("TrueMoon.Aether.Abstractions", dependencies);
        Assert.DoesNotContain(dependencies, name => name is "TrueMoon.Kul" or "TrueMoon.MiniAudioEx" || name!.StartsWith("TrueMoon.Aether.Runtime", StringComparison.Ordinal));
        var schema = AudioComponentSchemas.Source;
        Assert.Equal(new Guid("b5ec3c81-79a9-4a11-b052-9fe141d5b001"), schema.TypeId.Value);
        Assert.Equal(1, schema.Version); Assert.False(schema.AllowMultiple); Assert.Empty(schema.RequiredTypes); Assert.Equal(4, schema.Properties.Count);
        Assert.Equal(PropertyValueKind.String, schema.Properties["asset"].Kind); Assert.Equal("", schema.Properties["asset"].DefaultValue.GetString());
        Assert.Equal(1d, schema.Properties["volume"].DefaultValue.GetNumber());
        Assert.False(schema.Properties["loop"].DefaultValue.GetBoolean()); Assert.False(schema.Properties["autoPlay"].DefaultValue.GetBoolean());
    }

    [Fact]
    public void Registration_RejectsNullDuplicateAndMissingProviderComposition()
    {
        var content = MissingCapture();
        Assert.Throws<ArgumentNullException>(() => Bridge.UseAetherKul(null!, content));
        Assert.Throws<ArgumentNullException>(() => Bridge.AddKulAudio(null!));
        Assert.Throws<ArgumentNullException>(() => AudioComponentRegistry.Register(null!));
        Assert.Throws<InvalidOperationException>(() => App.Builder(_ => { }).Setup(context => context.UseAetherKul(content)).Build());
        Assert.Throws<ArgumentNullException>(() => App.Builder(_ => { }).Setup(context => { context.UseAudio(); context.UseAetherKul(null!); }).Build());
        Assert.Throws<InvalidOperationException>(() => App.Builder(_ => { }).Setup(context => { context.UseAudio(); context.UseAetherKul(content); context.UseAetherKul(content); }).Build());
        Assert.Throws<ArgumentException>(() => new SceneSubsystemsBuilder().AddKulAudio().AddKulAudio());
        var registry = AudioComponentRegistry.Register(new()); Assert.Throws<ArgumentException>(() => AudioComponentRegistry.Register(registry));
    }

    [Theory]
    [InlineData("sources", 0L)] [InlineData("sources", 257L)] [InlineData("voices", 0L)] [InlineData("voices", 257L)]
    [InlineData("assets", 0L)] [InlineData("assets", 65L)] [InlineData("bytes", 0L)] [InlineData("bytes", 1073741825L)]
    public void Options_RejectOutOfRangeAdmissionBeforeAppCreatesNativeResources(string field, long value)
        => Assert.Throws<ArgumentOutOfRangeException>(() => AudioFixture.Build(false, MissingCapture(), bridge: options => Set(options, field, value)));

    [WindowsAudioTheory]
    [InlineData(1)] [InlineData(256)]
    public async Task Options_AdmitExactBoundsWithoutOpeningMissingSceneContentAtBuildOrStart(int limit)
    {
        await using var app = AudioFixture.Build(false, MissingCapture(), bridge: options =>
        { options.MaximumSources = limit; options.MaximumPreparedVoices = limit; options.MaximumAssets = limit == 1 ? 1 : 64; options.MaximumEncodedBytes = limit == 1 ? 1 : 1073741824; });
        var audio = app.Services.Resolve<IAudioService>()!;
        Assert.False(audio.GetSnapshot().IsRunning); Assert.Equal(0, audio.GetSnapshot().Sessions);
        Assert.NotNull(app.Services.Resolve<IFactory<AudioSceneSubsystem, SceneSubsystemArguments>>());
        await app.StartAsync(); Assert.True(audio.GetSnapshot().IsRunning); Assert.Equal(0, audio.GetSnapshot().Sessions);
        Assert.Equal(0, audio.GetSnapshot().Voices); Assert.Equal(0, audio.GetSnapshot().Clips);
    }

    [WindowsAudioTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task LazyFactoryAndCopiedOptions_CreateIndependentSceneBanksOnlyAfterStart(bool microsoft)
    {
        AudioBridgeOptions? mutable = null;
        await using var fixture = new AudioFixture(microsoft, bridge: options => mutable = options);
        Assert.NotNull(fixture.Subsystems); Assert.Equal(0, fixture.Audio.GetSnapshot().Sessions);
        mutable!.MaximumAssets = 0; mutable.MaximumPreparedVoices = 0;
        await fixture.Start(); Assert.Equal(0, fixture.Audio.GetSnapshot().Sessions);
        using var first = fixture.Game(); using var second = fixture.Game();
        first.LoadSceneWorld(new(AudioFixture.Document(new AudioSource(AudioFixture.SoundId, AutoPlay: true))));
        second.LoadSceneWorld(new(AudioFixture.Document(new AudioSource(AudioFixture.OtherId, AutoPlay: true))));
        Assert.NotEqual(first.ActiveScene!.World.Id, second.ActiveScene!.World.Id); Assert.Equal(2, fixture.Audio.GetSnapshot().Sessions);
        AudioFixture.Silent(fixture.Render()); first.SetPlaying(true); fixture.Render(); AudioFixture.Constant(fixture.Render(), .25f);
        second.SetPlaying(true); fixture.Render(); AudioFixture.Constant(fixture.Render(), .375f);
        first.Dispose(); fixture.Render(); AudioFixture.Constant(fixture.Render(), .125f); Assert.Equal(1, fixture.Audio.GetSnapshot().Sessions);
        second.Dispose(); Assert.Equal(0, fixture.Audio.GetSnapshot().Sessions);
    }

    [WindowsAudioTheory]
    [InlineData("assets")] [InlineData("voices")] [InlineData("bytes")]
    public async Task CopiedPreparationLimits_RejectOverBudgetCaptureBeforeNativeSessionAdmission(string budget)
    {
        await using var fixture = new AudioFixture(bridge: options =>
        { if (budget == "assets") options.MaximumAssets = 1; else if (budget == "voices") options.MaximumPreparedVoices = 2; else options.MaximumEncodedBytes = 1; });
        await fixture.Start(); using var game = fixture.Game();
        Assert.Throws<InvalidDataException>(() => game.LoadSceneWorld(new(AudioFixture.Document())));
        Assert.Null(game.ActiveScene); Assert.Equal(0, fixture.Audio.GetSnapshot().Sessions); Assert.Equal(0, fixture.Audio.GetSnapshot().Clips);
        using var write = new FileStream(fixture.PathFor(AudioFixture.SoundId), FileMode.Open, FileAccess.ReadWrite, FileShare.None); Assert.True(write.Length > 0);
    }

    [WindowsAudioTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task ConfiguredRegistration_BindsExactCommonSchemaAndAppliesExplicitOverrideBeforeCopy(bool microsoft)
    {
        await using var fixture = new AudioFixture(); AudioBridgeOptions? mutable = null;
        var profile = ConfigurationProfiles.Create(builder =>
        { builder.AddSchema(AudioBridgeConfigurationSchema.Default); builder.Override("Audio:Bridge:MaximumSources", 2); builder.Override("Audio:Bridge:MaximumPreparedVoices", 2); });
        await using var app = App.Builder(context => { if (microsoft) context.UseDI(); }).Setup(context =>
        {
            context.UseConfiguration(profile); context.UseAudio(options => options.Mode = AudioMode.Offline);
            context.UseAetherEcs().UseAetherPrimitiveEcs(); context.UseAetherSceneLoading(_ => AudioComponentRegistry.Register(PrimitiveEcsComponents.Register(new())).Build());
            context.UseConfiguredAetherKul(fixture.Content, options => { options.MaximumSources = 1; options.MaximumPreparedVoices = 3; mutable = options; });
            context.UseAetherSceneSubsystems(builder => builder.AddKulAudio()); AudioFixture.RegisterGame(context);
        }).Build();
        mutable!.MaximumSources = 0; mutable.MaximumPreparedVoices = 0; await app.StartAsync();
        using var first = app.Services.Resolve<IFactory<PrimitiveGameSession>>()!.Create()!;
        first.LoadSceneWorld(new(AudioFixture.Document(new AudioSource(AudioFixture.SoundId))));
        Assert.Equal(1, app.Services.Resolve<IAudioService>()!.GetSnapshot().Sessions);
        using var bad = app.Services.Resolve<IFactory<PrimitiveGameSession>>()!.Create()!;
        Assert.Throws<InvalidDataException>(() => bad.LoadSceneWorld(new(AudioFixture.Document(new(AudioFixture.SoundId), new(AudioFixture.SoundId)))));
        Assert.Null(bad.ActiveScene); Assert.Equal(1, app.Services.Resolve<IAudioService>()!.GetSnapshot().Sessions);
    }

    [Fact]
    public void ConfiguredRegistration_RequiresTheExactRegisteredSchema()
    {
        var profile = ConfigurationProfiles.Create(_ => { });
        Assert.Throws<ArgumentException>(() => App.Builder(_ => { }).Setup(context =>
        { context.UseConfiguration(profile); context.UseAudio(); context.UseConfiguredAetherKul(MissingCapture()); }).Build());
    }

    [WindowsAudioFact]
    public async Task Registry_RejectsUnsupportedVersionAndInvalidAssetSyntaxBeforeNativePreparation()
    {
        await using var fixture = new AudioFixture(); await fixture.Start();
        var source = AudioFixture.Document(new AudioSource(AudioFixture.SoundId)); var definition = source.Definitions.Single();
        var changed = new SceneDocument(source.Id, source.Revision, source.Entities,
            [new(definition.Id, new(definition.Changes.AddedComponents.Select(component => new ComponentData(component.Id, component.TypeId, 2, component.Properties))))],
            source.Schemas.Select(schema => schema.TypeId == AudioComponentSchemas.SourceId ? new ComponentSchema(schema.TypeId, 2, schema.Properties.Values) : schema));
        Assert.Equal(SceneWorldLoadError.UnsupportedSchemaVersion, Assert.Throws<SceneWorldLoadException>(() => fixture.Loader.Load(changed)).Error);
        Assert.Throws<SceneWorldLoadException>(() => fixture.Loader.Load(AudioFixture.Document(new AudioSource(default))));
        Assert.Equal(0, fixture.Audio.GetSnapshot().Sessions); Assert.Equal(0, fixture.Audio.GetSnapshot().Voices);
    }

    private static void Set(AudioBridgeOptions options, string field, long value)
    {
        switch (field)
        {
            case "sources": options.MaximumSources = (int)value; break;
            case "voices": options.MaximumPreparedVoices = (int)value; break;
            case "assets": options.MaximumAssets = (int)value; break;
            default: options.MaximumEncodedBytes = value; break;
        }
    }
}
