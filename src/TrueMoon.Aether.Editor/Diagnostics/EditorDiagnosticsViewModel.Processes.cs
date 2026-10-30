using System.Text;
using TrueMoon.Aether.Diagnostics;
using TrueMoon.Diagnostics;

namespace TrueMoon.Aether.Editor.Diagnostics;

public sealed partial class EditorDiagnosticsViewModel
{
    private readonly IDiagnosticHealthReader? _healthReader;
    private readonly IProcessDiagnosticsSource? _processSource;
    private DiagnosticHealthSnapshot? _health;
    private ProcessDiagnosticBatch? _processes;
    private EditorDiagnosticMode _mode;
    private string? _contextSelection;
    private string _inspectedContext = "";
    private IReadOnlyList<EditorDiagnosticRow> _rows = Array.Empty<EditorDiagnosticRow>();

    /// <summary>Creates a presentation model over borrowed history, health and process snapshot sources.</summary>
    public EditorDiagnosticsViewModel(IDiagnosticRecordReader? reader, EditorDiagnosticsOptions options, EditorDiagnosticReportWriter reports,
        IDiagnosticHealthReader? healthReader, IProcessDiagnosticsSource? processSource)
        : this(reader, options, reports, healthReader, processSource, null) { }

    /// <summary>Creates a presentation model borrowing an optional source-navigation hook.</summary>
    public EditorDiagnosticsViewModel(IDiagnosticRecordReader? reader, EditorDiagnosticsOptions options, EditorDiagnosticReportWriter reports,
        IDiagnosticHealthReader? healthReader, IProcessDiagnosticsSource? processSource, ISourceFileNavigator? navigator)
    {
        ArgumentNullException.ThrowIfNull(options); ArgumentNullException.ThrowIfNull(reports);
        _reader = reader; _options = options.Snapshot(); _reports = reports; _healthReader = healthReader; _processSource = processSource; _navigator = navigator;
        UpdateDetails();
    }

    public IReadOnlyList<EditorDiagnosticRow> Rows => _rows;
    public ProcessDiagnosticBatch? Processes => _processes;
    public DiagnosticHealthSnapshot? CollectorHealth => _health;
    public bool CanClear => _mode == EditorDiagnosticMode.Structured;
    public bool CanFilter => _mode != EditorDiagnosticMode.Health;
    public bool CanRefreshDetails => _mode != EditorDiagnosticMode.Structured && _contextSelection != null;
    public EditorDiagnosticMode Mode
    {
        get => _mode;
        set
        {
            VerifyOpen(); if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (_mode == value) return; _mode = value; _contextSelection = null; _inspectedContext = "";
            _selectedSource = null;
            RebuildRows(); UpdateDetails(); Changed(nameof(Mode)); Changed(nameof(CanClear)); Changed(nameof(CanFilter)); Changed(nameof(CanRefreshDetails)); Changed(nameof(Summary)); Changed(nameof(CanOpenSource));
        }
    }

    /// <summary>Explicitly updates the inspected raw/health snapshot. Ordinary polling preserves selection and the inspected text.</summary>
    public void RefreshDetails()
    { VerifyOpen(); if (_contextSelection is { } id) SelectContext(id); }

    private void RefreshContext()
    {
        _health = BoundHealth(_healthReader?.ReadHealth());
        var batch = _processSource?.Read(_options.MaximumProcesses, _options.MaximumRawCharacters);
        var compilerRemaining = _options.MaximumRecords; var compilerBytes = _options.MaximumRetainedBytes;
        _processes = batch == null ? null : batch with
        {
            Processes = Array.AsReadOnly(batch.Processes.Take(_options.MaximumProcesses).Select(process =>
                BoundProcess(process) with { CompilerDiagnostics = BoundCompiler(process.CompilerDiagnostics, ref compilerRemaining, ref compilerBytes) }).ToArray())
        };
        if (Mode != EditorDiagnosticMode.Structured) RebuildRows();
        // Keep an inspected snapshot stable while output grows. Remove it only if its owner leaves the bounded history.
        if (_contextSelection != null && Mode is EditorDiagnosticMode.StandardOutput or EditorDiagnosticMode.StandardError &&
            !(_processes?.Processes.Any(process => process.Id == _contextSelection) ?? false))
        { _contextSelection = null; _inspectedContext = ""; UpdateDetails(); }
        if (Mode == EditorDiagnosticMode.Compiler && _contextSelection != null && !HasCompilerSelection(_contextSelection)) SelectCompiler(null);
        Changed(nameof(Summary));
    }

