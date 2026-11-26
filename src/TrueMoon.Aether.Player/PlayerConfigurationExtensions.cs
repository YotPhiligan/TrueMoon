using TrueMoon.Argentis.Styling;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Player.Scenarios;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis.Views;
using TrueMoon.Configuration;

namespace TrueMoon.Aether.Player;

/// <summary>Explicit Player App/window composition. No Editor package or process-host graph is needed.</summary>
public static class PlayerConfigurationExtensions
{
    /// <summary>Registers one local runtime, typed View/data/style/factory and owned window loop.</summary>
    /// <param name="context">Player App.</param><param name="start">Captured deployment/scene.</param><param name="fonts">Borrowed immutable font snapshots.</param>
    /// <param name="configure">Pure startup window options, copied now.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseAetherPlayer(this IAppConfigurationContext context,PlayerStart start,SkiaFontCatalog fonts,Action<PlayerOptions>? configure=null)
    {
        ArgumentNullException.ThrowIfNull(context);ArgumentNullException.ThrowIfNull(fonts);
        var options=new PlayerOptions();configure?.Invoke(options);var settings=options.Snapshot();
        context.UseAetherLocalRuntime(start);
        context.Services(services=>
        {
            services.Singleton<PlayerInput>(_ => new())
                .Singleton<PlayerViewModel>(_ => new(settings.Title));
            services.Singleton<PlayerViewport>(_ => new());

            if (!services.Exist<IFactory<PlayerCanvas>>())
                services.Singleton<IFactory<PlayerCanvas>>(resolver =>
                {
                    var input = resolver.Resolve<PlayerInput>();
                    return new Factory<PlayerCanvas>(() => new(input));
                });

            if (!services.Exist<IFactory<PlayerView>>())
                services.View<PlayerView, PlayerViewStyle>();
            else if (!services.Exist<IViewStyle<PlayerView>>())
                services.Singleton<IViewStyle<PlayerView>>(_ => new PlayerViewStyle());
        });
        context.UsePresentation(
            static resolver =>
            {
                var view = resolver.Resolve<IFactory<PlayerView>>().Create()
                    ?? throw new InvalidOperationException("The Player view factory returned no view.");
                return resolver.Resolve<PlayerViewport>().Track(view);
            },
            options => options.UseControlFactory<PlayerCanvas>()
                .ConfigurePresenters(PlayerPresenters.Register)
                .UseSkiaOpenGL(fonts)
                .UseSilkWindow(window =>
                {
                    window.Width = settings.Width;
                    window.Height = settings.Height;
                    window.Title = settings.Title;
                }));
        context.Services(services=>
        {
            services.Singleton<PlayerWindow>(resolver => new(resolver.Resolve<HostedUiWindow>(), resolver.Resolve<PlayerSession>(),
                resolver.Resolve<PlayerInput>(), resolver.Resolve<PlayerViewport>(), settings));
            if (settings.ManualTicks)
                services.Singleton<IPlayerScenarioDriver>(resolver => new WindowScenarioDriver(resolver.Resolve<PlayerWindow>()));
            services.Singleton<IStartable>(resolver=>new AppServiceLifecycle(startable:resolver.Resolve<PlayerWindow>()));
            services.Singleton<IStoppable>(resolver=>new AppServiceLifecycle(stoppable:resolver.Resolve<PlayerWindow>()));
        });
        return context;
    }

    /// <summary>Registers an App-owned local game and a manually stepped scenario driver without a window or surface.</summary>
    /// <param name="context">Player App configuration.</param>
    /// <param name="start">Captured game Build and scene.</param>
    /// <returns>The same context.</returns>
    public static IAppConfigurationContext UseAetherHeadlessPlayer(this IAppConfigurationContext context, PlayerStart start)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.UseAetherLocalRuntime(start);
        context.Services(services =>
            services.Singleton<IPlayerScenarioDriver>(resolver => new HeadlessScenarioDriver(resolver.Resolve<PlayerSession>())));
        return context;
    }
    /// <summary>Uses the standard prepared PlayerOptions.Schema and explicit sources; artifact identity stays outside configuration providers.</summary>
    /// <param name="context">App.</param><param name="start">Captured deployment/scene.</param><param name="fonts">Fonts.</param><param name="configure">Pure explicit overrides.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseConfiguredAetherPlayer(this IAppConfigurationContext context,PlayerStart start,SkiaFontCatalog fonts,Action<PlayerOptions>? configure=null)
        =>ConfigurationModuleBinding.Configure(context,PlayerOptions.Schema,(app,value)=>app.UseAetherPlayer(start,fonts,options=>
        {options.Width=value.Width;options.Height=value.Height;options.Title=value.Title;options.Validation=value.Validation;}),configure);
}
