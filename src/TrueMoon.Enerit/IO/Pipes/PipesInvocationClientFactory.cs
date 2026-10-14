using TrueMoon.Diagnostics;

namespace TrueMoon.Enerit.IO.Pipes;

public class PipesInvocationClientFactory : IInvocationClientFactory
{
    private readonly IEventsSourceFactory _eventsSourceFactory;

    private readonly Func<Type, string>? _endpoint;
    public PipesInvocationClientFactory(IEventsSourceFactory eventsSourceFactory) : this(eventsSourceFactory, null) { }
    public PipesInvocationClientFactory(IEventsSourceFactory eventsSourceFactory, Func<Type, string>? endpoint)
    {
        ArgumentNullException.ThrowIfNull(eventsSourceFactory);
        _eventsSourceFactory = eventsSourceFactory;
        _endpoint = endpoint;
    }
    
    public IInvocationClient<T> Create<T>() => new PipesInvocationClient<T>(_eventsSourceFactory.Create<PipesInvocationClient<T>>(), _endpoint is null ? typeof(T).FullName! : _endpoint(typeof(T)));
}