    private ProcessDiagnosticSnapshot BoundProcess(ProcessDiagnosticSnapshot process) => process with
    {
        Id = Display(process.Id, 256), Source = Display(process.Source, 128), RuntimeRole = process.RuntimeRole == null ? null : Display(process.RuntimeRole, 128),
        GameBuildId = process.GameBuildId == null ? null : Display(process.GameBuildId, 256), JobId = process.JobId == null ? null : Display(process.JobId, 128),
        StandardOutput = BoundStream(process.StandardOutput), StandardError = BoundStream(process.StandardError),
        Delivery = process.Delivery == null ? null : process.Delivery with
        {
            LastFailureType = process.Delivery.LastFailureType == null ? null : Display(process.Delivery.LastFailureType, 256),
            RuntimeObservation = BoundObservation(process.Delivery.RuntimeObservation)
        }
    };
    private static RuntimeDiagnosticObservation? BoundObservation(RuntimeDiagnosticObservation? observation)
    {
        if (observation == null) return null;
        var health = BoundHealth(observation.Collector);
        return observation with
        {
            Collector = health == null ? null : health with { Sinks = Array.AsReadOnly(health.Sinks.Take(16).ToArray()) },
            OmittedSinks = (int)Math.Min(int.MaxValue, Math.Max(0L, observation.OmittedSinks) + Math.Max(0, (observation.Collector?.Sinks.Count ?? 0) - 16)),
            GraphicsDevice = observation.GraphicsDevice is not { } device ? null : device with { Backend = Display(device.Backend, 64), Name = Display(device.Name, 256) }
        };
    }
    private ProcessStreamDiagnostic? BoundStream(ProcessStreamDiagnostic? stream)
    {
        if (stream == null) return null;
        var output = stream.Output.Limit(_options.MaximumRawCharacters);
        return stream with
        {
            Output = output with { Text = Display(output.Text, _options.MaximumRawCharacters), FileError = output.FileError == null ? null : Display(output.FileError, 1024) },
            ReadFailureType = stream.ReadFailureType == null ? null : Display(stream.ReadFailureType, 256), FileFailureType = stream.FileFailureType == null ? null : Display(stream.FileFailureType, 256)
        };
    }
    private static DiagnosticHealthSnapshot? BoundHealth(DiagnosticHealthSnapshot? health) => health == null ? null : health with
    {
        Sinks = Array.AsReadOnly(health.Sinks.Take(66).Select(sink => sink with { Name = Display(sink.Name, 256), LastError = sink.LastError == null ? null : Display(sink.LastError, 1024) }).ToArray()),
        Emergency = health.Emergency == null ? null : health.Emergency with { LastError = health.Emergency.LastError == null ? null : Display(health.Emergency.LastError, 256) },
        Retention = health.Retention == null ? null : health.Retention with { LastError = health.Retention.LastError == null ? null : Display(health.Retention.LastError, 1024) }
    };

    private void RebuildRows()
    {
        IEnumerable<EditorDiagnosticRow> rows = Mode switch
        {
            EditorDiagnosticMode.Structured => _entries.Select(entry => new EditorDiagnosticRow(entry.Id, entry.Label) { Warning = entry.Record.Level >= DiagnosticLevel.Warning }),
            EditorDiagnosticMode.Health => HealthRows(),
            EditorDiagnosticMode.Compiler => CompilerRows(),
            _ => (_processes?.Processes ?? []).Where(MatchesProcess).Select(process =>
            {
                var stream = Stream(process); var output = stream?.Output;
                return new EditorDiagnosticRow(process.Id, $"RAW {(Mode == EditorDiagnosticMode.StandardOutput ? "stdout" : "stderr")} · {process.Source} · PID {process.ProcessId?.ToString() ?? "—"} · " +
                    (process.SessionId.HasValue ? $"g{process.Generation} {process.RuntimeRole}" : $"job {process.JobId}") +
                    $" · {(process.Completed ? "завершён" : "активен")} · {output?.TotalCharacters.ToString() ?? "недоступно"} chars");
            })
        };
        var next = rows.ToArray();
        if (_rows.SequenceEqual(next)) return;
        _rows = Array.AsReadOnly(next); Changed(nameof(Rows)); Changed(nameof(SelectedId)); Changed(nameof(CanRefreshDetails));
    }

