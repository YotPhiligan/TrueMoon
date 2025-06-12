namespace TrueMoon.Cobalt;

public class UnboundResolversContainer : IUnboundResolversContainer
{
    private List<Func<IServicesRegistrationAccessor,IUnboundGenericResolver>>? _factories;
    private IUnboundGenericResolver[]? _resolvers;
    public UnboundResolversContainer(Type type, List<Func<IServicesRegistrationAccessor, IUnboundGenericResolver>> func)
    {
        _type = type;
        TypeId = TypeUtils.GetTypeId(_type);
        Add(func);
    }
    
    private readonly Type _type;
    private bool _resolversInitialized;
    public string TypeId { get; }

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
        _resolvers = new IUnboundGenericResolver[len];
        for (var i = 0; i < len; i++)
        {
            _resolvers[i] = _factories[i](accessor);
        }
        
        _resolversInitialized = true;
    }
    
    public void Add(List<Func<IServicesRegistrationAccessor, IUnboundGenericResolver>> resolverFactories)
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

    public IUnboundGenericResolver GetLastResolver(IServicesRegistrationAccessor accessor)
    {
        if (!_resolversInitialized)
        {
            InitializeResolvers(accessor);
        }
        return _resolvers![^1];
    }
}