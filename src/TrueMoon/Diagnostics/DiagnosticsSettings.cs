namespace TrueMoon.Diagnostics;

/// <summary>Data-only startup defaults for the bounded diagnostic pipeline. Factories and listeners are separate composition inputs.</summary>
public sealed record DiagnosticsSettings
{
    /// <summary>Initial minimum severity.</summary>
    public DiagnosticLevel MinimumLevel { get; set; } = DiagnosticLevel.Information;
    /// <summary>Maximum queued and executing records per sink.</summary>
    public int QueueCapacity { get; set; } = 256;
    /// <summary>Reserved error slots.</summary>
    public int ReservedErrorCapacity { get; set; } = 16;
    /// <summary>Maximum retained bytes per sink.</summary>
    public long QueueByteCapacity { get; set; } = 4 * 1024 * 1024;
    /// <summary>Maximum memory history records.</summary>
    public int MemoryCapacity { get; set; } = 512;
    /// <summary>Maximum memory history bytes.</summary>
    public long MemoryByteCapacity { get; set; } = 4 * 1024 * 1024;
    /// <summary>Maximum message characters.</summary>
    public int MaxMessageChars { get; set; } = 2048;
    /// <summary>Maximum exception field characters.</summary>
    public int MaxExceptionChars { get; set; } = 4096;
    /// <summary>Maximum property value characters.</summary>
    public int MaxPropertyChars { get; set; } = 256;
    /// <summary>Maximum copied properties.</summary>
    public int MaxProperties { get; set; } = 16;
    /// <summary>Maximum diagnostic name characters.</summary>
    public int MaxNameChars { get; set; } = 128;
    /// <summary>Cooperative pipeline drain deadline.</summary>
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(5);
    /// <summary>Optional JSON output directory.</summary>
    public string? JsonDirectory { get; set; } = null;
    /// <summary>Maximum bytes in one JSON file.</summary>
    public long JsonFileSizeBytes { get; set; } = 1024 * 1024;
    /// <summary>Maximum JSON file count.</summary>
    public int JsonMaxFiles { get; set; } = 4;
    /// <summary>Whether JSON files use owned run directories.</summary>
    public bool JsonUseRunDirectories { get; set; } = false;
    /// <summary>Whether to write to borrowed Console.Out.</summary>
    public bool ConsoleOutput { get; set; } = false;

    /// <summary>Longest-prefix source severity overrides.</summary>
    public IReadOnlyDictionary<string, DiagnosticLevel> SourceMinimumLevels { get; set; } = new Dictionary<string, DiagnosticLevel>();
    /// <summary>Disabled stable event names.</summary>
    public string[] DisabledEvents { get; set; } = [];
    /// <summary>Fixed additional sink thresholds.</summary>
    public IReadOnlyDictionary<string, DiagnosticLevel> SinkMinimumLevels { get; set; } = new Dictionary<string, DiagnosticLevel>();
    /// <summary>Maximum retained closed JSON runs.</summary>
    public int JsonRetentionMaxRuns { get; set; } = 8;
    /// <summary>Maximum bytes retained across closed JSON runs.</summary>
    public long JsonRetentionMaxTotalBytes { get; set; } = 32L * 1024 * 1024;
    /// <summary>Maximum age of a closed JSON run.</summary>
    public TimeSpan JsonRetentionMaxAge { get; set; } = TimeSpan.FromDays(7);
    /// <summary>Whether internal failures can use the independent emergency lane.</summary>
    public bool EmergencyEnabled { get; set; } = true;
    /// <summary>Emergency queued plus executing reports.</summary>
    public int EmergencyQueueCapacity { get; set; } = 8;
    /// <summary>Maximum emergency report attempts.</summary>
    public int EmergencyMaxReports { get; set; } = 16;
    /// <summary>Maximum emergency report characters.</summary>
    public int EmergencyMaxMessageChars { get; set; } = 1024;
    /// <summary>Whether operation tracing can be sampled.</summary>
    public bool TracingEnabled { get; set; } = true;
    /// <summary>Whether operation metrics can be published.</summary>
    public bool MetricsEnabled { get; set; } = true;
    /// <summary>Maximum distinct operation names.</summary>
    public int TelemetryMaxOperationNames { get; set; } = 64;
    /// <summary>Maximum inspected trace properties.</summary>
    public int TelemetryMaxProperties { get; set; } = 16;
    /// <summary>Maximum trace property value characters.</summary>
    public int TelemetryMaxPropertyChars { get; set; } = 256;
    /// <summary>Maximum operation and trace property name characters.</summary>
    public int TelemetryMaxNameChars { get; set; } = 128;

    internal DiagnosticsSettings Copy() => this with
    {
        SourceMinimumLevels = new Dictionary<string, DiagnosticLevel>(SourceMinimumLevels, StringComparer.Ordinal),
        SinkMinimumLevels = new Dictionary<string, DiagnosticLevel>(SinkMinimumLevels, StringComparer.Ordinal),
        DisabledEvents = DisabledEvents.ToArray()
    };

    internal DiagnosticPipelineOptions ToOptions() => new()
    {
        MinimumLevel = MinimumLevel,
        QueueCapacity = QueueCapacity,
        ReservedErrorCapacity = ReservedErrorCapacity,
        QueueByteCapacity = QueueByteCapacity,
        MemoryCapacity = MemoryCapacity,
        MemoryByteCapacity = MemoryByteCapacity,
        MaxMessageChars = MaxMessageChars,
        MaxExceptionChars = MaxExceptionChars,
        MaxPropertyChars = MaxPropertyChars,
        MaxProperties = MaxProperties,
        MaxNameChars = MaxNameChars,
        ShutdownTimeout = ShutdownTimeout,
        JsonDirectory = JsonDirectory,
        JsonFileSizeBytes = JsonFileSizeBytes,
        JsonMaxFiles = JsonMaxFiles,
        JsonUseRunDirectories = JsonUseRunDirectories,
        ConsoleOutput = ConsoleOutput,
        SourceMinimumLevels = SourceMinimumLevels, DisabledEvents = DisabledEvents, SinkMinimumLevels = SinkMinimumLevels,
        JsonRetention = new() { MaxRuns = JsonRetentionMaxRuns, MaxTotalBytes = JsonRetentionMaxTotalBytes, MaxAge = JsonRetentionMaxAge },
        Emergency = new() { Enabled = EmergencyEnabled, QueueCapacity = EmergencyQueueCapacity, MaxReports = EmergencyMaxReports, MaxMessageChars = EmergencyMaxMessageChars },
        Telemetry = new() { TracingEnabled = TracingEnabled, MetricsEnabled = MetricsEnabled, MaxOperationNames = TelemetryMaxOperationNames,
            MaxProperties = TelemetryMaxProperties, MaxPropertyChars = TelemetryMaxPropertyChars, MaxNameChars = TelemetryMaxNameChars }
    };
}
