using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;

namespace TrueMoon;

/// <inheritdoc />
public class DefaultApp : IApp
{
    private readonly IEventsSource<DefaultApp> _eventsSource;

    public DefaultApp(IEventsSource<DefaultApp> eventsSource, IConfiguration parameters, 
        IServiceProvider services)
    {
        _eventsSource = eventsSource;
        Configuration = parameters;
        Services = services;
        
        Console.CancelKeyPress += (_, _) =>
        {
            Services.Resolve<IAppLifetime>()?.Cancel();
        };
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
        _eventsSource.Trace("Starting");
        var services = Services.ResolveAll<IStartable>();

        foreach (var service in services)
        {
            await service.StartAsync(cancellationToken);
        }
        
        _eventsSource.Trace("Started");
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _eventsSource.Trace("Stopping");
        var services = Services.ResolveAll<IStoppable>();
        foreach (var service in services)
        {
            try
            {
                await service.StopAsync(cancellationToken);
            }
            catch (Exception e)
            {
                _eventsSource.Exception(e);
            }
        }
        
        _eventsSource.Trace("Stopped");
    }
}