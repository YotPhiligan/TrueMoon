using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Kul.Runtime;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Loading;
using TrueMoon.Aether.Scenes;
using A = TrueMoon.Aether.Ecs;
using EcsSystemContext = TrueMoon.Aether.Ecs.EcsSystemContext;
using EcsSystemDescription = TrueMoon.Aether.Ecs.EcsSystemDescription;
using EntityHandle = TrueMoon.Aether.Ecs.EntityHandle;
using ISystem = TrueMoon.Aether.Ecs.ISystem;

namespace TrueMoon.Aether.Kul.Tests;

internal sealed class AudioGraph : IDisposable
{
    internal SceneWorld Scene { get; }
    internal World World => Scene.World;
    internal AudioSceneSubsystem Audio { get; }
    internal EcsScheduler Scheduler { get; }
    private AudioGraph(SceneWorld scene, AudioSceneSubsystem audio, EcsScheduler scheduler)
    { Scene = scene; Audio = audio; Scheduler = scheduler; }
    internal static AudioGraph Create(AudioFixture fixture, SceneDocument document, params EcsSystemRegistration[] additional)
    {
        var scene = fixture.Loader.Load(document);
        var audio = fixture.Subsystems.Create(new(scene, AudioFixture.Step))!;
        var scheduler = EcsScheduler.Create(scene.World,
        [
            new(new EcsSystemDescription("builtin.primitive-extraction", EcsPhase.Extraction), new Factory<ISystem>(() => new Callback(_ => { }))),
            .. audio.Systems, .. additional
        ]);
        return new(scene, audio, scheduler);
    }
    internal EntityHandle Entity(int index = 0) => Scene.Entities[AudioFixture.Entity(index)];
    internal void Tick() => Scheduler.Tick(AudioFixture.Step);
    internal AudioSourceState State(int index = 0) => World.GetComponent<AudioSourceState>(Entity(index));
    internal AudioSourceState Current(int index = 0)
    {
        Xunit.Assert.True(Audio.TryGetState(Entity(index), out var state)); return state;
    }
    internal void Command(ulong sequence, AudioCommandKind kind, ulong frame = 0, int index = 0)
    {
        var entity = Entity(index); var command = new AudioPlaybackCommand(sequence, kind, frame);
        if (World.HasComponent<AudioPlaybackCommand>(entity)) World.SetComponent(entity, command); else World.AddComponent(entity, command);
    }
    public void Dispose() { Scheduler.Dispose(); Audio.Dispose(); Scene.Dispose(); }
    internal sealed class Callback(Action<EcsSystemContext> execute) : ISystem
    {
        public void Execute(EcsSystemContext context) => execute(context);
        public void Dispose() { }
    }
}
