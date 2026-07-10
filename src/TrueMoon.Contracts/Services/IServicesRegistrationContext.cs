namespace TrueMoon.Services;

public interface IServicesRegistrationContext
{
    IServicesRegistrationContext Singleton<TService>() where TService : class;
    IServicesRegistrationContext Singleton<TService, TImplementation>()
        where TImplementation : class, TService where TService : class;
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
    
    IServicesRegistrationContext Composite<TImplementation,TService1,TService2,TService3,TService4,TService5>() 
        where TImplementation : class, TService1, TService2, TService3, TService4, TService5
        where TService1 : class 
        where TService2 : class 
        where TService3 : class
        where TService4 : class
        where TService5 : class;
    
    IServicesRegistrationContext Composite<TImplementation,TService1,TService2,TService3,TService4,TService5, TService6>() 
        where TImplementation : class, TService1, TService2, TService3, TService4, TService5, TService6
        where TService1 : class 
        where TService2 : class 
        where TService3 : class
        where TService4 : class
        where TService5 : class
        where TService6 : class;
    
    IServicesRegistrationContext Composite<TImplementation,TService1,TService2,TService3,TService4,TService5,TService6,TService7>() 
        where TImplementation : class, TService1, TService2, TService3, TService4, TService5, TService6, TService7
        where TService1 : class 
        where TService2 : class 
        where TService3 : class
        where TService4 : class
        where TService5 : class
        where TService6 : class
        where TService7 : class;
    
    IServicesRegistrationContext Composite<TImplementation,TService1,TService2,TService3,TService4,TService5,TService6,TService7,TService8>() 
        where TImplementation : class, TService1, TService2, TService3, TService4, TService5, TService6, TService7, TService8
        where TService1 : class 
        where TService2 : class 
        where TService3 : class
        where TService4 : class
        where TService5 : class
        where TService6 : class
        where TService7 : class
        where TService8 : class;

    IServicesRegistrationContext Instance<TService>(TService instance);
    
    IServicesRegistrationContext Remove<TService,TImplementation>() where TImplementation : class, TService;

    IServicesRegistrationContext RemoveAll<T>();
    
    bool Exist<TService>();
    bool Exist<TService, TImplementation>() where TImplementation : class, TService;
    bool Exist(Type serviceType);
    bool Exist(Type serviceType, Type implementationType);
}

public static class ServicesRegistrationContextExtensions
{
    public static IServicesRegistrationContext TrySingleton<TService>(this IServicesRegistrationContext context) 
        where TService : class
    {
        if (context.Exist<TService>())
        {
            return context;
        }
        
        return context.Singleton<TService>();
    }
    
    public static IServicesRegistrationContext TrySingleton<TService, TImplementation>(this IServicesRegistrationContext context) 
        where TImplementation : class, TService where TService : class
    {
        if (context.Exist<TService,TImplementation>())
        {
            return context;
        }
        
        return context.Singleton<TService,TImplementation>();
    }
    
    public static IServicesRegistrationContext TryTransient<TService>(this IServicesRegistrationContext context) 
        where TService : class
    {
        if (context.Exist<TService>())
        {
            return context;
        }
        
        return context.Transient<TService>();
    }
    
    public static IServicesRegistrationContext TryTransient<TService, TImplementation>(this IServicesRegistrationContext context) 
        where TImplementation : class, TService where TService : class
    {
        if (context.Exist<TService,TImplementation>())
        {
            return context;
        }
        
        return context.Transient<TService,TImplementation>();
    }
}