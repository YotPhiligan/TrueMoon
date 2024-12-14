using System.Collections.Frozen;

namespace TrueMoon.Cobalt;

public class ServiceResolvers
{
    public static readonly ServiceResolvers Shared = new ();
    private readonly Dictionary<Type,ResolverFactory> _factories = [];
    
    public void Add<TService>(Func<IResolver<TService>> func)
    {
        var factory = new ResolverFactory(func);
        _factories.Add(typeof(TService), factory);
    }

    public FrozenDictionary<Type, IResolver> GetResolvers()
    {
        var t = _factories
            .Select(t => (t.Key, t.Value.Create()!))
            .ToFrozenDictionary(t => t.Key, b=>b.Item2);

        return t;
    }
    
    private class ResolverFactory(Func<IResolver> func) : IFactory<IResolver>
    {
        public IResolver? Create() => func();

        public IResolver? Create<TData>(TData? data = default) => Create();
    }
}