namespace TrueMoon.Dependencies;

public sealed class SimpleDependencyInjectionProvider : IDependencyInjectionProvider
{
    public IServiceProvider GetServiceProvider(IReadOnlyList<IDependencyDescriptor> dependencyDescriptors)
    {
        return new SimpleServiceProvider(dependencyDescriptors);
    }
    
    private class SimpleServiceProvider : IServiceProvider, IDisposable, IAsyncDisposable
    {
        private readonly IReadOnlyList<IDependencyDescriptor> _dependencyDescriptors;

        public SimpleServiceProvider(IReadOnlyList<IDependencyDescriptor> dependencyDescriptors)
        {
            _dependencyDescriptors = dependencyDescriptors;
        }

        public object? GetService(Type serviceType)
        {
            throw new NotImplementedException();
            // var desk = _dependencyDescriptors.FirstOrDefault(t => t.GetServiceType() == serviceType);
            //
            // if (desk == null)
            // {
            //     return default;
            // }
            //
            // var serviceDescriptor = desk switch
            // {
            //     {} when desk.TryGetInstance(out var instance) =>
            //         new ServiceDescriptor(descriptor.GetServiceType(), instance),
            //     
            //     {Lifetime:ServiceLifetime.Singleton} when descriptor.TryGetFactory(out var factory) 
            //         => new ServiceDescriptor(descriptor.GetServiceType(), factory, Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton),
            //     {Lifetime:ServiceLifetime.Transient} when descriptor.TryGetFactory(out var factory) 
            //         => new ServiceDescriptor(descriptor.GetServiceType(), factory, Microsoft.Extensions.DependencyInjection.ServiceLifetime.Transient),
            //
            //     _ => throw new InvalidOperationException()
            // };
            //
            // var implementationType = desk.GetImplementationType();
            //
            // var obj = Activator.CreateInstance(implementationType);
            //
            // return obj;
        }

        public void Dispose()
        {
            
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}