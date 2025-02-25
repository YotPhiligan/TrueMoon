using System.Diagnostics;
using System.Reflection;
using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Modules;

namespace TrueMoon.Titanium;

public class TitaniumModule : ITitaniumModule
{
    private readonly IEventsSource<TitaniumModule> _eventsSource;
    private readonly List<IUnitConfiguration> _units = new ();

    public TitaniumModule(IEventsSource<TitaniumModule> eventsSource)
    {
        _eventsSource = eventsSource;
    }

    public ModuleExecutionFlowOrder ExecutionFlowOrder => ModuleExecutionFlowOrder.Start;
    public string Name => nameof(TitaniumModule);

    public void Configure(IAppConfigurationContext context)
    {
        if (context.Configuration.IsProcessingUnit() 
            && context.Configuration.GetProcessingUnitId() is { } id)
        {
            var unit = _units.FirstOrDefault(t => t.Index == id);
            unit?.ConfigurationDelegate(context);

            if (context.Configuration.GetProcessingUnitParentId() is {} p)
            {
                context.AddDependencies(t => t.Add<UnitParentProcessEventsHandler>(d=>d.With<IStartable>()));
            }
            return;
        }
        
        var s1 = context.Configuration.Get<string>("-ud",ConfigurationSectionNames.CommandLineArguments);
        if (s1 == "1")
        {
            _eventsSource.Write(()=>"ud mode","Configured");
            return;
        }
        
        context.AddDependencies(t => t
            .Add<UnitsController>(s=>s
                .With<IUnitsController>()
                .WithAppLifetime()
            )
            .Add(new UnitConfigurationStorage(_units))
        );
        
        _eventsSource.Trace("Configured");
    }

    public void Execute(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        if (!configuration.IsProcessingUnit())
        {
            CheckTrailingProcesses();
        }
        _eventsSource.Trace("Executed");
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

    public void AddUnitConfiguration(Action<IAppConfigurationContext> action, Action<IUnitConfiguration>? configureAction = default)
    {
        var unitConfiguration = new UnitConfiguration(_units.Count+1, action);
        configureAction?.Invoke(unitConfiguration);
        
        CheckUnitConfiguration(unitConfiguration);

        _units.Add(unitConfiguration);
    }

    private static void CheckUnitConfiguration(UnitConfiguration unitConfiguration)
    {
        unitConfiguration.HostingPolicy ??= UnitHostingPolicy.ChildProcess;
        unitConfiguration.LifetimePolicy ??= UnitLifetimePolicy.App;
        unitConfiguration.RestartPolicy ??= UnitRestartPolicy.OnCrash;
        unitConfiguration.StartupPolicy ??= UnitStartupPolicy.Immediate;
    }
}