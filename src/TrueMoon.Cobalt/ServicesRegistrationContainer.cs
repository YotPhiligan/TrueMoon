using TrueMoon.Services;

namespace TrueMoon.Cobalt;

public class ServicesRegistrationContainer : IServicesRegistrationContainer, IServicesRegistrationAccessor
{
    private readonly Dictionary<Type,object> _instances = [];
    private readonly List<IFactoryContainer> _factories = [];
    
    public void RegisterInstance<TInstance>(TInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        _instances[typeof(TInstance)] = instance;
    }

    public void RegisterFactory<TService>(Func<IServiceResolver,TService> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factories.Add(new FactoryContainer<TService>(factory));
    }
    
    public TInstance? GetInstance<TInstance>() =>
        _instances.TryGetValue(typeof(TInstance), out var instance)
            ? (TInstance?)instance 
            : default;

    public IFactoryContainer<TService> GetFactory<TService>()
    {
        var factory = _factories.FirstOrDefault(c => c.Id == TypeUtils.GetTypeId<TService>());
        return (IFactoryContainer<TService>)factory;
    }
}