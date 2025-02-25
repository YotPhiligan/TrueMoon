namespace TrueMoon.Dependencies;

public static class DependenciesRegistrationContextExtensions
{
    public static IDependenciesRegistrationContext Add<T>(this IDependenciesRegistrationContext context, T instance)
    {
        context.AddDescriptor(new DependencyDescriptor<T>(instance));
        return context;
    }
    
    public static IDependenciesRegistrationContext Add<T>(this IDependenciesRegistrationContext context, ServiceLifetime serviceLifetime = ServiceLifetime.Singleton)
        where T : class
    {
        context.AddDescriptor(new DependencyDescriptor<T>(serviceLifetime));
        return context;
    }

    public static IDependenciesRegistrationContext Add<T, TImplementation>(this IDependenciesRegistrationContext context, ServiceLifetime serviceLifetime = ServiceLifetime.Singleton) 
        where TImplementation : class,T
    {
        context.AddDescriptor(new DependencyDescriptor<T,TImplementation>(serviceLifetime));
        return context;
    }
    
    public static IDependenciesRegistrationContext Add(this IDependenciesRegistrationContext context, Type service, Type implementation, ServiceLifetime serviceLifetime = ServiceLifetime.Singleton)
    {
        context.AddDescriptor(new DependencyDescriptor(service, implementation, serviceLifetime));
        return context;
    }

    public static IDependenciesRegistrationContext Add<T>(this IDependenciesRegistrationContext context, Func<IServiceProvider,T> func , ServiceLifetime serviceLifetime = ServiceLifetime.Singleton)
    {
        if (func == null) throw new ArgumentNullException(nameof(func));
        context.AddDescriptor(new DependencyDescriptor<T>(func,serviceLifetime));
        return context;
    }
    
    public static IDependenciesRegistrationContext Add<TImplementation>(this IDependenciesRegistrationContext context, Action<IDependencyRegistrationContext<TImplementation>> action) 
        where TImplementation : class
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        var ctx = new DependencyRegistrationContext<TImplementation>();
        action(ctx);
        var descriptor = ctx.GetDescriptor();
        context.AddDescriptor(descriptor);
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

    public static IDependenciesRegistrationContext AddSingleton<T>(this IDependenciesRegistrationContext context)
        where T : class =>
        context.Add<T>();
    
    public static IDependenciesRegistrationContext AddSingleton<T,TImplementation>(this IDependenciesRegistrationContext context)
        where TImplementation : class,T
        => context.Add<T,TImplementation>();
    
    public static IDependenciesRegistrationContext AddTransient<T>(this IDependenciesRegistrationContext context) 
        where T : class =>
        context.Add<T>(ServiceLifetime.Transient);
    
    public static IDependenciesRegistrationContext AddTransient<T,TImplementation>(this IDependenciesRegistrationContext context)
        where TImplementation : class,T
        => context.Add<T,TImplementation>(ServiceLifetime.Transient);

    public static IDependenciesRegistrationContext TryAdd<T>(this IDependenciesRegistrationContext context, ServiceLifetime serviceLifetime = ServiceLifetime.Singleton)
        where T : class
        => context.Exist<T>() 
            ? context 
            : context.Add<T>(serviceLifetime);
    
    public static IDependenciesRegistrationContext TryAdd<T,TImplementation>(this IDependenciesRegistrationContext context, ServiceLifetime serviceLifetime = ServiceLifetime.Singleton) 
        where TImplementation : class,T
        => context.Exist<T>() 
            ? context 
            : context.Add<T,TImplementation>(serviceLifetime);

    public static IDependenciesRegistrationContext TryAddTransient<T>(this IDependenciesRegistrationContext context) 
        where T : class =>
        context.TryAdd<T>(ServiceLifetime.Transient);
    
    public static IDependenciesRegistrationContext TryAddSingleton<T>(this IDependenciesRegistrationContext context) 
        where T : class =>
        context.TryAdd<T>();
    
    public static IDependenciesRegistrationContext TryAddTransient<T,TImplementation>(this IDependenciesRegistrationContext context)
        where TImplementation : class, T
        => context.TryAdd<T,TImplementation>(ServiceLifetime.Transient);
    
    public static IDependenciesRegistrationContext TryAddSingleton<T,TImplementation>(this IDependenciesRegistrationContext context)
        where TImplementation : class, T
        => context.TryAdd<T,TImplementation>(); 

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
    
    public static IDependencyRegistrationContext<T> WithAppLifetime<T>(this IDependencyRegistrationContext<T> context)
        where T : IStartable, IStoppable
    {
        return context.With<IStartable>().With<IStoppable>();
    }

    public interface IDependencyRegistrationContext<TImplementation>
    {
        IDependencyRegistrationContext<TImplementation> With<T>();
    }

    internal class DependencyRegistrationContext<TImplementation> : IDependencyRegistrationContext<TImplementation>
    {
        private readonly List<Type> _types = [];
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