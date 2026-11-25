using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Views;
using TrueMoon.Argentis.Windows;
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
    /// <summary>Registers a standalone window whose root is created on the UI owner thread by a registered factory.</summary>
    /// <typeparam name="TView">Root element.</typeparam>
    /// <param name="app">Application configuration.</param>
    /// <param name="configure">Selects the window and renderer; settings are snapshotted during registration.</param>
    /// <returns>The application configuration for chaining.</returns>
    /// <remarks>Register an <see cref="IFactory{TView}"/> explicitly or with View registration.
    /// The window owns the fresh root returned by the factory.</remarks>
    public static IAppConfigurationContext UsePresentation<TView>(this IAppConfigurationContext app,
        Action<AlloyOptions> configure) where TView : Element
        => UsePresentation(app, static resolver => resolver.Resolve<IFactory<TView>>().Create()!, configure);

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
            if (!services.Exist<IUiControlFactories>()) services.Singleton<IUiControlFactories>(_ => new UiControlFactories());
            if (!services.Exist<IFactory<IElementPresenter<IconButtonPaintState>>>())
                services.Singleton<IFactory<IElementPresenter<IconButtonPaintState>>>(_ => StockPresenters.StockIconFactory);
            services.Singleton<IRenderBackend>(_ => options.BackendFactory!())
                .Singleton<HostedUiSessionFactory>(resolver =>
                {
                    var icon = resolver.Resolve<IFactory<IElementPresenter<IconButtonPaintState>>>();
                    var configure = options.BindPresenters(resolver, ReferenceEquals(icon, StockPresenters.StockIconFactory) ? null : icon);
                    return new HostedUiSessionFactory(resolver.Resolve<IRenderBackend>(), resolver.Resolve<IUiControlFactories>(), configure);
                })
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
