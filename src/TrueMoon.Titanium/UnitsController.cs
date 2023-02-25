using TrueMoon.Diagnostics;
using TrueMoon.Titanium.Units;

namespace TrueMoon.Titanium;

public class UnitsController : IUnitsController, IStartable, IStoppable, IDisposable, IAsyncDisposable
{
    private readonly UnitConfigurationStorage _storage;
    private readonly IEventsSource<UnitsController> _eventsSource;
    private readonly IAppLifetime _appLifetime;

    public UnitsController(UnitConfigurationStorage storage, 
        IEventsSource<UnitsController> eventsSource, 
        IAppLifetime appLifetime)
    {
        _storage = storage;
        _eventsSource = eventsSource;
        _appLifetime = appLifetime;
    }

    private readonly List<IUnitHandle> _unitHandles = new ();

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        foreach (var unitConfiguration in _storage.UnitConfigurations.Where(t=>t.StartupPolicy is UnitStartupPolicy.Immediate))
        {
            await SpawnUnitCoreAsync(unitConfiguration, cancellationToken: cancellationToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        foreach (var handle in _unitHandles)
        {
            await handle.StopAsync(cancellationToken);
        }
    }

    public void Dispose()
    {
        foreach (var handle in _unitHandles
                     .Where(t=>t is IDisposable)
                     .Cast<IDisposable>())
        {
            handle.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var handle in _unitHandles
                     .Where(t=>t is IAsyncDisposable)
                     .Cast<IAsyncDisposable>())
        {
            await handle.DisposeAsync();
        }
    }

    public async Task SpawnUnitAsync(string unitName, IReadOnlyList<object>? parameters = default, CancellationToken cancellationToken = default)
    {
        var config = _storage.UnitConfigurations.First(t => t.Name == unitName);

        await SpawnUnitCoreAsync(config, parameters, cancellationToken);
    }

    public async Task SpawnUnitAsync(int index, IReadOnlyList<object>? parameters = default, CancellationToken cancellationToken = default)
    {
        var config = _storage.UnitConfigurations.First(t => t.Index == index);

        await SpawnUnitCoreAsync(config, parameters, cancellationToken);
    }
    
    private async Task SpawnUnitCoreAsync(IUnitConfiguration config, IReadOnlyList<object>? parameters = default, CancellationToken cancellationToken = default)
    {
        IUnitHandle handle = config.HostingPolicy switch
        {
            UnitHostingPolicy.ChildProcess => new ChildProcessUnitHandle(config, _eventsSource),
            UnitHostingPolicy.MainProcess => new MainProcessUnitHandle(config, _eventsSource),
            UnitHostingPolicy.External => new ExternalUnitHandle(config, _eventsSource),
            _ => throw new ArgumentOutOfRangeException()
        };

        _unitHandles.Add(handle);

        handle.OnExit(OnUnitExit);
        
        await handle.StartAsync(cancellationToken);
        
        _eventsSource.Write(()=>$"{handle.GetConfiguration().Name} started");
    }

    private void OnUnitExit(IUnitHandle handle)
    {
        if (handle.GetConfiguration().IsControlAppLifetime is true)
        {
            _appLifetime.Cancel();
        }
    }
}