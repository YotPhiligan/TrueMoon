namespace TrueMoon.Services;

public interface IServicesRegistrationContext
{
    IServicesRegistrationContext Singleton<TService>() where TService : class;
    IServicesRegistrationContext Singleton<TService, TImplementation>()
        where TImplementation : class, 
        TService where TService : class;
    IServicesRegistrationContext Singleton<TService>(Func<IServiceResolver, TService> factory) where TService : class;
    IServicesRegistrationContext Singleton(Type service, Type implementation);
    IServicesRegistrationContext Transient<TService>() where TService : class;
    IServicesRegistrationContext Transient<TService, TImplementation>() 
        where TImplementation : class, 
        TService where TService : class;
    IServicesRegistrationContext Transient<TService>(Func<IServiceResolver, TService> factory) where TService : class;
    IServicesRegistrationContext Transient(Type service, Type implementation);

    IServicesRegistrationContext Composite<TImplementation, TService1, TService2>()
        where TImplementation : class, TService1, TService2
        where TService1 : class
        where TService2 : class;

    IServicesRegistrationContext Composite<TImplementation, TService1, TService2, TService3>()
        where TImplementation : class, TService1, TService2, TService3
        where TService1 : class
        where TService2 : class
        where TService3 : class;
    IServicesRegistrationContext Composite<TImplementation,TService1,TService2,TService3,TService4>() 
        where TImplementation : class, TService1, TService2, TService3, TService4
        where TService1 : class 
        where TService2 : class 
        where TService3 : class
        where TService4 : class;

    IServicesRegistrationContext Instance<TService>(TService instance);
    
    IServicesRegistrationContext Remove<TService,TImplementation>() where TImplementation : class, TService;

    IServicesRegistrationContext Replace<TService, TImplementation, TImplementationReplacement>()
        where TImplementation : class,
        TService
        where TImplementationReplacement : class,
        TService
        where TService : class;
}