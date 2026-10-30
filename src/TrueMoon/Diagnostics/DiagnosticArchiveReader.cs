using System.Text.Json;

namespace TrueMoon.Diagnostics;

/// <summary>Read-only, finite discovery and tail reads over the configured logger's files.</summary>
internal static class DiagnosticArchiveReader
{
    private const int DiscoveryLimit = 256;
    internal static Task<DiagnosticHistorySnapshot> ReadAsync(string? root, bool managed, string? current,
        int slots, DiagnosticHistoryReadOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options); options.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        if (root == null) return Task.FromResult(new DiagnosticHistorySnapshot(false, []));
        return Task.Run(async () =>
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(options.TimeBudget); var token = deadline.Token;
            var state = new ReadState(options); var candidates = new List<string>(); var examined = 0;
            try
            {
                DiagnosticRunStore.RejectLinks(root);
                if (!Directory.Exists(root)) return state.Snapshot();
                if (managed)
                {
                    if (current != null) AddFiles(current);
                    foreach (var directory in Directory.EnumerateDirectories(root))
                    {
                        token.ThrowIfCancellationRequested();
                        if (++examined > DiscoveryLimit) { state.Limited = true; break; }
                        if (directory == current) continue;
                        if (!Guid.TryParseExact(Path.GetFileName(directory), "N", out _)) { state.Skipped++; continue; }
                        try
                        {
                            DiagnosticRunStore.RejectLinks(directory);
                            var marker = Path.Combine(directory, DiagnosticRunStore.MarkerFile); DiagnosticRunStore.RejectLinks(marker);
                            using var lease = new FileStream(marker, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                            if (!DiagnosticRunStore.TryReadCreation(lease, out _)) { state.Skipped++; continue; }
                            AddFiles(directory);
                        }
                        catch (Exception error) when (FileError(error)) { state.Fail(error); }
                    }
                }
                else AddFiles(root);
                var timed = new List<(string Path, DateTime Written)>();
                foreach (var path in candidates)
                {
                    token.ThrowIfCancellationRequested();
                    try { timed.Add((path, File.GetLastWriteTimeUtc(path))); }
                    catch (Exception error) when (FileError(error)) { state.Fail(error); }
                }
                var ordered = timed.OrderByDescending(item => item.Written).ThenBy(item => item.Path, StringComparer.Ordinal).ToArray();
                for (var index = 0; index < ordered.Length; index++)
                {
                    token.ThrowIfCancellationRequested();
                    if (state.Files >= options.MaximumFiles || state.Bytes >= options.MaximumReadBytes) { state.Limited = true; break; }
                    try
                    {
                        var path = ordered[index].Path; DiagnosticRunStore.RejectLinks(path);
                        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous);
                        state.Files++;
                        var count = (int)Math.Min(file.Length, options.MaximumReadBytes - state.Bytes);
                        var offset = Math.Max(0, file.Length - count); file.Position = offset;
                        var bytes = new byte[count]; var read = 0;
                        while (read < count)
                        {
                            var chunk = await file.ReadAsync(bytes.AsMemory(read), token).ConfigureAwait(false);
                            if (chunk == 0) break; read += chunk; state.Bytes += chunk;
                        }
                        if (offset > 0) state.Limited = true;
                        state.Parse(bytes.AsSpan(0, read), offset > 0, token);
                    }
                    catch (Exception error) when (FileError(error)) { state.Fail(error); }
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            { state.TimedOut = state.Limited = true; }
            catch (Exception error) when (FileError(error)) { state.Fail(error); }
            cancellationToken.ThrowIfCancellationRequested();
            return state.Snapshot();

            void AddFiles(string directory)
            {
                DiagnosticRunStore.RejectLinks(directory);
                foreach (var path in Directory.EnumerateFiles(directory))
                {
                    token.ThrowIfCancellationRequested();
                    if (++examined > DiscoveryLimit) { state.Limited = true; break; }
                    var name = Path.GetFileName(path);
                    // A recognized closed run can have a different rotation configuration from this App.
                    if (!DiagnosticRunStore.IsEventFile(name) || !int.TryParse(name[7..^6], out var slot) || !managed && slot >= slots) continue;
                    candidates.Add(path);
                }
            }
        }, cancellationToken);
    }

