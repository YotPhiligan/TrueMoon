using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Loading;
using TrueMoon.Aether.Runtime.Ecs.Motion;
using TrueMoon.Aether.Runtime.Ecs.Transforms;
using TrueMoon.Aether.Runtime.Ecs.Runtime;
using TrueMoon.Aether.Kton.Runtime;
using TrueMoon.Aether.Scenes;
using A = TrueMoon.Aether.Ecs;
using EcsSystemContext = TrueMoon.Aether.Ecs.EcsSystemContext;
using ISystem = TrueMoon.Aether.Ecs.ISystem;

namespace TrueMoon.Aether.Kton.Tests;

internal sealed class PhysicsGraph : IDisposable
{
    internal SceneWorld Scene { get; }
    internal World World => Scene.World;
    internal PhysicsSceneSubsystem Physics { get; }
    internal EcsScheduler Scheduler { get; }
    private PhysicsGraph(SceneWorld scene, PhysicsSceneSubsystem physics, EcsScheduler scheduler)
    { Scene = scene; Physics = physics; Scheduler = scheduler; }
    internal static PhysicsGraph Create(IApp app, SceneDocument document, params EcsSystemRegistration[] additional)
        => Create(app, document, null, additional);
    internal static PhysicsGraph Create(IApp app, SceneDocument document, EcsSchedulerOptions? options, params EcsSystemRegistration[] additional)
    {
        var scene = PhysicsFixture.Loader(app).Load(document);
        var physics = PhysicsFixture.Subsystems(app).Create(new(scene, PhysicsFixture.Step))!;
        var transforms = new Factory<TransformSystem, World>(world => new(world)).Create(scene.World);
        transforms.InitializeScene(scene); transforms.Update(0);
        var scheduler = EcsScheduler.Create(scene.World,
        [
            new(MovementSystem.DescribeContract(), new Factory<ISystem, World>(world => new MovementSystem(world))),
            new(TransformSystem.DescribeContract(), new Factory<ISystem, World>(_ => transforms)),
            .. physics.Systems, .. additional
        ], options);
        return new(scene, physics, scheduler);
    }
    internal void Tick() => Scheduler.Tick(PhysicsFixture.Step);
    public void Dispose() { Scheduler.Dispose(); Physics.Dispose(); Scene.Dispose(); }
    internal sealed class Callback(Action<EcsSystemContext> execute) : ISystem
    {
        public void Execute(EcsSystemContext context) => execute(context);
        public void Dispose() { }
    }
}
