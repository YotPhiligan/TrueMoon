namespace TrueMoon.Dependencies;

public interface IDependenciesRegistrationContext
{
    void AddDependency(IDependencyDescriptor dependencyDescriptor);
    IReadOnlyList<IDependencyDescriptor> GetDescriptors();
    IReadOnlyList<IDependencyDescriptor<T>> GetDescriptors<T>(Func<IDependencyDescriptor<T>, bool>? searchFunc = default);
    bool RemoveDependency(IDependencyDescriptor dependencyDescriptor);
}