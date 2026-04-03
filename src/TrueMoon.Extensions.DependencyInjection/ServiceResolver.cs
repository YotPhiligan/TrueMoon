using Microsoft.Extensions.DependencyInjection;
using TrueMoon.Services;

namespace TrueMoon.Extensions.DependencyInjection;

public class ServiceResolver(ServiceProvider serviceProvider) : IServiceResolver
{
    public object? GetService(Type serviceType)
    {
        return serviceProvider.GetService(serviceType);
    }

    public T Resolve<T>()
    {
        if (typeof(T) == typeof(IServiceResolver))
        {
            return (T)(object)this;
        }

        return serviceProvider.GetService<T>();
    }

    public T? TryResolve<T>()
    {
        throw new NotImplementedException();
    }
}