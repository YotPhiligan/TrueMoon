namespace TrueMoon.Dependencies;

public class DependenciesRegistrationContext : IDependenciesRegistrationContext
{
    private readonly List<IDependencyDescriptor> _descriptors = new();
    public void AddDependency(IDependencyDescriptor dependencyDescriptor)
    {
        throw new NotImplementedException();
    }

    public IReadOnlyList<IDependencyDescriptor> GetDescriptors()
    {
        throw new NotImplementedException();
    }

    public IReadOnlyList<IDependencyDescriptor<T>> GetDescriptors<T>(Func<IDependencyDescriptor<T>, bool>? searchFunc = default)
    {
        throw new NotImplementedException();
    }

    public bool RemoveDependency(IDependencyDescriptor dependencyDescriptor)
    {
        throw new NotImplementedException();
    }
}