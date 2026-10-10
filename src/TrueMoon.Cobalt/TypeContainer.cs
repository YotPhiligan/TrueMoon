namespace TrueMoon.Cobalt;

public class TypeContainer(Type type) : ITypeContainer
{
    private readonly Lock _lock = new();
    private readonly List<Func<IResolver>> _resolverFactories = [];
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

    public void Add(Func<IResolver> resolver)
    {
        lock (_lock)
        {
            if (_resolvers != null) throw new InvalidOperationException("The resolver container is already initialized.");
            _resolverFactories.Add(resolver);
        }
    }

    public void Add(IEnumerable<Func<IResolver>> resolvers)
    {
        foreach (var resolver in resolvers) Add(resolver);
    }
}
