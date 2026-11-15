using TrueMoon.Aether.Abstractions.Ecs;

namespace TrueMoon.Aether.Kton.Runtime;

// The scheduler owns this system; the subsystem owns its borrowed native dependencies.
internal sealed class PhysicsBridgeSystem(PhysicsSceneSubsystem owner) : ISystem
{
    private PhysicsSceneSubsystem? _owner = owner;
    public void Execute(EcsSystemContext context) => (_owner ?? throw new ObjectDisposedException(nameof(PhysicsBridgeSystem))).Advance(context);
    public void Stop() => _owner?.SetPlaying(false);
    public void Dispose() => _owner = null;
}
