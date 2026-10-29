using System.Text;
using System.Threading.Channels;

namespace TrueMoon;

/// <summary>Continuously reads a borrowed stream, retaining a bounded tail and optionally a bounded UTF8 file.</summary>
/// <remarks>Starts immediately. The process owner must await Completion after process exit before releasing the reader.
/// File IO runs on one dedicated background worker behind eight bounded chunks;
/// overflow and failures never stop pipe drainage. File drain does not require a shared pool worker after EOF.
/// This type owns its file and tasks, but never disposes the borrowed reader.</remarks>
public sealed class ProcessOutputCapture
{
    private readonly Lock _gate = new();
    private readonly TextReader _reader;
    private readonly char[] _tail;
    private readonly string? _path;
    private readonly long _fileBudget;
    private readonly SemaphoreSlim? _fileAvailable;
    private readonly Channel<byte[]> _chunks = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(8)
        { SingleWriter = true, SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    private int _start, _count;
    private long _characters, _written, _fileDropped;
    private string? _fileError;
    private string _fileStage;

    /// <summary>Starts capture. No newline is required and the reader remains borrowed.</summary>
    /// <param name="reader">Redirected text stream owned by the process lease.</param>
    /// <param name="tailCapacity">Maximum retained UTF16 characters.</param>
    /// <param name="filePath">Optional output path; its parent must already exist.</param>
    /// <param name="maxFileBytes">Maximum persisted UTF8 bytes.</param>
    public ProcessOutputCapture(TextReader reader, int tailCapacity = 32 * 1024,
        string? filePath = null, long maxFileBytes = 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(reader);
        if (tailCapacity < 1 || tailCapacity > 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(tailCapacity));
        if (maxFileBytes < 1) throw new ArgumentOutOfRangeException(nameof(maxFileBytes));
        _reader = reader; _tail = new char[tailCapacity]; _path = filePath is null ? null : Path.GetFullPath(filePath);
        _fileBudget = maxFileBytes;
        _fileAvailable = _path is null ? null : new SemaphoreSlim(0);
        _fileStage = _path is null ? "Disabled" : "Queued";
        FileCompletion = _path is null ? Task.CompletedTask : Task.Factory.StartNew(Write,
            CancellationToken.None, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
        ReadCompletion = Task.Run(ReadAsync);
        Completion = CompleteAsync();
    }

    /// <summary>Completes at EOF or read failure, independently of the file sink.</summary>
    public Task ReadCompletion { get; }

    /// <summary>Completes when admitted file chunks and the file handle have drained.</summary>
    /// <remarks>File failures remain available from Read; the pipe reader continues independently.</remarks>
    public Task FileCompletion { get; }

    /// <summary>Current file-sink stage for diagnosing a bounded drain timeout.</summary>
    /// <remarks>Disabled, Queued, Opening, WaitingForChunk, Writing, Closing or Completed. Does not expose output text.</remarks>
    public string FileStage { get { lock (_gate) return _fileStage; } }

    /// <summary>Completes after EOF and all admitted file chunks have drained; a read failure remains observable.</summary>
    public Task Completion { get; }

    /// <summary>Returns a stable tail and status snapshot while capture is running or after EOF.</summary>
    public ProcessOutputSnapshot Read()
        => Read(_tail.Length);

    /// <summary>Copies at most the requested tail window. Window omissions do not count as capture eviction.</summary>
    public ProcessOutputSnapshot Read(int maximumCharacters)
    {
        if (maximumCharacters is < 0 or > 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(maximumCharacters));
        lock (_gate)
        {
            var count = Math.Min(_count, maximumCharacters); var omitted = _count - count;
            var start = (_start + omitted) % _tail.Length;
            var chars = new char[count];
            var first = Math.Min(count, _tail.Length - start);
            Array.Copy(_tail, start, chars, 0, first);
            Array.Copy(_tail, 0, chars, first, count - first);
            // A ring eviction may have removed the high half of the first surrogate pair.
            var offset = chars.Length > 0 && char.IsLowSurrogate(chars[0]) ? 1 : 0;
            var captureOffset = _count > 0 && char.IsLowSurrogate(_tail[_start]) ? 1 : 0;
            return new(new string(chars, offset, chars.Length - offset), _characters,
                _characters - _count + captureOffset, _written, _fileDropped, _fileError)
            { OmittedCharacters = omitted + offset - captureOffset };
        }
    }

    private void Append(ReadOnlySpan<char> chars)
    {
        lock (_gate)
        {
            _characters += chars.Length;
            foreach (var c in chars)
            {
                if (_count == _tail.Length) { _tail[_start] = c; _start = (_start + 1) % _tail.Length; }
                else { _tail[(_start + _count) % _tail.Length] = c; _count++; }
            }
        }
    }

    private async Task ReadAsync()
    {
        var buffer = new char[4096];
        var bytes = new byte[Encoding.UTF8.GetMaxByteCount(buffer.Length)];
        var encoder = Encoding.UTF8.GetEncoder();
        long offered = 0;
        try
        {
            int count;
            while ((count = await _reader.ReadAsync(buffer.AsMemory()).ConfigureAwait(false)) != 0)
            {
                Append(buffer.AsSpan(0, count));
                if (_path is not null)
                {
                    var size = encoder.GetBytes(buffer.AsSpan(0, count), bytes, false);
                    Offer(bytes.AsSpan(0, size));
                }
            }
            if (_path is not null)
            {
                var size = encoder.GetBytes(ReadOnlySpan<char>.Empty, bytes, true);
                Offer(bytes.AsSpan(0, size));
            }
        }
        finally { _chunks.Writer.TryComplete(); _fileAvailable?.Release(); }

        void Offer(ReadOnlySpan<byte> encoded)
        {
            var keep = (int)Math.Min(encoded.Length, _fileBudget - offered);
            // Keep complete UTF8 code points at the file-byte boundary.
            while (keep > 0 && keep < encoded.Length && (encoded[keep] & 0xc0) == 0x80) keep--;
            if (keep != 0)
            {
                // Budget counts offered bytes, including chunks dropped by a slow file sink.
                offered += keep;
                if (_chunks.Writer.TryWrite(encoded[..keep].ToArray())) _fileAvailable!.Release();
                else lock (_gate) _fileDropped += keep;
            }
            lock (_gate) _fileDropped += encoded.Length - keep;
        }
    }

    private async Task CompleteAsync()
    {
        try { await Task.WhenAll(ReadCompletion, FileCompletion).ConfigureAwait(false); }
        finally { _fileAvailable?.Dispose(); }
    }

    private void Write()
    {
        FileStream? file = null;
        try
        {
            Stage("Opening");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path!)!);
                file = new FileStream(_path!, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.None);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { Failure(error); }
            Stage("WaitingForChunk");
            // This worker owns its thread. Blocking here never occupies a shared
            // pool worker or delays the pipe reader when the file queue is full.
            while (true)
            {
                // Converting the channel's async wait to Task would itself need
                // the pool to run its completion callback. Signal the sync worker
                // directly: one permit per admitted chunk, plus the terminal permit.
                _fileAvailable!.Wait();
                if (!_chunks.Reader.TryRead(out var bytes))
                {
                    if (_chunks.Reader.Completion.IsCompleted) break;
                    continue;
                }
                if (file is null) { lock (_gate) _fileDropped += bytes.Length; continue; }
                try
                {
                    Stage("Writing");
                    file.Write(bytes);
                    lock (_gate) _written += bytes.Length;
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    Failure(error);
                    lock (_gate) _fileDropped += bytes.Length;
                    Stage("Closing");
                    try { file.Dispose(); } catch (IOException cleanup) { Failure(cleanup); }
                    file = null;
                }
                Stage("WaitingForChunk");
            }
        }
        finally
        {
            Stage("Closing");
            if (file is not null)
                try { file.Dispose(); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { Failure(error); }
            Stage("Completed");
        }
        void Stage(string stage) { lock (_gate) _fileStage = stage; }
        void Failure(Exception error)
        {
            var text = error.GetType().Name + ": " + error.Message;
            lock (_gate) _fileError = text[..Math.Min(text.Length, 1024)];
        }
    }
}
