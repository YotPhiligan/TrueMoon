using TrueMoon.Services;

namespace TrueMoon.Cobalt;

public class FactoryResolver<TService>(IFactoryContainer<TService> factory, ServiceLifetime lifetime) : IResolver<TService>, IObjectResolver
{
    private readonly Lock _lock = new();
    private TService? _instance;
    private bool _initialized;
    public TService? Resolve(IServiceResolver resolver)
    {
        if (ServiceLifetime != ServiceLifetime.Singleton) return factory.Get()(resolver);
        lock (_lock)
        {
            if (!_initialized)
            {
                _instance = factory.Get()(resolver);
                _initialized = true;
            }
            return _instance;
        }
    }
    // Runtime values may implement disposal even when TService does not.
    public bool IsServiceDisposable => true;
    public ServiceLifetime ServiceLifetime { get; } = lifetime;
    object? IObjectResolver.Resolve(IServiceResolver context) => Resolve(context);
}
