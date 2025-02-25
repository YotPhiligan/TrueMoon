namespace TrueMoon.Cobalt;

public class TypedResolversContainer<TService> : ITypedResolversContainer<TService>
{
    private IResolver<TService>[]? _resolvers;
    private List<Func<IServicesRegistrationAccessor,IResolver<TService>>>? _factories;
    private bool _resolversInitialized;

    public TypedResolversContainer(List<Func<IServicesRegistrationAccessor,IResolver<TService>>> resolverFactories)
    {
        Add(resolverFactories);
    }
    
    public void Add(List<Func<IServicesRegistrationAccessor,IResolver<TService>>> resolverFactories)
    {
        if (_factories == null)
        {
            _factories = [..resolverFactories];
        }
        else
        {
            _factories.AddRange(resolverFactories);
        }
    }

    public IResolver<TService>[] GetTypedResolvers(IServicesRegistrationAccessor accessor)
    {
        if (_resolvers != null)
        {
            return _resolvers;
        }

        InitializeResolvers(accessor);
        return _resolvers;
    }

    private void InitializeResolvers(IServicesRegistrationAccessor accessor)
    {
        if (_factories == null)
        {
            return;
        }
        
        if (_resolvers != null)
        {
            return;
        }
        
        var len = _factories.Count;
        _resolvers = new IResolver<TService>[len];
        for (var i = 0; i < len; i++)
        {
            _resolvers[i] = _factories[i](accessor);
        }
        
        _resolversInitialized = true;
    }

    public IResolver<TService> GetLastResolver(IServicesRegistrationAccessor accessor)
    {
        if (!_resolversInitialized)
        {
            InitializeResolvers(accessor);
        }
        return _resolvers![^1];
    }

    public IResolver<TService> GetFirstResolver(IServicesRegistrationAccessor accessor)
    {
        if (!_resolversInitialized)
        {
            InitializeResolvers(accessor);
        }
        return _resolvers![0];
    }
    
    IResolver ITypedResolversContainer.GetFirstResolver(IServicesRegistrationAccessor accessor) => GetFirstResolver(accessor);

    IResolver ITypedResolversContainer.GetLastResolver(IServicesRegistrationAccessor accessor) => GetLastResolver(accessor);

    public string TypeId { get; } = TypeUtils.GetTypeId<TService>();
    public Type EnumerableType { get; } = typeof(IEnumerable<TService>);
    
    public IResolver[] GetResolvers(IServicesRegistrationAccessor accessor) => GetTypedResolvers(accessor);
}