using TrueMoon.Configuration;

namespace TrueMoon.Diagnostics;

public static class AppCreationContextExtensions
{
    /// <summary>Enables diagnostics owned by the application. Repeated calls replace earlier settings and registrations.</summary>
    /// <param name="context">Application setup context.</param>
    /// <param name="action">Optional settings for the replacement diagnostics configuration.</param>
    /// <returns>The application setup context.</returns>
    public static IAppConfigurationContext UseDiagnostics(this IAppConfigurationContext context, Action<IDiagnosticsConfiguration>? action = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var configuration = new DiagnosticsConfiguration();
        configuration.AddFilter("TrueMoon");
        action?.Invoke(configuration);
        context.Configuration(conf => conf.Set<IDiagnosticsConfiguration>(ConfigurationExtensions.DiagnosticsConfigurationName, configuration));
        context.Services(services => services
            .RemoveAll<DiagnosticSubscription>()
            .RemoveAll<IEventsSourceFactory>()
            .Singleton<DiagnosticSubscription>(_ => new DiagnosticSubscription(configuration))
            .Singleton<IEventsSourceFactory>(resolver =>
            {
                // Ensure subscription exists before creating sources. Resolver owns both singleton services.
                resolver.Resolve<DiagnosticSubscription>();
                return new EventsSourceFactory();
            }));
        return context;
    }
}
