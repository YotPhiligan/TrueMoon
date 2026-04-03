using TrueMoon.Aluminum;
using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;

namespace TrueMoon.Alloy;

public class AlloyModule
{
    private readonly IEventsSource<AlloyModule> _eventsSource;

    public AlloyModule(IEventsSource<AlloyModule> eventsSource)
    {
        _eventsSource = eventsSource;

        Configuration = new PresentationConfiguration();
    }
    
    public string Name => nameof(AlloyModule);
    
    public void Configure(IAppConfigurationContext context)
    {
        context.Services(registrationContext => registrationContext
            .Singleton<IViewManager,ViewManager>()
            .Singleton<IFactory<IGraphicsPlatform>, GlGraphicsPlatformFactory>()
            .Singleton<IFactory<IViewPresenter>, SkiaGlViewPresenterFactory>()
            .Singleton<IFactory<IContentPresenter>, SkiaContentPresenterFactory>()
            .Singleton<IFactory<IViewHandle>, ViewHandleFactory>()
            .Singleton<IVisualTreeBuilder, VisualTreeBuilder>()
        );

        if (Configuration.StartupViewType is { IsAbstract: true } or { IsInterface: true })
        {
            throw new InvalidOperationException($"\"{nameof(Configuration.StartupViewType)}\" is abstract or interface");
        }

        if (Configuration.StartupViewType != null && !typeof(IView).IsAssignableFrom(Configuration.StartupViewType))
        {
            throw new InvalidOperationException($"\"{nameof(Configuration.StartupViewType)}\" is not \"{typeof(IView)}\"");
        }
        
        if (Configuration.StartupViewType is { IsClass: true, IsAbstract: false })
        {
            context.Services(registrationContext => registrationContext
                .Singleton(Configuration.StartupViewType, Configuration.StartupViewType)
            );
        }
    }

    public void Execute(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        var viewManager = serviceProvider.Resolve<IViewManager>()!;
     
        if (Configuration.StartupViewType is not null)
        {
            IView startupView = (IView)serviceProvider.GetService(Configuration.StartupViewType)!;
            viewManager.Show(startupView);
        }
        else
        {
            viewManager.ShowEmpty();
        }
    }

    public PresentationConfiguration Configuration { get; }
}