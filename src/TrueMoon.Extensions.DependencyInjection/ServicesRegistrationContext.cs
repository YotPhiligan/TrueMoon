using Microsoft.Extensions.DependencyInjection;
using TrueMoon.Services;

namespace TrueMoon.Extensions.DependencyInjection;

public class ServicesRegistrationContext : IServicesRegistrationContext
{
    private readonly ServiceCollection _serviceCollection;

    public ServicesRegistrationContext()
    {
        _serviceCollection = new ServiceCollection();
    }
    
    public IServicesRegistrationContext Singleton<TService>() where TService : class
    {
        _serviceCollection.AddSingleton<TService>();
        return this;
    }

    public IServicesRegistrationContext Singleton<TService, TImplementation>() 
        where TImplementation : class, 
        TService where TService : class
    {
        _serviceCollection.AddSingleton<TService,TImplementation>();
        return this;
    }

    public IServicesRegistrationContext Singleton<TService>(Func<IServiceResolver, TService> factory) 
        where TService : class
    {
        _serviceCollection.AddSingleton<TService>(s => factory(s.GetService<IServiceResolver>()));
        return this;
    }

    public IServicesRegistrationContext Singleton(Type service, Type implementation)
    {
        _serviceCollection.AddSingleton(service, implementation);
        return this;
    }

    public IServicesRegistrationContext Transient<TService>() where TService : class
    {
        _serviceCollection.AddTransient<TService>();
        return this;
    }

    public IServicesRegistrationContext Transient<TService, TImplementation>() 
        where TImplementation : class, 
        TService where TService : class
    {
        _serviceCollection.AddTransient<TService,TImplementation>();
        return this;
    }

    public IServicesRegistrationContext Transient<TService>(Func<IServiceResolver, TService> factory) 
        where TService : class
    {
        _serviceCollection.AddTransient<TService>(s => factory(s.GetService<IServiceResolver>()));
        return this;
    }

    public IServicesRegistrationContext Transient(Type service, Type implementation)
    {
        _serviceCollection.AddTransient(service, implementation);
        return this;
    }

    public IServicesRegistrationContext Composite<TImplementation, TService1, TService2>()
        where TImplementation : class, TService1, TService2 
        where TService1 : class 
        where TService2 : class
    {
        _serviceCollection.AddSingleton<TImplementation>();
        _serviceCollection.AddSingleton<TService1>(s=> s.GetService<TImplementation>());
        _serviceCollection.AddSingleton<TService2>(s=> s.GetService<TImplementation>());
        return this;
    }

    public IServicesRegistrationContext Composite<TImplementation, TService1, TService2, TService3>() 
        where TImplementation : class, TService1, TService2, TService3 
        where TService1 : class 
        where TService2 : class 
        where TService3 : class
    {
        _serviceCollection.AddSingleton<TImplementation>();
        _serviceCollection.AddSingleton<TService1>(s=> s.GetService<TImplementation>());
        _serviceCollection.AddSingleton<TService2>(s=> s.GetService<TImplementation>());
        _serviceCollection.AddSingleton<TService3>(s=> s.GetService<TImplementation>());
        return this;
    }

    public IServicesRegistrationContext Composite<TImplementation, TService1, TService2, TService3, TService4>()
        where TImplementation : class, TService1, TService2, TService3, TService4
        where TService1 : class 
        where TService2 : class 
        where TService3 : class
        where TService4 : class
    {
        _serviceCollection.AddSingleton<TImplementation>();
        _serviceCollection.AddSingleton<TService1>(s=> s.GetService<TImplementation>());
        _serviceCollection.AddSingleton<TService2>(s=> s.GetService<TImplementation>());
        _serviceCollection.AddSingleton<TService3>(s=> s.GetService<TImplementation>());
        _serviceCollection.AddSingleton<TService4>(s=> s.GetService<TImplementation>());
        return this;
    }

    public IServicesRegistrationContext Instance<TService>(TService instance)
    {
        _serviceCollection.AddSingleton(typeof(TService), instance);
        return this;
    }

    public IServicesRegistrationContext Remove<TService, TImplementation>() where TImplementation : class, TService
    {
        var desk = _serviceCollection.FirstOrDefault(t =>
            t.ServiceType == typeof(TService) && t.ImplementationType == typeof(TImplementation));
        
        if (desk != null)
        {
            _serviceCollection.Remove(desk);
        }
        
        return this;
    }

    public IServicesRegistrationContext Replace<TService, TImplementation, TImplementationReplacement>() 
        where TImplementation : class, 
        TService where TImplementationReplacement : class, 
        TService where TService : class
    {
        Remove<TService, TImplementation>();
        _serviceCollection.AddSingleton<TService,TImplementationReplacement>();
        return this;
    }
    
    public IServiceCollection GetServiceCollection() => _serviceCollection;
}