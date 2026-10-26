namespace TrueMoon.Diagnostics;

/// <summary>App-owned bounded recent history and independent asynchronous output workers.</summary>
/// <remarks>Legacy DiagnosticListener sources remain process-wide. ApplicationId identifies this collector.
/// Dispose closes admission and drains accepted writes; a timeout preserves ownership for a later retry.</remarks>
public sealed class DiagnosticPipeline : IDisposable, IAsyncDisposable
{
    private readonly Lock _gate = new();
    private readonly DiagnosticPipelineOptions _options;
    private readonly List<DiagnosticSinkWorker> _workers = [];
    private readonly Queue<(DiagnosticRecord Record, long Bytes)> _memory = new();
    private readonly Dictionary<string, DiagnosticLevel> _sourceLevels;
    private readonly HashSet<string> _disabledEvents;
    private long _sequence, _memoryBytes, _evicted, _captureFailures, _lateWrites;
    private bool _closed;
    private int _minimumLevel;
    private Task? _dispose;
    private readonly DiagnosticEmergencyOutput? _emergency;
    private readonly JsonDiagnosticSink? _jsonSink;
    /// <summary>Independent emergency-worker health; null when disabled.</summary>
    public DiagnosticEmergencyStatus? EmergencyStatus => _emergency?.Read();
    /// <summary>Actual JSONL output directory, or null before managed-run creation or when JSON output is disabled.</summary>
    public string? JsonDirectory => _jsonSink?.DirectoryPath;
    /// <summary>Latest managed JSONL run maintenance status, or null before initialization or in flat mode.</summary>
    public DiagnosticRetentionStatus? JsonRetentionStatus => _jsonSink?.RetentionStatus;

    /// <summary>Creates the configured sinks using their borrowed factories, taking ownership of each result.</summary>
    /// <param name="options">Validated budgets; copied before any sink is created.</param>
    public DiagnosticPipeline(DiagnosticPipelineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Snapshot(); _minimumLevel = (int)_options.MinimumLevel;
        _sourceLevels = new(_options.SourceMinimumLevels, StringComparer.Ordinal);
        _disabledEvents = new(_options.DisabledEvents, StringComparer.Ordinal);
        if (_options.Emergency.Enabled) _emergency = new(_options.Emergency, ApplicationId);
        var acquired = new HashSet<IDiagnosticSink>(ReferenceEqualityComparer.Instance);
        try
        {
            if (_options.JsonDirectory is not null)
            {
                _jsonSink = new JsonDiagnosticSink(_options.JsonDirectory, _options.JsonFileSizeBytes, _options.JsonMaxFiles,
                    "events", _options.JsonUseRunDirectories ? _options.JsonRetention : null);
                Add("jsonl", _jsonSink);
            }
            if (_options.ConsoleOutput) Add("console", new ConsoleDiagnosticSink());
            for (var index = 0; index < _options.SinkFactories.Count; index++)
                Add("custom-" + index, _options.SinkFactories[index].Create() ?? throw new InvalidOperationException("Sink factory returned null."));
        }
        catch (Exception original)
        {
            var failures = new List<Exception> { original };
            _emergency?.Report("Startup", "pipeline", original);
            foreach (var worker in _workers) worker.Close();
            try
            {
                if (!RollbackAsync().Wait(_options.ShutdownTimeout))
                    failures.Add(new TimeoutException("Diagnostic sink creation rollback exceeded its common drain budget."));
            }
            catch (Exception cleanup) { failures.Add(cleanup); }
            if (failures.Count > 1) throw new AggregateException(failures);
            throw;
        }
        void Add(string name, IDiagnosticSink sink)
        {
            if (!acquired.Add(sink)) throw new InvalidOperationException("Each diagnostic sink must have exactly one owner.");
            _workers.Add(new(name, sink, _options, _emergency));
        }
    }

