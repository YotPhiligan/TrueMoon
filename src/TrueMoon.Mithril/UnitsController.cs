using System.Diagnostics;
using System.Reflection;
using TrueMoon.Diagnostics;
using TrueMoon.Mithril.Units;

namespace TrueMoon.Mithril;

public class UnitsController : IUnitsController, IStartable, IStoppable, IDisposable, IAsyncDisposable
{
    private readonly IEventsSource<UnitsController> _eventsSource;
    private readonly IAppLifetime _appLifetime;

    public UnitsController( 
        IEventsSource<UnitsController> eventsSource, 
        IAppLifetime appLifetime)
    {
        _eventsSource = eventsSource;
        _appLifetime = appLifetime;

        var list = AppCreationContextExtensions.GetUnitConfigurations();
        
        _unitConfigurations = [..list.Select(t=>
        {
            var unitConfiguration = new UnitConfiguration(t.index);
            t.config?.Invoke(unitConfiguration);
            return unitConfiguration;
        })];
    }

    private readonly List<IUnitHandle> _unitHandles = [];
    private readonly IReadOnlyList<IUnitConfiguration> _unitConfigurations;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _eventsSource.Trace();
        CheckTrailingProcesses();
        foreach (var unitConfiguration in _unitConfigurations.Where(t=>t.StartupPolicy is UnitStartupPolicy.Immediate))
        {
            await SpawnUnitCoreAsync(unitConfiguration, cancellationToken: cancellationToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _eventsSource.Trace();
        foreach (var handle in _unitHandles)
        {
            await handle.StopAsync(cancellationToken);
        }
    }

    public void Dispose()
    {
        foreach (var handle in _unitHandles.OfType<IDisposable>())
        {
            handle.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var handle in _unitHandles.OfType<IAsyncDisposable>())
        {
            await handle.DisposeAsync();
        }
    }

    public async Task SpawnUnitAsync(string unitName, IReadOnlyList<object>? parameters = default, CancellationToken cancellationToken = default)
    {
        var config = _unitConfigurations.First(t => t.Name == unitName);

        await SpawnUnitCoreAsync(config, parameters, cancellationToken);
    }

    public async Task SpawnUnitAsync(int index, IReadOnlyList<object>? parameters = default, CancellationToken cancellationToken = default)
    {
        var config = _unitConfigurations.First(t => t.Index == index);

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
    
    private static void CheckTrailingProcesses()
    {
        try
        {
            var currentProcessId = Environment.ProcessId;
            var name = Assembly.GetEntryAssembly()?.GetName().Name;
            if (string.IsNullOrWhiteSpace(name)) return;
            
            var process = Process.GetProcessesByName(name);
            foreach (var p in process.Where(t => t.Id != currentProcessId).ToList())
            {
                try
                {
                    p.Kill(true);
                }
                catch (Exception)
                {
                    //
                }
            }
        }
        catch (Exception)
        {
            //
        }
    }
}