using TrueMoon.Services;

namespace TrueMoon.Cobalt;

public class InstanceResolver<TService> : IResolver<TService>, IObjectResolver
{
    private readonly TService _instance;

    public InstanceResolver(TService instance)
    {
        _instance = instance;
        
        IsServiceDisposable = _instance is IDisposable;
    }
    
    public TService Resolve(IServiceResolver context) => _instance;

    public bool IsServiceDisposable { get; }
    public ServiceLifetime ServiceLifetime => ServiceLifetime.Singleton;
    
    object? IObjectResolver.Resolve(IServiceResolver context) => Resolve(context);
}