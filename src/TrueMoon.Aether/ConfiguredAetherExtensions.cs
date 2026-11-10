using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Rendering.Resources;
using TrueMoon.Aether.Scenes.Authoring;
using TrueMoon.Configuration;
using TrueMoon.Aether.Scenes;
using TrueMoon.Services;
using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether;

/// <summary>Connects prepared settings to existing factories, validation and ownership.</summary>
public static class ConfiguredAetherExtensions
{
    /// <summary>Binds child engine budgets while retaining the protected bootstrap's identity, report root and delivery capture.</summary>
    /// <param name="context">Child App context.</param><param name="bootstrap">Frozen pre-App launch.</param><param name="configure">Pure budget overrides.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseConfiguredAetherRuntime(this IAppConfigurationContext context, RuntimeBootstrap bootstrap,
        Action<AetherSettings>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(bootstrap); ArgumentNullException.ThrowIfNull(bootstrap.Settings);
        var frozen = bootstrap with { Settings = bootstrap.Settings with { }, Launch = bootstrap.Launch with
            { DiagnosticDelivery = bootstrap.Launch.DiagnosticDelivery?.Snapshot() } };
        return ConfigurationModuleBinding.Configure(context, AetherConfigurationSchemas.Engine, (app, data) =>
        {
            app.UseAether(options =>
            {
                options.HostContractId = frozen.Launch.Identity.HostContractId; options.GameBuildId = frozen.Launch.Identity.GameBuildId;
                data.ApplyTo(options);
            });
            if (frozen.Launch.Role == RuntimeRole.EditPreview) app.UseAetherPreviewRuntime(frozen);
            else app.UseAetherRuntime(frozen);
        }, data =>
        {
            configure?.Invoke(data);
            data.ReportDirectory = frozen.Launch.ReportDirectory;
            data.ChildDiagnostics = frozen.Launch.DiagnosticDelivery?.Snapshot() ?? new() { Enabled = false };
        });
    }
    /// <summary>Registers engine data with explicit identity and borrowed composition.</summary>
    /// <param name="context">App context.</param><param name="composition">Identity and factories.</param><param name="configure">Pure data overrides.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseConfiguredAether(this IAppConfigurationContext context, AetherComposition composition, Action<AetherSettings>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(composition); var frozen = composition with { };
        return ConfigurationModuleBinding.Configure(context, AetherConfigurationSchemas.Engine, (app, data) => app.UseAether(options =>
        { frozen.ApplyTo(options); data.ApplyTo(options); }), configure);
    }
    /// <summary>Registers prepared World limits; products keep existing caller ownership.</summary>
    /// <param name="context">App context.</param><param name="configure">Pure data overrides.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseConfiguredAetherEcs(this IAppConfigurationContext context, Action<WorldOptions>? configure = null)
        => ConfigurationModuleBinding.Configure(context, AetherConfigurationSchemas.World, (app, value) => app.UseAetherEcs(options =>
        {
            options.InitialEntityCapacity = value.InitialEntityCapacity;
            options.MaximumEntityCount = value.MaximumEntityCount;
            options.MaximumComponentTypes = value.MaximumComponentTypes;
        }), configure);
    /// <summary>Registers prepared Scheduler limits; products keep existing caller ownership.</summary>
    /// <param name="context">App context.</param><param name="systems">Explicit system graph.</param><param name="configure">Pure data overrides.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseConfiguredAetherEcsSystems(this IAppConfigurationContext context, Action<EcsSystemsBuilder> systems, Action<EcsSchedulerOptions>? configure = null)
        => ConfigurationModuleBinding.Configure(context, AetherConfigurationSchemas.Scheduler, (app, value) => app.UseAetherEcsSystems(systems, options =>
        {
            options.MaximumSystems = value.MaximumSystems;
            options.MaximumCommandsPerPhase = value.MaximumCommandsPerPhase;
        }), configure);
    /// <summary>Registers prepared SceneLoading limits; products keep existing caller ownership.</summary>
    /// <param name="context">App context.</param><param name="registryFactory">Frozen bindings factory.</param><param name="configure">Pure data overrides.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseConfiguredAetherSceneLoading(this IAppConfigurationContext context, Func<IServiceResolver, ComponentRegistry> registryFactory, Action<SceneWorldLoadOptions>? configure = null)
        => ConfigurationModuleBinding.Configure(context, AetherConfigurationSchemas.SceneLoading, (app, value) => app.UseAetherSceneLoading(registryFactory, options =>
        {
            options.MaximumEntities = value.MaximumEntities;
            options.MaximumComponentInstances = value.MaximumComponentInstances;
            options.MaximumPropertyValues = value.MaximumPropertyValues;
            options.MaximumPreparedResources = value.MaximumPreparedResources;
        }), configure);
    /// <summary>Registers prepared RenderResources limits; products keep existing caller ownership.</summary>
    /// <param name="context">App context.</param><param name="configure">Pure data overrides.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseConfiguredAetherRenderResources(this IAppConfigurationContext context, Action<RenderResourceOptions>? configure = null)
        => ConfigurationModuleBinding.Configure(context, AetherConfigurationSchemas.RenderResources, (app, value) => app.UseAetherRenderResources(options =>
        {
            options.MaximumCpuRevisions = value.MaximumCpuRevisions;
            options.MaximumCpuBytes = value.MaximumCpuBytes;
            options.MaximumCpuScopes = value.MaximumCpuScopes;
            options.MaximumCpuLeases = value.MaximumCpuLeases;
            options.MaximumGeometryBytes = value.MaximumGeometryBytes;
            options.MaximumUploadStagingBytes = value.MaximumUploadStagingBytes;
            options.MaximumCpuStagingBytes = value.MaximumCpuStagingBytes;
            options.MaximumActivePreparations = value.MaximumActivePreparations;
            options.MaximumQueuedPreparations = value.MaximumQueuedPreparations;
            options.MaximumGpuEntries = value.MaximumGpuEntries;
        }), configure);
    /// <summary>Registers prepared Authoring limits; products keep existing caller ownership.</summary>
    /// <param name="context">App context.</param><param name="configure">Pure data overrides.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseConfiguredAetherAuthoring(this IAppConfigurationContext context, Action<SceneAuthoringOptions>? configure = null)
        => ConfigurationModuleBinding.Configure(context, AetherConfigurationSchemas.Authoring, (app, value) => app.UseAetherAuthoring(options =>
        {
            options.MaxHistoryEntries = value.MaxHistoryEntries;
            options.MaxBatchEdits = value.MaxBatchEdits;
            options.MaxOperations = value.MaxOperations;
        }), configure);
    /// <summary>Registers prepared Primitives limits; products keep existing caller ownership.</summary>
    /// <param name="context">App context.</param><param name="configure">Pure data overrides.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseConfiguredAetherPrimitiveEcs(this IAppConfigurationContext context, Action<PrimitiveSimulationOptions>? configure = null)
        => ConfigurationModuleBinding.Configure(context, AetherConfigurationSchemas.Primitives, (app, value) => app.UseAetherPrimitiveEcs(options =>
        {
            options.MaximumTransformEntities = value.MaximumTransformEntities;
            options.MaximumPrimitives = value.MaximumPrimitives;
            options.FixedStep = value.FixedStep;
            options.Camera = value.Camera;
            options.FieldOfView = value.FieldOfView;
            options.Near = value.Near;
            options.Far = value.Far;
        }), configure);
}
