using TrueMoon.Services;

namespace TrueMoon.Cobalt;

public class ServicesRegistrationContainer : IServicesRegistrationContainer, IServicesRegistrationAccessor
{
    private readonly List<ServiceRegistrationHandle> _registrationHandles = [];
    private readonly Lock _lock = new ();

    private void Add(ServiceRegistrationHandle handle)
    {
        lock (_lock)
        {
            _registrationHandles.Add(handle);
        }
    }
    
    public void RegisterInstance<TInstance>(TInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        Add(ServiceRegistration.Instance(instance));
    }

    public void RegisterSingleton<TService>(Func<IServiceResolver, TService> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        var container = new FactoryContainer<TService>(factory);
        Add(ServiceRegistration.Singleton(container));
    }

    public void RegisterSingleton<TService>() => RegisterSingleton(typeof(TService));

    public void RegisterSingleton<TService, TImplementation>() where TImplementation : class, TService 
        => RegisterSingleton(typeof(TService), typeof(TImplementation));

    public void RegisterSingleton(Type service, Type implementation) => Add(ServiceRegistration.Singleton(service, implementation));

    public void RegisterSingleton(Type service) => Add(ServiceRegistration.Singleton(service));

    public void RegisterTransient<TService>(Func<IServiceResolver, TService> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        var container = new FactoryContainer<TService>(factory);
        Add(ServiceRegistration.Transient(container));
    }

    public void RegisterTransient<TService>() 
        => RegisterTransient(typeof(TService));

    public void RegisterTransient<TService, TImplementation>() where TImplementation : class, TService 
        => RegisterTransient(typeof(TService), typeof(TImplementation));

    public void RegisterTransient(Type service, Type implementation) 
        => Add(ServiceRegistration.Transient(service, implementation));

    public void RegisterTransient(Type service) => Add(ServiceRegistration.Transient(service));

    public void RemoveRegistration<TService, TImplementation>() 
        where TImplementation : class, TService
    {
        lock (_lock)
        {
            var handle = _registrationHandles.FirstOrDefault(t =>
                t.ServiceType == typeof(TService) && t.ImplementationType == typeof(TImplementation));

            if (handle != null)
            {
                _registrationHandles.Remove(handle);
            }
        }
    }

    public void RemoveAllRegistration<TService>()
    {
        lock (_lock)
        {
            _registrationHandles.RemoveAll(t=>t.ServiceType == typeof(TService));
        }
    }

    public IReadOnlyList<ServiceRegistrationHandle> GetHandles() => _registrationHandles;
}