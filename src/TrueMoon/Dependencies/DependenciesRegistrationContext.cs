namespace TrueMoon.Dependencies;

public class DependenciesRegistrationContext : IDependenciesRegistrationContext
{
    private readonly List<IDependencyDescriptor> _descriptors = new();
    public void AddDependency(IDependencyDescriptor dependencyDescriptor)
    {
        if (_descriptors.Contains(dependencyDescriptor))
        {
            return;
        }

        _descriptors.Add(dependencyDescriptor);
    }

    public IReadOnlyList<IDependencyDescriptor> GetDescriptors() => _descriptors;

    public IReadOnlyList<IDependencyDescriptor<T>> GetDescriptors<T>(
        Func<IDependencyDescriptor<T>, bool>? searchFunc = default)
    {
        var list = searchFunc != null 
                ? _descriptors
                    .Where(t => t is IDependencyDescriptor<T> b && searchFunc(b))
                    .Cast<IDependencyDescriptor<T>>()
                    .ToList()
                : _descriptors
                    .Where(t => t is IDependencyDescriptor<T>)
                    .Cast<IDependencyDescriptor<T>>()
                    .ToList();
        return list;
    }

    public bool RemoveDependency(IDependencyDescriptor dependencyDescriptor) => _descriptors.Remove(dependencyDescriptor);
}