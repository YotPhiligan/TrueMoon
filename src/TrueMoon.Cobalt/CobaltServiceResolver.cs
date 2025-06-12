using System.Collections.Frozen;
using TrueMoon.Services;

namespace TrueMoon.Cobalt;

public class CobaltServiceResolver : IServiceResolver, IDisposable, IAsyncDisposable
{
    private readonly IServicesRegistrationAccessor _registrationAccessor;
    private readonly FrozenDictionary<Type, IResolversContainerBase> _resolvers;
    private readonly DisposablesContainer _disposables = new ();

    public CobaltServiceResolver(IServicesRegistrationAccessor registrationAccessor)
    {
        _registrationAccessor = registrationAccessor;
        _resolvers = ServiceResolvers.Shared.GetResolvers();
    }
    
    public T Resolve<T>()
    {
        IResolvingContext ctx = new CobaltResolvingContext(_resolvers, _registrationAccessor, _disposables);
        
        return ctx.Resolve<T>();
    }

    public object? GetService(Type type)
    {
        IResolvingContext ctx = new CobaltResolvingContext(_resolvers, _registrationAccessor, _disposables);
        
        return ctx.GetService(type);
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        return _disposables.DisposeAsync();
    }
}