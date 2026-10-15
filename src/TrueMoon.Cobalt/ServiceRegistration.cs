namespace TrueMoon.Cobalt;

public static class ServiceRegistration
{
    public static ServiceRegistrationHandle Singleton<TService>(IFactoryContainer<TService> container)
    {
        ArgumentNullException.ThrowIfNull(container);
        var handle = new ServiceRegistrationHandle
        {
            ServiceType = typeof(TService),
            Lifetime = ServiceLifetime.Singleton,
            Resolver = () => new FactoryResolver<TService>(container, ServiceLifetime.Singleton)
        };
        return handle;
    }
    
    public static ServiceRegistrationHandle Singleton(Type serviceType, Type? implementationType = null)
    {
        ValidateTypes(serviceType, implementationType ?? serviceType, nameof(serviceType), nameof(implementationType));
        return CreateTypeHandle(serviceType, implementationType ?? serviceType, ServiceLifetime.Singleton);
    }
    
    public static ServiceRegistrationHandle Transient<TService>(IFactoryContainer<TService> container)
    {
        ArgumentNullException.ThrowIfNull(container);
        var handle = new ServiceRegistrationHandle
        {
            ServiceType = typeof(TService),
            Lifetime = ServiceLifetime.Transient,
            Resolver = () => new FactoryResolver<TService>(container, ServiceLifetime.Transient)
        };
        return handle;
    }
    
    public static ServiceRegistrationHandle Transient(Type serviceType, Type? implementationType = null)
    {
        ValidateTypes(serviceType, implementationType ?? serviceType, nameof(serviceType), nameof(implementationType));
        return CreateTypeHandle(serviceType, implementationType ?? serviceType, ServiceLifetime.Transient);
    }
    
    public static ServiceRegistrationHandle Instance<TService>(TService instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var handle = new ServiceRegistrationHandle
        {
            ServiceType = typeof(TService),
            ImplementationType = instance.GetType(),
            Instance = instance,
            Lifetime = ServiceLifetime.Singleton,
            Resolver = () => new InstanceResolver<TService>(instance)
        };
        return handle;
    }

    internal static ServiceRegistrationHandle CreateTypeHandle(Type service, Type implementation, ServiceLifetime lifetime) => new()
    {
        ServiceType = service,
        ImplementationType = implementation,
        Lifetime = lifetime
    };

    internal static void ValidateTypes(Type service, Type implementation, string serviceParameter, string implementationParameter)
    {
        ArgumentNullException.ThrowIfNull(service, serviceParameter);
        ArgumentNullException.ThrowIfNull(implementation, implementationParameter);
        if (!ReferenceShape(service) || service.ContainsGenericParameters && !service.IsGenericTypeDefinition)
            throw new ArgumentException($"Service {service} must be a class/interface, fully closed or an open generic definition.", serviceParameter);
        if (!ReferenceShape(implementation) || implementation.IsInterface || implementation.IsAbstract ||
            implementation.ContainsGenericParameters && !implementation.IsGenericTypeDefinition)
            throw new ArgumentException($"Implementation {implementation} must be a non-abstract class, fully closed or an open generic definition.", implementationParameter);
        if (service.IsGenericTypeDefinition != implementation.IsGenericTypeDefinition)
            throw new ArgumentException($"Both {service} and {implementation} must be closed or both must be open generic definitions.", implementationParameter);
        if (!service.IsGenericTypeDefinition)
        {
            if (!service.IsAssignableFrom(implementation))
                throw new ArgumentException($"Implementation {implementation} is not assignable to service {service}.", implementationParameter);
            return;
        }
        if (GenericContainingType(service) || GenericContainingType(implementation))
            throw new ArgumentException("Open types inside generic containing types are not supported.", implementationParameter);
        var parameters = implementation.GetGenericArguments();
        if (service.GetGenericArguments().Length != parameters.Length ||
            !implementation.GetInterfaces().Concat(BaseTypes(implementation)).Any(candidate =>
                candidate.IsGenericType && candidate.GetGenericTypeDefinition() == service &&
                candidate.GetGenericArguments().SequenceEqual(parameters)))
            throw new ArgumentException($"Open mapping {service} -> {implementation} requires equal arity and positional type parameters.", implementationParameter);
    }

    private static bool ReferenceShape(Type type) => (type.IsClass || type.IsInterface) && !type.HasElementType &&
        !type.IsByRefLike && !typeof(Delegate).IsAssignableFrom(type);

    private static bool GenericContainingType(Type type)
    {
        for (var parent = type.DeclaringType; parent != null; parent = parent.DeclaringType)
            if (parent.IsGenericType) return true;
        return false;
    }

    private static IEnumerable<Type> BaseTypes(Type type)
    {
        for (var current = type; current != null; current = current.BaseType) yield return current;
    }
}