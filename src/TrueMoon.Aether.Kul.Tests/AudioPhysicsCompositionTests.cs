using System.Numerics;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Ecs.Transforms;
using TrueMoon.Aether.Kton;
using TrueMoon.Aether.Kton.Runtime;
using TrueMoon.Aether.Kul.Runtime;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Loading;
using TrueMoon.Aether.Runtime.Ecs.Runtime;
using TrueMoon.Aether.Scenes;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Kton.Bepu;
using TrueMoon.Kton.Bepu.Hosting;
using TrueMoon.Kul;
using Xunit;

namespace TrueMoon.Aether.Kul.Tests;

public sealed class AudioPhysicsCompositionTests
{
    [WindowsAudioTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task SharedSession_ComposesKtonAndKulOnSameEntityWithIndependentTimeAndOrderedCleanup(bool microsoft)
    {
        await using var fixture = new AudioFixture(); await using var app = Build(fixture, microsoft); await app.StartAsync();
        using var game = app.Services.Resolve<IFactory<PrimitiveGameSession>>()!.Create()!;
        game.LoadSceneWorld(new(Document())); var scene = game.ActiveScene!; var entity = scene.Entities[AudioFixture.Entity(0)];
        var audio = app.Services.Resolve<IAudioService>()!; Assert.Equal(1, audio.GetSnapshot().Sessions);
        game.Tick(); Assert.InRange(scene.World.GetComponent<LocalTransform>(entity).Position.X, .0999f, .1001f);
        Assert.Equal(new AudioSourceState(1, 0, AudioSourcePhase.Paused, 0), scene.World.GetComponent<AudioSourceState>(entity));
        AudioFixture.Silent(Render(audio)); game.SetPlaying(true); Render(audio); AudioFixture.Constant(Render(audio), .25f);
        var position = scene.World.GetComponent<LocalTransform>(entity).Position; Render(audio);
        Assert.Equal(position, scene.World.GetComponent<LocalTransform>(entity).Position); // native audio callback never advances physics
        game.SetPlaying(false); game.Tick(); Assert.InRange(scene.World.GetComponent<LocalTransform>(entity).Position.X, .1999f, .2001f);
        var state = scene.World.GetComponent<AudioSourceState>(entity); Assert.Equal(2UL, state.Tick); Assert.Equal(AudioSourcePhase.Paused, state.Phase);
        Assert.True(state.CursorFrames > 0); AudioFixture.Silent(Render(audio));
        var rendered = Assert.Single(game.CaptureRenderScene().Primitives); Assert.InRange(rendered.World.Translation.X, .1999f, .2001f);
        game.Dispose(); Assert.True(scene.IsDisposed); Assert.Equal(0, audio.GetSnapshot().Sessions);
        Assert.Equal(0, app.Services.Resolve<BepuPhysicsHost>()!.Resources.ActiveResourceOwners);
    }

    [WindowsAudioFact]
    public async Task PrimitiveExtractionFailure_PreventsPendingAudioCommandAndKeepsAudioTickUnpublished()
    {
        await using var fixture = new AudioFixture(); await using var app = Build(fixture, false); await app.StartAsync();
        using var game = app.Services.Resolve<IFactory<PrimitiveGameSession>>()!.Create()!; game.LoadSceneWorld(new(Document(autoPlay: false)));
        var world = game.ActiveScene!.World; var entity = game.ActiveScene.Entities[AudioFixture.Entity(0)];
        world.AddComponent(entity, new AudioPlaybackCommand(1, AudioCommandKind.Play));
        world.SetComponent(entity, new PrimitiveAppearance(new(float.NaN, 0, 0))); game.SetPlaying(true);
        Assert.Throws<InvalidDataException>(() => game.Tick()); Assert.True(game.IsFaulted); Assert.Equal(0UL, game.TickNumber);
        Assert.Equal(new AudioSourceState(0, 0, AudioSourcePhase.Stopped, 0), world.GetComponent<AudioSourceState>(entity));
        AudioFixture.Silent(Render(app.Services.Resolve<IAudioService>()!)); Assert.Throws<InvalidOperationException>(() => game.SetPlaying(true));
    }

    private static IApp Build(AudioFixture fixture, bool microsoft) => App.Builder(context => { if (microsoft) context.UseDI(); }).Setup(context =>
    {
        context.UseAudio(options => { options.Mode = AudioMode.Offline; options.Volume = 1; });
        context.UseBepuPhysics(options => options.DefaultWorld = new() { Gravity = Vector3.Zero });
        context.UseAetherEcs().UseAetherPrimitiveEcs();
        context.UseAetherSceneLoading(_ => AudioComponentRegistry.Register(PhysicsComponentRegistry.Register(PrimitiveEcsComponents.Register(new()))).Build());
        context.UseAetherKton(); context.UseAetherKul(fixture.Content);
        context.UseAetherSceneSubsystems(builder => builder.AddKtonPhysics().AddKulAudio()); AudioFixture.RegisterGame(context);
    }).Build();

    private static float[] Render(IAudioService audio) { float[] output = new float[2048]; audio.Render(output); return output; }
    private static SceneDocument Document(bool autoPlay = true)
    {
        var source = AudioFixture.Document(new AudioSource(AudioFixture.SoundId, AutoPlay: autoPlay)); var definition = source.Definitions.Single();
        ComponentData[] added =
        [
            .. definition.Changes.AddedComponents,
            new(new(Guid.NewGuid()), BuiltInComponents.TransformId, 1),
            new(new(Guid.NewGuid()), BuiltInComponents.AppearanceId, 1),
            new(new(Guid.NewGuid()), PhysicsComponentSchemas.BodyId, 1),
            new(new(Guid.NewGuid()), PhysicsComponentSchemas.VelocityId, 1, [new("x", PropertyValue.From(6d))])
        ];
        return new(source.Id, source.Revision, source.Entities, [new(definition.Id, new(added))],
            source.Schemas.Concat([PhysicsComponentSchemas.Body, PhysicsComponentSchemas.Velocity]));
    }
}
