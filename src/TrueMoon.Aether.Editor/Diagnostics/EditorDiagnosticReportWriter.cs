using System.Runtime.InteropServices;
using System.Text.Json;
using TrueMoon.Diagnostics;
using TrueMoon.Aether.Diagnostics;

namespace TrueMoon.Aether.Editor.Diagnostics;

/// <summary>Writes an explicitly partial metadata report. Does not read project files, environment, control endpoints or disk history.</summary>
public sealed class EditorDiagnosticReportWriter
{
    public const int DefaultMaximumBytes = 512 * 1024;
    private static readonly string[] ContextKeys = ["SessionId", "Generation", "RuntimeRole", "GameBuildId", "BuildJobId", "JobId", "ArtifactId"];

    /// <summary>Creates a new report atomically. Free text, exceptions, stacks, caller and arbitrary properties are always omitted.</summary>
    /// <remarks>The snapshot must come from EditorDiagnosticsViewModel.Snapshot. Cancellation/time limits are cooperative filesystem limits.</remarks>
    public Task WriteAsync(EditorDiagnosticSnapshot snapshot, string path, int maximumBytes = DefaultMaximumBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot); ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (maximumBytes is < 4096 or > 4 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        var destination = Path.GetFullPath(path);
        return Task.Run(async () =>
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(5));
            var token = deadline.Token; token.ThrowIfCancellationRequested();
            using var buffer = new MemoryStream();
            using (var json = new Utf8JsonWriter(buffer))
            {
                json.WriteStartObject(); json.WriteNumber("schemaVersion", 1); json.WriteBoolean("partial", true);
                json.WriteString("textPolicy", "Messages, exceptions, stacks, caller and arbitrary properties omitted; source/event identifiers retained.");
                json.WriteString("createdUtc", DateTimeOffset.UtcNow);
                json.WriteString("applicationVersion", System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString());
                json.WriteString("editorVersion", typeof(EditorDiagnosticReportWriter).Assembly.GetName().Version?.ToString());
                json.WriteString("contractsVersion", typeof(IDiagnosticRecordReader).Assembly.GetName().Version?.ToString());
                json.WriteString("os", RuntimeInformation.OSDescription); json.WriteString("architecture", RuntimeInformation.ProcessArchitecture.ToString());
                json.WriteBoolean("collectorAvailable", snapshot.CollectorAvailable);
                json.WriteNumber("cursor", snapshot.Cursor); json.WriteNumber("unavailableSequences", snapshot.UnavailableSequences);
                json.WriteNumber("presentationEvicted", snapshot.PresentationEvicted); json.WriteNumber("presentationTruncated", snapshot.PresentationTruncated);
                json.WriteNumber("maximumBytes", maximumBytes);
                json.WritePropertyName("presentationLimits"); JsonSerializer.Serialize(json, snapshot.PresentationLimits);
                WriteContext(json, snapshot, maximumBytes);
                json.WriteStartArray("unavailable");
                foreach (var item in new[] { "persistentHistory", "effectiveCollectorConfiguration", "backendDevice", "compilerNavigation", "childSinkHealth" }) json.WriteStringValue(item);
                if (snapshot.Health == null) json.WriteStringValue("sinkHealth");
                if (snapshot.Processes == null) json.WriteStringValue("rawStdoutStderr");
                else
                {
                    if (!snapshot.Processes.RuntimeAvailable) json.WriteStringValue("runtimeCapture");
                    if (!snapshot.Processes.BuildAvailable) json.WriteStringValue("buildCapture");
                }
                json.WriteEndArray(); json.WriteStartArray("omittedByPolicy");
                foreach (var item in new[] { "rawText", "messages", "exceptions", "stacks", "caller", "arbitraryProperties", "sinkErrorText" }) json.WriteStringValue(item);
                json.WriteEndArray(); json.WriteStartArray("records"); json.Flush();
                var written = 0;
                // Export the most recent metadata, in collector sequence order. Bound input iteration as well as output bytes.
                var encoded = new List<byte[]>(); var bytes = (int)buffer.Length + 256;
                for (var i = snapshot.Records.Count - 1; i >= Math.Max(0, snapshot.Records.Count - 1024); i--)
                {
                    token.ThrowIfCancellationRequested(); var record = snapshot.Records[i];
                    var data = JsonSerializer.SerializeToUtf8Bytes(new
                    {
                        record.TimestampUtc, record.Sequence, record.ApplicationId, record.ProcessId,
                        Source = Identifier(record.Source), record.Level, record.EventId, EventName = Identifier(record.EventName), record.IsTruncated, record.Origin,
                        Context = ContextKeys.Where(key => record.Properties.TryGetValue(key, out var value) && SafeContext(value))
                            .ToDictionary(key => key, key => record.Properties[key], StringComparer.Ordinal)
                    });
                    if (bytes + data.Length + 1 > maximumBytes) break;
                    encoded.Add(data); bytes += data.Length + 1;
                }
                for (var i = encoded.Count - 1; i >= 0; i--) { json.WriteRawValue(encoded[i]); written++; }
                json.WriteEndArray(); json.WriteNumber("exportedRecords", written);
                json.WriteNumber("omittedRecords", snapshot.Records.Count - written); json.WriteEndObject(); json.Flush();
            }
            if (buffer.Length > maximumBytes) throw new InvalidOperationException("Report header exceeds the export budget.");
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

    private static void WriteContext(Utf8JsonWriter json, EditorDiagnosticSnapshot snapshot, int maximumBytes)
    {
        var health = snapshot.Health;
        var core = health == null ? null : new
        {
            health.ApplicationId, health.Closed, health.LatestSequence, health.RetainedRecords, health.RetainedBytes,
            health.MemoryEvicted, health.CaptureFailures, health.LateWrites,
            Sinks = health.Sinks.Take(66).Select(sink => new { Name = Identifier(sink.Name), sink.Accepted, sink.Written, sink.Dropped,
                sink.Failures, sink.Queued, sink.Bytes, HasError = sink.LastError != null }),
            Emergency = health.Emergency is not { } emergency ? null : new { emergency.Accepted, emergency.Written, emergency.Dropped,
                emergency.Suppressed, emergency.Failures, emergency.Queued, HasError = emergency.LastError != null },
            Retention = health.Retention is not { } retention ? null : new { retention.RetainedRuns, retention.RetainedBytes, retention.ActiveRuns,
                retention.SkippedDirectories, retention.DeletedRuns, retention.Failures, retention.IsOverBudget, retention.MaintenanceBusy, HasError = retention.LastError != null }
        };
        var metadataBudget = maximumBytes / 2;
        var coreData = JsonSerializer.SerializeToUtf8Bytes(core);
        var includeCore = health != null && json.BytesCommitted + json.BytesPending + coreData.Length + 768 <= metadataBudget;
        json.WritePropertyName("coreHealth"); if (includeCore) json.WriteRawValue(coreData); else json.WriteNullValue();
        json.WriteBoolean("coreHealthOmittedByBudget", health != null && !includeCore);
        json.WriteBoolean("runtimeCaptureAvailable", snapshot.Processes?.RuntimeAvailable == true);
        json.WriteBoolean("buildCaptureAvailable", snapshot.Processes?.BuildAvailable == true);
        json.WriteStartArray("processes"); var count = 0;
        foreach (var process in snapshot.Processes?.Processes.Take(64) ?? [])
        {
            var data = JsonSerializer.SerializeToUtf8Bytes(new
            {
                Source = Identifier(process.Source), process.StartedUtc, process.ProcessId, process.SessionId, process.Generation,
                RuntimeRole = process.RuntimeRole == null ? null : Identifier(process.RuntimeRole),
                GameBuildId = SafeContext(process.GameBuildId) ? process.GameBuildId : null, JobId = SafeContext(process.JobId) ? process.JobId : null,
                process.SourceRevision, process.Completed, process.ResourcesReleased,
                StandardOutput = StreamMetadata(process.StandardOutput), StandardError = StreamMetadata(process.StandardError),
                Delivery = process.Delivery is not { } lane ? null : new { lane.Enabled, lane.Closed, lane.Released, lane.DrainComplete,
                    lane.Accepted, lane.Rejected, lane.Lost, lane.Unconfirmed, lane.Failures, lane.Duplicates, lane.RateLimited, lane.Suppressed,
                    lane.ChildReportedLost, lane.ChildReportedUnconfirmed, HasError = lane.LastFailureType != null }
            });
            if (json.BytesCommitted + json.BytesPending + data.Length + 768 > metadataBudget) break;
            json.WriteRawValue(data); count++;
        }
        json.WriteEndArray(); json.WriteNumber("processMetadataOmitted", (snapshot.Processes?.Processes.Count ?? 0) - count);
    }

    private static object? StreamMetadata(ProcessStreamDiagnostic? stream) => stream == null ? null : new
    {
        stream.Output.TotalCharacters, stream.Output.DroppedCharacters, stream.Output.OmittedCharacters, stream.Output.FileBytesWritten,
        stream.Output.FileBytesDropped, HasFileError = stream.Output.FileError != null, stream.ReadFinished, stream.FileFinished,
        HasReadFailure = stream.ReadFailureType != null, HasFileFailure = stream.FileFailureType != null
    };

    private static string Identifier(string text) => new(text.Take(256).Select(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-' ? character : '_').ToArray());
    private static bool SafeContext(string? value) => value is { Length: > 0 and <= 128 } &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-' or ':');
}
