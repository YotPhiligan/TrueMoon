namespace TrueMoon.Kul.Hosting;

// Same non-disposable object is registered for both interfaces, allowing App startup rollback and deduplication.
internal sealed class AudioLifecycle(AudioHost host) : IStartable, IStoppable
{
    public Task StartAsync(CancellationToken cancellationToken = default) => host.StartAsync(cancellationToken);
    public Task StopAsync(CancellationToken cancellationToken = default) => host.StopAsync(cancellationToken);
}
