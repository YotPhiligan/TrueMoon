using Microsoft.Extensions.DependencyInjection;
using TrueMoon.Services;

namespace TrueMoon.Extensions.DependencyInjection;

public class ServiceResolverBuilder : IServiceResolverBuilder
{
    public IServiceResolver Build(IEnumerable<Action<IServicesRegistrationContext>> registrations)
    {
        var ctx = new ServicesRegistrationContext();
        foreach (var registration in registrations)
        {
            registration(ctx);
        }

        var serviceCollection = ctx.GetServiceCollection();
        
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var resolver = new ServiceResolver(serviceProvider);
        
        return resolver;
    }
}

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
}