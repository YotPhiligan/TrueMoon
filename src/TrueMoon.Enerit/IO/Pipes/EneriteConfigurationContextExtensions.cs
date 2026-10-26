using TrueMoon.Diagnostics;

namespace TrueMoon.Enerit.IO.Pipes;

public static class EneriteConfigurationContextExtensions
{
    public static IAppConfigurationContext PipesServiceTransport(this IAppConfigurationContext context)
        => Configure(context, null, new PipesTransportOptions());

    /// <summary>Uses finite resource limits for App-owned pipes transports. Values are copied during configuration.</summary>
    public static IAppConfigurationContext PipesServiceTransport(this IAppConfigurationContext context, PipesTransportOptions options)
        => Configure(context, null, options);

    /// <summary>Uses an app-selected endpoint for each service; both peers must agree.</summary>
    /// <param name="context">The App configuration context.</param>
    /// <param name="endpoint">Returns a pipe name without the tm_ prefix for a service type.</param>
    /// <returns>The same context.</returns>
    public static IAppConfigurationContext PipesServiceTransport(this IAppConfigurationContext context, Func<Type, string> endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        return Configure(context, endpoint, new PipesTransportOptions());
    }

    /// <summary>Uses explicit endpoints and copied finite resource limits for this App.</summary>
    public static IAppConfigurationContext PipesServiceTransport(this IAppConfigurationContext context, Func<Type, string> endpoint, PipesTransportOptions options)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        return Configure(context, endpoint, options);
    }

    private static IAppConfigurationContext Configure(IAppConfigurationContext context, Func<Type, string>? endpoint, PipesTransportOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);
        var snapshot = options.Snapshot();
        context.Services(r => r.RemoveAll<IInvocationClientFactory>().RemoveAll<IInvocationServerFactory>()
            .Singleton<IInvocationClientFactory>(p => new PipesInvocationClientFactory(p.Resolve<IEventsSourceFactory>(), endpoint, snapshot, p.Resolve<IDiagnosticTelemetry>()))
            .Singleton<IInvocationServerFactory>(p => new PipesInvocationServerFactory(p.Resolve<IEventsSourceFactory>(), p.Resolve<IInvocationServerHandlerResolver>(), endpoint, snapshot, p.Resolve<IDiagnosticTelemetry>()))
            .Transient<IInvocationServerHandlerResolver>(p => new InvocationServerHandlerResolver(p)));
        return context;
    }
}
