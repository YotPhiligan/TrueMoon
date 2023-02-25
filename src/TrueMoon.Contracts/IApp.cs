using TrueMoon.Configuration;

namespace TrueMoon;

public interface IApp : IDisposable, IAsyncDisposable
{
    string Name { get; }
    IServiceProvider Services { get; }
    IConfiguration Configuration { get; }
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}