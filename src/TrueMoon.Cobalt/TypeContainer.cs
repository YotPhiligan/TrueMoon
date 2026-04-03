namespace TrueMoon.Cobalt;

public class TypeContainer(Type type) : ITypeContainer
{
    public string TypeId { get; } = TypeUtils.GetTypeId(type);
    public Type Type { get; } = type;
    public Type EnumerableType { get; } = TypeUtils.CreateEnumerableType(type);
    private readonly List<Func<IResolver>> _resolverFactories = [];
    private IResolver[]? _resolvers;

    public IResolver[] GetResolvers()
    {
        if (_resolvers == null)
        {
            _resolvers = _resolverFactories.Select(f => f()).ToArray();
        }

        return _resolvers;
    }

    public IResolver GetResolver()
    {
        if (_resolvers == null)
        {
            _resolvers = new IResolver[_resolverFactories.Count];
            _resolvers[^1] = _resolverFactories[^1]();
        }

        return _resolvers[^1];
    }

    public void Add(Func<IResolver> resolver)
    {
        _resolverFactories.Add(resolver);
    }
    
    public void Add(IEnumerable<Func<IResolver>> resolvers)
    {
        _resolverFactories.AddRange(resolvers);
    }
}