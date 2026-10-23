using TrueMoon.Diagnostics;

namespace TrueMoon.Enerit.IO.Pipes;

public class PipesInvocationClientFactory : IInvocationClientFactory
{
    private readonly IEventsSourceFactory _eventsSourceFactory;

    private readonly Func<Type, string>? _endpoint;
    private readonly PipesTransportOptions _options;
    public PipesInvocationClientFactory(IEventsSourceFactory eventsSourceFactory) : this(eventsSourceFactory, null) { }
    public PipesInvocationClientFactory(IEventsSourceFactory eventsSourceFactory, Func<Type, string>? endpoint)
        : this(eventsSourceFactory, endpoint, new PipesTransportOptions()) { }
    /// <summary>Creates a factory with App-local endpoints and copied finite resource limits.</summary>
    public PipesInvocationClientFactory(IEventsSourceFactory eventsSourceFactory, Func<Type, string>? endpoint, PipesTransportOptions options)
    {
        ArgumentNullException.ThrowIfNull(eventsSourceFactory);
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Snapshot();
        _eventsSourceFactory = eventsSourceFactory;
        _endpoint = endpoint;
    }
    
    public IInvocationClient<T> Create<T>() => new PipesInvocationClient<T>(_eventsSourceFactory.Create<PipesInvocationClient<T>>(), _endpoint is null ? typeof(T).FullName! : _endpoint(typeof(T)), _options);
}

/// <summary>Creates caller-owned clients for explicit endpoints from injected diagnostics.</summary>
/// <typeparam name="TService">Invocation contract.</typeparam>
public sealed class PipesInvocationClientFactory<TService>
    : IFactory<PipesInvocationClient<TService>, string>
{
    private readonly IEventsSource<PipesInvocationClient<TService>> _events;
    private readonly PipesTransportOptions _options;
    /// <summary>Creates an endpoint factory with default resource limits.</summary>
    public PipesInvocationClientFactory(IEventsSource<PipesInvocationClient<TService>> events) : this(events, new PipesTransportOptions()) { }
    /// <summary>Creates an endpoint factory with copied resource limits and borrowed diagnostics.</summary>
    public PipesInvocationClientFactory(IEventsSource<PipesInvocationClient<TService>> events, PipesTransportOptions options)
    {
        ArgumentNullException.ThrowIfNull(events); ArgumentNullException.ThrowIfNull(options);
        _events = events; _options = options.Snapshot();
    }
    /// <inheritdoc />
    public PipesInvocationClient<TService> Create(string arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(arguments);
        return new(_events, arguments, _options);
    }
}
