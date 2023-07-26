using TrueMoon.Diagnostics;

namespace TrueMoon.Thorium.IO.SharedMemory;

public class MemorySignalsHandleFactory : ISignalsHandleFactory
{
    private readonly ThoriumConfiguration _thoriumConfiguration;
    private readonly IEventsSourceFactory _eventsSourceFactory;

    public MemorySignalsHandleFactory(ThoriumConfiguration thoriumConfiguration,
        IEventsSourceFactory eventsSourceFactory)
    {
        _thoriumConfiguration = thoriumConfiguration;
        _eventsSourceFactory = eventsSourceFactory;
    }
    
    public ISignalsHandle<T> Create<T>() 
        => new MemorySignalsHandle<T>(_thoriumConfiguration, _eventsSourceFactory.Create<MemorySignalsHandle<T>>());
}