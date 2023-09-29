using TrueMoon.Dependencies;
using TrueMoon.Thorium.IO;

namespace TrueMoon.Thorium;

public class InvocationServiceStorage
{
    private static readonly InvocationServiceStorage _storage = new ();
    public static InvocationServiceStorage Shared => _storage;
    
    private readonly Dictionary<Type, Type> _services = new ();
    private readonly Dictionary<Type, Type> _handlers = new ();
    //private readonly Dictionary<Type,Action<IDependenciesRegistrationContext>> _signalsHandlesRegistrations = new ();

    public void Register<T,TService>()
        where TService : class,T
    {
        var type = typeof(T);
        var value = typeof(TService);
        _services[type] = value;
        
        // if (_signalsHandlesRegistrations.ContainsKey(type))
        // {
        //     return;
        // }
        
        // void Registration(IDependenciesRegistrationContext context) =>
        //     context.Add<ISignalsHandle>(provider =>
        //     {
        //         var factory = provider.Resolve<ISignalsHandleFactory>();
        //         return factory.Create<T>();
        //     });
        //
        // _signalsHandlesRegistrations.Add(type,Registration);
    }
    
    public void RegisterHandler<T,TService>()
        where TService : class,T
    {
        var type = typeof(T);
        var value = typeof(TService);
        _handlers[type] = value;
    }

    internal Type GetService<T>() => _services.TryGetValue(typeof(T), out var result) ? result : throw new InvalidOperationException($"implementation for \"{typeof(T)}\" not found");
    internal Type GetHandler<T>() => _handlers.TryGetValue(typeof(IInvocationServerHandler<T>), out var result) ? result : throw new InvalidOperationException($"implementation for \"{typeof(T)}\" not found");

    //internal IReadOnlyList<Action<IDependenciesRegistrationContext>> GetSignalHandlesRegistrations() => _signalsHandlesRegistrations.Values.ToList();
}