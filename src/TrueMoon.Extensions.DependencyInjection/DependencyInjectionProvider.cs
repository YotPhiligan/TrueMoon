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
                {} when descriptor.TryGetInstance(out var instance) =>
                    new ServiceDescriptor(descriptor.GetServiceType(), instance),
                
                {Lifetime:ServiceLifetime.Singleton} when descriptor.TryGetFactory(out var factory) 
                    => new ServiceDescriptor(descriptor.GetServiceType(), factory, Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton),
                {Lifetime:ServiceLifetime.Transient} when descriptor.TryGetFactory(out var factory) 
                    => new ServiceDescriptor(descriptor.GetServiceType(), factory, Microsoft.Extensions.DependencyInjection.ServiceLifetime.Transient),
                
                {Lifetime:ServiceLifetime.Singleton} => new ServiceDescriptor(descriptor.GetServiceType(), descriptor.GetImplementationType(), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton),
                {Lifetime:ServiceLifetime.Transient} => new ServiceDescriptor(descriptor.GetServiceType(), descriptor.GetImplementationType(), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Transient),

                _ => throw new InvalidOperationException()
            };
            serviceCollection.Add(serviceDescriptor);

            var additionalTypes = descriptor.GetAdditionalTypes();
            if (additionalTypes == null) continue;
            
            foreach (var additionalType in additionalTypes)
            {
                var s = new ServiceDescriptor(additionalType, descriptor.GetImplementationType(),
                    Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton);
                serviceCollection.Add(s);
            }
        }

        var serviceProvider = serviceCollection.BuildServiceProvider(new ServiceProviderOptions{ValidateOnBuild = true});

        return serviceProvider;
    }
}