using TrueMoon.Aether.Runtime;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;
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
            services.Singleton<PlayerInput>(_=>new());services.Singleton<PlayerViewModel>(_=>new(settings.Title));
            if(!services.Exist<IViewStyle<PlayerView>>())services.Singleton<IViewStyle<PlayerView>>(_=>new PlayerViewStyle());
            if(!services.Exist<IFactory<PlayerView>>())services.Singleton<IFactory<PlayerView>>(resolver=>
            {
                var data=resolver.Resolve<PlayerViewModel>();var input=resolver.Resolve<PlayerInput>();var style=resolver.Resolve<IViewStyle<PlayerView>>();
                return new Factory<PlayerView>(()=>new(data,input,style));
            });
        });
        context.UsePresentationFactory<PlayerView>(options=>
        {options.UseSkiaOpenGL(fonts);options.UseSilkWindow(window=>{window.Width=settings.Width;window.Height=settings.Height;window.Title=settings.Title;});});
        context.Services(services=>
        {
            services.Singleton<PlayerWindow>(resolver=>new(resolver.Resolve<HostedUiWindow>(),resolver.Resolve<PlayerSession>(),resolver.Resolve<PlayerInput>(),settings));
            services.Singleton<IStartable>(resolver=>new AppServiceLifecycle(startable:resolver.Resolve<PlayerWindow>()));
            services.Singleton<IStoppable>(resolver=>new AppServiceLifecycle(stoppable:resolver.Resolve<PlayerWindow>()));
        });
        return context;
    }
    /// <summary>Uses the standard prepared PlayerOptions.Schema and explicit sources; artifact identity stays outside configuration providers.</summary>
    /// <param name="context">App.</param><param name="start">Captured deployment/scene.</param><param name="fonts">Fonts.</param><param name="configure">Pure explicit overrides.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseConfiguredAetherPlayer(this IAppConfigurationContext context,PlayerStart start,SkiaFontCatalog fonts,Action<PlayerOptions>? configure=null)
        =>ConfigurationModuleBinding.Configure(context,PlayerOptions.Schema,(app,value)=>app.UseAetherPlayer(start,fonts,options=>
        {options.Width=value.Width;options.Height=value.Height;options.Title=value.Title;options.Validation=value.Validation;}),configure);
}
