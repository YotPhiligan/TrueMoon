using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Motion;
using TrueMoon.Aether.Runtime.Ecs.Runtime;
using TrueMoon.Aether.Runtime.Ecs.Transforms;

namespace TrueMoon.Aether.Runtime.Ecs.Rendering;

internal sealed class PrimitiveSimulationFactory(IFactory<TransformSystem, World> transforms,
    IFactory<MovementSystem, World> movement, IFactory<PrimitiveExtractionSystem, PrimitiveExtractionArguments> extraction,
    PrimitiveSimulationOptions options, IFactory<SceneSubsystemSession, SceneSubsystemArguments>? subsystems = null) : IFactory<PrimitiveSimulation, PrimitiveSimulationRequest>
{
    private readonly PrimitiveSimulationOptions _options = options.Snapshot();
    public PrimitiveSimulation Create(PrimitiveSimulationRequest arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments); ArgumentNullException.ThrowIfNull(arguments.Scene);
        var token = arguments.CancellationToken; token.ThrowIfCancellationRequested();
        var scene = arguments.Scene;
        if (!scene.IsReady || scene.IsDisposed) throw new InvalidOperationException("A healthy private scene is required.");
        var owner = new PrimitiveSimulation(scene.World, _options.FixedStep);
        try
        {
            if (subsystems != null)
            {
                try
                {
                    owner.Subsystems = subsystems.Create(new(scene, _options.FixedStep, token)) ?? throw new InvalidOperationException("Subsystem session factory returned null.");
                    owner.AttachSubsystems(owner.Subsystems);
                }
                catch (SceneWorldPreparationException error) { owner.AttachSubsystems(error.CleanupOwner); throw; }
            }
            EcsScheduler scheduler;
            try
            {
                scheduler = EcsScheduler.Create(scene.World,
                [
                    new(MovementSystem.DescribeContract(), new Factory<ISystem, World>(world => owner.Movement = movement.Create(world))),
                    new(TransformSystem.DescribeContract(), new Factory<ISystem, World>(world => owner.Transforms = transforms.Create(world))),
                    new(PrimitiveExtractionSystem.DescribeContract(), new Factory<ISystem, World>(world => owner.Extraction =
                        extraction.Create(new(world, scene.Scene.Id, scene.Scene.Revision, _options)))),
                    .. owner.Subsystems?.Systems ?? []
                ]);
            }
            catch (EcsSchedulerCreationException error) { owner.Attach(error.CleanupOwner); throw; }
            owner.Attach(scheduler); owner.Prime(scene, token); return owner;
        }
        catch (Exception original)
        {
            try { owner.Dispose(); }
            catch (Exception cleanup) { throw new SceneWorldPreparationException(new AggregateException(original, cleanup), owner); }
            throw;
        }
    }
}
