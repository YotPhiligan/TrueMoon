using TrueMoon.Argentis;
using TrueMoon.Services;

namespace TrueMoon.Alloy.Hosting;

/// <summary>Opt-in UI integration with TrueMoon App. Games retain ownership of devices and frame loops.</summary>
public static class AppConfigurationExtensions
{
    /// <summary>Registers renderer/session services for an external game host, without creating a window/device.</summary>
    public static IAppConfigurationContext UseAlloy(this IAppConfigurationContext app, Action<AlloyOptions> configure)
    {
        var options = Configure(configure, false);
        Register(app, options);
        return app;
    }
    /// <summary>Registers a standalone UI window with explicit platform and renderer selection.</summary>
    public static IAppConfigurationContext UsePresentation<TView>(this IAppConfigurationContext app, Action<AlloyOptions> configure) where TView : Element
    {
        var options = Configure(configure, true);
        Register(app, options);
        app.Services(services => services.Transient<TView>()
            .Singleton<HostedUiWindow>(resolver => new HostedUiWindow(options.Window!, resolver.Resolve<IUiSessionFactory>(), () => resolver.Resolve<TView>(), () => resolver.Resolve<IAppLifetime>().Cancel()))
            .Singleton<IStartable>(resolver => resolver.Resolve<HostedUiWindow>())
            .Singleton<IStoppable>(resolver => resolver.Resolve<HostedUiWindow>()));
        return app;
    }
    private static AlloyOptions Configure(Action<AlloyOptions> configure, bool presentation)
    { ArgumentNullException.ThrowIfNull(configure); var options = new AlloyOptions(); configure(options); options.Validate(presentation); return options; }
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