    private bool MatchesProcess(ProcessDiagnosticSnapshot process) => Contains(process.Source, _source) &&
        (_session.Length == 0 || string.Equals(process.SessionId?.ToString("D"), _session, StringComparison.OrdinalIgnoreCase)) &&
        (_generation.Length == 0 || process.Generation?.ToString(System.Globalization.CultureInfo.InvariantCulture) == _generation) &&
        (_role.Length == 0 || string.Equals(process.RuntimeRole, _role, StringComparison.OrdinalIgnoreCase)) &&
        (Mode == EditorDiagnosticMode.Compiler || _search.Length == 0 || Contains(FormatProcess(process), _search));
    private ProcessStreamDiagnostic? Stream(ProcessDiagnosticSnapshot process) => Mode == EditorDiagnosticMode.StandardError ? process.StandardError : process.StandardOutput;
    private void SelectContext(string? id)
    {
        if (Mode == EditorDiagnosticMode.Compiler) { SelectCompiler(id); return; }
        _contextSelection = id;
        if (Mode == EditorDiagnosticMode.Health) _inspectedContext = HealthDetails(id);
        else
        {
            var process = _processes?.Processes.FirstOrDefault(process => process.Id == id);
            if (process == null) _contextSelection = null;
            _inspectedContext = process == null ? "" : FormatProcess(process);
        }
        UpdateDetails(); Changed(nameof(CanRefreshDetails));
    }

    private string ContextSummary => Mode == EditorDiagnosticMode.Health
        ? $"Health · Core parent {(_health == null ? "недоступно" : "доступен")} · runtime capture {(_processes?.RuntimeAvailable == true ? "доступен" : "недоступно")} · Accepted/Written не означают disk durability"
        : Mode == EditorDiagnosticMode.Compiler
        ? $"Compiler · SARIF2.1 · build capture {(_processes?.BuildAvailable == true ? "доступен" : "недоступно")} · до {_options.MaximumRecords} diagnostics / {_options.MaximumRetainedBytes / 1024} KiB · координаты относятся к исходной сборке"
        : $"RAW {(Mode == EditorDiagnosticMode.StandardOutput ? "stdout" : "stderr")} · {_rows.Count} процессов · tail до {_options.MaximumRawCharacters} символов · level/event filters: только structured · Обновить детали — новый снимок";

    private IEnumerable<EditorDiagnosticRow> HealthRows()
    {
        yield return new("collector", _health == null ? "Core collector health: недоступно" :
            $"Core · retained {_health.RetainedRecords} · evicted {_health.MemoryEvicted} · capture failures {_health.CaptureFailures} · late {_health.LateWrites} · closed {_health.Closed}");
        if (_health != null)
        {
            for (var index = 0; index < Math.Min(66, _health.Sinks.Count); index++)
            {
                var sink = _health.Sinks[index];
                yield return new("sink:" + index, $"Sink {Display(sink.Name, 256)} · accepted {sink.Accepted} · written {sink.Written} · dropped {sink.Dropped} · failures {sink.Failures} · queued {sink.Queued}");
            }
            yield return new("emergency", _health.Emergency is not { } emergency ? "Emergency: недоступно/выключен" : $"Emergency · dropped {emergency.Dropped} · suppressed {emergency.Suppressed} · failures {emergency.Failures} · queued {emergency.Queued}");
            yield return new("retention", _health.Retention is not { } retention ? "JSONL retention: недоступно/выключен" : $"Retention · runs {retention.RetainedRuns} · failures {retention.Failures} · over budget {retention.IsOverBudget} · busy {retention.MaintenanceBusy}");
        }
        yield return new("capabilities", $"Process capture · runtime {(_processes?.RuntimeAvailable == true ? "доступен" : "недоступно")} · build {(_processes?.BuildAvailable == true ? "доступен" : "недоступно")}");
        foreach (var process in _processes?.Processes ?? [])
            yield return new("process:" + process.Id, $"{process.Source} PID {process.ProcessId} · {(process.Completed ? "завершён" : "активен")} · released {process.ResourcesReleased} · " +
                (process.Delivery is { } lane ? $"delivery accepted {lane.Accepted} / lost {lane.Lost} / unconfirmed {lane.Unconfirmed} / failures {lane.Failures}" : "delivery: недоступно/выключен"));
    }

