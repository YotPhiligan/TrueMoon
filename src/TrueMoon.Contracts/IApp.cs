namespace TrueMoon;

public interface IApp : IDisposable, IAsyncDisposable
{
    IServiceProvider Services { get; }
    IAppParameters Parameters { get; }
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}