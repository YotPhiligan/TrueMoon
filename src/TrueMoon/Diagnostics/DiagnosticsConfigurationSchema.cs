using TrueMoon.Configuration;

namespace TrueMoon.Diagnostics;

/// <summary>Standard scalar diagnostic settings. All fields require a new pipeline/App.</summary>
public static class DiagnosticsConfigurationSchema
{
    /// <summary>The exact schema instance consumed by UseConfiguredDiagnostics.</summary>
    public static IConfigurationSchema<DiagnosticsSettings> Default { get; } = ConfigurationSchemas.Create("Diagnostics",
        () => new DiagnosticsSettings(), value => value.Copy(), value => { _ = value.ToOptions().Snapshot(); }, fields =>
        {
            fields.Field("MinimumLevel", value => value.MinimumLevel, (value, next) => value.MinimumLevel = next);
            fields.Field("QueueCapacity", value => value.QueueCapacity, (value, next) => value.QueueCapacity = next);
            fields.Field("ReservedErrorCapacity", value => value.ReservedErrorCapacity, (value, next) => value.ReservedErrorCapacity = next);
            fields.Field("QueueByteCapacity", value => value.QueueByteCapacity, (value, next) => value.QueueByteCapacity = next);
            fields.Field("MemoryCapacity", value => value.MemoryCapacity, (value, next) => value.MemoryCapacity = next);
            fields.Field("MemoryByteCapacity", value => value.MemoryByteCapacity, (value, next) => value.MemoryByteCapacity = next);
            fields.Field("MaxMessageChars", value => value.MaxMessageChars, (value, next) => value.MaxMessageChars = next);
            fields.Field("MaxExceptionChars", value => value.MaxExceptionChars, (value, next) => value.MaxExceptionChars = next);
            fields.Field("MaxPropertyChars", value => value.MaxPropertyChars, (value, next) => value.MaxPropertyChars = next);
            fields.Field("MaxProperties", value => value.MaxProperties, (value, next) => value.MaxProperties = next);
            fields.Field("MaxNameChars", value => value.MaxNameChars, (value, next) => value.MaxNameChars = next);
            fields.Field("ShutdownTimeout", value => value.ShutdownTimeout, (value, next) => value.ShutdownTimeout = next);
            fields.Field("JsonDirectory", value => value.JsonDirectory, (value, next) => value.JsonDirectory = next);
            fields.Field("JsonFileSizeBytes", value => value.JsonFileSizeBytes, (value, next) => value.JsonFileSizeBytes = next);
            fields.Field("JsonMaxFiles", value => value.JsonMaxFiles, (value, next) => value.JsonMaxFiles = next);
            fields.Field("JsonUseRunDirectories", value => value.JsonUseRunDirectories, (value, next) => value.JsonUseRunDirectories = next);
            fields.Field("ConsoleOutput", value => value.ConsoleOutput, (value, next) => value.ConsoleOutput = next);
            fields.Field("SourceMinimumLevels", value => value.SourceMinimumLevels, (value, next) => value.SourceMinimumLevels = next, codec: new DiagnosticLevelsCodec());
            fields.Field("DisabledEvents", value => value.DisabledEvents, (value, next) => value.DisabledEvents = next);
            fields.Field("SinkMinimumLevels", value => value.SinkMinimumLevels, (value, next) => value.SinkMinimumLevels = next, codec: new DiagnosticLevelsCodec());
            fields.Field("JsonRetention:MaxRuns", value => value.JsonRetentionMaxRuns, (value, next) => value.JsonRetentionMaxRuns = next);
            fields.Field("JsonRetention:MaxTotalBytes", value => value.JsonRetentionMaxTotalBytes, (value, next) => value.JsonRetentionMaxTotalBytes = next);
            fields.Field("JsonRetention:MaxAge", value => value.JsonRetentionMaxAge, (value, next) => value.JsonRetentionMaxAge = next);
            fields.Field("Emergency:Enabled", value => value.EmergencyEnabled, (value, next) => value.EmergencyEnabled = next);
            fields.Field("Emergency:QueueCapacity", value => value.EmergencyQueueCapacity, (value, next) => value.EmergencyQueueCapacity = next);
            fields.Field("Emergency:MaxReports", value => value.EmergencyMaxReports, (value, next) => value.EmergencyMaxReports = next);
            fields.Field("Emergency:MaxMessageChars", value => value.EmergencyMaxMessageChars, (value, next) => value.EmergencyMaxMessageChars = next);
            fields.Field("Telemetry:TracingEnabled", value => value.TracingEnabled, (value, next) => value.TracingEnabled = next);
            fields.Field("Telemetry:MetricsEnabled", value => value.MetricsEnabled, (value, next) => value.MetricsEnabled = next);
            fields.Field("Telemetry:MaxOperationNames", value => value.TelemetryMaxOperationNames, (value, next) => value.TelemetryMaxOperationNames = next);
            fields.Field("Telemetry:MaxProperties", value => value.TelemetryMaxProperties, (value, next) => value.TelemetryMaxProperties = next);
            fields.Field("Telemetry:MaxPropertyChars", value => value.TelemetryMaxPropertyChars, (value, next) => value.TelemetryMaxPropertyChars = next);
            fields.Field("Telemetry:MaxNameChars", value => value.TelemetryMaxNameChars, (value, next) => value.TelemetryMaxNameChars = next);
        });
}
