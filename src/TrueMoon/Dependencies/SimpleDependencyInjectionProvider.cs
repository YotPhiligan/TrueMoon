namespace TrueMoon.Dependencies;

public sealed class SimpleDependencyInjectionProvider : IDependencyInjectionProvider
{
    public IServiceProvider GetServiceProvider(IReadOnlyList<IDependencyDescriptor> dependencyDescriptors)
    {
        throw new NotImplementedException();
    }
}