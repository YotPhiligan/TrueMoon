using TrueMoon.Diagnostics;

namespace TrueMoon.Enerit.IO.Pipes;

public class PipesInvocationServerFactory : IInvocationServerFactory
{
    private readonly IEventsSourceFactory _eventsSourceFactory;
    private readonly IInvocationServerHandlerResolver _resolver;

    private readonly Func<Type, string>? _endpoint;
    public PipesInvocationServerFactory(IEventsSourceFactory eventsSourceFactory, 
        IInvocationServerHandlerResolver resolver) : this(eventsSourceFactory, resolver, null) { }
    public PipesInvocationServerFactory(IEventsSourceFactory eventsSourceFactory, IInvocationServerHandlerResolver resolver, Func<Type, string>? endpoint)
    {
        ArgumentNullException.ThrowIfNull(eventsSourceFactory);
        ArgumentNullException.ThrowIfNull(resolver);
        _eventsSourceFactory = eventsSourceFactory;
        _endpoint = endpoint;
        _resolver = resolver;
    }
    
    public IInvocationServer<T> Create<T>()
    {
        var handler = _resolver.Resolve<T>();
        return new PipesInvocationServer<T>(_eventsSourceFactory.Create<PipesInvocationServer<T>>(), handler, _endpoint is null ? typeof(T).FullName! : _endpoint(typeof(T)));
    }
}

/// <summary>Explicit endpoint and borrowed handler for an owned invocation server.</summary>
/// <param name="Endpoint">Private endpoint.</param><param name="Handler">Invocation handler.</param>
public sealed record PipesInvocationServerArguments<TService>(string Endpoint, IInvocationServerHandler<TService> Handler);

/// <summary>Creates caller-owned servers from injected diagnostics.</summary>
/// <typeparam name="TService">Invocation contract.</typeparam>
/// <param name="events">Borrowed diagnostic source.</param>
public sealed class PipesInvocationServerFactory<TService>(IEventsSource<PipesInvocationServer<TService>> events)
    : IFactory<PipesInvocationServer<TService>, PipesInvocationServerArguments<TService>>
{
    /// <inheritdoc />
    public PipesInvocationServer<TService> Create(PipesInvocationServerArguments<TService> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(arguments.Endpoint);
        ArgumentNullException.ThrowIfNull(arguments.Handler);
        return new(events, arguments.Handler, arguments.Endpoint);
    }
}
