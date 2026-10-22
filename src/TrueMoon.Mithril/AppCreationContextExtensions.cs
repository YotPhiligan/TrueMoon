using System.Runtime.CompilerServices;
using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Services;
using TrueMoon.Mithril.Units;

namespace TrueMoon.Mithril;

/// <summary>Configures app-local processing units.</summary>
public static class AppCreationContextExtensions
{
    private sealed record Definition(IUnitConfiguration Configuration, Action<IAppConfigurationContext> Action);
    private sealed class Definitions { internal readonly List<Definition> Items = []; }
    private static readonly ConditionalWeakTable<IAppConfigurationContext, Definitions> Registries = new();

    /// <summary>Adds an app-owned unit; indexes start at one in each App configuration context.</summary>
    /// <param name="context">The application's configuration context.</param>
    /// <param name="action">Services/configuration applied to this unit, or the parent graph for main hosting.</param>
    /// <param name="configureAction">Hosting and lifecycle settings; defaults remain child/immediate/app/always.</param>
    /// <returns>The same application configuration context.</returns>
    public static IAppConfigurationContext AddUnit(this IAppConfigurationContext context,
        Action<IAppConfigurationContext> action, Action<IUnitConfiguration>? configureAction = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(action);
        if (context is UnitContext) throw new NotSupportedException("Nested unit declarations are unsupported.");
        var definitions = Registries.GetValue(context, _ => new Definitions());
        lock (definitions)
        {
            var unit = new UnitConfiguration(definitions.Items.Count + 1);
            configureAction?.Invoke(unit);
            definitions.Items.Add(new Definition(unit, action));
            if (definitions.Items.Count == 1)
                context.Configuration(configuration => Prepare(context, configuration, definitions));
        }
        return context;
    }

    private static void Prepare(IAppConfigurationContext context, IConfiguration configuration, Definitions definitions)
    {
        Definition[] declared;
        lock (definitions) declared = definitions.Items.ToArray();
        var snapshots = UnitConfigurationSnapshot.Create(declared.Select(d => d.Configuration));
        var unitContext = new UnitContext(context, configuration);
        if (configuration.IsProcessingUnit())
        {
            var id = configuration.GetProcessingUnitId() ?? throw new ArgumentException("Invalid processing unit id.");
            var index = Array.FindIndex(snapshots.ToArray(), u => u.Index == id);
            if (index < 0) throw new ArgumentException($"Unknown processing unit id {id}.");
            declared[index].Action(unitContext);
            if (configuration.Get<string>(UnitsConfigurationExtensions.UnitParentIdArg) is { } parent)
            {
                if (!int.TryParse(parent, out var parentId) || parentId <= 0 || parentId == Environment.ProcessId)
                    throw new ArgumentException("Invalid parent process id.");
                context.Services(r => r
                    .Singleton<IFactory<UnitParentProcessEventsHandler>>(resolver => new Factory<UnitParentProcessEventsHandler>(() =>
                        new(configuration, resolver.Resolve<IAppLifetime>(), resolver.Resolve<IEventsSource<UnitParentProcessEventsHandler>>())))
                    .Singleton<UnitParentProcessEventsHandler>(resolver => resolver.Resolve<IFactory<UnitParentProcessEventsHandler>>().Create()!)
                    .Singleton<IStartable>(resolver => new AppServiceLifecycle(startable: resolver.Resolve<UnitParentProcessEventsHandler>()))
                    .Singleton<IStoppable>(resolver => new AppServiceLifecycle(stoppable: resolver.Resolve<UnitParentProcessEventsHandler>())));
            }
            return;
        }
        if (configuration.Get<string>("-ud", ConfigurationSectionNames.CommandLineArguments) == "1") return;
        for (var i = 0; i < snapshots.Count; i++)
            if (snapshots[i].HostingPolicy == UnitHostingPolicy.MainProcess) declared[i].Action(unitContext);
        context.Services(r =>
        {
            r.Instance<IReadOnlyList<IUnitConfiguration>>(snapshots);
            if (!r.Exist<IUnitsController>())
            {
                if (!r.Exist<IFactory<IUnitHandle, IUnitConfiguration>>())
                    r.Singleton<IFactory<IUnitHandle, IUnitConfiguration>>(resolver => new UnitHandleFactory(resolver.Resolve<IEventsSource<UnitsController>>()));
                r.Singleton<IFactory<UnitsController>>(resolver => new Factory<UnitsController>(() =>
                    new(resolver.Resolve<IEventsSource<UnitsController>>(), resolver.Resolve<IAppLifetime>(), snapshots,
                        resolver.Resolve<IFactory<IUnitHandle, IUnitConfiguration>>())))
                .Singleton<UnitsController>(resolver => resolver.Resolve<IFactory<UnitsController>>().Create()!)
                .Singleton<IUnitsController>(resolver => new BorrowedUnitsController(resolver.Resolve<UnitsController>()))
                .Singleton<IStartable>(resolver => new AppServiceLifecycle(startable: resolver.Resolve<UnitsController>()))
                .Singleton<IStoppable>(resolver => new AppServiceLifecycle(stoppable: resolver.Resolve<UnitsController>()));
            }
        });
    }

    private sealed class UnitContext(IAppConfigurationContext parent, IConfiguration configuration) : IAppConfigurationContext
    {
        public IAppConfigurationContext Services(Action<IServicesRegistrationContext> action) { parent.Services(action); return this; }
        public IAppConfigurationContext Services(Action<IConfiguration, IServicesRegistrationContext> action) { parent.Services(action); return this; }
        public IAppConfigurationContext Configuration(Action<IConfiguration> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            action(configuration);
            return this;
        }
    }
}
