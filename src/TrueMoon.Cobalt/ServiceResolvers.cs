using System.Collections.Frozen;

namespace TrueMoon.Cobalt;

public class ServiceResolvers
{
    public static readonly ServiceResolvers Shared = new ();
    private readonly Dictionary<Type, ITypedResolversContainer> _factories = [];
    
    public void Add<TService>(params List<Func<IServicesRegistrationAccessor,IResolver<TService>>> func)
    {
        if (_factories.TryGetValue(typeof(TService), out var c) 
            && c is TypedResolversContainer<TService> container)
        {
            container.Add(func);
        }
        else
        {
            _factories[typeof(TService)] = new TypedResolversContainer<TService>(func);
        }
    }

    public FrozenDictionary<Type, ITypedResolversContainer> GetResolvers() => _factories.ToFrozenDictionary();
}