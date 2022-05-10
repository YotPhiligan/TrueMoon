namespace TrueMoon.Dependencies;

public interface IDependencyInjectionProvider
{
    IServiceProvider GetServiceProvider(IReadOnlyList<IDependencyDescriptor> dependencyDescriptors);
}