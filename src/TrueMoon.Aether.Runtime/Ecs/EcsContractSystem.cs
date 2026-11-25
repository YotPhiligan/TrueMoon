using A = TrueMoon.Aether.Ecs;
using ISystem = TrueMoon.Aether.Ecs.ISystem;

namespace TrueMoon.Aether.Runtime.Ecs;

// Owned compatibility edge inside the existing scheduler. The inner instance is the ownership identity.
internal sealed class EcsContractSystem(ISystem system, World world) : IEcsSystem
{
    private ISystem? _system = system;
    private readonly EcsContractAccess _access = new(world);
    internal object OwnershipIdentity => _system ?? throw new ObjectDisposedException(nameof(EcsContractSystem));
    public void Execute(EcsSystemContext context)
    {
        var invocation = _access.Activate(context);
        try { (_system ?? throw new ObjectDisposedException(nameof(EcsContractSystem))).Execute(invocation); }
        finally { _access.Deactivate(); }
    }
    public void Stop() => _system?.Stop();
    public void Dispose()
    {
        if (_system == null) return;
        _system.Dispose(); _system = null; _access.Release();
    }
}
