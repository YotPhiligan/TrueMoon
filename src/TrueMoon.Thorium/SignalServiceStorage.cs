using TrueMoon.Dependencies;
using TrueMoon.Thorium.IO;

namespace TrueMoon.Thorium;

public class SignalServiceStorage
{
    private static readonly SignalServiceStorage _storage = new ();
    public static SignalServiceStorage Shared => _storage;
    
    private readonly Dictionary<Type, Type> _services = new ();
    private readonly Dictionary<Type, Type> _handlers = new ();
    private readonly List<Action<IDependenciesRegistrationContext>> _signalsHandlesRegistrations = new ();

    public void Register<T,TService>()
        where TService : class,T
    {
        var type = typeof(T);
        var value = typeof(TService);
        if (_services.ContainsKey(type))
        {
            _services[type] = value;
        }
        else
        {
            _services.Add(type,value);
        }
        
        void Registration(IDependenciesRegistrationContext context) =>
            context.Add<ISignalsHandle>(provider =>
            {
                var factory = provider.Resolve<ISignalsHandleFactory>();
                return factory.Create<T>();
            });

        _signalsHandlesRegistrations.Add(Registration);
    }
    
    public void RegisterHandler<T,TService>()
        where TService : class,T
    {
        var type = typeof(T);
        var value = typeof(TService);
        if (_handlers.ContainsKey(type))
        {
            _handlers[type] = value;
        }
        else
        {
            _handlers.Add(type,value);
        }
    }

    internal Type GetService<T>() => _services.TryGetValue(typeof(T), out var result) ? result : throw new InvalidOperationException($"implementation for \"{typeof(T)}\" not found");
    internal Type GetHandler<T>() => _handlers.TryGetValue(typeof(ISignalServerHandler<T>), out var result) ? result : throw new InvalidOperationException($"implementation for \"{typeof(T)}\" not found");

    internal IReadOnlyList<Action<IDependenciesRegistrationContext>> GetSignalHandlesRegistrations() => _signalsHandlesRegistrations;
}