using TrueMoon.Diagnostics;

namespace TrueMoon.Thorium.IO.SharedMemory;

public class MemorySignalServerFactory : ISignalServerFactory
{
    private readonly IEventsSourceFactory _eventsSourceFactory;
    private readonly ISignalServerHandlerResolver _resolver;
    private readonly SignalsTaskScheduler _signalsTaskScheduler;

    public MemorySignalServerFactory(IEventsSourceFactory eventsSourceFactory, 
        ISignalServerHandlerResolver resolver, 
        SignalsTaskScheduler signalsTaskScheduler)
    {
        _eventsSourceFactory = eventsSourceFactory;
        _resolver = resolver;
        _signalsTaskScheduler = signalsTaskScheduler;
    }
    
    public ISignalServer<T> Create<T>()
    {
        var handler = _resolver.Resolve<T>();
        return new MemorySignalServer<T>(_eventsSourceFactory.Create<MemorySignalServer<T>>(), _signalsTaskScheduler, handler);
    }
}