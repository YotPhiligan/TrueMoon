namespace TrueMoon.Dependencies;

public interface IDependenciesRegistrationContext
{
    void AddDescriptor(IDependencyDescriptor dependencyDescriptor);
    IReadOnlyList<IDependencyDescriptor> GetDescriptors();
    IReadOnlyList<IDependencyDescriptor<T>> GetDescriptors<T>(Func<IDependencyDescriptor<T>, bool>? searchFunc = default);
    IReadOnlyList<IDependencyDescriptor> GetDescriptors(Func<IDependencyDescriptor, bool>? searchFunc);
    IDependencyDescriptor? GetDescriptor(Func<IDependencyDescriptor, bool> searchFunc);
    IDependencyDescriptor<T>? GetDescriptor<T>(Func<IDependencyDescriptor<T>, bool>? searchFunc = default);

    bool Exist(Func<IDependencyDescriptor, bool> searchFunc);
    bool Exist<T>(Func<IDependencyDescriptor<T>, bool>? searchFunc = default);
    
    bool RemoveDependency(IDependencyDescriptor dependencyDescriptor);
}