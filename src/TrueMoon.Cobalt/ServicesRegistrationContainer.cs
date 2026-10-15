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

    public void RegisterSingleton(Type service, Type implementation) => RegisterType(service, implementation, ServiceLifetime.Singleton);

    public void RegisterSingleton(Type service) => RegisterType(service, service, ServiceLifetime.Singleton);

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
        => RegisterType(service, implementation, ServiceLifetime.Transient);

    public void RegisterTransient(Type service) => RegisterType(service, service, ServiceLifetime.Transient);

    private void RegisterType(Type service, Type implementation, ServiceLifetime lifetime)
    {
        ServiceRegistration.ValidateTypes(service, implementation, nameof(service), nameof(implementation));
        Add(ServiceRegistration.CreateTypeHandle(service, implementation, lifetime));
    }

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

    internal bool Contains(Type service, Type? implementation = null)
    {
        lock (_lock)
            return _registrationHandles.Any(handle => handle.ServiceType == service &&
                (implementation == null || handle.ImplementationType == implementation));
    }

    /// <summary>Returns a detached, read-only snapshot of metadata; service instances and factory delegates retain their identity.</summary>
    public IReadOnlyList<ServiceRegistrationHandle> GetHandles()
    {
        lock (_lock)
            return Array.AsReadOnly(_registrationHandles.Select(handle => new ServiceRegistrationHandle
            {
                ServiceType = handle.ServiceType,
                ImplementationType = handle.ImplementationType,
                Lifetime = handle.Lifetime,
                Instance = handle.Instance,
                Resolver = handle.Resolver,
                Key = handle.Key
            }).ToArray());
    }
}