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
    
    public static IDependenciesRegistrationContext Add<TImplementation>(this IDependenciesRegistrationContext context, Action<IDependencyRegistrationContext<TImplementation>> action) 
        where TImplementation : class
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        var ctx = new DependencyRegistrationContext<TImplementation>();
        action(ctx);
        var descriptor = ctx.GetDescriptor();
        context.AddDependency(descriptor);
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

    public interface IDependencyRegistrationContext<TImplementation>
    {
        IDependencyRegistrationContext<TImplementation> With<T>();
    }

    internal class DependencyRegistrationContext<TImplementation> : IDependencyRegistrationContext<TImplementation>
    {
        private readonly List<Type> _types = new ();
        public IDependencyRegistrationContext<TImplementation> With<T>()
        {
            var type = typeof(T);
            if (_types.Contains(type))
            {
                return this;
            }
            _types.Add(type);
            return this;
        }

        public DependencyDescriptor<TImplementation> GetDescriptor()
        {
            var descriptor = new DependencyDescriptor<TImplementation>(_types);
            return descriptor;
        }
    }
}