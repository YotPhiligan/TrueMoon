namespace TrueMoon.Kton.Bepu.Hosting;

internal sealed class PhysicsLifecycle(BepuPhysicsHost host) : IStartable, IStoppable
{
    public Task StartAsync(CancellationToken cancellationToken = default) => host.StartAsync(cancellationToken);
    public Task StopAsync(CancellationToken cancellationToken = default) => host.StopAsync(cancellationToken);
}
