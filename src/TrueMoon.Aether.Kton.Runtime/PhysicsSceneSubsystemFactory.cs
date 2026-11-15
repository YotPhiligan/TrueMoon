using TrueMoon.Aether.Runtime.Ecs.Runtime;
using TrueMoon.Kton;

namespace TrueMoon.Aether.Kton.Runtime;

internal sealed class PhysicsSceneSubsystemFactory(IFactory<IPhysicsWorld> worlds,
    IFactory<PhysicsBridgeSystem, PhysicsSceneSubsystem> systems, PhysicsBridgeOptions options)
    : IFactory<PhysicsSceneSubsystem, SceneSubsystemArguments>
{
    private readonly PhysicsBridgeOptions _options = options.Snapshot();
    public PhysicsSceneSubsystem Create(SceneSubsystemArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments); ArgumentNullException.ThrowIfNull(arguments.Scene);
        arguments.CancellationToken.ThrowIfCancellationRequested();
        if (arguments.FixedStep != PhysicsSceneSubsystem.FixedStep) throw new NotSupportedException("Kton PHYS5 requires the exact60Hz scene fixed step.");
        var owner = new PhysicsSceneSubsystem(arguments, _options);
        try { owner.Prepare(worlds, systems, arguments.CancellationToken); return owner; }
        catch (Exception error)
        {
            try { owner.Dispose(); }
            catch (Exception cleanup) { throw new SceneWorldPreparationException(new AggregateException(error, cleanup), owner); }
            throw;
        }
    }
}
