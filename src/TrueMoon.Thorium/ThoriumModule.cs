using System.Reflection;
using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;

namespace TrueMoon.Thorium;

public class ThoriumModule : IThoriumModule
{
    private readonly IEventsSource<ThoriumModule> _eventsSource;
    private ThoriumConfiguration _configuration = new ();
    public string Name => nameof(ThoriumModule);
    private readonly SignalMappingStorage _signalMappingStorage;

    public ThoriumModule(IEventsSource<ThoriumModule> eventsSource)
    {
        _eventsSource = eventsSource;
        _signalMappingStorage = new SignalMappingStorage(_eventsSource);
    }

    public void Configure(IAppCreationContext context)
    {
        var name = string.IsNullOrWhiteSpace(_configuration.Name)
            ? $"tm_{Assembly.GetEntryAssembly()?.GetName().Name}"
            : _configuration.Name;

        _configuration.Name = name;
        
        if (context.Configuration.IsProcessingUnit())
        {
            context.AddDependencies(t => t
                .Add(_configuration)
                .Add(_signalMappingStorage)
                .Add<ISignalInvoker,SignalInvoker>()
                .TryAdd<ISignalMediator,SignalMediator>()
                .Add<SignalProcessor>(s => s.With<IStartable>())
            );

        }
        else
        {
            context.AddDependencies(t => t
                .Add<StorageHandle>()
                .Add(_configuration)
            );
        }
        
        _eventsSource.Trace("Configured");
    }

    public void Execute(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        if (configuration.IsProcessingUnit())
        {
            _signalMappingStorage.Initialize();
            return;
        }
        
        var handle = serviceProvider.Resolve<StorageHandle>();
        var details = handle.GetDescriptor();

        _eventsSource.Write(() => details, "Executed");
    }

    public void SetConfiguration(ThoriumConfiguration configuration)
    {
        _configuration = configuration;
    }

    public ThoriumConfiguration GetConfiguration() => _configuration;

    public void ListenMessage<TMessage>()
    {
        _signalMappingStorage.Register<TMessage>();
    }
}