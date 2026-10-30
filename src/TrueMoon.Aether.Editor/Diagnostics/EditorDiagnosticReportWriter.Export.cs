using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Diagnostics;
using TrueMoon.Diagnostics;

namespace TrueMoon.Aether.Editor.Diagnostics;

public sealed partial class EditorDiagnosticReportWriter
{
    private readonly IDiagnosticArchiveReader? _archives;
    private readonly IDiagnosticConfigurationReader? _configuration;

    /// <summary>Creates a report writer without optional archive/configuration capabilities.</summary>
    public EditorDiagnosticReportWriter() : this(null, null) { }
    /// <summary>Creates a report writer borrowing Core's nondisposable read-only views.</summary>
    /// <param name="archives">Configured JSONL archive view, when available.</param>
    /// <param name="configuration">Memory-only effective scalar configuration.</param>
    public EditorDiagnosticReportWriter(IDiagnosticArchiveReader? archives, IDiagnosticConfigurationReader? configuration)
    { _archives = archives; _configuration = configuration; }

    /// <summary>Writes a bounded schema2 report from a frozen UI snapshot and optional persistent history.</summary>
    /// <param name="snapshot">Detached input from the console owner.</param><param name="path">New destination file; existing files are preserved.</param>
    /// <param name="options">Frozen text/history/privacy/budget choices.</param><param name="cancellationToken">User/App cancellation.</param>
    /// <returns>Collection and atomic publication completion. No IO runs on the UI owner.</returns>
    public Task WriteReportAsync(EditorDiagnosticSnapshot snapshot, string path, EditorDiagnosticExportOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot); ArgumentException.ThrowIfNullOrWhiteSpace(path); ArgumentNullException.ThrowIfNull(options);
        var settings = options.Snapshot(); var destination = Path.GetFullPath(path);
        return Task.Run(async () =>
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); deadline.CancelAfter(settings.TimeBudget);
            var token = deadline.Token; token.ThrowIfCancellationRequested();
            if (File.Exists(destination)) throw new IOException("Report destination already exists.");
            DiagnosticCollectorConfiguration? configuration = null; var failures = new Dictionary<string, string>();
            try { configuration = _configuration?.ReadConfiguration(); }
            catch (Exception error) when (error is not OperationCanceledException and not OutOfMemoryException) { failures["configuration"] = error.GetType().Name; }
            var history = new DiagnosticHistorySnapshot(false, []);
            if (settings.IncludePersistentHistory && _archives != null)
            {
                try { history = await _archives.ReadHistoryAsync(settings.History, token).ConfigureAwait(false); }
                catch (Exception error) when (error is not OperationCanceledException and not OutOfMemoryException) { failures["history"] = error.GetType().Name; }
            }
            token.ThrowIfCancellationRequested();
            using var buffer = new MemoryStream();
            using (var json = new Utf8JsonWriter(buffer))
            {
                json.WriteStartObject(); json.WriteNumber("schemaVersion", 2); json.WriteBoolean("partial", true);
                json.WriteString("textPolicy", settings.IncludeText ? "Explicit producer-text opt-in; may contain personal paths/user data. Control-field lines and configured sensitive literals are redacted. Not automatic anonymization."
                    : "Metadata only; producer text, exception fields, caller, arbitrary properties and source paths omitted.");
                json.WriteBoolean("includesText", settings.IncludeText); json.WriteBoolean("historyRequested", settings.IncludePersistentHistory);
                json.WriteString("createdUtc", DateTimeOffset.UtcNow); json.WriteString("captureConsistency", "Independent snapshots; no sink flush or durability guarantee.");
                json.WriteString("applicationVersion", System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString());
                json.WriteString("editorVersion", typeof(EditorDiagnosticReportWriter).Assembly.GetName().Version?.ToString());
                json.WriteString("contractsVersion", typeof(IDiagnosticRecordReader).Assembly.GetName().Version?.ToString());
                json.WriteString("os", RuntimeInformation.OSDescription); json.WriteString("architecture", RuntimeInformation.ProcessArchitecture.ToString());
                json.WriteNumber("maximumBytes", settings.MaximumBytes); json.WriteNumber("maximumTextCharacters", settings.MaximumTextCharacters);
                json.WriteNumber("timeBudgetMilliseconds", settings.TimeBudget.TotalMilliseconds);
                json.WriteBoolean("collectorAvailable", snapshot.CollectorAvailable); json.WriteNumber("cursor", snapshot.Cursor);
                json.WriteNumber("unavailableSequences", snapshot.UnavailableSequences); json.WriteNumber("presentationEvicted", snapshot.PresentationEvicted);
                json.WriteNumber("presentationTruncated", snapshot.PresentationTruncated);
                json.WritePropertyName("presentationLimits"); JsonSerializer.Serialize(json, snapshot.PresentationLimits);
                Optional("collectorConfiguration", configuration);
                Optional("presentation", snapshot.Presentation == null ? null : new
                {
                    Backend = Identifier(snapshot.Presentation.Backend), snapshot.Presentation.Width, snapshot.Presentation.Height, snapshot.Presentation.Scale,
                    Device = snapshot.Presentation.Device == null ? null : Identifier(snapshot.Presentation.Device)
                });
                WriteContext(json, snapshot, settings.MaximumBytes);
                json.WritePropertyName("history"); JsonSerializer.Serialize(json, new
                {
                    history.Available, history.FilesRead, history.BytesRead, history.InvalidLines, history.IncompleteLines, history.OversizedLines,
                    history.MissingEntries, history.SkippedEntries, history.ReadFailures, history.OmittedRecords, history.DuplicateRecords,
                    history.IsTruncated, history.TimeBudgetExceeded, CapturedRecords = history.Records.Count, Limits = settings.History
                });
                json.WritePropertyName("sourceFailures"); JsonSerializer.Serialize(json, failures);
                json.WriteStartArray("unavailable");
                json.WriteStringValue("persistentCompilerHistory");
                var runtimeProcesses = snapshot.Processes?.Processes.Where(process => process.SessionId != null).Take(64).ToArray() ?? [];
                if (runtimeProcesses.Length == 0 || runtimeProcesses.Any(process => process.Delivery?.RuntimeObservation?.Collector == null)) json.WriteStringValue("childSinkHealth");
                if (runtimeProcesses.Length == 0 || runtimeProcesses.Any(process => process.Delivery?.RuntimeObservation?.GraphicsDevice == null)) json.WriteStringValue("runtimeGraphicsDevice");
                if (configuration == null) json.WriteStringValue("effectiveCollectorConfiguration");
                if (snapshot.Presentation == null) json.WriteStringValue("presentationBackend");
                if (snapshot.Presentation?.Device == null) json.WriteStringValue("presentationDevice");
                if (settings.IncludePersistentHistory && !history.Available) json.WriteStringValue("persistentHistory");
                if (snapshot.Health == null) json.WriteStringValue("sinkHealth");
                if (snapshot.Processes == null) json.WriteStringValue("rawStdoutStderr");
                else
                {
                    if (!snapshot.Processes.RuntimeAvailable) json.WriteStringValue("runtimeCapture");
                    if (!snapshot.Processes.BuildAvailable) json.WriteStringValue("buildCapture");
                }
                json.WriteEndArray(); json.WriteStartArray("omittedByPolicy");
                foreach (var name in new[] { "bootstrap", "environment", "controlEndpoints", "projectFiles", "caller", "arbitraryProperties", "configurationRuleNames" }) json.WriteStringValue(name);
                if (!settings.IncludePersistentHistory) json.WriteStringValue("persistentHistory");
                json.WriteStringValue("childSinkErrorText");
                if (!settings.IncludeText) foreach (var name in new[] { "rawText", "messages", "exceptions", "stacks", "sourcePaths", "compilerText", "sinkErrorText" }) json.WriteStringValue(name);
                json.WriteEndArray();

                json.WriteString("runtimeObservationPolicy", "Last independently received observation; may be stale, including after crash. Missing data is unavailable, not healthy zero.");
                json.WriteStartArray("runtimeObservations"); var observationsOmitted = 0;
                foreach (var process in runtimeProcesses)
                {
                    token.ThrowIfCancellationRequested(); var observation = process.Delivery?.RuntimeObservation;
                    var device = observation?.GraphicsDevice;
                    var encoded = JsonSerializer.SerializeToUtf8Bytes(new
                    {
                        process.SessionId, process.Generation, process.ProcessId, process.Completed,
                        CapturedUtc = observation?.CapturedUtc, CollectorAvailable = observation?.Collector != null, DeviceAvailable = device != null,
                        Collector = HealthMetadata(observation?.Collector, 16), OmittedSinks = observation?.OmittedSinks,
                        ObservationOmittedPackets = process.Delivery?.ObservationOmittedPackets,
                        GraphicsDevice = device == null ? null : new { Backend = Identifier(device.Backend), Name = Identifier(device.Name), device.ApiVersion, device.DriverVersion, device.VendorId, device.DeviceId }
                    });
                    if (json.BytesCommitted + json.BytesPending + encoded.Length + 2048 > settings.MaximumBytes) { observationsOmitted++; continue; }
                    json.WriteRawValue(encoded);
                }
                json.WriteEndArray(); json.WriteNumber("runtimeObservationsOmittedByBudget", observationsOmitted);

                var text = new ExportText(settings); var merged = Merge(snapshot.Records, history.Records);
                var records = new List<byte[]>(); var recordBytes = 0; json.Flush();
                var remaining = settings.MaximumBytes - buffer.Length - 1024;
                // Keep a bounded share for optional raw/compiler text rather than letting record text consume the entire report.
                var recordBudget = settings.IncludeText ? remaining * 2 / 3 : remaining;
                foreach (var record in merged.OrderByDescending(record => record.TimestampUtc).ThenByDescending(record => record.ApplicationId).ThenByDescending(record => record.Sequence))
                {
                    token.ThrowIfCancellationRequested();
                    var data = JsonSerializer.SerializeToUtf8Bytes(new
                    {
                        record.TimestampUtc, record.Sequence, record.ApplicationId, record.ProcessId, Source = Identifier(record.Source), record.Level,
                        record.EventId, EventName = Identifier(record.EventName), record.IsTruncated, record.Origin,
                        TraceId = Hex(record.TraceId, 32), SpanId = Hex(record.SpanId, 16),
                        Context = ContextKeys.Where(key => record.Properties.TryGetValue(key, out var value) && SafeContext(value))
                            .ToDictionary(key => key, key => record.Properties[key], StringComparer.Ordinal),
                        Text = settings.IncludeText ? new { Message = text.Clean(record.MessageTemplate), ExceptionType = text.Clean(record.ExceptionType),
                            ExceptionMessage = text.Clean(record.ExceptionMessage), ExceptionStack = text.Clean(record.ExceptionStackTrace) } : null
                    });
                    if (recordBytes + data.Length + 1 > recordBudget) break;
                    records.Add(data); recordBytes += data.Length + 1;
                }
                json.WriteStartArray("records"); for (var index = records.Count - 1; index >= 0; index--) json.WriteRawValue(records[index]); json.WriteEndArray();
                json.WriteNumber("exportedRecords", records.Count); json.WriteNumber("omittedRecords", merged.Count - records.Count);
                json.WriteNumber("memoryRecordsCaptured", Math.Min(snapshot.Records.Count, 1024));
                var rawWritten = 0; var rawOmitted = 0; var compilerWritten = 0; var compilerOmitted = 0;
                json.WriteStartArray("rawStreams");
                foreach (var process in snapshot.Processes?.Processes.Take(64) ?? [])
                {
                    foreach (var (stream, name) in new[] { (process.StandardOutput, "stdout"), (process.StandardError, "stderr") })
                    {
                        if (stream == null) continue;
                        if (!settings.IncludeText) { rawOmitted++; continue; }
                        token.ThrowIfCancellationRequested();
                        if (Entry(new { Source = Identifier(process.Source), process.SessionId, process.Generation, JobId = SafeContext(process.JobId) ? process.JobId : null,
                            process.SourceRevision, Stream = name, stream.Output.TotalCharacters, stream.Output.DroppedCharacters, stream.Output.OmittedCharacters,
                            Text = text.Clean(stream.Output.Text), FileError = text.Clean(stream.Output.FileError),
                            ReadFailure = stream.ReadFailureType == null ? null : Identifier(stream.ReadFailureType),
                            FileFailure = stream.FileFailureType == null ? null : Identifier(stream.FileFailureType) })) rawWritten++; else rawOmitted++;
                    }
                }
                json.WriteEndArray(); json.WriteStartArray("compilerDiagnostics");
                foreach (var process in snapshot.Processes?.Processes.Take(64) ?? [])
                {
                    if (process.CompilerDiagnostics is not { } compiler) continue;
                    foreach (var diagnostic in compiler.Entries.Take(128))
                    {
                        if (!settings.IncludeText) { compilerOmitted++; continue; }
                        token.ThrowIfCancellationRequested();
                        if (Entry(new { JobId = SafeContext(process.JobId) ? process.JobId : null, process.SourceRevision,
                            Code = Identifier(diagnostic.Code), diagnostic.Severity, Message = text.Clean(diagnostic.Message),
                            FilePath = text.Clean(diagnostic.FilePath), diagnostic.Line, diagnostic.Column })) compilerWritten++; else compilerOmitted++;
                    }
                }
                json.WriteEndArray(); json.WriteStartArray("sinkErrors"); var omittedSinkErrors = 0;
                foreach (var sink in snapshot.Health?.Sinks.Take(66) ?? [])
                    if (sink.LastError != null && settings.IncludeText && !Entry(new { Sink = Identifier(sink.Name), Error = text.Clean(sink.LastError) })) omittedSinkErrors++;
                if (settings.IncludeText && snapshot.Health?.Emergency?.LastError is { } emergencyError && !Entry(new { Sink = "emergency", Error = text.Clean(emergencyError) })) omittedSinkErrors++;
                if (settings.IncludeText && snapshot.Health?.Retention?.LastError is { } retentionError && !Entry(new { Sink = "retention", Error = text.Clean(retentionError) })) omittedSinkErrors++;
                json.WriteEndArray(); json.WriteNumber("omittedSinkErrorsByBudget", omittedSinkErrors);
                json.WriteNumber("exportedRawStreams", rawWritten); json.WriteNumber("omittedRawStreams", rawOmitted);
                json.WriteNumber("exportedCompilerDiagnostics", compilerWritten); json.WriteNumber("omittedCompilerDiagnostics", compilerOmitted);
                json.WriteNumber("textFieldsTruncated", text.Truncated); json.WriteNumber("textFieldsRedacted", text.Redacted);
                json.WriteEndObject(); json.Flush();
                if (buffer.Length > settings.MaximumBytes) throw new InvalidOperationException("Report header exceeds its byte budget.");

                void Optional(string name, object? value)
                {
                    var encoded = JsonSerializer.SerializeToUtf8Bytes(value);
                    var fits = value != null && encoded.Length <= settings.MaximumBytes / 4 && json.BytesCommitted + json.BytesPending + encoded.Length + 2304 <= settings.MaximumBytes;
                    json.WritePropertyName(name); if (fits) json.WriteRawValue(encoded); else json.WriteNullValue();
                    json.WriteBoolean(name + "OmittedByBudget", value != null && !fits);
                }
                bool Entry(object value)
                {
                    var encoded = JsonSerializer.SerializeToUtf8Bytes(value);
                    if (json.BytesCommitted + json.BytesPending + encoded.Length + 768 > settings.MaximumBytes) return false;
                    json.WriteRawValue(encoded); return true;
                }
            }
            token.ThrowIfCancellationRequested();
            var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
                {
                    buffer.Position = 0; await buffer.CopyToAsync(file, token).ConfigureAwait(false); await file.FlushAsync(token).ConfigureAwait(false);
                }
                token.ThrowIfCancellationRequested(); File.Move(temporary, destination);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }, cancellationToken);
    }

    private static List<DiagnosticRecord> Merge(IReadOnlyList<DiagnosticRecord> memory, IReadOnlyList<DiagnosticRecord> history)
    {
        var records = new Dictionary<(Guid, long), DiagnosticRecord>();
        foreach (var record in history.Take(1024)) records[(record.ApplicationId, record.Sequence)] = record;
        foreach (var record in memory.Take(1024)) records[(record.ApplicationId, record.Sequence)] = record;
        return records.Values.ToList();
    }
    private static string? Hex(string? value, int length) => value?.Length == length && value.All(char.IsAsciiHexDigit) ? value : null;
    private sealed class ExportText(EditorDiagnosticExportOptions options)
    {
        internal int Truncated, Redacted;
        internal string? Clean(string? value)
        {
            if (value == null) return null;
            // Inspect a bounded suffix too, so a configured literal crossing the output boundary is removed before clipping.
            var count = Math.Min(value.Length, options.MaximumTextCharacters + 512); var truncated = count < value.Length;
            if (count > 0 && char.IsHighSurrogate(value[count - 1])) count--;
            var result = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(value[..count])).ReplaceLineEndings("\n"); var redacted = false;
            var lines = result.Split('\n');
            for (var index = 0; index < lines.Length; index++)
                if (SensitiveKeys.Any(key => lines[index].Contains(key, StringComparison.OrdinalIgnoreCase)))
                { lines[index] = "[control data omitted]"; redacted = true; }
            result = string.Join('\n', lines);
            foreach (var literal in options.RedactedValues)
                if (result.Contains(literal, StringComparison.Ordinal)) { result = result.Replace(literal, "[redacted]", StringComparison.Ordinal); redacted = true; }
            if (result.Length > options.MaximumTextCharacters)
            {
                var length = options.MaximumTextCharacters; if (char.IsHighSurrogate(result[length - 1])) length--;
                result = result[..length]; truncated = true;
            }
            if (truncated) Truncated++; if (redacted) Redacted++; return result;
        }
        private static readonly string[] SensitiveKeys = ["nonce", "bootstrap", "endpoint", "environment", "authorization", "api_key", "api-key", "token=", "token:"];
    }
}
