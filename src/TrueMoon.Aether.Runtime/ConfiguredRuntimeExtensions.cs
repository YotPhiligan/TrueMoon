using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Loading;
using TrueMoon.Aether.Runtime.Ecs.Rendering;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Runtime.Rendering.Resources;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Configuration;
using TrueMoon.Services;

namespace TrueMoon.Aether.Runtime;

/// <summary>Connects prepared settings to existing factories, validation and ownership.</summary>
public static class ConfiguredRuntimeExtensions
{
    /// <summary>Binds child engine budgets while retaining the protected bootstrap's identity, report root and delivery capture.</summary>
    /// <param name="context">Child App context.</param><param name="bootstrap">Frozen pre-App launch.</param><param name="configure">Pure budget overrides.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseConfiguredAetherRuntime(this IAppConfigurationContext context, RuntimeBootstrap bootstrap,
        Action<RuntimeSettings>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(bootstrap); ArgumentNullException.ThrowIfNull(bootstrap.Settings);
        var frozen = bootstrap with { Settings = bootstrap.Settings with { }, Launch = bootstrap.Launch with
            { DiagnosticDelivery = bootstrap.Launch.DiagnosticDelivery?.Snapshot() } };
        return ConfigurationModuleBinding.Configure(context, RuntimeConfigurationSchemas.Engine, (app, data) =>
        {
            app.UseAetherRuntime(options =>
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
    /// <summary>Registers prepared World limits; products keep existing caller ownership.</summary>
    /// <param name="context">App context.</param><param name="configure">Pure data overrides.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseConfiguredAetherEcs(this IAppConfigurationContext context, Action<WorldOptions>? configure = null)
        => ConfigurationModuleBinding.Configure(context, RuntimeConfigurationSchemas.World, (app, value) => app.UseAetherEcs(options =>
        {
            options.InitialEntityCapacity = value.InitialEntityCapacity;
            options.MaximumEntityCount = value.MaximumEntityCount;
            options.MaximumComponentTypes = value.MaximumComponentTypes;
        }), configure);
    /// <summary>Registers prepared Scheduler limits; products keep existing caller ownership.</summary>
    /// <param name="context">App context.</param><param name="systems">Explicit system graph.</param><param name="configure">Pure data overrides.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseConfiguredAetherEcsSystems(this IAppConfigurationContext context, Action<EcsSystemsBuilder> systems, Action<EcsSchedulerOptions>? configure = null)
        => ConfigurationModuleBinding.Configure(context, RuntimeConfigurationSchemas.Scheduler, (app, value) => app.UseAetherEcsSystems(systems, options =>
        {
            options.MaximumSystems = value.MaximumSystems;
            options.MaximumCommandsPerPhase = value.MaximumCommandsPerPhase;
        }), configure);
    /// <summary>Registers prepared SceneLoading limits; products keep existing caller ownership.</summary>
    /// <param name="context">App context.</param><param name="registryFactory">Frozen bindings factory.</param><param name="configure">Pure data overrides.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseConfiguredAetherSceneLoading(this IAppConfigurationContext context, Func<IServiceResolver, ComponentRegistry> registryFactory, Action<SceneWorldLoadOptions>? configure = null)
        => ConfigurationModuleBinding.Configure(context, RuntimeConfigurationSchemas.SceneLoading, (app, value) => app.UseAetherSceneLoading(registryFactory, options =>
        {
            options.MaximumEntities = value.MaximumEntities;
            options.MaximumComponentInstances = value.MaximumComponentInstances;
            options.MaximumPropertyValues = value.MaximumPropertyValues;
            options.MaximumPreparedResources = value.MaximumPreparedResources;
        }), configure);
    /// <summary>Registers prepared RenderResources limits; products keep existing caller ownership.</summary>
    /// <param name="context">App context.</param><param name="configure">Pure data overrides.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseConfiguredAetherRenderResources(this IAppConfigurationContext context, Action<RenderResourceOptions>? configure = null)
        => ConfigurationModuleBinding.Configure(context, RuntimeConfigurationSchemas.RenderResources, (app, value) => app.UseAetherRenderResources(options =>
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
    /// <summary>Registers the required surface view using the same prepared render-resource schema and priorities.</summary>
    /// <param name="context">Child/Player App context.</param><param name="configure">Pure data overrides.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseConfiguredAetherSurfaceRendering(this IAppConfigurationContext context, Action<RenderResourceOptions>? configure = null)
        => ConfigurationModuleBinding.Configure(context, RuntimeConfigurationSchemas.RenderResources, (app, value) => app.UseAetherSurfaceRendering(options =>
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
    /// <summary>Registers prepared Primitives limits; products keep existing caller ownership.</summary>
    /// <param name="context">App context.</param><param name="configure">Pure data overrides.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseConfiguredAetherPrimitiveEcs(this IAppConfigurationContext context, Action<PrimitiveSimulationOptions>? configure = null)
        => ConfigurationModuleBinding.Configure(context, RuntimeConfigurationSchemas.Primitives, (app, value) => app.UseAetherPrimitiveEcs(options =>
        {
            options.MaximumTransformEntities = value.MaximumTransformEntities;
            options.MaximumPrimitives = value.MaximumPrimitives;
            options.FixedStep = value.FixedStep;
            options.Camera = value.Camera;
            options.FieldOfView = value.FieldOfView;
            options.Near = value.Near;
            options.Far = value.Far;
        }), configure);
    /// <summary>Binds user runtime settings with explicitly supplied identity and borrowed factories.</summary>
    public static IAppConfigurationContext UseConfiguredAetherRuntime(this IAppConfigurationContext context, RuntimeComposition composition,
        Action<RuntimeSettings>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(composition); var frozen = composition with { };
        return ConfigurationModuleBinding.Configure(context, RuntimeConfigurationSchemas.Engine, (app, data) => app.UseAetherRuntime(options =>
        { frozen.ApplyTo(options); data.ApplyTo(options); }), configure);
    }
}
