using TrueMoon.Aluminum;
using TrueMoon.Dependencies;

namespace TrueMoon.Alloy;

public static class AppCreationContextExtensions
{
    public static IAppCreationContext UsePresentation(this IAppCreationContext context, Action<PresentationConfiguration>? configurationDelegate = default)
    {
        var module = context.GetAlloy();
        
        configurationDelegate?.Invoke(module.Configuration);
        
        return context;
    }
    
    public static IAppCreationContext UsePresentation(this IAppCreationContext context, Action<IView> viewConfigurationDelegate, Action<PresentationConfiguration>? configurationDelegate = default)
    {
        var module = context.GetAlloy();
        
        configurationDelegate?.Invoke(module.Configuration);
        module.Configuration.StartupViewCreationDelegate = viewConfigurationDelegate;
        
        return context;
    }
    
    public static IAppCreationContext UsePresentation<TView>(this IAppCreationContext context, Action<PresentationConfiguration>? configurationDelegate = default)
        where TView : IView
    {
        var module = context.GetAlloy();
        
        configurationDelegate?.Invoke(module.Configuration);
        module.Configuration.StartupViewType = typeof(TView);

        return context;
    }
    
    public static IAlloyModule GetAlloy(this IAppCreationContext context)
    {
        var module = context.GetModule<IAlloyModule>();
        if (module is not null) return module;
        module = new AlloyModule(context.CreateEventsSource<AlloyModule>());
        context.AddModule(module);
        return module;
    }
}