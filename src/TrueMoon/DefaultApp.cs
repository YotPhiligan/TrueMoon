using TrueMoon.Configuration;
using TrueMoon.Dependencies;

namespace TrueMoon;

/// <inheritdoc />
public class DefaultApp : IApp
{
    public DefaultApp(IConfiguration parameters, 
        IServiceProvider services)
    {
        Configuration = parameters;
        Services = services;
    }

    private bool _isDisposed;
    
    /// <inheritdoc />
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        
        if (Services is IDisposable disposable)
        {
            disposable.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        
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

    public string Name => Configuration.GetName() ?? "";
    public IServiceProvider Services { get; }
    public IConfiguration Configuration { get; }

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