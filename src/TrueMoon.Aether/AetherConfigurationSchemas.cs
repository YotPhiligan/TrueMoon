using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Rendering.Resources;
using TrueMoon.Aether.Scenes.Authoring;
using TrueMoon.Configuration;

namespace TrueMoon.Aether;

/// <summary>Standard startup schemas. Session/build identity, World instances and resource factories remain explicit composition arguments.</summary>
public static class AetherConfigurationSchemas
{
    /// <summary>Engine user settings, excluding identity and factories.</summary>
    public static IConfigurationSchema<AetherSettings> Engine { get; } = ConfigurationSchemaDefinitions.Create("Aether", () => new AetherSettings(),
        value => value.Copy(), value => value.Validate(), fields =>
        {
            fields.Field("ReportDirectory", value => value.ReportDirectory, (value, next) => value.ReportDirectory = next, new(Required: true));
            fields.Field("BuildDirectory", value => value.BuildDirectory, (value, next) => value.BuildDirectory = next);
            fields.Field("BuildTimeout", value => value.BuildTimeout, (value, next) => value.BuildTimeout = next);
            fields.Field("GpuValidation", value => value.GpuValidation, (value, next) => value.GpuValidation = next);
            fields.Field("BuildRetention:MaxJobs", value => value.BuildRetention.MaxJobs, (value, next) => value.BuildRetention.MaxJobs = next);
            fields.Field("BuildRetention:MaxTotalBytes", value => value.BuildRetention.MaxTotalBytes, (value, next) => value.BuildRetention.MaxTotalBytes = next);
            fields.Field("BuildRetention:MaxAge", value => value.BuildRetention.MaxAge, (value, next) => value.BuildRetention.MaxAge = next);
            fields.Field("ChildDiagnostics:Enabled", value => value.ChildDiagnostics.Enabled, (value, next) => value.ChildDiagnostics.Enabled = next);
            fields.Field("ChildDiagnostics:MaxBatchRecords", value => value.ChildDiagnostics.MaxBatchRecords, (value, next) => value.ChildDiagnostics.MaxBatchRecords = next);
            fields.Field("ChildDiagnostics:MaxBatchBytes", value => value.ChildDiagnostics.MaxBatchBytes, (value, next) => value.ChildDiagnostics.MaxBatchBytes = next);
            fields.Field("ChildDiagnostics:RecordsPerSecond", value => value.ChildDiagnostics.RecordsPerSecond, (value, next) => value.ChildDiagnostics.RecordsPerSecond = next);
            fields.Field("ChildDiagnostics:BytesPerSecond", value => value.ChildDiagnostics.BytesPerSecond, (value, next) => value.ChildDiagnostics.BytesPerSecond = next);
            fields.Field("ChildDiagnostics:PollInterval", value => value.ChildDiagnostics.PollInterval, (value, next) => value.ChildDiagnostics.PollInterval = next);
            fields.Field("ChildDiagnostics:RequestTimeout", value => value.ChildDiagnostics.RequestTimeout, (value, next) => value.ChildDiagnostics.RequestTimeout = next);
            fields.Field("ChildDiagnostics:ShutdownTimeout", value => value.ChildDiagnostics.ShutdownTimeout, (value, next) => value.ChildDiagnostics.ShutdownTimeout = next);
            fields.Field("Deadlines:Startup", value => value.Deadlines.Startup, (value, next) => value.Deadlines = value.Deadlines with { Startup = next });
            fields.Field("Deadlines:Command", value => value.Deadlines.Command, (value, next) => value.Deadlines = value.Deadlines with { Command = next });
            fields.Field("Deadlines:HeartbeatInterval", value => value.Deadlines.HeartbeatInterval, (value, next) => value.Deadlines = value.Deadlines with { HeartbeatInterval = next });
            fields.Field("Deadlines:HeartbeatResponse", value => value.Deadlines.HeartbeatResponse, (value, next) => value.Deadlines = value.Deadlines with { HeartbeatResponse = next });
            fields.Field("ReplacementDeadlines:Total", value => value.ReplacementDeadlines.Total, (value, next) => value.ReplacementDeadlines = value.ReplacementDeadlines with { Total = next });
            fields.Field("ReplacementDeadlines:Forward", value => value.ReplacementDeadlines.Forward, (value, next) => value.ReplacementDeadlines = value.ReplacementDeadlines with { Forward = next });
            fields.Field("ReplacementDeadlines:Cleanup", value => value.ReplacementDeadlines.Cleanup, (value, next) => value.ReplacementDeadlines = value.ReplacementDeadlines with { Cleanup = next });
        });
    /// <summary>Aether:Ecs:World startup settings.</summary>
    public static IConfigurationSchema<WorldOptions> World { get; } = ConfigurationSchemaDefinitions.Create("Aether:Ecs:World", () => new WorldOptions(),
        value => value.Snapshot(), value => { _ = value.Snapshot(); }, fields =>
        {
            fields.Field("InitialEntityCapacity", value => value.InitialEntityCapacity, (value, next) => value.InitialEntityCapacity = next);
            fields.Field("MaximumEntityCount", value => value.MaximumEntityCount, (value, next) => value.MaximumEntityCount = next);
            fields.Field("MaximumComponentTypes", value => value.MaximumComponentTypes, (value, next) => value.MaximumComponentTypes = next);
        });
    /// <summary>Aether:Ecs:Scheduler startup settings.</summary>
    public static IConfigurationSchema<EcsSchedulerOptions> Scheduler { get; } = ConfigurationSchemaDefinitions.Create("Aether:Ecs:Scheduler", () => new EcsSchedulerOptions(),
        value => value.Snapshot(), value => { _ = value.Snapshot(); }, fields =>
        {
            fields.Field("MaximumSystems", value => value.MaximumSystems, (value, next) => value.MaximumSystems = next);
            fields.Field("MaximumCommandsPerPhase", value => value.MaximumCommandsPerPhase, (value, next) => value.MaximumCommandsPerPhase = next);
        });
    /// <summary>Aether:Ecs:SceneLoading startup settings.</summary>
    public static IConfigurationSchema<SceneWorldLoadOptions> SceneLoading { get; } = ConfigurationSchemaDefinitions.Create("Aether:Ecs:SceneLoading", () => new SceneWorldLoadOptions(),
        value => value.Snapshot(), value => { _ = value.Snapshot(); }, fields =>
        {
            fields.Field("MaximumEntities", value => value.MaximumEntities, (value, next) => value.MaximumEntities = next);
            fields.Field("MaximumComponentInstances", value => value.MaximumComponentInstances, (value, next) => value.MaximumComponentInstances = next);
            fields.Field("MaximumPropertyValues", value => value.MaximumPropertyValues, (value, next) => value.MaximumPropertyValues = next);
            fields.Field("MaximumPreparedResources", value => value.MaximumPreparedResources, (value, next) => value.MaximumPreparedResources = next);
        });
    /// <summary>Aether:RenderResources startup settings.</summary>
    public static IConfigurationSchema<RenderResourceOptions> RenderResources { get; } = ConfigurationSchemaDefinitions.Create("Aether:RenderResources", () => new RenderResourceOptions(),
        value => value.Snapshot(), value => { _ = value.Snapshot(); }, fields =>
        {
            fields.Field("MaximumCpuRevisions", value => value.MaximumCpuRevisions, (value, next) => value.MaximumCpuRevisions = next);
            fields.Field("MaximumCpuBytes", value => value.MaximumCpuBytes, (value, next) => value.MaximumCpuBytes = next);
            fields.Field("MaximumCpuScopes", value => value.MaximumCpuScopes, (value, next) => value.MaximumCpuScopes = next);
            fields.Field("MaximumCpuLeases", value => value.MaximumCpuLeases, (value, next) => value.MaximumCpuLeases = next);
            fields.Field("MaximumGeometryBytes", value => value.MaximumGeometryBytes, (value, next) => value.MaximumGeometryBytes = next);
            fields.Field("MaximumUploadStagingBytes", value => value.MaximumUploadStagingBytes, (value, next) => value.MaximumUploadStagingBytes = next);
            fields.Field("MaximumCpuStagingBytes", value => value.MaximumCpuStagingBytes, (value, next) => value.MaximumCpuStagingBytes = next);
            fields.Field("MaximumActivePreparations", value => value.MaximumActivePreparations, (value, next) => value.MaximumActivePreparations = next);
            fields.Field("MaximumQueuedPreparations", value => value.MaximumQueuedPreparations, (value, next) => value.MaximumQueuedPreparations = next);
            fields.Field("MaximumGpuEntries", value => value.MaximumGpuEntries, (value, next) => value.MaximumGpuEntries = next);
        });
    /// <summary>Aether:Authoring startup settings.</summary>
    public static IConfigurationSchema<SceneAuthoringOptions> Authoring { get; } = ConfigurationSchemaDefinitions.Create("Aether:Authoring", () => new SceneAuthoringOptions(),
        value => value.Snapshot(), value => { _ = value.Snapshot(); }, fields =>
        {
            fields.Field("MaxHistoryEntries", value => value.MaxHistoryEntries, (value, next) => value.MaxHistoryEntries = next);
            fields.Field("MaxBatchEdits", value => value.MaxBatchEdits, (value, next) => value.MaxBatchEdits = next);
            fields.Field("MaxOperations", value => value.MaxOperations, (value, next) => value.MaxOperations = next);
        });
    /// <summary>Aether:Ecs:Primitives startup settings.</summary>
    public static IConfigurationSchema<PrimitiveSimulationOptions> Primitives { get; } = ConfigurationSchemaDefinitions.Create("Aether:Ecs:Primitives", () => new PrimitiveSimulationOptions(),
        value => value.Snapshot(), value => { _ = value.Snapshot(); }, fields =>
        {
            fields.Field("MaximumTransformEntities", value => value.MaximumTransformEntities, (value, next) => value.MaximumTransformEntities = next);
            fields.Field("MaximumPrimitives", value => value.MaximumPrimitives, (value, next) => value.MaximumPrimitives = next);
            fields.Field("FixedStep", value => value.FixedStep, (value, next) => value.FixedStep = next);
            fields.Field("FieldOfView", value => value.FieldOfView, (value, next) => value.FieldOfView = next);
            fields.Field("Near", value => value.Near, (value, next) => value.Near = next);
            fields.Field("Far", value => value.Far, (value, next) => value.Far = next);
        });
}
