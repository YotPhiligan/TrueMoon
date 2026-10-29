using TrueMoon.Alloy.Runtime;
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
    /// <summary>Registers a window whose owner-thread root is created by an injected factory.</summary>
    /// <typeparam name="TView">Root element.</typeparam>
    /// <param name="app">Application configuration.</param><param name="configure">Window and renderer settings.</param>
    /// <returns>The application configuration.</returns>
    public static IAppConfigurationContext UsePresentationFactory<TView>(this IAppConfigurationContext app,
        Action<AlloyOptions> configure) where TView : Element
        => UsePresentation(app, resolver => resolver.Resolve<IFactory<TView>>().Create()
            ?? throw new InvalidOperationException("Root factory returned null."), configure);
    private static void RegisterWindow(IAppConfigurationContext app, AlloyOptions options, Func<IServiceResolver, Element> rootFactory)
    {
        app.Services(services => services.Singleton<HostedUiDispatcher>(_ => new HostedUiDispatcher())
            .Singleton<HostedUiWindow>(resolver =>
            {
                // Cleanup can run while the resolver is disposing a partially started window.
                // Capture the borrowed lifetime now; callbacks must not resolve services during teardown.
                var lifetime = resolver.Resolve<IAppLifetime>();
                return new HostedUiWindow(options.Window!, resolver.Resolve<IUiSessionFactory>(),
                    () => rootFactory(resolver), lifetime.Cancel, resolver.Resolve<HostedUiDispatcher>());
            })
            .Singleton<IStartable>(resolver => new AppServiceLifecycle(startable: resolver.Resolve<HostedUiWindow>()))
            .Singleton<IStoppable>(resolver => new AppServiceLifecycle(stoppable: resolver.Resolve<HostedUiWindow>()))
            .Singleton<IStoppable>(resolver => new AppServiceLifecycle(stoppable: resolver.Resolve<HostedUiDispatcher>())));
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
                .Singleton<IUiSessionFactory>(resolver => new BorrowedUiSessionFactory(resolver.Resolve<HostedUiSessionFactory>()));
            services.Singleton<IFactory<UiSession, UiSessionCreation>>(resolver =>
            {
                var factory = (IFactory<UiSession, UiSessionCreation>)resolver.Resolve<IUiSessionFactory>();
                return new Factory<UiSession, UiSessionCreation>(factory.Create);
            });
            if (options.ExternalHost) services.Singleton<IStoppable>(resolver => new AppServiceLifecycle(stoppable: resolver.Resolve<HostedUiSessionFactory>()));
        });
    }
}
