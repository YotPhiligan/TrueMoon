namespace TrueMoon.Dependencies;

public interface IDependencyDescriptor
{
    Type GetServiceType();
    Type GetImplementationType();
    ServiceLifetime Lifetime { get; }
    IReadOnlyList<Type>? GetAdditionalTypes();
    object? GetInstance();
}

public interface IDependencyDescriptor<T> : IDependencyDescriptor
{
    T? Instance { get; }
    Func<IServiceProvider,T>? Factory { get; }
}

public interface IDependencyDescriptor<T,TImplementation> : IDependencyDescriptor<T> where TImplementation : T
{

}