using TrueMoon.Aluminum;
using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;

namespace TrueMoon.Alloy;

public class AlloyModule : IAlloyModule
{
    private readonly IEventsSource<AlloyModule> _eventsSource;

    public AlloyModule(IEventsSource<AlloyModule> eventsSource)
    {
        _eventsSource = eventsSource;

        Configuration = new PresentationConfiguration();
    }

    public ModuleExecutionFlowOrder ExecutionFlowOrder => ModuleExecutionFlowOrder.End;
    
    public string Name => nameof(AlloyModule);
    
    public void Configure(IAppCreationContext context)
    {
        context.AddDependencies(registrationContext => registrationContext
            .AddSingleton<IViewManager,ViewManager>()
            .AddSingleton<IFactory<IGraphicsPlatform>, GlGraphicsPlatformFactory>()
            .AddSingleton<IFactory<IViewPresenter>, SkiaGlViewPresenterFactory>()
            .AddSingleton<IFactory<IContentPresenter>, SkiaContentPresenterFactory>()
            .AddSingleton<IFactory<IViewHandle>, ViewHandleFactory>()
            .AddSingleton<IVisualTreeBuilder, VisualTreeBuilder>()
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
            context.AddDependencies(registrationContext => registrationContext
                .Add(Configuration.StartupViewType, Configuration.StartupViewType)
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