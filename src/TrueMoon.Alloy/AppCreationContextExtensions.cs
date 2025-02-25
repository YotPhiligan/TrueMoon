using TrueMoon.Aluminum;
using TrueMoon.Dependencies;

namespace TrueMoon.Alloy;

public static class AppCreationContextExtensions
{
    public static IAppConfigurationContext UsePresentation(this IAppConfigurationContext context, Action<PresentationConfiguration>? configurationDelegate = default)
    {
        var module = context.GetAlloy();
        
        configurationDelegate?.Invoke(module.Configuration);
        
        return context;
    }
    
    public static IAppConfigurationContext UsePresentation(this IAppConfigurationContext context, Action<IView> viewConfigurationDelegate, Action<PresentationConfiguration>? configurationDelegate = default)
    {
        var module = context.GetAlloy();
        
        configurationDelegate?.Invoke(module.Configuration);
        module.Configuration.StartupViewCreationDelegate = viewConfigurationDelegate;
        
        return context;
    }
    
    public static IAppConfigurationContext UsePresentation<TView>(this IAppConfigurationContext context, Action<PresentationConfiguration>? configurationDelegate = default)
        where TView : IView
    {
        var module = context.GetAlloy();
        
        configurationDelegate?.Invoke(module.Configuration);
        module.Configuration.StartupViewType = typeof(TView);

        return context;
    }
    
    public static IAlloyModule GetAlloy(this IAppConfigurationContext context)
    {
        var module = context.GetModule<IAlloyModule>();
        if (module is not null) return module;
        module = new AlloyModule(context.CreateEventsSource<AlloyModule>());
        context.AddModule(module);
        return module;
    }
}