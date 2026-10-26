namespace TrueMoon.Diagnostics;

/// <summary>Budgets copied and validated before the application creates diagnostic resources.</summary>
public sealed class DiagnosticPipelineOptions
{
    /// <summary>Initial minimum severity; may subsequently be changed on the pipeline.</summary>
    public DiagnosticLevel MinimumLevel { get; set; } = DiagnosticLevel.Information;
    /// <summary>Source prefix overrides; the longest matching prefix wins. At most 64 rules.</summary>
    public IReadOnlyDictionary<string, DiagnosticLevel> SourceMinimumLevels { get; set; } = new Dictionary<string, DiagnosticLevel>();
    /// <summary>Stable event names disabled before property creation. At most 256 names.</summary>
    public IReadOnlyList<string> DisabledEvents { get; set; } = [];
    /// <summary>Additional fixed sink thresholds keyed by jsonl, console or custom-N.</summary>
    public IReadOnlyDictionary<string, DiagnosticLevel> SinkMinimumLevels { get; set; } = new Dictionary<string, DiagnosticLevel>();
    /// <summary>Maximum queued plus executing records per sink.</summary>
    public int QueueCapacity { get; set; } = 256;
    /// <summary>Slots unavailable to events below Error.</summary>
    public int ReservedErrorCapacity { get; set; } = 16;
    /// <summary>Maximum estimated retained bytes per sink, including its executing record.</summary>
    public long QueueByteCapacity { get; set; } = 4 * 1024 * 1024;
    /// <summary>Maximum recent records in the memory ring.</summary>
    public int MemoryCapacity { get; set; } = 512;
    /// <summary>Maximum estimated retained bytes in the memory ring.</summary>
    public long MemoryByteCapacity { get; set; } = 4 * 1024 * 1024;
    /// <summary>Maximum message template characters.</summary>
    public int MaxMessageChars { get; set; } = 2048;
    /// <summary>Maximum characters in each exception field.</summary>
    public int MaxExceptionChars { get; set; } = 4096;
    /// <summary>Maximum characters in each property value.</summary>
    public int MaxPropertyChars { get; set; } = 256;
    /// <summary>Maximum copied properties.</summary>
    public int MaxProperties { get; set; } = 16;
    /// <summary>Maximum characters in source, event name, caller and property keys.</summary>
    public int MaxNameChars { get; set; } = 128;
    /// <summary>Maximum wait for a disposal attempt; resources remain owned after a timeout.</summary>
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(5);
    /// <summary>Optional dedicated directory for rotating JSONL files.</summary>
    public string? JsonDirectory { get; set; }
    /// <summary>Maximum UTF8 bytes in one JSONL file, including line terminators.</summary>
    public long JsonFileSizeBytes { get; set; } = 1024 * 1024;
    /// <summary>Maximum files belonging to this pipeline's JSONL sink.</summary>
    public int JsonMaxFiles { get; set; } = 4;
    /// <summary>Whether JsonDirectory is a managed root containing one GUID directory per sink lifetime.</summary>
    public bool JsonUseRunDirectories { get; set; }
    /// <summary>Closed-run count, bytes and age limits. Used only with JsonUseRunDirectories.</summary>
    public DiagnosticRunRetentionOptions JsonRetention { get; set; } = new();
    /// <summary>Independent finite output for internal diagnostics failures.</summary>
    public DiagnosticEmergencyOptions Emergency { get; set; } = new();
    /// <summary>Listener-driven tracing and bounded operation metrics, copied during setup.</summary>
    public DiagnosticTelemetryOptions Telemetry { get; set; } = new();
    /// <summary>Whether to write JSON records to the borrowed Console.Out.</summary>
    public bool ConsoleOutput { get; set; }
    /// <summary>Factories whose returned sinks transfer ownership to the pipeline. Factories are borrowed.</summary>
    public IReadOnlyList<IFactory<IDiagnosticSink>> SinkFactories { get; set; } = [];

    internal DiagnosticPipelineOptions Snapshot()
    {
        ArgumentNullException.ThrowIfNull(JsonRetention);
        ArgumentNullException.ThrowIfNull(Emergency);
        ArgumentNullException.ThrowIfNull(Telemetry);
        var retention = JsonRetention.Snapshot();
        var emergency = Emergency.Snapshot();
        var telemetry = Telemetry.Snapshot();
        if (!Enum.IsDefined(MinimumLevel)) throw new ArgumentOutOfRangeException(nameof(MinimumLevel));
        if (QueueCapacity < 1 || ReservedErrorCapacity < 0 || ReservedErrorCapacity >= QueueCapacity)
            throw new ArgumentOutOfRangeException(nameof(QueueCapacity));
        if (QueueByteCapacity < 1 || MemoryCapacity < 1 || MemoryByteCapacity < 1 ||
            MaxMessageChars < 1 || MaxExceptionChars < 1 || MaxPropertyChars < 1 ||
            MaxProperties < 0 || MaxNameChars < 1 || JsonFileSizeBytes < 1 || JsonMaxFiles < 1)
            throw new ArgumentOutOfRangeException(nameof(DiagnosticPipelineOptions));
        if (ShutdownTimeout <= TimeSpan.Zero || ShutdownTimeout > TimeSpan.FromSeconds(30))
            throw new ArgumentOutOfRangeException(nameof(ShutdownTimeout));
        ArgumentNullException.ThrowIfNull(SinkFactories);
        var factories = SinkFactories.ToArray();
        if (factories.Length > 64) throw new ArgumentException("At most 64 sink factories are supported.", nameof(SinkFactories));
        if (factories.Any(factory => factory is null)) throw new ArgumentException("Null sink factory.", nameof(SinkFactories));
        ArgumentNullException.ThrowIfNull(SourceMinimumLevels);
        ArgumentNullException.ThrowIfNull(DisabledEvents);
        ArgumentNullException.ThrowIfNull(SinkMinimumLevels);
        var sourceLevels = SourceMinimumLevels.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var disabled = DisabledEvents.ToArray();
        var sinkLevels = SinkMinimumLevels.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        if (sourceLevels.Count > 64 || disabled.Length > 256 || sinkLevels.Count > 66 ||
            sourceLevels.Concat(sinkLevels).Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Key.Length > 512 || !Enum.IsDefined(pair.Value)) ||
            disabled.Any(name => string.IsNullOrWhiteSpace(name) || name.Length > 512))
            throw new ArgumentException("Invalid or excessive diagnostic filter rules.");
        return new()
        {
            MinimumLevel = MinimumLevel, QueueCapacity = QueueCapacity, ReservedErrorCapacity = ReservedErrorCapacity,
            SourceMinimumLevels = sourceLevels, DisabledEvents = disabled, SinkMinimumLevels = sinkLevels,
            QueueByteCapacity = QueueByteCapacity, MemoryCapacity = MemoryCapacity, MemoryByteCapacity = MemoryByteCapacity,
            MaxMessageChars = MaxMessageChars, MaxExceptionChars = MaxExceptionChars, MaxPropertyChars = MaxPropertyChars,
            MaxProperties = MaxProperties, MaxNameChars = MaxNameChars, ShutdownTimeout = ShutdownTimeout,
            JsonDirectory = JsonDirectory is null ? null : Path.GetFullPath(JsonDirectory),
            JsonFileSizeBytes = JsonFileSizeBytes, JsonMaxFiles = JsonMaxFiles, ConsoleOutput = ConsoleOutput,
            JsonUseRunDirectories = JsonUseRunDirectories, JsonRetention = retention, Emergency = emergency, Telemetry = telemetry,
            SinkFactories = factories
        };
    }
}
