namespace TrueMoon.Dependencies;

public static class ServicesRegistrationContextExtensions
{
    public static IDependenciesRegistrationContext Add<T>(this IDependenciesRegistrationContext context, ServiceLifetime serviceLifetime = ServiceLifetime.Singleton)
    {
        context.AddDependency(new DependencyDescriptor<T>(serviceLifetime));
        return context;
    }

    public static IDependenciesRegistrationContext Add<T, TImplementation>(this IDependenciesRegistrationContext context, ServiceLifetime serviceLifetime = ServiceLifetime.Singleton) 
        where TImplementation : T
    {
        context.AddDependency(new DependencyDescriptor<T,TImplementation>(serviceLifetime));
        return context;
    }

    public static IDependenciesRegistrationContext Add<T>(this IDependenciesRegistrationContext context, Func<IServiceProvider,T> func , ServiceLifetime serviceLifetime = ServiceLifetime.Singleton)
    {
        context.AddDependency(new DependencyDescriptor<T>(func,serviceLifetime));
        return context;
    }
    
    public static IDependenciesRegistrationContext RemoveAll<T>(this IDependenciesRegistrationContext context)
    {
        var descriptors= context.GetDescriptors<T>();
        foreach (var descriptor in descriptors)
        {
            context.RemoveDependency(descriptor);
        }
        return context;
    }
}