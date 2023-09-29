using TrueMoon.Diagnostics;

namespace TrueMoon.Thorium.IO.Pipes;

public class PipesInvocationServerFactory : IInvocationServerFactory
{
    private readonly IEventsSourceFactory _eventsSourceFactory;
    private readonly IInvocationServerHandlerResolver _resolver;

    public PipesInvocationServerFactory(IEventsSourceFactory eventsSourceFactory, 
        IInvocationServerHandlerResolver resolver)
    {
        _eventsSourceFactory = eventsSourceFactory;
        _resolver = resolver;
    }
    
    public IInvocationServer<T> Create<T>()
    {
        var handler = _resolver.Resolve<T>();
        return new PipesInvocationServer<T>(_eventsSourceFactory.Create<PipesInvocationServer<T>>(), handler);
    }
}