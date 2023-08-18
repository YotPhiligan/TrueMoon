using TrueMoon.Diagnostics;

namespace TrueMoon.Thorium.IO.SharedMemory;

public class MemorySignalClientFactory : ISignalClientFactory
{
    private readonly IEventsSourceFactory _eventsSourceFactory;
    private readonly SignalsTaskScheduler _signalsTaskScheduler;

    public MemorySignalClientFactory(IEventsSourceFactory eventsSourceFactory, SignalsTaskScheduler signalsTaskScheduler)
    {
        _eventsSourceFactory = eventsSourceFactory;
        _signalsTaskScheduler = signalsTaskScheduler;
    }
    
    public ISignalClient<T> Create<T>() => new MemorySignalClient<T>(_eventsSourceFactory.Create<MemorySignalClient<T>>(),_signalsTaskScheduler);
}