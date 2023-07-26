using System.Text;
using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Thorium.IO;
using TrueMoon.Thorium.IO.SharedMemory;

namespace TrueMoon.Thorium;

public class ThoriumModule : IThoriumModule
{
    private readonly IEventsSource<ThoriumModule> _eventsSource;
    private ThoriumConfiguration _configuration = new ();
    public ModuleExecutionFlowOrder ExecutionFlowOrder => ModuleExecutionFlowOrder.End;
    public string Name => nameof(ThoriumModule);
    private readonly List<(Type service,Type implementation, Action<IDependenciesRegistrationContext> registrationDelegate)> _clientServices = new ();
    private readonly List<(Type service,Type implementation, Type handlerAbstraction, Type handler, Action<IDependenciesRegistrationContext> registrationDelegate)> _handlers = new ();
    private Action<IThoriumConfigurationContext>? _configurationDelegate;

    public ThoriumModule(IEventsSource<ThoriumModule> eventsSource)
    {
        _eventsSource = eventsSource;
    }

    public void Configure(IAppCreationContext context)
    {
        context.AddDependencies(t => t.Add<ISignalServerHandlerResolver, SignalServerHandlerResolver>());
        
        var configurationContext = new ThoriumConfigurationContext(context);
        if (_configurationDelegate != null)
        {
            _configurationDelegate(configurationContext);
        }
        else
        {
            configurationContext.Memory();
        }
        
        if (context.Configuration.IsProcessingUnit())
        {
            context.AddDependencies(t =>
            {
                t.Add(_configuration);
                
                foreach (var (service, implementation, registrationDelegate) in _clientServices)
                {
                    t.Add(service, implementation);
                    registrationDelegate(t);
                }
                
                foreach (var (service, implementation, handlerAbstraction, handler, registrationDelegate) in _handlers)
                {
                    t.Add(service, implementation);
                    t.Add(handlerAbstraction, handler);
                    registrationDelegate(t);
                }
            });
        }
        else
        {
            context.AddDependencies(t =>
            {
                t.Add(_configuration);
                var list = SignalServiceStorage.Shared.GetSignalHandlesRegistrations();
                foreach (var action in list)
                {
                    action(t);
                }
            });
        }

        _eventsSource.Trace("Configured");
    }

    public void Execute(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        var sb = new StringBuilder();
        if (configuration.IsProcessingUnit())
        {
            var servers = serviceProvider.ResolveAll<ISignalServer>().ToList();
            
            if (servers.Any())
            {
                sb.AppendLine("Signal servers: ");
                foreach (var server in servers)
                {
                    sb.AppendLine($"    {server.Id} ({server.GetType()})");
                }
            }
        }
        else
        {
            var handles = serviceProvider.ResolveAll<ISignalsHandle>().ToList();
            
            if (handles.Any())
            {
                sb.AppendLine("Signal Handles: ");
                foreach (var handle in handles)
                {
                    sb.AppendLine($"    {handle.Name} ({handle.GetType()})");
                }
            }
        }
        _eventsSource.Write(() => sb, "Executed");
    }

    public void SetConfiguration(ThoriumConfiguration configuration)
    {
        _configuration = configuration;
    }

    public ThoriumConfiguration GetConfiguration() => _configuration;

    public void UseService<T>()
    {
        void Registration(IDependenciesRegistrationContext context) =>
            context.Add(provider =>
            {
                var factory = provider.Resolve<ISignalClientFactory>();
                return factory.Create<T>();
            });

        _clientServices.Add((typeof(T),SignalServiceStorage.Shared.GetService<T>(), Registration));
    }

    public void ListenService<T, TService>() where TService : class, T
    {
        var handlerTypeAbstraction = typeof(ISignalServerHandler<T>);
        var handlerType = SignalServiceStorage.Shared.GetHandler<T>();
        var serviceType = typeof(T);
        var implementationType = typeof(TService);

        void Registration(IDependenciesRegistrationContext context) =>
            context.Add<ISignalServer>(provider =>
            {
                var factory = provider.Resolve<ISignalServerFactory>();
                return factory.Create<T>();
            });

        _handlers.Add((serviceType, implementationType, handlerTypeAbstraction, handlerType, Registration));
    }

    public void SetConfigurationDelegate(Action<IThoriumConfigurationContext>? action)
    {
        _configurationDelegate = action;
    }
}