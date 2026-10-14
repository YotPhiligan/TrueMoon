using TrueMoon.Diagnostics;

namespace TrueMoon.Enerit.IO.Pipes;

public static class EneriteConfigurationContextExtensions
{
    public static IAppConfigurationContext PipesServiceTransport(this IAppConfigurationContext context)
        => Configure(context, null);

    /// <summary>Uses an app-selected endpoint for each service; both peers must agree.</summary>
    /// <param name="context">The App configuration context.</param>
    /// <param name="endpoint">Returns a pipe name without the tm_ prefix for a service type.</param>
    /// <returns>The same context.</returns>
    public static IAppConfigurationContext PipesServiceTransport(this IAppConfigurationContext context, Func<Type, string> endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        return Configure(context, endpoint);
    }

    private static IAppConfigurationContext Configure(IAppConfigurationContext context, Func<Type, string>? endpoint)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services(r => r.RemoveAll<IInvocationClientFactory>().RemoveAll<IInvocationServerFactory>()
            .Singleton<IInvocationClientFactory>(p => new PipesInvocationClientFactory(p.Resolve<IEventsSourceFactory>(), endpoint))
            .Singleton<IInvocationServerFactory>(p => new PipesInvocationServerFactory(p.Resolve<IEventsSourceFactory>(), p.Resolve<IInvocationServerHandlerResolver>(), endpoint))
            .Transient<IInvocationServerHandlerResolver>(p => new InvocationServerHandlerResolver(p)));
        return context;
    }
}
