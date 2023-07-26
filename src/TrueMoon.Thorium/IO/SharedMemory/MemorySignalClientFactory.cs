using TrueMoon.Diagnostics;

namespace TrueMoon.Thorium.IO.SharedMemory;

public class MemorySignalClientFactory : ISignalClientFactory
{
    private readonly IEventsSourceFactory _eventsSourceFactory;

    public MemorySignalClientFactory(IEventsSourceFactory eventsSourceFactory)
    {
        _eventsSourceFactory = eventsSourceFactory;
    }
    
    public ISignalClient<T> Create<T>()
    {
        return new MemorySignalClient<T>(_eventsSourceFactory.Create<MemorySignalClient<T>>());
    }
}