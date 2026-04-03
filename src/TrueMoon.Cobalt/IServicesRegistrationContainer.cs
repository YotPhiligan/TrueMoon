using TrueMoon.Services;

namespace TrueMoon.Cobalt;

public interface IServicesRegistrationContainer
{
    void RegisterInstance<TInstance>(TInstance instance);
    void RegisterSingleton<TService>(Func<IServiceResolver, TService> factory);
    void RegisterSingleton<TService>();
    void RegisterSingleton<TService,TImplementation>() where TImplementation : class, TService;
    void RegisterSingleton(Type service, Type implementation);
    void RegisterSingleton(Type service);
    void RegisterTransient<TService>(Func<IServiceResolver, TService> factory);
    void RegisterTransient<TService>();
    void RegisterTransient<TService,TImplementation>() where TImplementation : class, TService;
    void RegisterTransient(Type service, Type implementation);
    void RegisterTransient(Type service);
    void RemoveRegistration<TService, TImplementation>() where TImplementation : class, TService;
    void RemoveAllRegistration<T>();
}