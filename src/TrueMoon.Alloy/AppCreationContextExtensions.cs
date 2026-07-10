using TrueMoon.Argentis;

namespace TrueMoon.Alloy;

public static class AppCreationContextExtensions
{
    public static IAppConfigurationContext UsePresentation<TView>(this IAppConfigurationContext context, Action<PresentationConfiguration>? configurationDelegate = null)
        where TView : class, IView
    {
        context.ConfigurePresentationBase();
        
        var configuration = new PresentationConfiguration();
        configurationDelegate?.Invoke(configuration);
        configuration.StartupViewType ??= typeof(TView);

        context.Services(registrationContext => registrationContext
            .Singleton<TView>()
            .Singleton<IView>(t => t.Resolve<TView>())
            .Instance(configuration)
        );
        
        return context;
    }
    
    private static void ConfigurePresentationBase(this IAppConfigurationContext context)
    {
        context.Services(registrationContext => registrationContext
            .Singleton<IViewManager,ViewManager>()
            .Singleton<IFactory<IGraphicsPlatform>, GlGraphicsPlatformFactory>()
            .Singleton<IFactory<IViewPresenter>, SkiaGlViewPresenterFactory>()
            .Singleton<IFactory<IContentPresenter>, SkiaContentPresenterFactory>()
            .Singleton<IFactory<IViewHandle>, ViewHandleFactory>()
            .Singleton<IVisualTreeBuilder, VisualTreeBuilder>()
            .Composite<PresentationInitializer,IPresentationInitializer,IStartable>()
        );
    }
}