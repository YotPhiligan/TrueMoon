namespace TrueMoon.Dependencies;

public class DependenciesRegistrationContext : IDependenciesRegistrationContext
{
    private readonly List<IDependencyDescriptor> _descriptors = new();

    /// <inheritdoc />
    public void AddDescriptor(IDependencyDescriptor dependencyDescriptor)
    {
        if (_descriptors.Contains(dependencyDescriptor))
        {
            return;
        }

        _descriptors.Add(dependencyDescriptor);
    }

    /// <inheritdoc />
    public IReadOnlyList<IDependencyDescriptor> GetDescriptors() => _descriptors;

    /// <inheritdoc />
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

    /// <inheritdoc />
    public IReadOnlyList<IDependencyDescriptor> GetDescriptors(Func<IDependencyDescriptor, bool>? searchFunc)
    {
        var list = searchFunc == null 
            ? _descriptors.ToList()
            : _descriptors
                .Where(searchFunc)
                .ToList();
        return list;
    }

    /// <inheritdoc />
    public IDependencyDescriptor? GetDescriptor(Func<IDependencyDescriptor, bool> searchFunc) => _descriptors.FirstOrDefault(searchFunc);

    /// <inheritdoc />
    public IDependencyDescriptor<T>? GetDescriptor<T>(Func<IDependencyDescriptor<T>, bool>? searchFunc = default)
    {
        var result = searchFunc != null
            ? _descriptors.FirstOrDefault(t => t is IDependencyDescriptor<T> desk && searchFunc(desk)) as
                IDependencyDescriptor<T>
            : _descriptors.FirstOrDefault(t => t is IDependencyDescriptor<T>) as IDependencyDescriptor<T>;
        return result;
    }

    /// <inheritdoc />
    public bool Exist(Func<IDependencyDescriptor, bool> searchFunc) => _descriptors.Any(searchFunc);

    /// <inheritdoc />
    public bool Exist<T>(Func<IDependencyDescriptor<T>, bool>? searchFunc = default)
    {
        var result = searchFunc == null
            ? _descriptors.Any(t=> t is IDependencyDescriptor<T> || t.GetServiceType() == typeof(T))
            : _descriptors.Any(t => t is IDependencyDescriptor<T> desk && searchFunc(desk));
        return result;
    }

    /// <inheritdoc />
    public bool RemoveDependency(IDependencyDescriptor dependencyDescriptor) => _descriptors.Remove(dependencyDescriptor);
}