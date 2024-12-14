using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrueMoon.Dependencies;
using ServiceLifetime = TrueMoon.Dependencies.ServiceLifetime;
using msServiceLifetime = Microsoft.Extensions.DependencyInjection.ServiceLifetime;

namespace TrueMoon.Extensions.DependencyInjection;

/// <inheritdoc />
public class DependencyInjectionProvider : IDependencyInjectionProvider
{
    public IServiceProvider GetServiceProvider(IReadOnlyList<IDependencyDescriptor> dependencyDescriptors)
    {
        var serviceCollection = new ServiceCollection();
        
        foreach (var descriptor in dependencyDescriptors)
        {
            // TODO
            var serviceType = descriptor.GetServiceType();
            var implementationType = descriptor.GetImplementationType();

            var instance = descriptor.GetInstance();
            var factory = descriptor.GetFactory();
            
            var serviceDescriptor = descriptor switch
            {
                not null when instance != null =>
                    new ServiceDescriptor(serviceType, instance),
                
                {Lifetime:ServiceLifetime.Singleton} when factory != null
                    => new ServiceDescriptor(serviceType, factory, msServiceLifetime.Singleton),
                {Lifetime:ServiceLifetime.Transient} when factory != null
                    => new ServiceDescriptor(serviceType, factory, msServiceLifetime.Transient),
                
                {Lifetime:ServiceLifetime.Singleton} => new ServiceDescriptor(serviceType, implementationType, msServiceLifetime.Singleton),
                {Lifetime:ServiceLifetime.Transient} => new ServiceDescriptor(serviceType, implementationType, msServiceLifetime.Transient),

                _ => throw new InvalidOperationException()
            };
            serviceCollection.Add(serviceDescriptor);

            var additionalTypes = descriptor.GetAdditionalTypes();
            if (additionalTypes == null) continue;

            var lf = descriptor.Lifetime switch
            {
                ServiceLifetime.Singleton => msServiceLifetime.Singleton,
                ServiceLifetime.Transient => msServiceLifetime.Transient,
                _ => msServiceLifetime.Singleton
            };
            
            foreach (var additionalType in additionalTypes)
            {
                var s = instance != null 
                    ? new ServiceDescriptor(additionalType, instance) 
                    : new ServiceDescriptor(additionalType, p => p.GetService(serviceType)!, lf);
                serviceCollection.Add(s);
            }
        }

        var serviceProvider = serviceCollection.BuildServiceProvider(Debugger.IsAttached);

        return serviceProvider;
    }
}