using System.Runtime.CompilerServices;

namespace TrueMoon.Titanium;

public static class AppCreationContextExtensions
{
    /// <summary>
    /// Configure isolated processing unit
    /// <para>Depending on the settings may or may not be started in separate child process (by default in separate process)</para>
    /// <remarks>Processing unit uses the same dependencies added with <see cref="IAppConfigurationContext.AddDependencies"/> in main process. This can be overridden in processing unit configuration</remarks>
    /// </summary>
    /// <param name="context">app creation context</param>
    /// <param name="action">processing unit configuration delegate</param>
    /// <returns></returns>
    public static IAppConfigurationContext AddUnit(this IAppConfigurationContext context, Action<IAppConfigurationContext> action, Action<IUnitConfiguration>? configureAction = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        var module = context.GetTitanium();

        module.AddUnitConfiguration(action, configureAction);

        return context;
    }
    
    /// <summary>
    /// Configure isolated processing unit
    /// <para>Depending on the settings may or may not be started in separate child process (by default in separate process)</para>
    /// <remarks>Processing unit uses the same dependencies added with <see cref="IAppConfigurationContext.AddDependencies"/> in main process. This can be overridden in processing unit configuration</remarks>
    /// </summary>
    /// <param name="context">app creation context</param>
    /// <param name="action">processing unit configuration delegate</param>
    /// <returns></returns>
    public static IAppConfigurationContext AddUnit(this IAppConfigurationContext context, Action<IUnitConfiguration> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var module = context.GetTitanium();

        module.AddUnitConfiguration(_ => {}, action);

        return context;
    }

    public static ITitaniumModule GetTitanium(this IAppConfigurationContext context)
    {
        var module = context.GetModule<ITitaniumModule>();
        if (module is not null) return module;
        module = new TitaniumModule(context.CreateEventsSource<TitaniumModule>());
        context.AddModule(module);
        return module;
    }
    
    public static IAppConfigurationContext UseProcessingUnits(this IAppConfigurationContext context)
    {
        _ = context.GetTitanium();
        return context;
    }
}