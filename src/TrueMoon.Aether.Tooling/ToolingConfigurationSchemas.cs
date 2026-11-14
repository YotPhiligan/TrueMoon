using TrueMoon.Aether.Tooling.Hosting;
using TrueMoon.Configuration;

namespace TrueMoon.Aether.Tooling;

/// <summary>Common App configuration schema for headless startup data. Target host identity stays explicit.</summary>
public static class ToolingConfigurationSchemas
{
    /// <summary>Build budgets, artifact directory, authoring limits and source scheduling. No document/project state provider is introduced.</summary>
    public static IConfigurationSchema<ToolingSettings> Settings { get; } = ConfigurationSchemaDefinitions.Create("Aether:Tooling",
        () => new ToolingSettings(), value => value.Snapshot(), value => { _ = value.Snapshot(); }, fields =>
        {
            fields.Field("BuildDirectory", v => v.BuildDirectory, (v, n) => v.BuildDirectory = n);
            fields.Field("BuildTimeout", v => v.BuildTimeout, (v, n) => v.BuildTimeout = n);
            fields.Field("BuildRetention:MaxJobs", v => v.BuildRetention.MaxJobs, (v, n) => v.BuildRetention.MaxJobs = n);
            fields.Field("BuildRetention:MaxTotalBytes", v => v.BuildRetention.MaxTotalBytes, (v, n) => v.BuildRetention.MaxTotalBytes = n);
            fields.Field("BuildRetention:MaxAge", v => v.BuildRetention.MaxAge, (v, n) => v.BuildRetention.MaxAge = n);
            fields.Field("Authoring:MaxHistoryEntries", v => v.Authoring.MaxHistoryEntries, (v, n) => v.Authoring.MaxHistoryEntries = n);
            fields.Field("Authoring:MaxBatchEdits", v => v.Authoring.MaxBatchEdits, (v, n) => v.Authoring.MaxBatchEdits = n);
            fields.Field("Authoring:MaxOperations", v => v.Authoring.MaxOperations, (v, n) => v.Authoring.MaxOperations = n);
            fields.Field("SourceTracking:Enabled", v => v.SourceTracking.Enabled, (v, n) => v.SourceTracking.Enabled = n);
            fields.Field("SourceTracking:UseFileSystemNotifications", v => v.SourceTracking.UseFileSystemNotifications, (v, n) => v.SourceTracking.UseFileSystemNotifications = n);
            fields.Field("SourceTracking:Debounce", v => v.SourceTracking.Debounce, (v, n) => v.SourceTracking.Debounce = n);
            fields.Field("SourceTracking:MaximumDebounce", v => v.SourceTracking.MaximumDebounce, (v, n) => v.SourceTracking.MaximumDebounce = n);
            fields.Field("SourceTracking:PollInterval", v => v.SourceTracking.PollInterval, (v, n) => v.SourceTracking.PollInterval = n);
            fields.Field("SourceTracking:ScanBudget", v => v.SourceTracking.ScanBudget, (v, n) => v.SourceTracking.ScanBudget = n);
            fields.Field("SourceTracking:MaximumWatchers", v => v.SourceTracking.MaximumWatchers, (v, n) => v.SourceTracking.MaximumWatchers = n);
        });
}
