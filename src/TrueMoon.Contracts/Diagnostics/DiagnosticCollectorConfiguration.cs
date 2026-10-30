namespace TrueMoon.Diagnostics;

/// <summary>Effective scalar collector settings. Rule names, output paths and sink factories are intentionally omitted.</summary>
public sealed record DiagnosticCollectorConfiguration
{
    /// <summary>Collector identity.</summary>
    public Guid ApplicationId { get; init; }
    /// <summary>Current minimum level.</summary>
    public DiagnosticLevel MinimumLevel { get; init; }
    /// <summary>Executing-inclusive per-sink record limit.</summary>
    public int QueueCapacity { get; init; }
    /// <summary>Reserved error slots.</summary>
    public int ReservedErrorCapacity { get; init; }
    /// <summary>Executing-inclusive per-sink byte limit.</summary>
    public long QueueByteCapacity { get; init; }
    /// <summary>Memory ring count limit.</summary>
    public int MemoryCapacity { get; init; }
    /// <summary>Memory ring byte limit.</summary>
    public long MemoryByteCapacity { get; init; }
    /// <summary>Message character limit.</summary>
    public int MaxMessageChars { get; init; }
    /// <summary>Exception field character limit.</summary>
    public int MaxExceptionChars { get; init; }
    /// <summary>Property value character limit.</summary>
    public int MaxPropertyChars { get; init; }
    /// <summary>Copied property count limit.</summary>
    public int MaxProperties { get; init; }
    /// <summary>Identifier character limit.</summary>
    public int MaxNameChars { get; init; }
    /// <summary>Current source-prefix rule count.</summary>
    public int SourceRuleCount { get; init; }
    /// <summary>Current disabled event count.</summary>
    public int DisabledEventCount { get; init; }
    /// <summary>Fixed sink-threshold rule count.</summary>
    public int SinkRuleCount { get; init; }
    /// <summary>Whether JSONL output is configured.</summary>
    public bool JsonEnabled { get; init; }
    /// <summary>Configured JSONL slot byte limit.</summary>
    public long JsonFileSizeBytes { get; init; }
    /// <summary>Configured JSONL slot count.</summary>
    public int JsonMaxFiles { get; init; }
    /// <summary>Whether JSONL uses managed GUID run directories.</summary>
    public bool JsonUseRunDirectories { get; init; }
    /// <summary>Managed closed-run count limit.</summary>
    public int JsonMaxRuns { get; init; }
    /// <summary>Managed closed-run byte limit.</summary>
    public long JsonMaxTotalBytes { get; init; }
    /// <summary>Managed closed-run age limit.</summary>
    public TimeSpan JsonMaxAge { get; init; }
    /// <summary>Whether console output is configured.</summary>
    public bool ConsoleOutput { get; init; }
    /// <summary>Configured custom sink count.</summary>
    public int CustomSinkCount { get; init; }
    /// <summary>Whether the independent emergency channel is enabled.</summary>
    public bool EmergencyEnabled { get; init; }
    /// <summary>Whether tracing is enabled.</summary>
    public bool TracingEnabled { get; init; }
    /// <summary>Whether operation metrics are enabled.</summary>
    public bool MetricsEnabled { get; init; }
    /// <summary>Maximum admitted operation names.</summary>
    public int MaxOperationNames { get; init; }
    /// <summary>Common cooperative sink-drain budget.</summary>
    public TimeSpan ShutdownTimeout { get; init; }
}
