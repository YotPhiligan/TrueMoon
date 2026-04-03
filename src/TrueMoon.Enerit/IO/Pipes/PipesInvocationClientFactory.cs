using TrueMoon.Diagnostics;

namespace TrueMoon.Enerit.IO.Pipes;

public class PipesInvocationClientFactory : IInvocationClientFactory
{
    private readonly IEventsSourceFactory _eventsSourceFactory;

    public PipesInvocationClientFactory(IEventsSourceFactory eventsSourceFactory)
    {
        _eventsSourceFactory = eventsSourceFactory;
    }
    
    public IInvocationClient<T> Create<T>() => new PipesInvocationClient<T>(_eventsSourceFactory.Create<PipesInvocationClient<T>>());
}