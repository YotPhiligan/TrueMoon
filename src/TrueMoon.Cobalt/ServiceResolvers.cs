using System.Collections.Frozen;

namespace TrueMoon.Cobalt;

public class ServiceResolvers
{
    public static readonly ServiceResolvers Shared = new();
    private readonly Lock _lock = new();
    private readonly Dictionary<Type, List<Func<IResolver>>> _factories = [];
    private readonly Dictionary<(Type Service, Type Implementation, ServiceLifetime Lifetime), Func<IResolver>> _registrations = [];

    public void Add<TService>(Func<IResolver<TService>> factory) => Add(typeof(TService), factory);

    public void Add(Type service, Func<IResolver> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        lock (_lock)
        {
            if (!_factories.TryGetValue(service, out var factories))
                _factories.Add(service, factories = []);
            factories.Add(factory);
        }
    }

    /// <summary>Registers generated construction code for an exact runtime registration.</summary>
    public void Add(Type service, Type implementation, ServiceLifetime lifetime, Func<IResolver> factory)
    {
        lock (_lock)
        {
            _registrations[(service, implementation, lifetime)] = factory;
            if (!_factories.TryGetValue(service, out var factories))
                _factories.Add(service, factories = []);
            factories.Add(factory);
        }
    }

    internal Func<IResolver> GetFactory(Type service, Type implementation, ServiceLifetime lifetime)
    {
        lock (_lock)
        {
            if (_registrations.TryGetValue((service, implementation, lifetime), out var factory))
                return factory;
            // Compatibility for manually authored resolvers using the original registration API.
            if (_factories.TryGetValue(service, out var factories))
            {
                foreach (var candidate in factories)
                {
                    var resolver = candidate();
                    var matches = resolver.GetType().GetInterfaces().Any(i => i.IsGenericType &&
                        ((i.GetGenericTypeDefinition() == typeof(IResolver<,>) && i.GenericTypeArguments[0] == service && i.GenericTypeArguments[1] == implementation) ||
                         (i.GetGenericTypeDefinition() == typeof(IResolver<>) && i.GenericTypeArguments[0] == service && implementation == service)));
                    if (matches && (resolver.ServiceLifetime == lifetime || resolver.ServiceLifetime == ServiceLifetime.None))
                        return candidate;
                }
            }
        }
        throw new InvalidOperationException($"No generated resolver for {service} -> {implementation} ({lifetime}).");
    }

    public FrozenDictionary<Type, List<Func<IResolver>>> GetResolvers()
    {
        lock (_lock)
            return _factories.ToFrozenDictionary(p => p.Key, p => new List<Func<IResolver>>(p.Value));
    }
}
