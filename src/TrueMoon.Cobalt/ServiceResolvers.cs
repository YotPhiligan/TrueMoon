using System.Collections.Frozen;

namespace TrueMoon.Cobalt;

public class ServiceResolvers
{
    public static readonly ServiceResolvers Shared = new ();
    private readonly Dictionary<Type, IResolversContainerBase> _factories = [];
    
    public void Add<TService>(params List<Func<IServicesRegistrationAccessor,IResolver<TService>>> func)
    {
        var type = typeof(TService);
        if (_factories.TryGetValue(type, out var c) 
            && c is TypedResolversContainer<TService> container)
        {
            container.Add(func);
        }
        else
        {
            _factories[type] = new TypedResolversContainer<TService>(func);
        }
    }
    
    public void Add(Type type, params List<Func<IServicesRegistrationAccessor,IUnboundGenericResolver>> func)
    {
        if (_factories.TryGetValue(type, out var c) 
            && c is IUnboundResolversContainer container)
        {
            container.Add(func);
        }
        else
        {
            _factories[type] = new UnboundResolversContainer(type,func);
        }
    }
    
    public FrozenDictionary<Type, IResolversContainerBase> GetResolvers() => _factories.ToFrozenDictionary();
}