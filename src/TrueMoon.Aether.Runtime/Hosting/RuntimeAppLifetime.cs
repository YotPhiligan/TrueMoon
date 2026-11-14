namespace TrueMoon.Aether.Runtime.Hosting;

internal sealed class RuntimeAppLifetime(RuntimeEngine engine) : IStartable, IStoppable
{
    public Task StartAsync(CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); engine.EnsureOpen(); return Task.CompletedTask; }
    public Task StopAsync(CancellationToken cancellationToken = default) => engine.StopAsync();
}
