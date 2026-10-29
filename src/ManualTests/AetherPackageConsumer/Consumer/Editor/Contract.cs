using TrueMoon;

namespace PackageEditor;

// A consumer-owned contract makes delivery of the actual Enerit analyzer observable.
public interface IPackageEcho
{
    Task<string> EchoAsync(string value, CancellationToken cancellationToken = default);
}

internal static class GeneratedContract
{
    internal static void Register() => UseInvocationService<IPackageEcho>();
    private static void UseInvocationService<T>() { }
}

public sealed class PackageDependency
{
    public Guid Identity { get; } = Guid.NewGuid();
}

public sealed class PackageService(PackageDependency dependency)
{
    public PackageDependency Dependency { get; } = dependency;
}
