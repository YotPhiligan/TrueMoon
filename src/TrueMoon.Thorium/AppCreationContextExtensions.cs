namespace TrueMoon.Thorium;

public static class AppCreationContextExtensions
{
    public static IAppConfigurationContext UseInvocations(this IAppConfigurationContext context, Action<IThoriumConfigurationContext>? action = default)
    {
        var thorium = context.GetThorium();

        thorium.SetConfigurationDelegate(action);

        return context;
    }
    
    public static IThoriumModule GetThorium(this IAppConfigurationContext context)
    {
        var module = context.GetModule<IThoriumModule>();
        if (module is not null) return module;
        module = new ThoriumModule(context.CreateEventsSource<ThoriumModule>());
        context.AddModule(module);
        return module;
    }

    public static IAppConfigurationContext UseInvocationService<T>(this IAppConfigurationContext context)
    {
        var module = context.GetThorium();
        module.UseService<T>();
        return context;
    }
    
    public static IAppConfigurationContext ListenInvocationService<T,TService>(this IAppConfigurationContext context)
        where TService: class, T
    {
        var module = context.GetThorium();
        module.ListenService<T,TService>();
        return context;
    }
}