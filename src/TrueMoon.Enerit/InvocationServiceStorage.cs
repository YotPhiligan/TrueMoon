using System.Collections.Concurrent;
using TrueMoon.Enerit.IO;
using TrueMoon.Services;

namespace TrueMoon.Enerit;

/// <summary>Compiled service metadata and registration factories; runtime instances remain App-local.</summary>
public class InvocationServiceStorage
{
    public static InvocationServiceStorage Shared { get; } = new();

    private sealed record Registration(Type Implementation, Action<IServicesRegistrationContext>? Configure);
    private readonly ConcurrentDictionary<Type, Registration> _services = new();
    private readonly ConcurrentDictionary<Type, Registration> _handlers = new();

    public void Register<T, TService>() where TService : class, T => _services[typeof(T)] = new(typeof(TService), null);

    /// <summary>Registers the generator's typed factory without depending on a DI-specific generator.</summary>
    public void Register<T, TService>(Action<IServicesRegistrationContext> configure) where TService : class, T
    {
        ArgumentNullException.ThrowIfNull(configure);
        _services[typeof(T)] = new(typeof(TService), configure);
    }

    public void RegisterHandler<T, TService>() where TService : class, T => _handlers[typeof(T)] = new(typeof(TService), null);

    /// <summary>Registers a typed server-handler factory for any supported App provider.</summary>
    public void RegisterHandler<T, TService>(Action<IServicesRegistrationContext> configure) where TService : class, T
    {
        ArgumentNullException.ThrowIfNull(configure);
        _handlers[typeof(T)] = new(typeof(TService), configure);
    }

    private Registration Service<T>() => _services.TryGetValue(typeof(T), out var item) ? item
        : throw new InvalidOperationException($"implementation for \"{typeof(T)}\" not found");
    private Registration Handler<T>() => _handlers.TryGetValue(typeof(IInvocationServerHandler<T>), out var item) ? item
        : throw new InvalidOperationException($"implementation for \"{typeof(T)}\" not found");

    internal Type GetService<T>() => Service<T>().Implementation;
    internal Type GetHandler<T>() => Handler<T>().Implementation;

    internal void ConfigureService<T>(IServicesRegistrationContext context)
    {
        var item = Service<T>();
        if (item.Configure is { } factory) factory(context);
        else context.Singleton(typeof(T), item.Implementation);
    }

    internal void ConfigureHandler<T>(IServicesRegistrationContext context)
    {
        var item = Handler<T>();
        if (item.Configure is { } factory) factory(context);
        else context.Singleton(typeof(IInvocationServerHandler<T>), item.Implementation);
    }
}
