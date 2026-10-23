using TrueMoon.Diagnostics;

namespace TrueMoon.Enerit.IO.Pipes;

public class PipesInvocationServerFactory : IInvocationServerFactory
{
    private readonly IEventsSourceFactory _eventsSourceFactory;
    private readonly IInvocationServerHandlerResolver _resolver;

    private readonly Func<Type, string>? _endpoint;
    private readonly PipesTransportOptions _options;
    public PipesInvocationServerFactory(IEventsSourceFactory eventsSourceFactory, 
        IInvocationServerHandlerResolver resolver) : this(eventsSourceFactory, resolver, null) { }
    public PipesInvocationServerFactory(IEventsSourceFactory eventsSourceFactory, IInvocationServerHandlerResolver resolver, Func<Type, string>? endpoint)
        : this(eventsSourceFactory, resolver, endpoint, new PipesTransportOptions()) { }
    /// <summary>Creates an App-local factory with copied finite resource limits.</summary>
    public PipesInvocationServerFactory(IEventsSourceFactory eventsSourceFactory, IInvocationServerHandlerResolver resolver, Func<Type, string>? endpoint, PipesTransportOptions options)
    {
        ArgumentNullException.ThrowIfNull(eventsSourceFactory);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Snapshot();
        _eventsSourceFactory = eventsSourceFactory;
        _endpoint = endpoint;
        _resolver = resolver;
    }
    
    public IInvocationServer<T> Create<T>()
    {
        var handler = _resolver.Resolve<T>();
        return new PipesInvocationServer<T>(_eventsSourceFactory.Create<PipesInvocationServer<T>>(), handler, _endpoint is null ? typeof(T).FullName! : _endpoint(typeof(T)), _options);
    }
}

/// <summary>Explicit endpoint and borrowed handler for an owned invocation server.</summary>
/// <param name="Endpoint">Private endpoint.</param><param name="Handler">Invocation handler.</param>
public sealed record PipesInvocationServerArguments<TService>(string Endpoint, IInvocationServerHandler<TService> Handler);

/// <summary>Creates caller-owned servers from injected diagnostics.</summary>
/// <typeparam name="TService">Invocation contract.</typeparam>
public sealed class PipesInvocationServerFactory<TService>
    : IFactory<PipesInvocationServer<TService>, PipesInvocationServerArguments<TService>>
{
    private readonly IEventsSource<PipesInvocationServer<TService>> _events;
    private readonly PipesTransportOptions _options;
    /// <summary>Creates an endpoint factory with default limits.</summary>
    public PipesInvocationServerFactory(IEventsSource<PipesInvocationServer<TService>> events) : this(events, new PipesTransportOptions()) { }
    /// <summary>Creates an endpoint factory with copied limits and borrowed diagnostics.</summary>
    public PipesInvocationServerFactory(IEventsSource<PipesInvocationServer<TService>> events, PipesTransportOptions options)
    {
        ArgumentNullException.ThrowIfNull(events); ArgumentNullException.ThrowIfNull(options);
        _events = events; _options = options.Snapshot();
    }
    /// <inheritdoc />
    public PipesInvocationServer<TService> Create(PipesInvocationServerArguments<TService> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(arguments.Endpoint);
        ArgumentNullException.ThrowIfNull(arguments.Handler);
        return new(_events, arguments.Handler, arguments.Endpoint, _options);
    }
}
