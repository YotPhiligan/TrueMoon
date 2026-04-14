using Microsoft.Extensions.DependencyInjection;
using TrueMoon.Services;

namespace TrueMoon.Extensions.DependencyInjection;

public class ServiceResolver(IServiceProvider serviceProvider) : IServiceResolver
{
    private static readonly Type ResolverType = typeof(IServiceResolver);
    public object? GetService(Type serviceType)
    {
        if (serviceType == ResolverType)
        {
            return this;
        }
        return serviceProvider.GetService(serviceType);
    }

    public T Resolve<T>()
    {
        if (typeof(T) == ResolverType)
        {
            return (T)(object)this;
        }

        return serviceProvider.GetService<T>();
    }

    public T? TryResolve<T>()
    {
        if (typeof(T) == ResolverType)
        {
            return (T)(object)this;
        }
        
        return serviceProvider.GetService<T>();
    }
}