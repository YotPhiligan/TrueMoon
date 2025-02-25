using TrueMoon.Configuration;
using TrueMoon.Dependencies;

namespace TrueMoon.Diagnostics;

public static class AppCreationContextExtensions
{
    public static IAppConfigurationContext UseDiagnostics(this IAppConfigurationContext context, Action<IDiagnosticsConfiguration>? action = default)
    {
        var configuration = new DiagnosticsConfiguration();
        configuration.AddFilters("TrueMoon");
        action?.Invoke(configuration);
        var subscription = new DiagnosticSubscription(configuration);
        context.Configuration(conf =>
            conf.Set<IDiagnosticsConfiguration>(ConfigurationExtensions.DiagnosticsConfigurationName, configuration));
        context.Services(t => t
            .Instance(subscription)
            .Singleton<IEventsSourceFactory, EventsSourceFactory>()
        );
        return context;
    }
}