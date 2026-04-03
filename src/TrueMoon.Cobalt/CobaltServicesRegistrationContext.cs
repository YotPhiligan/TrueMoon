using System.Runtime.CompilerServices;
using TrueMoon.Services;

namespace TrueMoon.Cobalt;

public class CobaltServicesRegistrationContext : IServicesRegistrationContext
{
    private readonly ServicesRegistrationContainer _container = new ();

    public ServicesRegistrationContainer GetContainer() => _container;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public IServicesRegistrationContext Singleton<TService>() where TService : class
    {
        _container.RegisterSingleton<TService>();
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public IServicesRegistrationContext Singleton<TService, TImplementation>() where TImplementation : class, TService where TService : class
    {
        _container.RegisterSingleton<TService,TImplementation>();
        return this;
    } 

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public IServicesRegistrationContext Singleton<TService>(Func<IServiceResolver, TService> factory) where TService : class
    {
        _container.RegisterSingleton(factory);
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public IServicesRegistrationContext Singleton(Type service, Type implementation)
    {
        _container.RegisterSingleton(service, implementation);
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public IServicesRegistrationContext Transient<TService>() where TService : class
    {
        _container.RegisterTransient<TService>();
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public IServicesRegistrationContext Transient<TService, TImplementation>() where TImplementation : class, TService where TService : class
    {
        _container.RegisterTransient<TService,TImplementation>();
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public IServicesRegistrationContext Transient<TService>(Func<IServiceResolver, TService> factory) where TService : class
    {
        _container.RegisterTransient(factory);
        return this;
    }
   
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public IServicesRegistrationContext Transient(Type service, Type implementation)
    {
        _container.RegisterTransient(service,implementation);
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public IServicesRegistrationContext Composite<TImplementation, TService1, TService2>() where TImplementation : class, TService1, TService2 where TService1 : class where TService2 : class
    {
        _container.RegisterSingleton<TService1, TImplementation>();
        _container.RegisterSingleton<TService2, TImplementation>();
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public IServicesRegistrationContext Composite<TImplementation, TService1, TService2, TService3>() where TImplementation : class, TService1, TService2, TService3 where TService1 : class where TService2 : class where TService3 : class
    {
        _container.RegisterSingleton<TService1, TImplementation>();
        _container.RegisterSingleton<TService2, TImplementation>();
        _container.RegisterSingleton<TService3, TImplementation>();
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public IServicesRegistrationContext Composite<TImplementation, TService1, TService2, TService3, TService4>() where TImplementation : class, TService1, TService2, TService3, TService4 where TService1 : class where TService2 : class where TService3 : class where TService4 : class
    {
        _container.RegisterSingleton<TService1, TImplementation>();
        _container.RegisterSingleton<TService2, TImplementation>();
        _container.RegisterSingleton<TService3, TImplementation>();
        _container.RegisterSingleton<TService4, TImplementation>();
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public IServicesRegistrationContext Instance<TService>(TService instance)
    {
        _container.RegisterInstance(instance);
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public IServicesRegistrationContext Remove<TService, TImplementation>() where TImplementation : class, TService
    {
        _container.RemoveRegistration<TService,TImplementation>();
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public IServicesRegistrationContext RemoveAll<T>()
    {
        _container.RemoveAllRegistration<T>();
        return this;
    }

    public bool Exist<TService>()
    {
        throw new NotImplementedException();
    }

    public bool Exist<TService, TImplementation>() where TImplementation : class, TService
    {
        throw new NotImplementedException();
    }

    public bool Exist(Type serviceType)
    {
        throw new NotImplementedException();
    }

    public bool Exist(Type serviceType, Type implementationType)
    {
        throw new NotImplementedException();
    }
}