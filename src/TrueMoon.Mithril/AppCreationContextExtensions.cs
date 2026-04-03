using TrueMoon.Configuration;

namespace TrueMoon.Mithril;

public static class AppCreationContextExtensions
{
    private static readonly List<(int index, Action<IAppConfigurationContext> action, Action<IUnitConfiguration>? config)> List = [];

    internal static IReadOnlyList<(int index, Action<IUnitConfiguration>? config)> GetUnitConfigurations() =>
        List.Select(t => (t.index, t.config)).ToList();
    
    /// <summary>
    /// Configure isolated processing unit
    /// <para>Depending on the settings may or may not be started in separate child process (by default in separate process)</para>
    /// <remarks>Processing unit uses the same dependencies added with <see cref="IAppConfigurationContext.AddDependencies"/> in main process. This can be overridden in processing unit configuration</remarks>
    /// </summary>
    /// <param name="context">app creation context</param>
    /// <param name="action">processing unit configuration delegate</param>
    /// <returns></returns>
    public static IAppConfigurationContext AddUnit(this IAppConfigurationContext context, Action<IAppConfigurationContext> action, Action<IUnitConfiguration>? configureAction = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        List.Add((List.Count, action, configureAction));
        
        context.Services((configuration, ctx) =>
        {
            if (configuration.Get<bool>("mithril_configured"))
            {
                return;
            }
            
            if (configuration.IsProcessingUnit()
                && configuration.GetProcessingUnitId() is { } id)
            {
                var v = List.FirstOrDefault(t => t.index == id);
                
                v.action(context);

                if (configuration.GetProcessingUnitParentId() is {} p)
                {
                    if (!ctx.Exist<IStartable,UnitParentProcessEventsHandler>())
                    {
                        ctx.Singleton<IStartable,UnitParentProcessEventsHandler>();
                    }
                }

                configuration.Set(true, "mithril_configured");
                return;
            }
        
            var s1 = configuration.Get<string>("-ud",ConfigurationSectionNames.CommandLineArguments);
            if (s1 == "1")
            {
                //_eventsSource.Write(()=>"ud mode","Configured");
                return;
            }

            if (!configuration.IsProcessingUnit())
            {
                if (!ctx.Exist<IUnitsController>())
                {
                    ctx.Composite<UnitsController, IUnitsController, IStartable, IStoppable>();
                    
                    configuration.Set(true, "mithril_configured");
                }
            }
            
            //_eventsSource.Trace("Configured");
        });

        return context;
    }
}