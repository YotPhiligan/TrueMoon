using TrueMoon.Dependencies;

namespace TrueMoon.Configuration;

// Lifecycle participation is borrowed; DI aliases never dispose the runtime twice.
internal sealed class ConfigurationLifetime(ConfigurationRuntime runtime) : IStartable, IStoppable
{
    public Task StartAsync(CancellationToken cancellationToken = default) { _ = runtime.Current; cancellationToken.ThrowIfCancellationRequested(); return Task.CompletedTask; }
    public Task StopAsync(CancellationToken cancellationToken = default) => runtime.StopAsync(cancellationToken);
}