    private string HealthDetails(string? id)
    {
        var text = new StringBuilder("HEALTH SNAPSHOT · Обновить детали — новый снимок\nCore health относится к parent App. Child health — последнее независимое наблюдение, в том числе после crash.\nAccepted/Written не подтверждают disk durability. Counters разных workers сняты независимо.\n");
        if (id == "collector" && _health is { } health)
            text.AppendLine($"App {health.ApplicationId}\nClosed {health.Closed}; sequence {health.LatestSequence}\nRetained {health.RetainedRecords}/{health.RetainedBytes} bytes\nMemory evicted {health.MemoryEvicted}; capture failures {health.CaptureFailures}; late writes {health.LateWrites}");
        else if (id?.StartsWith("sink:", StringComparison.Ordinal) == true && int.TryParse(id[5..], out var index) && _health != null && index >= 0 && index < _health.Sinks.Count)
            text.AppendLine(_health.Sinks[index].ToString());
        else if (id == "emergency") text.AppendLine(_health?.Emergency?.ToString() ?? "Недоступно/выключен.");
        else if (id == "retention") text.AppendLine(_health?.Retention?.ToString() ?? "Недоступно/выключен.");
        else if (id?.StartsWith("process:", StringComparison.Ordinal) == true && _processes?.Processes.FirstOrDefault(process => process.Id == id[8..]) is { } process)
        {
            text.AppendLine($"{process.Source}; PID {process.ProcessId}; session {process.SessionId}; generation {process.Generation}; role {process.RuntimeRole}; build {process.GameBuildId}; job {process.JobId}; revision {process.SourceRevision}");
            text.AppendLine($"Completed {process.Completed}; resources released {process.ResourcesReleased}");
            AppendStream(text, "stdout", process.StandardOutput, false); AppendStream(text, "stderr", process.StandardError, false);
            text.AppendLine("Delivery: " + (process.Delivery?.ToString() ?? "недоступно/выключен"));
            if (process.Delivery?.RuntimeObservation is { } observation)
            {
                text.AppendLine($"Child observed {observation.CapturedUtc:O}; omitted sinks {observation.OmittedSinks}; observation packets omitted {process.Delivery.ObservationOmittedPackets}");
                if (observation.Collector is { } child)
                {
                    text.AppendLine($"Child collector {child.ApplicationId}; retained {child.RetainedRecords}; evicted {child.MemoryEvicted}; capture failures {child.CaptureFailures}; closed {child.Closed}");
                    foreach (var sink in child.Sinks.Take(16)) text.AppendLine($"Child sink {sink.Name}: accepted {sink.Accepted}; written {sink.Written}; dropped {sink.Dropped}; failures {sink.Failures}; queued {sink.Queued}");
                    text.AppendLine($"Child emergency: {(child.Emergency == null ? "недоступно/выключен" : $"failures {child.Emergency.Failures}; dropped {child.Emergency.Dropped}")}");
                    text.AppendLine($"Child retention: {(child.Retention == null ? "недоступно/выключен" : $"failures {child.Retention.Failures}; over budget {child.Retention.IsOverBudget}")}");
                }
                else text.AppendLine("Child sinks: недоступны.");
                text.AppendLine(observation.GraphicsDevice is { } device ? $"Last runtime device: {device.Backend} / {device.Name} / API {device.ApiVersion} / driver {device.DriverVersion}" : "Runtime device: недоступен/CPU-only.");
            }
            else text.AppendLine("Child sinks/device: недоступны; наблюдение не получено.");
        }
        else text.AppendLine(ContextSummary);
        return Display(text.ToString(), 32768);
    }

    private string FormatProcess(ProcessDiagnosticSnapshot process)
    {
        var text = new StringBuilder($"RAW {(Mode == EditorDiagnosticMode.StandardError ? "STDERR" : "STDOUT")} SNAPSHOT · severity не определяется по stream\n");
        text.AppendLine($"{process.Source}; PID {process.ProcessId}; started {process.StartedUtc:O}\nSession {process.SessionId}; generation {process.Generation}; role {process.RuntimeRole}\nBuild {process.GameBuildId}; job {process.JobId}; source revision {process.SourceRevision}\nCompleted {process.Completed}; resources released {process.ResourcesReleased}");
        AppendStream(text, Mode == EditorDiagnosticMode.StandardError ? "stderr" : "stdout", Stream(process), true);
        return Display(text.ToString(), _options.MaximumRawCharacters + 4096);
    }
    private static void AppendStream(StringBuilder text, string name, ProcessStreamDiagnostic? stream, bool includeText)
    {
        if (stream == null) { text.AppendLine(name + ": capture недоступен."); return; }
        var output = stream.Output;
        text.AppendLine($"{name}: read finished {stream.ReadFinished}; file finished {stream.FileFinished}; reader failure {stream.ReadFailureType ?? "—"}; file failure {stream.FileFailureType ?? "—"}");
        text.AppendLine($"Captured {output.TotalCharacters} chars; capture evicted {output.DroppedCharacters}; display omitted {output.OmittedCharacters}; file written {output.FileBytesWritten} bytes; file dropped {output.FileBytesDropped} bytes\nFile error: {output.FileError ?? "—"}");
        if (includeText) text.AppendLine("--- raw tail ---").Append(output.Text);
    }
    private static string Display(string? text, int limit)
    {
        if (text == null) return "";
        var length = Math.Min(text.Length, limit); if (length > 0 && char.IsHighSurrogate(text[length - 1])) length--;
        return Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(text[..length])).ReplaceLineEndings("\n");
    }
}
