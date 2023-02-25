using TrueMoon.Diagnostics;

namespace TrueMoon.Titanium.Units;

public abstract class UnitHandleBase : IUnitHandle
{
    protected readonly IUnitConfiguration Configuration;
    protected readonly IEventsSource EventsSource;
    protected Action<IUnitHandle>? OnExitAction;

    protected UnitHandleBase(IUnitConfiguration configuration, IEventsSource source)
    {
        Configuration = configuration;
        EventsSource = source;
    }
    
    public virtual Task<bool?> StartAsync(CancellationToken cancellationToken = default)
    {
        
        return Task.FromResult<bool?>(true);
    }

    public virtual Task<bool?> StopAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<bool?>(true);
    }

    public IUnitConfiguration GetConfiguration() => Configuration;

    public void OnExit(Action<IUnitHandle> action)
    {
        OnExitAction = action;
    }
}