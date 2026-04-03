using TrueMoon.Services;

namespace TrueMoon.Cobalt;

public class FactoryResolver<TService> : IResolver<TService>, IObjectResolver
{
    private readonly IFactoryContainer<TService> _factory;
    private TService? _instance;

    public FactoryResolver(IFactoryContainer<TService> factory, ServiceLifetime lifetime)
    {
        _factory = factory;
        ServiceLifetime = lifetime;
    }
    
    public TService? Resolve(IServiceResolver serviceResolver)
    {
        if (ServiceLifetime == ServiceLifetime.Singleton)
        {
            if (_instance != null)
            {
                return _instance;
            }
            
            var func = _factory.Get();
            _instance = func(serviceResolver);
            return _instance;
        }
        var func1 = _factory.Get();
        return func1(serviceResolver);
    }

    public bool IsServiceDisposable { get; }
    public ServiceLifetime ServiceLifetime { get; }
    
    object? IObjectResolver.Resolve(IServiceResolver context) => Resolve(context);
}