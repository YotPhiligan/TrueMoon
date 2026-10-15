namespace TrueMoon.Cobalt;

/// <summary>The reason a required service could not be resolved.</summary>
public enum ServiceResolutionFailure
{
    /// <summary>No service value is available.</summary>
    MissingService,
    /// <summary>The same resolver and closed service type are already being constructed.</summary>
    CircularDependency,
    /// <summary>The dependency chain exceeds the supported resolution depth.</summary>
    DepthExceeded,
    /// <summary>The current thread does not have sufficient stack space for more construction.</summary>
    InsufficientExecutionStack
}

/// <summary>A missing dependency or an invalid recursive resolution, including its dependency path.</summary>
public class ServiceResolvingException : Exception
{
    private const string PathKey = "TrueMoon.Cobalt.DependencyPath";
    private static readonly Lock MetadataLock = new();

    /// <summary>The service which could not be resolved.</summary>
    public Type ServiceType { get; }
    /// <summary>The failure category; user factory and constructor exceptions retain their own types.</summary>
    public ServiceResolutionFailure Failure { get; }
    /// <summary>An immutable snapshot of the requested dependency chain.</summary>
    public IReadOnlyList<Type> DependencyPath { get; private set; }

    /// <summary>Creates an error for a missing required service.</summary>
    public ServiceResolvingException(Type type) : this(type, [type]) { }

    internal ServiceResolvingException(Type type, IReadOnlyList<Type> path,
        ServiceResolutionFailure failure = ServiceResolutionFailure.MissingService)
    {
        ArgumentNullException.ThrowIfNull(type);
        ServiceType = type;
        Failure = failure;
        DependencyPath = Array.AsReadOnly(path.ToArray());
    }

    /// <inheritdoc />
    public override string Message => (Failure switch
    {
        ServiceResolutionFailure.CircularDependency => $"Circular service dependency for type: {ServiceType}",
        ServiceResolutionFailure.DepthExceeded => $"Maximum service resolution depth ({CobaltServiceResolverBase.MaximumResolutionDepth}) exceeded for type: {ServiceType}",
        ServiceResolutionFailure.InsufficientExecutionStack => $"Insufficient execution stack while resolving service for type: {ServiceType}",
        _ => $"Could not resolve service for type: {ServiceType}"
    }) + $". Dependency path: {string.Join(" -> ", DependencyPath)}";

    /// <summary>Gets Cobalt's dependency snapshot without replacing the original exception.</summary>
    public static IReadOnlyList<Type> GetDependencyPath(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (exception is ServiceResolvingException error) return error.DependencyPath;
        try
        {
            lock (MetadataLock)
                return exception.Data[PathKey] as IReadOnlyList<Type> ?? Array.Empty<Type>();
        }
        catch (Exception) { return Array.Empty<Type>(); }
    }

    internal static void AttachDependencyPath(Exception exception, IReadOnlyList<Type> path)
    {
        if (exception is ServiceResolvingException error)
        {
            // Open generic construction discovers a missing parameter after its optional GetService call.
            if (path[^1] != error.ServiceType)
                path = path.Concat([error.ServiceType]).ToArray();
            error.DependencyPath = Array.AsReadOnly(path.ToArray());
            path = error.DependencyPath;
        }
        else path = Array.AsReadOnly(path.ToArray());
        try
        {
            lock (MetadataLock) exception.Data[PathKey] = path;
        }
        catch (Exception)
        {
            // Custom exceptions may expose read-only or unavailable Data. Metadata must not mask the cause.
        }
    }
}

/// <summary>An error identifying the missing required service type.</summary>
public class ServiceResolvingException<T> : ServiceResolvingException
{
    /// <summary>Creates an error for a missing required service.</summary>
    public ServiceResolvingException() : base(typeof(T))
    {
    }

    internal ServiceResolvingException(IReadOnlyList<Type> path) : base(typeof(T), path) { }
}