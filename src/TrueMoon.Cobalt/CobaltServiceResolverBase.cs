using System.Collections.Frozen;
using TrueMoon.Services;

namespace TrueMoon.Cobalt;

public abstract class CobaltServiceResolverBase : IServiceResolver, IDisposable, IAsyncDisposable
{
    private readonly Lock _lock = new();
    private readonly DisposablesContainer _disposables = new();
    private readonly FrozenDictionary<Type, ITypeContainer> _containers;
    private bool _disposed;

    protected CobaltServiceResolverBase(FrozenDictionary<Type, ITypeContainer> resolversContainers) => _containers = resolversContainers;

    public T Resolve<T>() => GetService(typeof(T)) is T value ? value : throw new ServiceResolvingException<T>();
    public T? TryResolve<T>() => GetService(typeof(T)) is T value ? value : default;

    public object? GetService(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        // Serialize construction and ownership bookkeeping with shutdown. The lock is reentrant for dependencies.
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (type == typeof(IServiceResolver) || type == typeof(IServiceProvider)) return this;
            if (_containers.TryGetValue(type, out var container)) return ResolveItem(container.GetResolver(), type);
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                var element = type.GenericTypeArguments[0];
                var source = GetContainer(element);
                var resolvers = source?.GetResolvers() ?? [];
                var result = Array.CreateInstance(element, resolvers.Length);
                for (var index = 0; index < resolvers.Length; index++)
                    result.SetValue(ResolveItem(resolvers[index], element), index);
                return result;
            }
            return GetContainer(type) is { } genericContainer ? ResolveItem(genericContainer.GetResolver(), type) : null;
        }
    }

    private ITypeContainer? GetContainer(Type type)
    {
        if (_containers.TryGetValue(type, out var container)) return container;
        return type.IsGenericType && _containers.TryGetValue(type.GetGenericTypeDefinition(), out container) ? container : null;
    }

    private object? ResolveItem(IResolver resolver, Type type)
    {
        var value = resolver switch
        {
            IGenericResolver generic => generic.Resolve(type.GenericTypeArguments, this),
            IObjectResolver objectResolver => objectResolver.Resolve(this),
            _ => ResolveTypedItem(resolver, type)
        };
        if (value != null && resolver.IsServiceDisposable) _disposables.Add(value);
        return value;
    }

    private object? ResolveTypedItem(IResolver resolver, Type type)
    {
        var contract = typeof(IResolver<>).MakeGenericType(type);
        if (!contract.IsInstanceOfType(resolver))
            throw new InvalidOperationException($"Resolver {resolver.GetType()} cannot resolve {type}.");
        try
        {
            return contract.GetMethod(nameof(IResolver<object>.Resolve))!.Invoke(resolver, [this]);
        }
        catch (System.Reflection.TargetInvocationException exception) when (exception.InnerException != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
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
