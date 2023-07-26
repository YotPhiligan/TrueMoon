using TrueMoon.Diagnostics;

namespace TrueMoon.Thorium.IO.SharedMemory;

public class MemorySignalServerFactory : ISignalServerFactory
{
    private readonly IEventsSourceFactory _eventsSourceFactory;
    private readonly ISignalServerHandlerResolver _resolver;

    public MemorySignalServerFactory(IEventsSourceFactory eventsSourceFactory, ISignalServerHandlerResolver resolver)
    {
        _eventsSourceFactory = eventsSourceFactory;
        _resolver = resolver;
    }
    
    public ISignalServer<T> Create<T>()
    {
        var handler = _resolver.Resolve<T>();
        return new MemorySignalServer<T>(_eventsSourceFactory.Create<MemorySignalServer<T>>(), handler);
    }
}