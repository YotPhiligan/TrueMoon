namespace TrueMoon.Dependencies;

public record DependencyDescriptor<T> : IDependencyDescriptor<T>
{
    private readonly IReadOnlyList<Type>? _additionalTypes;

    public DependencyDescriptor(T? instance)
    {
        Instance = instance;
    }
    
    public DependencyDescriptor(Func<IServiceProvider,T>? factory, ServiceLifetime lifetime = ServiceLifetime.Singleton) : this(lifetime)
    {
        Factory = factory;
    }

    public DependencyDescriptor(ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        Lifetime = lifetime;
    }
    
    public DependencyDescriptor(IReadOnlyList<Type> additionalTypes, ServiceLifetime lifetime = ServiceLifetime.Singleton) : this(lifetime)
    {
        _additionalTypes = additionalTypes;
    }
    
    public T? Instance { get; }
    public Func<IServiceProvider,T>? Factory { get; }
    public Type GetServiceType() => typeof(T);

    public virtual Type GetImplementationType() => GetServiceType();

    public ServiceLifetime Lifetime { get; }
    public IReadOnlyList<Type>? GetAdditionalTypes() => _additionalTypes;
}

public record DependencyDescriptor<T,TImplementation> : DependencyDescriptor<T>, IDependencyDescriptor<T,TImplementation> where TImplementation : T
{
    public DependencyDescriptor(ServiceLifetime lifetime = ServiceLifetime.Singleton) : base(lifetime) {}

    public override Type GetImplementationType() => typeof(TImplementation);
}