using TrueMoon.Argentis;
using TrueMoon.Services;

namespace TrueMoon.Alloy.Hosting;

/// <summary>Opt-in UI integration with TrueMoon App. Games retain ownership of devices and frame loops.</summary>
public static class AppConfigurationExtensions
{
    /// <summary>Registers renderer/session services for an external game host, without creating a window/device.</summary>
    public static IAppConfigurationContext UseAlloy(this IAppConfigurationContext app, Action<AlloyOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(app);
        var options = Configure(configure, false);
        Register(app, options);
        return app;
    }
    /// <summary>Registers a standalone UI window with explicit platform and renderer selection.</summary>
    public static IAppConfigurationContext UsePresentation<TView>(this IAppConfigurationContext app, Action<AlloyOptions> configure) where TView : Element
    {
        ArgumentNullException.ThrowIfNull(app);
        var options = Configure(configure, true);
        Register(app, options);
        app.Services(services => services.Transient<TView>());
        RegisterWindow(app, options, resolver => resolver.Resolve<TView>());
        return app;
    }
    /// <summary>Registers a standalone window with an explicit owner-thread root factory and renderer selection.</summary>
    /// <param name="app">Application configuration.</param>
    /// <param name="rootFactory">Creates a fresh, detached root on the UI owner thread from application services.
    /// The window owns a successfully returned root; factory exceptions remain observable through startup/Completion.</param>
    /// <param name="configure">Selects renderer and window settings, snapshotted during registration.</param>
    /// <returns>The application configuration for chaining.</returns>
    /// <remarks>No generic view registration is inferred. Register dependencies explicitly;
    /// Cobalt can use factory registrations without generating a resolver for the root view.</remarks>
    public static IAppConfigurationContext UsePresentation(this IAppConfigurationContext app,
        Func<IServiceResolver, Element> rootFactory, Action<AlloyOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(app); ArgumentNullException.ThrowIfNull(rootFactory);
        var options = Configure(configure, true);
        Register(app, options);
        RegisterWindow(app, options, rootFactory);
        return app;
    }
    private static void RegisterWindow(IAppConfigurationContext app, AlloyOptions options, Func<IServiceResolver, Element> rootFactory)
    {
        app.Services(services => services.Singleton<HostedUiDispatcher>(_ => new HostedUiDispatcher())
            .Singleton<HostedUiWindow>(resolver => new HostedUiWindow(options.Window!, resolver.Resolve<IUiSessionFactory>(),
                () => rootFactory(resolver), () => resolver.Resolve<IAppLifetime>().Cancel(), resolver.Resolve<HostedUiDispatcher>()))
            .Singleton<IStartable>(resolver => resolver.Resolve<HostedUiWindow>())
            .Singleton<IStoppable>(resolver => resolver.Resolve<HostedUiWindow>())
            .Singleton<IStoppable>(resolver => resolver.Resolve<HostedUiDispatcher>()));
    }
    private static AlloyOptions Configure(Action<AlloyOptions> configure, bool presentation)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var options = new AlloyOptions(); configure(options);
        var snapshot = options.Snapshot(); snapshot.Validate(presentation); return snapshot;
    }
    private static void Register(IAppConfigurationContext app, AlloyOptions options)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.Services(services =>
        {
            if (services.Exist<IUiSessionFactory>()) throw new InvalidOperationException("Alloy is already registered in this App.");
            services.Singleton<IRenderBackend>(_ => options.BackendFactory!())
                .Singleton<HostedUiSessionFactory>(resolver => new HostedUiSessionFactory(resolver.Resolve<IRenderBackend>()))
                .Singleton<IUiSessionFactory>(resolver => resolver.Resolve<HostedUiSessionFactory>());
            if (options.ExternalHost) services.Singleton<IStoppable>(resolver => resolver.Resolve<HostedUiSessionFactory>());
        });
    }
}
