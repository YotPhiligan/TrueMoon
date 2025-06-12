using System.Runtime.CompilerServices;
using TrueMoon.Services;

namespace TrueMoon.Cobalt;

public class CobaltServicesRegistrationContext : IServicesRegistrationContext
{
    private readonly ServicesRegistrationContainer _container = new ();

    public ServicesRegistrationContainer GetContainer() => _container;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public IServicesRegistrationContext Singleton<TService>() where TService : class => this;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public IServicesRegistrationContext Singleton<TService, TImplementation>() where TImplementation : class, TService where TService : class => this;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public IServicesRegistrationContext Singleton<TService>(Func<IServiceResolver, TService> factory) where TService : class
    {
        _container.RegisterFactory(factory);
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public IServicesRegistrationContext Singleton(Type service, Type implementation) => this;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public IServicesRegistrationContext Transient<TService>() where TService : class => this;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public IServicesRegistrationContext Transient<TService, TImplementation>() where TImplementation : class, TService where TService : class => this;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public IServicesRegistrationContext Transient<TService>(Func<IServiceResolver, TService> factory) where TService : class
    {
        _container.RegisterFactory(factory);
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public IServicesRegistrationContext Transient(Type service, Type implementation) => this;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public IServicesRegistrationContext Composite<TImplementation, TService1, TService2>() where TImplementation : class, TService1, TService2 where TService1 : class where TService2 : class => this;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public IServicesRegistrationContext Composite<TImplementation, TService1, TService2, TService3>() where TImplementation : class, TService1, TService2, TService3 where TService1 : class where TService2 : class where TService3 : class => this;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public IServicesRegistrationContext Composite<TImplementation, TService1, TService2, TService3, TService4>() where TImplementation : class, TService1, TService2, TService3, TService4 where TService1 : class where TService2 : class where TService3 : class where TService4 : class => this;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public IServicesRegistrationContext Instance<TService>(TService instance)
    {
        _container.RegisterInstance<TService>(instance);
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public IServicesRegistrationContext Remove<TService, TImplementation>() where TImplementation : class, TService
    {
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public IServicesRegistrationContext Replace<TService, TImplementation, TImplementationReplacement>() where TImplementation : class, TService where TImplementationReplacement : class, TService where TService : class
    {
        return this;
    }
}