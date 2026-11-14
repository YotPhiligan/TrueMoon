namespace TrueMoon.Aether.Tooling.Hosting;

// Non-disposable aliases borrow the one App-owned ToolingHost.
internal sealed class ToolingAppLifetime(ToolingHost host) : IStartable, IStoppable
{
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(host.IsClosed, host);
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken = default) => host.DisposeAsync().AsTask();
}
