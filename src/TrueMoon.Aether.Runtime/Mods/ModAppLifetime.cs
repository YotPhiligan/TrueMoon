namespace TrueMoon.Aether.Runtime.Mods;

// Non-disposable lifecycle adapter: only the concrete ModHost registration owns disposal.
internal sealed class ModAppLifetime(ModHost host) : IStartable, IStoppable
{
    public Task StartAsync(CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return Task.CompletedTask; }
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var status = await host.StopAsync().ConfigureAwait(false);
        if (status.Error != ModLoadError.None) throw new ModOperationException(status);
    }
}
