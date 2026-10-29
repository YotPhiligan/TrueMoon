using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using TrueMoon.Diagnostics;

namespace TrueMoon.Aether.Editor.Diagnostics;

/// <summary>UI-owner presentation over a borrowed collector. No producer callbacks, timers or native resources.</summary>
public sealed partial class EditorDiagnosticsViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IDiagnosticRecordReader? _reader;
    private readonly EditorDiagnosticsOptions _options;
    private readonly EditorDiagnosticReportWriter _reports;
    private readonly Queue<(DiagnosticRecord Record, int Bytes)> _records = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Lock _shutdownGate = new();
    private Task? _stop;
    private Task? _export;
    private bool _disposed, _open, _follow = true, _group = true;
    private string _search = "", _source = "", _event = "", _session = "", _generation = "", _role = "";
    private DiagnosticLevel? _level;
    private long? _selection;
    private long _cursor, _gaps, _evicted, _truncated;
    private int _bytes;
    private string _details = "", _exportStatus = "";
    private IReadOnlyList<EditorDiagnosticEntry> _entries = Array.Empty<EditorDiagnosticEntry>();

    public EditorDiagnosticsViewModel(IDiagnosticRecordReader? reader, EditorDiagnosticsOptions options, EditorDiagnosticReportWriter reports)
        : this(reader, options, reports, null, null) { }

    public TimeSpan RefreshInterval => _options.RefreshInterval;
    public bool CollectorAvailable => _reader != null;
    public IReadOnlyList<EditorDiagnosticEntry> Entries => _entries;
    public int RetainedCount => _records.Count;
    /// <summary>Conservative payload estimate including per-record/property overhead; not a managed heap measurement.</summary>
    public int RetainedBytes => _bytes;
    public long Cursor => _cursor;
    /// <summary>Collector sequences not returned to this console; this is not a sink/durability counter.</summary>
    public long UnavailableSequences => _gaps;
    public long PresentationEvicted => _evicted;
    public long PresentationTruncated => _truncated;
    public string Details => _details;
    public string? SelectedId => Mode == EditorDiagnosticMode.Structured ? _selection?.ToString(System.Globalization.CultureInfo.InvariantCulture) : _contextSelection;
    public string Summary => Mode != EditorDiagnosticMode.Structured ? ContextSummary : _reader == null ? "История недоступна: подключите bounded UseDiagnostics." :
        $"Structured · {_entries.Count} строк / {_records.Count} событий · {_bytes / 1024} KiB · пропуски {_gaps} · вытеснено {_evicted} · усечено {_truncated} · health: {(_health == null ? "недоступно" : "отдельная вкладка")}";
    public string ExportStatus => _exportStatus;
    public bool IsExporting => _export is { IsCompleted: false };
    public bool CanExport => !IsExporting;
    private string _exportPath = Path.Combine(Path.GetTempPath(), $"TrueMoon-diagnostics-{Guid.NewGuid():N}.json");
    public string ExportPath
    {
        get => _exportPath;
        set { VerifyOpen(); ArgumentNullException.ThrowIfNull(value); if (_exportPath == value) return; _exportPath = value; Changed(nameof(ExportPath)); }
    }
    public bool IsOpen { get => _open; set { VerifyOpen(); if (_open == value) return; _open = value; Changed(nameof(IsOpen)); } }
    public bool FollowTail { get => _follow; set { VerifyOpen(); if (_follow == value) return; _follow = value; Changed(nameof(FollowTail)); } }
    public bool GroupRepeats { get => _group; set { VerifyOpen(); if (_group == value) return; _group = value; Rebuild(); Changed(nameof(GroupRepeats)); } }
    public DiagnosticLevel? MinimumLevel { get => _level; set { VerifyOpen(); if (value.HasValue && !Enum.IsDefined(value.Value)) throw new ArgumentOutOfRangeException(nameof(value)); if (_level == value) return; _level = value; Rebuild(); Changed(nameof(MinimumLevel)); } }
    public string Search { get => _search; set => Filter(ref _search, value, nameof(Search)); }
    public string Source { get => _source; set => Filter(ref _source, value, nameof(Source)); }
    public string EventName { get => _event; set => Filter(ref _event, value, nameof(EventName)); }
    public string Session { get => _session; set => Filter(ref _session, value, nameof(Session)); }
    public string Generation { get => _generation; set => Filter(ref _generation, value, nameof(Generation)); }
    public string RuntimeRole { get => _role; set => Filter(ref _role, value, nameof(RuntimeRole)); }
    public event PropertyChangedEventHandler? PropertyChanged;

    public void Toggle() => IsOpen = !IsOpen;

    /// <summary>Reads at most one bounded batch. Call on the presentation owner, at RefreshInterval or slower.</summary>
    public void Refresh()
    {
        VerifyOpen(); ObserveExport(); RefreshContext();
        if (_reader == null) return;
        var latest = _reader.LatestSequence;
        var batch = _reader.ReadAfter(_cursor, _options.RecordsPerRefresh);
        var changed = false;
        foreach (var original in batch.Take(_options.RecordsPerRefresh))
        {
            if (original.Sequence <= _cursor) continue;
            _gaps += original.Sequence - _cursor - 1; _cursor = original.Sequence;
            var (record, bytes) = Bound(original);
            if (record.IsTruncated) _truncated++;
            while (_records.Count > 0 && (_records.Count >= _options.MaximumRecords || _bytes + bytes > _options.MaximumRetainedBytes)) Evict();
            if (bytes <= _options.MaximumRetainedBytes) { _records.Enqueue((record, bytes)); _bytes += bytes; }
            else _evicted++;
            changed = true;
        }
        if (batch.Count == 0 && latest > _cursor) { _gaps += latest - _cursor; _cursor = latest; changed = true; }
        if (changed) Rebuild();
    }

    /// <summary>Clears presentation only. Collector and persistent JSONL remain unchanged.</summary>
    public void Clear()
    {
        VerifyOpen(); if (!CanClear) return; _records.Clear(); _bytes = 0; _selection = null;
        _cursor = _reader?.LatestSequence ?? _cursor; _gaps = _evicted = _truncated = 0; Rebuild();
    }

    public void Select(string? id)
    {
        VerifyOpen();
        if (Mode != EditorDiagnosticMode.Structured) { SelectContext(id); return; }
        _selection = long.TryParse(id, out var sequence) && _records.Any(item => item.Record.Sequence == sequence) ? sequence : null;
        UpdateDetails();
    }

    /// <summary>Reports include retained history regardless of the active display filter.</summary>
    public EditorDiagnosticSnapshot Snapshot()
    { VerifyOpen(); return new(Array.AsReadOnly(_records.Select(item => item.Record).ToArray()), _cursor, _gaps, _evicted, _truncated, CollectorAvailable) { PresentationLimits = _options, Health = _health, Processes = _processes }; }

    /// <summary>Starts one explicit partial export. Existing files are never overwritten.</summary>
    public void Export()
    {
        VerifyOpen(); if (IsExporting) return;
        ObserveExport();
        _exportStatus = "Экспорт идентификаторов без текста сообщений и стеков…";
        try { _export = _reports.WriteAsync(Snapshot(), ExportPath, cancellationToken: _lifetime.Token); }
        catch (Exception error) { _export = Task.FromException(error); ObserveExport(); }
        Changed(nameof(ExportStatus)); Changed(nameof(IsExporting)); Changed(nameof(CanExport));
    }

    private void ObserveExport()
    {
        if (_export is not { IsCompleted: true } finished) return;
        _export = null;
        try { finished.GetAwaiter().GetResult(); _exportStatus = "Частичный отчёт сохранён: metadata и доступный health, без raw text/сообщений/стеков."; }
        catch (OperationCanceledException) { _exportStatus = "Экспорт отменён или истёк лимит времени."; }
        catch (Exception error) { _exportStatus = "Экспорт не выполнен: " + error.GetType().Name; }
        Changed(nameof(ExportStatus)); Changed(nameof(IsExporting)); Changed(nameof(CanExport));
    }

    public Task StopAsync()
    { lock (_shutdownGate) return _stop ??= DrainAsync(); }

    private async Task DrainAsync()
    {
        _lifetime.Cancel();
        if (_export is { } export) try { await export.ConfigureAwait(false); } catch { /* Owner observes the final task; the UI reports its result on Refresh. */ }
    }

    private void Filter(ref string field, string value, string property)
    {
        VerifyOpen(); ArgumentNullException.ThrowIfNull(value);
        if (value.Length > 256) value = value[..256];
        if (field == value) return; field = value; Rebuild(); Changed(property);
    }

    private void Evict() { var old = _records.Dequeue(); _bytes -= old.Bytes; _evicted++; }
    private void Rebuild()
    {
        var visible = _records.Select(item => item.Record).Where(Matches);
        _entries = Array.AsReadOnly((_group
            ? visible.GroupBy(GroupKey).Select(group => new EditorDiagnosticEntry(group.First(), group.Count()))
            : visible.Select(record => new EditorDiagnosticEntry(record, 1))).ToArray());
        if (_selection.HasValue && !_records.Any(item => item.Record.Sequence == _selection)) _selection = null;
        UpdateDetails(); RebuildRows(); Changed(nameof(Entries)); Changed(nameof(Summary));
    }

    private bool Matches(DiagnosticRecord record) => (!_level.HasValue || record.Level >= _level) &&
        Contains(record.Source, _source) && Contains(record.EventName, _event) &&
        Exact(record, "SessionId", _session) && Exact(record, "Generation", _generation) && Exact(record, "RuntimeRole", _role) &&
        (_search.Length == 0 || Contains(Format(record), _search));
    private static bool Contains(string? value, string filter) => filter.Length == 0 || value?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true;
    private static bool Exact(DiagnosticRecord record, string key, string value) => value.Length == 0 ||
        record.Properties.TryGetValue(key, out var actual) && string.Equals(actual, value, StringComparison.OrdinalIgnoreCase);
    // Include all context/properties and exception text: events from different generations/builds never group together.
    private static string GroupKey(DiagnosticRecord record) => System.Text.Json.JsonSerializer.Serialize(new
    {
        record.ApplicationId, record.ProcessId, record.Source, record.Level, record.EventId, record.EventName, record.MessageTemplate,
        Properties = record.Properties.OrderBy(pair => pair.Key, StringComparer.Ordinal), record.ExceptionType, record.ExceptionMessage,
        record.ExceptionStackTrace, record.TraceId, record.SpanId, record.Caller, record.IsTruncated, Origin = record.Origin?.ApplicationId
    });

    private void UpdateDetails()
    {
        var record = _records.FirstOrDefault(item => item.Record.Sequence == _selection).Record;
        var text = Mode != EditorDiagnosticMode.Structured ? (_inspectedContext.Length == 0 ? "Выберите снимок. Обновить детали — получить новый. Raw text и ошибки могут содержать личные пути." : _inspectedContext)
            : record == null ? "Выберите событие. Ctrl+C в списке копирует его детали; текст может содержать личные пути." : Format(record);
        if (_details != text) { _details = text; Changed(nameof(Details)); }
        Changed(nameof(SelectedId));
    }

    private static string Format(DiagnosticRecord record)
    {
        var text = new StringBuilder().Append(record.TimestampUtc.ToString("O")).Append(" · structured · #").Append(record.Sequence)
            .Append(" · ").Append(record.Level).Append(" · ").AppendLine(record.Source)
            .Append(record.EventName).Append(" (").Append(record.EventId).AppendLine(")").AppendLine(record.MessageTemplate)
            .Append("App ").Append(record.ApplicationId).Append(" · PID ").Append(record.ProcessId).Append(" · Origin ").AppendLine(record.Origin?.ToString() ?? "local");
        foreach (var property in record.Properties) text.Append(property.Key).Append(": ").AppendLine(property.Value);
        if (record.ExceptionType != null) text.AppendLine(record.ExceptionType).AppendLine(record.ExceptionMessage).AppendLine(record.ExceptionStackTrace);
        if (record.IsTruncated) text.AppendLine("[Событие усечено collector или console budget]");
        return text.ToString().ReplaceLineEndings("\n");
    }

    private (DiagnosticRecord Record, int Bytes) Bound(DiagnosticRecord original)
    {
        var remaining = _options.MaximumRecordCharacters; var truncated = original.IsTruncated;
        string? Text(string? value, int limit = int.MaxValue)
        {
            if (value == null) return null;
            var count = Math.Min(value.Length, Math.Min(remaining, limit)); remaining -= count;
            if (count != value.Length) truncated = true;
            // Avoid retaining the caller's large backing string or cutting a surrogate pair.
            if (count > 0 && count < value.Length && char.IsHighSurrogate(value[count - 1])) count--;
            var result = value[..count];
            char[]? repaired = null;
            for (var index = 0; index < result.Length; index++)
            {
                if (!char.IsSurrogate(result[index])) continue;
                if (char.IsHighSurrogate(result[index]) && index + 1 < result.Length && char.IsLowSurrogate(result[index + 1])) { index++; continue; }
                repaired ??= result.ToCharArray(); repaired[index] = '\uFFFD'; truncated = true;
            }
            return repaired == null ? result : new string(repaired);
        }
        var source = Text(original.Source, 256)!; var name = Text(original.EventName, 256)!;
        var message = Text(original.MessageTemplate, 2048)!;
        var properties = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var pair in original.Properties.Take(32))
        {
            var key = Text(pair.Key, 128)!; var value = Text(pair.Value, 512);
            if (key.Length == 0 || !properties.TryAdd(key, value)) truncated = true;
        }
        if (original.Properties.Count > 32) truncated = true;
        var exceptionType = Text(original.ExceptionType, 256); var exceptionMessage = Text(original.ExceptionMessage, 1024);
        var stack = Text(original.ExceptionStackTrace); var trace = Text(original.TraceId, 128); var span = Text(original.SpanId, 128); var caller = Text(original.Caller, 256);
        var record = original with { Source = source, EventName = name, MessageTemplate = message,
            Properties = new ReadOnlyDictionary<string, string?>(properties), ExceptionType = exceptionType, ExceptionMessage = exceptionMessage,
            ExceptionStackTrace = stack, TraceId = trace, SpanId = span, Caller = caller, IsTruncated = truncated };
        return (record, 256 + 2 * (_options.MaximumRecordCharacters - remaining) + properties.Count * 128);
    }

    private void Changed(string property) => PropertyChanged?.Invoke(this, new(property));
    private void VerifyOpen() => ObjectDisposedException.ThrowIf(_disposed, this);
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; StopAsync().GetAwaiter().GetResult(); _lifetime.Dispose(); _records.Clear(); _entries = Array.Empty<EditorDiagnosticEntry>();
        _bytes = 0; _details = ""; _rows = []; _processes = null; _health = null; _inspectedContext = ""; PropertyChanged = null;
    }
}
