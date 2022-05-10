using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrueMoon.Dependencies;
using ServiceLifetime = TrueMoon.Dependencies.ServiceLifetime;

namespace TrueMoon.Extensions.DependencyInjection;

public class DependencyInjectionProvider : IDependencyInjectionProvider
{
    public IServiceProvider GetServiceProvider(IReadOnlyList<IDependencyDescriptor> dependencyDescriptors)
    {
        var serviceCollection = new ServiceCollection();
        
        foreach (var descriptor in dependencyDescriptors)
        {
            // TODO
            var serviceDescriptor = descriptor switch
            {
                {Lifetime:ServiceLifetime.Singleton} => new ServiceDescriptor(descriptor.GetServiceType(), descriptor.GetImplementationType(), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton),
                {Lifetime:ServiceLifetime.Transient} => new ServiceDescriptor(descriptor.GetServiceType(), descriptor.GetImplementationType(), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Transient),

            };
            serviceCollection.Add(serviceDescriptor);
        }

        var serviceProvider = serviceCollection.BuildServiceProvider(new ServiceProviderOptions{ValidateOnBuild = true});

        return serviceProvider;
    }
}