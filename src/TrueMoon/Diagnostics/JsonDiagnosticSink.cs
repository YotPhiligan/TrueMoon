using System.Globalization;
using System.Text.Json;

namespace TrueMoon.Diagnostics;

/// <summary>Owns a fixed set of rotating UTF8 JSONL files in an explicitly selected directory.</summary>
public sealed class JsonDiagnosticSink : IDiagnosticSink
{
    private readonly string _directory;
    private readonly long _fileSize;
    private readonly int _maxFiles;
    private readonly string _prefix;
    private FileStream? _stream;
    private FileStream? _lease;
    private int _slot;
    private long _length;
    private bool _disposed;
    private bool _initialized;
    private readonly DiagnosticRunStore? _runs;
    private DiagnosticRunStore.Run? _run;
    /// <summary>Actual output directory; null until the first write creates a managed run.</summary>
    public string? DirectoryPath => _runs is null ? _directory : _run?.Directory;
    /// <summary>Most recent closed-run maintenance pass, or null in legacy flat mode.</summary>
    public DiagnosticRetentionStatus? RetentionStatus { get; private set; }

    /// <summary>Creates a sink; files are opened lazily on its worker.</summary>
    /// <param name="directory">Dedicated diagnostics directory.</param>
    /// <param name="fileSizeBytes">Per-file byte limit.</param>
    /// <param name="maxFiles">Maximum owned files.</param>
    /// <param name="filePrefix">Stable prefix for one exclusive owner; default is events.</param>
    public JsonDiagnosticSink(string directory, long fileSizeBytes = 1024 * 1024, int maxFiles = 4, string? filePrefix = null)
        : this(directory, fileSizeBytes, maxFiles, filePrefix, null) { }

    /// <summary>Creates a sink with optional managed GUID run directories. Managed mode requires the events prefix.</summary>
    /// <param name="directory">Dedicated flat directory or managed run root.</param>
    /// <param name="fileSizeBytes">Per-file byte limit.</param><param name="maxFiles">Maximum files in a run.</param>
    /// <param name="filePrefix">Flat-mode prefix; managed mode accepts null or events.</param>
    /// <param name="retention">Copied closed-run limits; null preserves flat mode.</param>
    public JsonDiagnosticSink(string directory, long fileSizeBytes, int maxFiles, string? filePrefix,
        DiagnosticRunRetentionOptions? retention)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (fileSizeBytes < 1 || maxFiles < 1) throw new ArgumentOutOfRangeException(nameof(fileSizeBytes));
        _directory = Path.GetFullPath(directory); _fileSize = fileSizeBytes; _maxFiles = maxFiles;
        _prefix = filePrefix ?? "events";
        ArgumentException.ThrowIfNullOrWhiteSpace(_prefix);
        if (_prefix.Length > 80 || _prefix.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            throw new ArgumentException("Expected a simple file prefix.", nameof(filePrefix));
        if (retention != null)
        {
            if (_prefix != "events") throw new ArgumentException("Managed runs require the events prefix.", nameof(filePrefix));
            _runs = new(_directory, retention);
        }
    }

    /// <inheritdoc />
    public async ValueTask WriteAsync(DiagnosticRecord record, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_initialized)
        {
            if (_runs != null && _run == null)
            {
                _run = _runs.Create(DateTimeOffset.UtcNow);
                RetentionStatus = _runs.Sweep(DateTimeOffset.UtcNow, _run.Directory);
            }
            Directory.CreateDirectory(DirectoryPath!);
            _lease ??= new FileStream(Path.Combine(DirectoryPath!, _prefix + ".lock"), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
            // Only this sink's numeric slots are owned. Other files/prefixes remain caller-owned.
            foreach (var path in Directory.EnumerateFiles(DirectoryPath!, _prefix + "-*.jsonl"))
            {
                var suffix = Path.GetFileNameWithoutExtension(path)[(_prefix.Length + 1)..];
                if (int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var slot) && slot >= _maxFiles)
                    File.Delete(path);
            }
            _initialized = true;
        }
        var bytes = JsonSerializer.SerializeToUtf8Bytes(record);
        if (bytes.LongLength + 1 > _fileSize) throw new IOException("Diagnostic record exceeds the JSONL file byte budget.");
        if (_stream is null || _length + bytes.LongLength + 1 > _fileSize)
        {
            if (_stream is not null)
            {
                await _stream.DisposeAsync().ConfigureAwait(false); _stream = null;
                _slot = (_slot + 1) % _maxFiles;
            }
            Directory.CreateDirectory(DirectoryPath!);
            _stream = new FileStream(Path.Combine(DirectoryPath!, _prefix + "-" + _slot + ".jsonl"),
                FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous);
            _length = 0;
        }
        // Account attempted bytes even if IO fails: a partial file cannot silently exceed its budget.
        _length += bytes.LongLength + 1;
        try
        {
            await _stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await _stream.WriteAsync("\n"u8.ToArray(), cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // A partial JSON record is not a valid append target on retry.
            await _stream.DisposeAsync().ConfigureAwait(false); _stream = null;
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_stream is not null) { await _stream.DisposeAsync().ConfigureAwait(false); _stream = null; }
        if (_lease is not null) { await _lease.DisposeAsync().ConfigureAwait(false); _lease = null; }
        if (_run is not null)
        {
            _run.Dispose();
            RetentionStatus = _runs!.Sweep(DateTimeOffset.UtcNow, _run.Directory);
        }
        _disposed = true;
    }
}
