using TrueMoon.Diagnostics;

namespace TrueMoon.Enerit.IO.Pipes;

public class PipesInvocationServerFactory : IInvocationServerFactory
{
    private readonly IEventsSourceFactory _eventsSourceFactory;
    private readonly IInvocationServerHandlerResolver _resolver;

    private readonly Func<Type, string>? _endpoint;
    public PipesInvocationServerFactory(IEventsSourceFactory eventsSourceFactory, 
        IInvocationServerHandlerResolver resolver) : this(eventsSourceFactory, resolver, null) { }
    public PipesInvocationServerFactory(IEventsSourceFactory eventsSourceFactory, IInvocationServerHandlerResolver resolver, Func<Type, string>? endpoint)
    {
        ArgumentNullException.ThrowIfNull(eventsSourceFactory);
        ArgumentNullException.ThrowIfNull(resolver);
        _eventsSourceFactory = eventsSourceFactory;
        _endpoint = endpoint;
        _resolver = resolver;
    }
    
    public IInvocationServer<T> Create<T>()
    {
        var handler = _resolver.Resolve<T>();
        return new PipesInvocationServer<T>(_eventsSourceFactory.Create<PipesInvocationServer<T>>(), handler, _endpoint is null ? typeof(T).FullName! : _endpoint(typeof(T)));
    }
}