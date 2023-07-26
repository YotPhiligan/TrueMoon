namespace TrueMoon.Thorium;

public static class AppCreationContextExtensions
{
    public static IAppCreationContext UseSignals(this IAppCreationContext context, Action<IThoriumConfigurationContext>? action = default)
    {
        var thorium = context.GetThorium();

        thorium.SetConfigurationDelegate(action);

        return context;
    }
    
    public static IThoriumModule GetThorium(this IAppCreationContext context)
    {
        var module = context.GetModule<IThoriumModule>();
        if (module is not null) return module;
        module = new ThoriumModule(context.CreateEventsSource<ThoriumModule>());
        context.AddModule(module);
        return module;
    }

    public static IAppCreationContext UseSignalService<T>(this IAppCreationContext context)
    {
        var module = context.GetThorium();
        module.UseService<T>();
        return context;
    }
    
    public static IAppCreationContext ListenSignalService<T,TService>(this IAppCreationContext context)
        where TService: class, T
    {
        var module = context.GetThorium();
        module.ListenService<T,TService>();
        return context;
    }
}