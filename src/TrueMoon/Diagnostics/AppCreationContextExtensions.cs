using TrueMoon.Configuration;

namespace TrueMoon.Diagnostics;

public static class AppCreationContextExtensions
{
    /// <summary>Enables diagnostics owned by the application. Repeated calls replace earlier settings and registrations.</summary>
    /// <param name="context">Application setup context.</param>
    /// <param name="action">Optional settings for the replacement diagnostics configuration.</param>
    /// <returns>The application setup context.</returns>
    public static IAppConfigurationContext UseDiagnostics(this IAppConfigurationContext context, Action<IDiagnosticsConfiguration>? action = default)
        => UseDiagnosticsCore(context, null, action);

    /// <summary>Enables App-owned bounded history and asynchronous sinks alongside optional synchronous legacy observers.</summary>
    /// <param name="context">Application setup context.</param>
    /// <param name="options">Copied budgets and sink factories. No resources are created during setup.</param>
    /// <param name="action">Optional legacy listener/discovery configuration.</param>
    /// <returns>The application setup context.</returns>
    public static IAppConfigurationContext UseDiagnostics(this IAppConfigurationContext context,
        DiagnosticPipelineOptions options, Action<IDiagnosticsConfiguration>? action = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        return UseDiagnosticsCore(context, options.Snapshot(), action);
    }

    private static IAppConfigurationContext UseDiagnosticsCore(IAppConfigurationContext context,
        DiagnosticPipelineOptions? options, Action<IDiagnosticsConfiguration>? action)
    {
        ArgumentNullException.ThrowIfNull(context);
        var configuration = new DiagnosticsConfiguration();
        configuration.AddFilter("TrueMoon");
        action?.Invoke(configuration);
        context.Configuration(conf => conf.Set<IDiagnosticsConfiguration>(ConfigurationExtensions.DiagnosticsConfigurationName, configuration));
        context.Services(services =>
        {
            services.RemoveAll<DiagnosticPipeline>();
            services.RemoveAll<DiagnosticRecordAccess>().RemoveAll<IDiagnosticRecordReader>().RemoveAll<IDiagnosticRecordImporter>();
            if (options is not null)
                services.Singleton<DiagnosticPipeline>(_ => new DiagnosticPipeline(options))
                    .Singleton<DiagnosticRecordAccess>(resolver => new(resolver.Resolve<DiagnosticPipeline>()))
                    .Singleton<IDiagnosticRecordReader>(resolver => resolver.Resolve<DiagnosticRecordAccess>())
                    .Singleton<IDiagnosticRecordImporter>(resolver => resolver.Resolve<DiagnosticRecordAccess>());
            services.RemoveAll<IDiagnosticTelemetry>().Singleton<IDiagnosticTelemetry>(resolver =>
                new DiagnosticTelemetry(options?.Telemetry, options is null ? null : resolver.Resolve<DiagnosticPipeline>().ApplicationId));
            services
            .RemoveAll<DiagnosticSubscription>()
            .RemoveAll<IEventsSourceFactory>()
            .Singleton<DiagnosticSubscription>(resolver => new DiagnosticSubscription(configuration,
                options is null ? null : resolver.Resolve<DiagnosticPipeline>()))
            .Singleton<IEventsSourceFactory>(resolver =>
            {
                // Ensure subscription exists before creating sources. Resolver owns both singleton services.
                resolver.Resolve<DiagnosticSubscription>();
                return new EventsSourceFactory();
            });
        });
        return context;
    }
}
