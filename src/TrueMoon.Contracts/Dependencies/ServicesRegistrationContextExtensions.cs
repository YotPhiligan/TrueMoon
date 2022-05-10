namespace TrueMoon.Dependencies;

public static class ServicesRegistrationContextExtensions
{
    public static IDependenciesRegistrationContext Add<T>(this IDependenciesRegistrationContext context, T instance)
    {
        context.AddDependency(new DependencyDescriptor<T>(instance));
        return context;
    }
    
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
        if (func == null) throw new ArgumentNullException(nameof(func));
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
    
    public static IDependenciesRegistrationContext ConfigureWith<T>(this IDependenciesRegistrationContext context, Action<T> action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        
        if (context is not ICustomDiContainerAccessor accessor) return context;
        
        if (accessor.TryGetTypedContainer(out T container))
        {
            action(container);
        }

        return context;
    }
}

public interface ICustomDiContainerAccessor
{
    bool TryGetTypedContainer<T>(out T container);
}