namespace TrueMoon.Dependencies;

public record DependencyDescriptor : IDependencyDescriptor
{
    public readonly Type Service;
    public readonly Type Implementation;
    public readonly object? _instance;
    protected readonly IReadOnlyList<Type>? _additionalTypes;

    public DependencyDescriptor(Type service, ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        Service = service;
        Implementation = Service;
        Lifetime = lifetime;
    }
    
    public DependencyDescriptor(Type service, Type implementation, ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        Service = service;
        Implementation = implementation;
        Lifetime = lifetime;
    }

    public DependencyDescriptor(Type service, Type implementation, IReadOnlyList<Type> additionalTypes, ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        Service = service;
        Implementation = implementation;
        _additionalTypes = additionalTypes;
        Lifetime = lifetime;
    }
    
    public DependencyDescriptor(object instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        
        _instance = instance;
        Service = _instance.GetType();
        Implementation = Service;
    }
    
    public DependencyDescriptor(Type service, object instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        
        _instance = instance;
        Service = service;
        Implementation = Service;
    }
    
    public virtual Type GetServiceType() => Service;

    public virtual Type GetImplementationType() => Implementation;

    public ServiceLifetime Lifetime { get; protected set; }
    public IReadOnlyList<Type>? GetAdditionalTypes() => _additionalTypes;
    public virtual object? GetInstance() => _instance;
}

public record DependencyDescriptor<T> : DependencyDescriptor, IDependencyDescriptor<T>
{
    public DependencyDescriptor(ServiceLifetime lifetime = ServiceLifetime.Singleton) : base(typeof(T), lifetime) {}
    public DependencyDescriptor(T instance) : base(typeof(T), instance!) { }
    
    public DependencyDescriptor(Func<IServiceProvider,T>? factory, ServiceLifetime lifetime = ServiceLifetime.Singleton) 
        : base(typeof(T), lifetime)
    {
        Factory = factory;
    }

    public DependencyDescriptor(IReadOnlyList<Type> additionalTypes, ServiceLifetime lifetime = ServiceLifetime.Singleton) 
        : base(typeof(T), typeof(T), additionalTypes, lifetime) { }

    public T? Instance => GetInstance() is T? 
        ? (T?)GetInstance() 
        : default;
    
    public Func<IServiceProvider,T>? Factory { get; }
}

public record DependencyDescriptor<T,TImplementation> : DependencyDescriptor, IDependencyDescriptor<T,TImplementation> where TImplementation : T
{
    public DependencyDescriptor(ServiceLifetime lifetime = ServiceLifetime.Singleton) : base(typeof(T), typeof(TImplementation), lifetime) {}

    public DependencyDescriptor(Func<IServiceProvider,T>? factory, ServiceLifetime lifetime = ServiceLifetime.Singleton) 
        : base(typeof(T), lifetime)
    {
        Factory = factory;
    }

    public DependencyDescriptor(IReadOnlyList<Type> additionalTypes, ServiceLifetime lifetime = ServiceLifetime.Singleton) 
        : base(typeof(T), typeof(TImplementation), additionalTypes, lifetime) { }

    public T? Instance => GetInstance() is T? 
        ? (T?)GetInstance() 
        : default;
    
    public Func<IServiceProvider,T>? Factory { get; }
}