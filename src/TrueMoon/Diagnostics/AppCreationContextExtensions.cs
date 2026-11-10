using TrueMoon.Configuration;

namespace TrueMoon.Diagnostics;

public static class AppCreationContextExtensions
{
    /// <summary>Binds standard scalar pipeline defaults before registering any diagnostic resources.</summary>
    /// <param name="context">App context with DiagnosticsConfigurationSchema.Default in its profile.</param>
    /// <param name="action">Listener and legacy source-filter composition.</param>
    /// <param name="configure">Pure explicit setting overrides.</param><param name="sinkFactories">Borrowed factories; products transfer to the pipeline.</param>
    /// <param name="emergencyWriter">Optional borrowed emergency output; never serialized or disposed by the pipeline.</param>
    /// <returns>The same context.</returns>
    public static IAppConfigurationContext UseConfiguredDiagnostics(this IAppConfigurationContext context,
        Action<IDiagnosticsConfiguration>? action = null, Action<DiagnosticsSettings>? configure = null,
        IReadOnlyList<IFactory<IDiagnosticSink>>? sinkFactories = null, TextWriter? emergencyWriter = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var factories = sinkFactories?.ToArray() ?? [];
        return context.ConfigureModule(DiagnosticsConfigurationSchema.Default, (app, settings) =>
        {
            app.Services(services => services.RemoveAll<DiagnosticsSettings>().Instance(settings));
            var options = settings.ToOptions();
            options.SinkFactories = factories;
            options.Emergency.Writer = emergencyWriter;
            UseDiagnosticsCore(app, options.Snapshot(), action, false);
        }, configure);
    }

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
        DiagnosticPipelineOptions? options, Action<IDiagnosticsConfiguration>? action, bool mirrorLegacy = true)
    {
        ArgumentNullException.ThrowIfNull(context);
        var configuration = new DiagnosticsConfiguration();
        configuration.AddFilter("TrueMoon");
        action?.Invoke(configuration);
        // The old object-bag entry is a compatibility mirror, never the resource activation mechanism.
        if (mirrorLegacy) context.Configuration(conf => conf.Set<IDiagnosticsConfiguration>(ConfigurationExtensions.DiagnosticsConfigurationName, configuration));
        context.Services(services =>
        {
            services.RemoveAll<IDiagnosticsConfiguration>().Instance<IDiagnosticsConfiguration>(configuration);
            services.RemoveAll<DiagnosticPipeline>();
            services.RemoveAll<DiagnosticRecordAccess>().RemoveAll<IDiagnosticRecordReader>().RemoveAll<IDiagnosticRecordImporter>().RemoveAll<IDiagnosticHealthReader>()
                .RemoveAll<IDiagnosticArchiveReader>().RemoveAll<IDiagnosticConfigurationReader>();
            if (options is not null)
                services.Singleton<DiagnosticPipeline>(_ => new DiagnosticPipeline(options))
                    .Singleton<DiagnosticRecordAccess>(resolver => new(resolver.Resolve<DiagnosticPipeline>()))
                    .Singleton<IDiagnosticRecordReader>(resolver => resolver.Resolve<DiagnosticRecordAccess>())
                    .Singleton<IDiagnosticRecordImporter>(resolver => resolver.Resolve<DiagnosticRecordAccess>())
                    .Singleton<IDiagnosticHealthReader>(resolver => resolver.Resolve<DiagnosticRecordAccess>())
                    .Singleton<IDiagnosticArchiveReader>(resolver => resolver.Resolve<DiagnosticRecordAccess>())
                    .Singleton<IDiagnosticConfigurationReader>(resolver => resolver.Resolve<DiagnosticRecordAccess>());
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
