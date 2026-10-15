using System.Collections.Frozen;
using TrueMoon.Services;

namespace TrueMoon.Cobalt;

public abstract class CobaltServiceResolverBase : IServiceResolver, IDisposable, IAsyncDisposable
{
    /// <summary>Maximum dependency path length, including synthesized enumerable elements.</summary>
    public const int MaximumResolutionDepth = 128;
    private readonly Lock _lock = new();
    private readonly DisposablesContainer _disposables = new();
    private readonly FrozenDictionary<Type, ITypeContainer> _containers;
    // All construction is synchronous and serialized by _lock. These stacks belong to this resolver only.
    private readonly List<Type> _dependencyPath = [];
    private readonly List<(IResolver Resolver, Type Service)> _activations = [];
    private readonly HashSet<Exception> _reportedFailures = new(ReferenceEqualityComparer.Instance);
    private bool _disposed;

    protected CobaltServiceResolverBase(FrozenDictionary<Type, ITypeContainer> resolversContainers) => _containers = resolversContainers;

    public T Resolve<T>() => (T)ResolveService(typeof(T), static path => new ServiceResolvingException<T>(path))!;
    public T? TryResolve<T>() => GetService(typeof(T)) is T value ? value : default;

    public object? GetService(Type type) => ResolveService(type, null);

    private object? ResolveService(Type type, Func<IReadOnlyList<Type>, ServiceResolvingException>? missing)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (type.ContainsGenericParameters) throw new ArgumentException("Resolution requires a closed service type.", nameof(type));
        // Serialize construction and ownership bookkeeping with shutdown. The lock is reentrant for dependencies.
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (type == typeof(IServiceResolver) || type == typeof(IServiceProvider)) return this;
            var root = _dependencyPath.Count == 0;
            _dependencyPath.Add(type);
            try
            {
                CheckDepth(type);
                var value = ResolveCore(type);
                if (value == null && missing != null) throw missing(_dependencyPath);
                return value;
            }
            catch (Exception exception) when (RecordFailure(exception)) { throw; }
            finally
            {
                _dependencyPath.RemoveAt(_dependencyPath.Count - 1);
                if (root) _reportedFailures.Clear();
            }
        }
    }

    private object? ResolveCore(Type type)
    {
        if (_containers.TryGetValue(type, out var container)) return ResolveItem(container.GetResolver(), type);
        // Explicit open IEnumerable registrations have the same precedence as other explicit services.
        if (type.IsConstructedGenericType && _containers.TryGetValue(type.GetGenericTypeDefinition(), out var open))
            return ResolveItem(open.GetResolver(), type);
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            var element = type.GenericTypeArguments[0];
            var resolvers = GetEnumerableResolvers(element);
            var result = Array.CreateInstance(element, resolvers.Length);
            for (var index = 0; index < resolvers.Length; index++)
                result.SetValue(ResolveItem(resolvers[index], element), index);
            return result;
        }
        return null;
    }

    private IResolver[] GetEnumerableResolvers(Type element)
    {
        _containers.TryGetValue(element, out var closed);
        ITypeContainer? open = null;
        if (element.IsConstructedGenericType) _containers.TryGetValue(element.GetGenericTypeDefinition(), out open);
        if (closed == null) return open?.GetResolvers() ?? [];
        if (open == null) return closed.GetResolvers();
        return Ordered(open).Concat(Ordered(closed)).OrderBy(item => item.Order).Select(item => item.Resolver).ToArray();

        static IEnumerable<(IResolver Resolver, int Order)> Ordered(ITypeContainer source)
        {
            var resolvers = source.GetResolvers();
            for (var index = 0; index < resolvers.Length; index++)
                yield return (resolvers[index], source is TypeContainer ordered ? ordered.GetRegistrationOrder(index) : int.MaxValue);
        }
    }

    private object? ResolveItem(IResolver resolver, Type type)
    {
        var element = _dependencyPath[^1] != type;
        if (element) _dependencyPath.Add(type);
        var active = false;
        try
        {
            CheckDepth(type);
            if (_activations.Any(a => ReferenceEquals(a.Resolver, resolver) && a.Service == type))
                throw new ServiceResolvingException(type, _dependencyPath, ServiceResolutionFailure.CircularDependency);
            _activations.Add((resolver, type));
            active = true;
            var value = resolver switch
            {
                IGenericResolver generic => generic.Resolve(type.GenericTypeArguments, this),
                IObjectResolver objectResolver => objectResolver.Resolve(this),
                _ => ResolveTypedItem(resolver, type)
            };
            if (value != null && resolver.IsServiceDisposable) _disposables.Add(value);
            return value;
        }
        catch (Exception exception) when (RecordFailure(exception)) { throw; }
        finally
        {
            if (active) _activations.RemoveAt(_activations.Count - 1);
            if (element) _dependencyPath.RemoveAt(_dependencyPath.Count - 1);
        }
    }

    private void CheckDepth(Type type)
    {
        if (_dependencyPath.Count > MaximumResolutionDepth)
            throw new ServiceResolvingException(type, _dependencyPath, ServiceResolutionFailure.DepthExceeded);
        if (!System.Runtime.CompilerServices.RuntimeHelpers.TryEnsureSufficientExecutionStack())
            throw new ServiceResolvingException(type, _dependencyPath, ServiceResolutionFailure.InsufficientExecutionStack);
    }

    private bool RecordFailure(Exception exception)
    {
        // Record in the exception-search pass, before finally unwinds the path. Returning false avoids
        // catch/rethrow handlers at every dependency level and preserves the original throw site.
        if (_reportedFailures.Add(exception)) ServiceResolvingException.AttachDependencyPath(exception, _dependencyPath);
        return false;
    }

    private object? ResolveTypedItem(IResolver resolver, Type type)
    {
        var contract = typeof(IResolver<>).MakeGenericType(type);
        if (!contract.IsInstanceOfType(resolver))
            throw new InvalidOperationException($"Resolver {resolver.GetType()} cannot resolve {type}.");
        return contract.GetMethod(nameof(IResolver<object>.Resolve))!.Invoke(resolver,
            System.Reflection.BindingFlags.DoNotWrapExceptions, binder: null, parameters: [this], culture: null);
    }

    public void Dispose()
    {
        lock (_lock) _disposed = true;
        _disposables.Dispose();
    }
    public ValueTask DisposeAsync()
    {
        lock (_lock) _disposed = true;
        return _disposables.DisposeAsync();
    }
}
