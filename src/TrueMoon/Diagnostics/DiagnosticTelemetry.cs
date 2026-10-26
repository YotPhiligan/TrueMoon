using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace TrueMoon.Diagnostics;

/// <summary>App-owned tracing and low-cardinality operation metrics using standard .NET listeners.</summary>
/// <remarks>Dispose fences new operations. Existing scopes retain instrumentation until their own disposal.
/// Listener callbacks run synchronously and must not block. Their exceptions are counted and isolated.</remarks>
public sealed class DiagnosticTelemetry : IDiagnosticTelemetry, IDisposable
{
    /// <summary>Default ActivitySource and Meter name.</summary>
    public const string InstrumentationName = "TrueMoon";
    private readonly Lock _gate = new();
    private readonly DiagnosticTelemetryOptions _options;
    private readonly HashSet<string> _names = new(StringComparer.Ordinal);
    private readonly Counter<long>? _count;
    private readonly Histogram<double>? _duration;
    private long _active, _rejected, _failures;
    private bool _closed, _released;
    [ThreadStatic] private static bool _notifying;

    /// <summary>Creates an owner; options are copied before instrumentation is allocated.</summary>
    public DiagnosticTelemetry(DiagnosticTelemetryOptions? options = null, Guid? applicationId = null)
    {
        _options = (options ?? new()).Snapshot(); ApplicationId = applicationId ?? Guid.NewGuid();
        try { ActivitySource = new(InstrumentationName); } catch { Failed(); }
        Meter = new(InstrumentationName);
        try { _count = Meter.CreateCounter<long>("truemoon.operation.count", "{operation}"); } catch { Failed(); }
        try { _duration = Meter.CreateHistogram<double>("truemoon.operation.duration", "s"); } catch { Failed(); }
    }

    /// <summary>Owner identity, present on traces only.</summary>
    public Guid ApplicationId { get; }
    /// <summary>Borrowed source for observer selection; null if observer notification rejected its creation. Only this owner may dispose it.</summary>
    public ActivitySource? ActivitySource { get; }
    /// <summary>Borrowed meter for observer selection. Only this owner may dispose it.</summary>
    public Meter Meter { get; }
    /// <summary>Returns an independent ownership and failure snapshot.</summary>
    public DiagnosticTelemetryStatus Read()
    { lock (_gate) return new(_active, _rejected, Interlocked.Read(ref _failures), _closed, _released); }

    /// <inheritdoc />
    public IDiagnosticOperation? StartOperation(string name, Func<IReadOnlyDictionary<string, string?>>? properties = null)
        => StartOperation(name, ActivityKind.Internal, null, properties);

