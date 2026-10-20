namespace TrueMoon.Aether.Hosting;

// Non-disposable aliases: only AetherHost is tracked as the resource owner by either provider.
internal sealed class AetherAppLifetime(AetherHost host) : IStartable, IStoppable
{
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        host.EnsureOpen();
        return Task.CompletedTask;
    }

    // Cancellation of the caller's wait must not abandon owned processes/readers.
    public Task StopAsync(CancellationToken cancellationToken = default) => host.DisposeAsync().AsTask();
}
