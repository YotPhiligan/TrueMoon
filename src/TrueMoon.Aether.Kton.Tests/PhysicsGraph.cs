using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Loading;
using TrueMoon.Aether.Runtime.Ecs.Motion;
using TrueMoon.Aether.Runtime.Ecs.Transforms;
using TrueMoon.Aether.Runtime.Ecs.Runtime;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Kton.Runtime;
using A = TrueMoon.Aether.Abstractions.Ecs;

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
            new(MovementSystem.DescribeContract(), new Factory<A.ISystem, World>(world => new MovementSystem(world))),
            new(TransformSystem.DescribeContract(), new Factory<A.ISystem, World>(_ => transforms)),
            .. physics.Systems, .. additional
        ], options);
        return new(scene, physics, scheduler);
    }
    internal void Tick() => Scheduler.Tick(PhysicsFixture.Step);
    public void Dispose() { Scheduler.Dispose(); Physics.Dispose(); Scene.Dispose(); }
    internal sealed class Callback(Action<A.EcsSystemContext> execute) : A.ISystem
    {
        public void Execute(A.EcsSystemContext context) => execute(context);
        public void Dispose() { }
    }
}