    private static bool FileError(Exception error) => error is IOException or UnauthorizedAccessException or System.Security.SecurityException;
    private sealed class ReadState(DiagnosticHistoryReadOptions options)
    {
        private readonly PriorityQueue<DiagnosticRecord, (long, Guid, long)> _records = new();
        private readonly HashSet<(Guid, long)> _identities = [];
        private readonly DiagnosticPipelineOptions _bounds = new();
        private long _retained, _omitted, _duplicates;
        internal int Files, Invalid, Incomplete, Oversized, Missing, Skipped, Failures;
        internal long Bytes;
        internal bool Limited, TimedOut;

        internal void Fail(Exception error)
        {
            Limited = true;
            if (error is FileNotFoundException or DirectoryNotFoundException) Missing++;
            else { Failures++; Skipped++; }
        }
        internal void Parse(ReadOnlySpan<byte> data, bool clippedPrefix, CancellationToken token)
        {
            var start = 0;
            if (clippedPrefix)
            {
                Incomplete++; var boundary = data.IndexOf((byte)'\n'); if (boundary < 0) return; start = boundary + 1;
            }
            while (start < data.Length)
            {
                token.ThrowIfCancellationRequested(); var length = data[start..].IndexOf((byte)'\n');
                if (length < 0) { Incomplete++; Limited = true; break; }
                var line = data.Slice(start, length); start += length + 1;
                if (line.Length == 0) continue;
                if (line.Length > options.MaximumLineBytes) { Oversized++; Limited = true; continue; }
                try
                {
                    var value = JsonSerializer.Deserialize<DiagnosticRecord>(line, JsonOptions);
                    if (value == null || value.ApplicationId == Guid.Empty || value.Sequence < 1 || value.ProcessId < 1 || !Enum.IsDefined(value.Level) ||
                        value.Origin is { } origin && (origin.ApplicationId == Guid.Empty || origin.Sequence < 1)) throw new JsonException("Invalid identity.");
                    value = DiagnosticSnapshot.Import(value, null, value.ApplicationId, value.Sequence, _bounds) with { Origin = value.Origin };
                    var identity = (value.ApplicationId, value.Sequence);
                    if (!_identities.Add(identity)) { _duplicates++; continue; }
                    _records.Enqueue(value, (value.TimestampUtc.UtcDateTime.Ticks, value.ApplicationId, value.Sequence)); _retained += DiagnosticSnapshot.Size(value);
                    while (_records.Count > options.MaximumRecords || _retained > options.MaximumRetainedBytes)
                    {
                        var old = _records.Dequeue(); _retained -= DiagnosticSnapshot.Size(old); _identities.Remove((old.ApplicationId, old.Sequence)); _omitted++; Limited = true;
                    }
                }
                catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException)
                { Invalid++; Limited = true; }
            }
        }
        internal DiagnosticHistorySnapshot Snapshot() => new(true, Array.AsReadOnly(_records.UnorderedItems.Select(item => item.Element)
            .OrderBy(record => record.TimestampUtc).ThenBy(record => record.ApplicationId).ThenBy(record => record.Sequence).ToArray()))
        {
            FilesRead = Files, BytesRead = Bytes, InvalidLines = Invalid, IncompleteLines = Incomplete, OversizedLines = Oversized,
            MissingEntries = Missing, SkippedEntries = Skipped, ReadFailures = Failures, OmittedRecords = _omitted,
            DuplicateRecords = _duplicates, IsTruncated = Limited, TimeBudgetExceeded = TimedOut
        };
        private static readonly JsonSerializerOptions JsonOptions = new() { MaxDepth = 32 };
    }
}
