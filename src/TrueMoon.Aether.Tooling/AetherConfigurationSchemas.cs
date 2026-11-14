using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Loading;
using TrueMoon.Aether.Runtime.Ecs.Rendering;
using TrueMoon.Aether.Runtime.Rendering.Resources;
using TrueMoon.Aether.Tooling.Hosting;
using TrueMoon.Aether.Tooling.Scenes.Authoring;
using TrueMoon.Configuration;

namespace TrueMoon.Aether.Tooling;

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
    public static IConfigurationSchema<WorldOptions> World => Runtime.RuntimeConfigurationSchemas.World;
    /// <summary>Aether:Ecs:Scheduler startup settings.</summary>
    public static IConfigurationSchema<EcsSchedulerOptions> Scheduler => Runtime.RuntimeConfigurationSchemas.Scheduler;
    /// <summary>Aether:Ecs:SceneLoading startup settings.</summary>
    public static IConfigurationSchema<SceneWorldLoadOptions> SceneLoading => Runtime.RuntimeConfigurationSchemas.SceneLoading;
    /// <summary>Aether:RenderResources startup settings.</summary>
    public static IConfigurationSchema<RenderResourceOptions> RenderResources => Runtime.RuntimeConfigurationSchemas.RenderResources;
    /// <summary>Aether:Authoring startup settings.</summary>
    public static IConfigurationSchema<SceneAuthoringOptions> Authoring { get; } = ConfigurationSchemaDefinitions.Create("Aether:Authoring", () => new SceneAuthoringOptions(),
        value => value.Snapshot(), value => { _ = value.Snapshot(); }, fields =>
        {
            fields.Field("MaxHistoryEntries", value => value.MaxHistoryEntries, (value, next) => value.MaxHistoryEntries = next);
            fields.Field("MaxBatchEdits", value => value.MaxBatchEdits, (value, next) => value.MaxBatchEdits = next);
            fields.Field("MaxOperations", value => value.MaxOperations, (value, next) => value.MaxOperations = next);
        });
    /// <summary>Aether:Ecs:Primitives startup settings.</summary>
    public static IConfigurationSchema<PrimitiveSimulationOptions> Primitives => Runtime.RuntimeConfigurationSchemas.Primitives;
}
