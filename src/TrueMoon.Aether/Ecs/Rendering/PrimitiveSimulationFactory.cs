using TrueMoon.Aether.Ecs.Motion;
using TrueMoon.Aether.Ecs.Runtime;
using TrueMoon.Aether.Ecs.Transforms;

namespace TrueMoon.Aether.Ecs.Rendering;

internal sealed class PrimitiveSimulationFactory(IFactory<TransformSystem, World> transforms,
    IFactory<MovementSystem, World> movement, IFactory<PrimitiveExtractionSystem, PrimitiveExtractionArguments> extraction,
    PrimitiveSimulationOptions options) : IFactory<PrimitiveSimulation, PrimitiveSimulationRequest>
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
            EcsScheduler scheduler;
            try
            {
                scheduler = EcsScheduler.Create(scene.World,
                [
                    new(MovementSystem.Describe(), new Factory<IEcsSystem, World>(world => owner.Movement = movement.Create(world))),
                    new(TransformSystem.Describe(), new Factory<IEcsSystem, World>(world => owner.Transforms = transforms.Create(world))),
                    new(PrimitiveExtractionSystem.Describe(), new Factory<IEcsSystem, World>(world => owner.Extraction =
                        extraction.Create(new(world, scene.Scene.Id, scene.Scene.Revision, _options))))
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
