namespace TrueMoon.Nom.Hosting;

// A single non-disposable instance allows DefaultApp to deduplicate start/stop aliases by reference.
internal sealed class NomLifecycle(NomStoreHost host) : IStartable, IStoppable
{
    public Task StartAsync(CancellationToken cancellationToken = default) => host.StartAsync(cancellationToken);
    public Task StopAsync(CancellationToken cancellationToken = default) => host.StopAsync(cancellationToken);
}
