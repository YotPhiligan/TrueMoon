namespace TrueMoon.Cobalt;

public class TypeContainer(Type type) : ITypeContainer
{
    private readonly Lock _lock = new();
    private readonly List<Func<IResolver>> _resolverFactories = [];
    private readonly List<int> _registrationOrder = [];
    private IResolver[]? _resolvers;
    public string TypeId { get; } = TypeUtils.GetTypeId(type);
    public Type Type { get; } = type;
    public Type EnumerableType { get; } = type.ContainsGenericParameters ? typeof(IEnumerable<>) : TypeUtils.CreateEnumerableType(type);

    public IResolver[] GetResolvers()
    {
        lock (_lock)
            return _resolvers ??= _resolverFactories.Select(factory => factory()).ToArray();
    }

    public IResolver GetResolver() => GetResolvers()[^1];

    public void Add(Func<IResolver> resolver) => Add(resolver, int.MaxValue);

    internal void Add(Func<IResolver> resolver, int registrationOrder)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        lock (_lock)
        {
            if (_resolvers != null) throw new InvalidOperationException("The resolver container is already initialized.");
            _resolverFactories.Add(resolver);
            _registrationOrder.Add(registrationOrder);
        }
    }

    internal int GetRegistrationOrder(int index)
    {
        lock (_lock) return _registrationOrder[index];
    }

    public void Add(IEnumerable<Func<IResolver>> resolvers)
    {
        foreach (var resolver in resolvers) Add(resolver);
    }
}
