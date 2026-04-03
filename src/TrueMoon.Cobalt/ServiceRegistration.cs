namespace TrueMoon.Cobalt;

public static class ServiceRegistration
{
    public static ServiceRegistrationHandle Singleton<TService>(IFactoryContainer<TService> container)
    {
        var handle = new ServiceRegistrationHandle
        {
            ServiceType = typeof(TService),
            Lifetime = ServiceLifetime.Singleton,
            Resolver = () => new FactoryResolver<TService>(container, ServiceLifetime.Singleton)
        };
        return handle;
    }
    
    public static ServiceRegistrationHandle Singleton(Type serviceType, Type? implementationType = null)
    {
        var handle = new ServiceRegistrationHandle
        {
            ServiceType = serviceType,
            ImplementationType = implementationType,
            Lifetime = ServiceLifetime.Singleton
        };
        return handle;
    }
    
    public static ServiceRegistrationHandle Transient<TService>(IFactoryContainer<TService> container)
    {
        var handle = new ServiceRegistrationHandle
        {
            ServiceType = typeof(TService),
            Lifetime = ServiceLifetime.Transient,
            Resolver = () => new FactoryResolver<TService>(container, ServiceLifetime.Transient)
        };
        return handle;
    }
    
    public static ServiceRegistrationHandle Transient(Type serviceType, Type? implementationType = null)
    {
        var handle = new ServiceRegistrationHandle
        {
            ServiceType = serviceType,
            ImplementationType = implementationType,
            Lifetime = ServiceLifetime.Transient
        };
        return handle;
    }
    
    public static ServiceRegistrationHandle Instance<TService>(TService instance)
    {
        var handle = new ServiceRegistrationHandle
        {
            ServiceType = typeof(TService),
            Instance = instance,
            Lifetime = ServiceLifetime.Singleton,
            Resolver = () => new InstanceResolver<TService>(instance)
        };
        return handle;
    }
}