    /// <inheritdoc />
    public IDiagnosticOperation? StartOperation(string name, ActivityKind kind, ActivityContext? parentContext,
        Func<IReadOnlyDictionary<string, string?>>? properties = null)
    {
        lock (_gate)
        {
            if (_closed || _notifying || string.IsNullOrWhiteSpace(name) || name.Length > _options.MaxNameChars || !Enum.IsDefined(kind))
            { _rejected++; return null; }
            if (!(_options.TracingEnabled && ActivitySource?.HasListeners() == true) &&
                !(_options.MetricsEnabled && (_count?.Enabled == true || _duration?.Enabled == true))) return null;
            if (!_names.Contains(name) && _names.Count >= _options.MaxOperationNames) { _rejected++; return null; }
            _names.Add(name); _active++;
        }
        var parent = Activity.Current;
        Activity? activity = null;
        _notifying = true;
        try
        {
            try
            {
                if (_options.TracingEnabled) activity = parentContext is { } context
                    ? ActivitySource?.StartActivity(name, kind, context)
                    : ActivitySource?.StartActivity(name, kind);
            }
            catch
            {
                Failed();
                if (Activity.Current is { } current && ReferenceEquals(current.Source, ActivitySource) && !ReferenceEquals(current, parent))
                { try { current.Dispose(); } catch { Failed(); } }
                Activity.Current = parent;
            }
            if (activity?.IsAllDataRequested == true)
            {
                activity.SetTag("truemoon.app.id", ApplicationId.ToString("D"));
                if (properties != null && _options.MaxProperties != 0)
                    try
                    {
                        var inspected = 0;
                        foreach (var pair in properties())
                        {
                            if (inspected++ >= _options.MaxProperties) break;
                            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Key.Length > _options.MaxNameChars ||
                                pair.Key is "truemoon.app.id" or "outcome" or "error.type") continue;
                            activity.SetTag(pair.Key, Clip(pair.Value, _options.MaxPropertyChars));
                        }
                    }
                    catch { Failed(); }
            }
            if (activity == null && !(_options.MetricsEnabled && (_count?.Enabled == true || _duration?.Enabled == true)))
            { Release(); return null; }
            return new Operation(this, name, activity, parent);
        }
        finally { _notifying = false; }
    }

    private static string? Clip(string? value, int limit)
    {
        if (value == null || value.Length <= limit) return value;
        if (char.IsHighSurrogate(value[limit - 1]) && char.IsLowSurrogate(value[limit])) limit--;
        return value[..limit];
    }
    private void Failed() => Interlocked.Increment(ref _failures);
    private void Finish(string name, Activity? activity, Activity? parent, long started,
        DiagnosticOperationOutcome outcome, string? errorType)
    {
        var previous = _notifying; _notifying = true;
        try
        {
            var elapsedSeconds = Stopwatch.GetElapsedTime(started).TotalSeconds;
            var result = outcome.ToString();
            if (activity != null)
            {
                try
                {
                    if (activity.IsAllDataRequested)
                    {
                        activity.SetTag("outcome", result);
                        if (errorType != null) activity.SetTag("error.type", errorType);
                        if (outcome == DiagnosticOperationOutcome.Failed) activity.SetStatus(ActivityStatusCode.Error);
                        else if (outcome == DiagnosticOperationOutcome.Succeeded) activity.SetStatus(ActivityStatusCode.Ok);
                    }
                    activity.Dispose();
                }
                catch { Failed(); }
                finally { if (ReferenceEquals(Activity.Current, activity)) Activity.Current = parent; }
            }
            if (_options.MetricsEnabled)
            {
                var tags = new TagList { { "operation", name }, { "outcome", result } };
                try { _count?.Add(1, tags); } catch { Failed(); }
                try { _duration?.Record(elapsedSeconds, tags); } catch { Failed(); }
            }
        }
        finally { Release(); _notifying = previous; }
    }
    private void Release()
    {
        bool release;
        lock (_gate) { _active--; release = _closed && _active == 0 && !_released; if (release) _released = true; }
        if (release) ReleaseInstrumentation();
    }
    private void ReleaseInstrumentation()
    {
        try { ActivitySource?.Dispose(); } catch { Failed(); }
        try { Meter.Dispose(); } catch { Failed(); }
    }
    /// <inheritdoc />
    public void Dispose()
    {
        bool release;
        lock (_gate) { _closed = true; release = _active == 0 && !_released; if (release) _released = true; }
        if (release) ReleaseInstrumentation();
    }
    private sealed class Operation(DiagnosticTelemetry owner, string name, Activity? activity, Activity? parent) : IDiagnosticOperation
    {
        private readonly Lock _gate = new();
        private readonly long _started = Stopwatch.GetTimestamp();
        private DiagnosticOperationOutcome _outcome = DiagnosticOperationOutcome.Incomplete;
        private string? _errorType;
        private bool _completed, _disposed;
        public void Complete(DiagnosticOperationOutcome outcome = DiagnosticOperationOutcome.Succeeded, Exception? error = null)
        {
            lock (_gate)
            {
                if (_completed || _disposed) return;
                _completed = true;
                _outcome = Enum.IsDefined(outcome) ? outcome : DiagnosticOperationOutcome.Incomplete;
                _errorType = Clip(error?.GetType().FullName, owner._options.MaxPropertyChars);
            }
        }
        public void Dispose()
        {
            DiagnosticOperationOutcome outcome; string? errorType;
            lock (_gate) { if (_disposed) return; _disposed = true; outcome = _outcome; errorType = _errorType; }
            owner.Finish(name, activity, parent, _started, outcome, errorType);
        }
    }
}
