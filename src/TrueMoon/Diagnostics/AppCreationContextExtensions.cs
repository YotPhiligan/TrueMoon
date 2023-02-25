using TrueMoon.Configuration;
using TrueMoon.Dependencies;

namespace TrueMoon.Diagnostics;

public static class AppCreationContextExtensions
{
    public static IAppCreationContext UseDiagnostics(this IAppCreationContext context, Action<IDiagnosticsConfiguration>? action = default)
    {
        var configuration = new DiagnosticsConfiguration();
        configuration.AddFilters("TrueMoon");
        action?.Invoke(configuration);
        var subscription = new DiagnosticSubscription(configuration);
        context.Configuration.Set<IDiagnosticsConfiguration>(ConfigurationExtensions.DiagnosticsConfigurationName, configuration);
        context.AddDependencies(t => t.Add(subscription));
        return context;
    }
}