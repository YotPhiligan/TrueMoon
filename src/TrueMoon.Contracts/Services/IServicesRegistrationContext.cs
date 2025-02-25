namespace TrueMoon.Services;

public interface IServicesRegistrationContext
{
    IServicesRegistrationContext Singleton<TService>() where TService : class;
    IServicesRegistrationContext Singleton<TService, TImplementation>() where TImplementation : class, TService;
    IServicesRegistrationContext Singleton<TService>(Func<IServiceResolver, TService> factory);
    IServicesRegistrationContext OpenSingleton<TService, TImplementation>() where TImplementation : class, TService;
    IServicesRegistrationContext Transient<TService>() where TService : class;
    IServicesRegistrationContext Transient<TService, TImplementation>() where TImplementation : class, TService;
    IServicesRegistrationContext Transient<TService>(Func<IServiceResolver, TService> factory);
    IServicesRegistrationContext OpenTransient<TService, TImplementation>() where TImplementation : class, TService;
    
    IServicesRegistrationContext Composite<TImplementation,TService1,TService2>() 
        where TImplementation : class, TService1, TService2;
    IServicesRegistrationContext Composite<TImplementation,TService1,TService2,TService3>() 
        where TImplementation : class, TService1, TService2, TService3;
    IServicesRegistrationContext Composite<TImplementation,TService1,TService2,TService3,TService4>() 
        where TImplementation : class, TService1, TService2, TService3, TService4;

    IServicesRegistrationContext Instance<TService>(TService instance);
    
    IServicesRegistrationContext Remove<TService,TImplementation>() where TImplementation : class, TService;

    IServicesRegistrationContext Replace<TService, TImplementation, TImplementationReplacement>()
        where TImplementation : class, TService
        where TImplementationReplacement : class, TService;
}