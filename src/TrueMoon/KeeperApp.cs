using TrueMoon.Dependencies;

namespace TrueMoon;

/// <inheritdoc />
public class KeeperApp : IApp
{
    public KeeperApp(IAppParameters parameters, 
        IServiceProvider services)
    {
        Parameters = parameters;
        Services = services;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Services is IDisposable disposable)
        {
            disposable.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        switch (Services)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync();
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }

        GC.SuppressFinalize(this);
    }

    public IServiceProvider Services { get; }
    public IAppParameters Parameters { get; }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var services = Services.ResolveAll<IStartable>();

        foreach (var service in services)
        {
            await service.StartAsync(cancellationToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var services = Services.ResolveAll<IStoppable>();
        foreach (var service in services)
        {
            await service.StopAsync(cancellationToken);
        }
    }
}