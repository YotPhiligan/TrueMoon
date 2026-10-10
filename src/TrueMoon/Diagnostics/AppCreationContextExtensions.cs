using TrueMoon.Configuration;

namespace TrueMoon.Diagnostics;

public static class AppCreationContextExtensions
{
    public static IAppConfigurationContext UseDiagnostics(this IAppConfigurationContext context, Action<IDiagnosticsConfiguration>? action = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var configuration = new DiagnosticsConfiguration();
        configuration.AddFilter("TrueMoon");
        action?.Invoke(configuration);
        context.Configuration(conf => conf.Set<IDiagnosticsConfiguration>(ConfigurationExtensions.DiagnosticsConfigurationName, configuration));
        context.Services(services => services
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