    /// <summary>Identity of this collector, independent of process-global source names.</summary>
    public Guid ApplicationId { get; } = Guid.NewGuid();
    /// <summary>Live minimum level for future emissions from existing sources.</summary>
    public DiagnosticLevel MinimumLevel
    {
        get => (DiagnosticLevel)Volatile.Read(ref _minimumLevel);
        set { if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value)); Volatile.Write(ref _minimumLevel, (int)value); }
    }
    /// <summary>Changes a source prefix threshold for existing sources. Longest matching prefix wins.</summary>
    /// <param name="sourcePrefix">Ordinal source prefix, up to 512 characters.</param><param name="level">New threshold.</param>
    public void SetSourceMinimumLevel(string sourcePrefix, DiagnosticLevel level)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePrefix);
        if (sourcePrefix.Length > 512 || !Enum.IsDefined(level)) throw new ArgumentOutOfRangeException(nameof(level));
        lock (_gate)
        {
            if (!_sourceLevels.ContainsKey(sourcePrefix) && _sourceLevels.Count >= 64) throw new InvalidOperationException("Source rule limit reached.");
            _sourceLevels[sourcePrefix] = level;
        }
    }
    /// <summary>Enables or disables a stable event name on existing sources.</summary>
    /// <param name="eventName">Stable name, up to 512 characters.</param><param name="enabled">Whether collection is allowed.</param>
    public void SetEventEnabled(string eventName, bool enabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        if (eventName.Length > 512) throw new ArgumentOutOfRangeException(nameof(eventName));
        lock (_gate)
        {
            if (enabled) _disabledEvents.Remove(eventName);
            else
            {
                if (!_disabledEvents.Contains(eventName) && _disabledEvents.Count >= 256) throw new InvalidOperationException("Event rule limit reached.");
                _disabledEvents.Add(eventName);
            }
        }
    }
    /// <summary>Number of records evicted or too large for the memory ring.</summary>
    public long MemoryEvicted { get { lock (_gate) return _evicted; } }
    /// <summary>Number of snapshots that failed before admission.</summary>
    public long CaptureFailures { get { lock (_gate) return _captureFailures; } }
    /// <summary>Number of attempted emissions after admission closed.</summary>
    public long LateWrites { get { lock (_gate) return _lateWrites; } }

    /// <summary>Whether the level currently passes collection filtering.</summary>
    /// <param name="level">Severity.</param><param name="source">Optional full source name.</param><param name="eventName">Optional stable event name.</param>
    /// <returns>Whether collection currently accepts the event.</returns>
    public bool IsEnabled(DiagnosticLevel level, string? source = null, string? eventName = null)
    {
        if (!Enum.IsDefined(level)) return false;
        lock (_gate)
        {
            if (_closed || eventName is not null && _disabledEvents.Contains(eventName)) return false;
            var minimum = MinimumLevel; var longest = -1;
            if (source is not null)
                foreach (var rule in _sourceLevels)
                    if (rule.Key.Length > longest && source.StartsWith(rule.Key, StringComparison.Ordinal))
                    { minimum = rule.Value; longest = rule.Key.Length; }
            return level >= minimum;
        }
    }

    internal void Publish(string source, DiagnosticMessage message)
    {
        lock (_gate)
        {
            if (_closed) { _lateWrites++; return; }
            if (!IsEnabled(message.Level, source, message.EventName)) return;
            DiagnosticRecord record;
            try { record = DiagnosticSnapshot.Create(source, message, ApplicationId, ++_sequence, _options); }
            catch (Exception error) { _captureFailures++; _emergency?.Report("Capture", source, error); return; }
            var bytes = DiagnosticSnapshot.Size(record);
            while (_memory.Count != 0 && (_memory.Count >= _options.MemoryCapacity || bytes > _options.MemoryByteCapacity - _memoryBytes))
            { _memoryBytes -= _memory.Dequeue().Bytes; _evicted++; }
            if (bytes <= _options.MemoryByteCapacity) { _memory.Enqueue((record, bytes)); _memoryBytes += bytes; }
            else _evicted++;
            foreach (var worker in _workers)
                if (!_options.SinkMinimumLevels.TryGetValue(worker.Name, out var threshold) || record.Level >= threshold)
                    worker.Offer(record, bytes);
        }
    }

    /// <summary>Returns an independent oldest-first snapshot of recent history.</summary>
    public IReadOnlyList<DiagnosticRecord> Read()
    {
        lock (_gate) return _memory.Select(item => item.Record).ToArray();
    }
    /// <summary>Returns independent bounded sink health snapshots.</summary>
    public IReadOnlyList<DiagnosticSinkStatus> Health() => _workers.Select(worker => worker.Read()).ToArray();
    /// <summary>Waits for all sink writes accepted before this call; does not close admission.</summary>
    /// <param name="cancellationToken">Cancels only this wait, preserving ownership.</param>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(_workers.Select(worker => worker.FlushAsync(cancellationToken))).ConfigureAwait(false);
        if (_emergency != null) await _emergency.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            _closed = true;
            foreach (var worker in _workers) worker.Close();
            return new(_dispose is { IsCompletedSuccessfully: true } or { IsCompleted: false } ? _dispose : _dispose = DisposeCoreAsync());
        }
    }
    private async Task DisposeCoreAsync()
    {
        try { await CleanupAsync().WaitAsync(_options.ShutdownTimeout).ConfigureAwait(false); }
        catch (TimeoutException error) { _emergency?.Report("Shutdown", "pipeline", error); throw; }
    }
    private async Task CleanupAsync()
    {
        await Task.WhenAll(_workers.Select(worker => worker.DisposeAsync())).ConfigureAwait(false);
        if (_emergency != null) await _emergency.DisposeAsync().ConfigureAwait(false);
    }
    private async Task RollbackAsync()
    {
        try { await Task.WhenAll(_workers.Select(worker => worker.DisposeAsync())).ConfigureAwait(false); }
        finally { if (_emergency != null) await _emergency.DisposeAsync().ConfigureAwait(false); }
    }
    /// <inheritdoc />
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}
