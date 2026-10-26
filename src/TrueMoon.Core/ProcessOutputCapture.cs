using System.Text;
using System.Threading.Channels;

namespace TrueMoon;

/// <summary>Continuously reads a borrowed stream, retaining a bounded tail and optionally a bounded UTF8 file.</summary>
/// <remarks>Starts immediately. The process owner must await Completion after process exit before releasing the reader.
/// File IO runs independently behind eight bounded chunks; overflow and failures never stop pipe drainage.
/// This type owns its file and tasks, but never disposes the borrowed reader.</remarks>
public sealed class ProcessOutputCapture
{
    private readonly Lock _gate = new();
    private readonly TextReader _reader;
    private readonly char[] _tail;
    private readonly string? _path;
    private readonly long _fileBudget;
    private readonly Channel<byte[]> _chunks = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(8)
        { SingleWriter = true, SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    private int _start, _count;
    private long _characters, _written, _fileDropped;
    private string? _fileError;

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
        var writer = _path is null ? Task.CompletedTask : Task.Run(WriteAsync);
        Completion = Task.WhenAll(Task.Run(ReadAsync), writer);
    }

    /// <summary>Completes after EOF and all admitted file chunks have drained; a read failure remains observable.</summary>
    public Task Completion { get; }

    /// <summary>Returns a stable tail and status snapshot while capture is running or after EOF.</summary>
    public ProcessOutputSnapshot Read()
    {
        lock (_gate)
        {
            var chars = new char[_count];
            var first = Math.Min(_count, _tail.Length - _start);
            Array.Copy(_tail, _start, chars, 0, first);
            Array.Copy(_tail, 0, chars, first, _count - first);
            // A ring eviction may have removed the high half of the first surrogate pair.
            var offset = chars.Length > 0 && char.IsLowSurrogate(chars[0]) ? 1 : 0;
            return new(new string(chars, offset, chars.Length - offset), _characters,
                _characters - _count + offset, _written, _fileDropped, _fileError);
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
        finally { _chunks.Writer.TryComplete(); }

        void Offer(ReadOnlySpan<byte> encoded)
        {
            var keep = (int)Math.Min(encoded.Length, _fileBudget - offered);
            // Keep complete UTF8 code points at the file-byte boundary.
            while (keep > 0 && keep < encoded.Length && (encoded[keep] & 0xc0) == 0x80) keep--;
            if (keep != 0)
            {
                // Budget counts offered bytes, including chunks dropped by a slow file sink.
                offered += keep;
                if (!_chunks.Writer.TryWrite(encoded[..keep].ToArray()))
                    lock (_gate) _fileDropped += keep;
            }
            lock (_gate) _fileDropped += encoded.Length - keep;
        }
    }

    private async Task WriteAsync()
    {
        FileStream? file = null;
        try
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path!)!);
                file = new FileStream(_path!, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { Failure(error); }
            await foreach (var bytes in _chunks.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                if (file is null) { lock (_gate) _fileDropped += bytes.Length; continue; }
                try
                {
                    await file.WriteAsync(bytes).ConfigureAwait(false);
                    lock (_gate) _written += bytes.Length;
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    Failure(error);
                    lock (_gate) _fileDropped += bytes.Length;
                    try { await file.DisposeAsync().ConfigureAwait(false); } catch (IOException cleanup) { Failure(cleanup); }
                    file = null;
                }
            }
        }
        finally
        {
            if (file is not null)
                try { await file.DisposeAsync().ConfigureAwait(false); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { Failure(error); }
        }
        void Failure(Exception error)
        {
            var text = error.GetType().Name + ": " + error.Message;
            lock (_gate) _fileError = text[..Math.Min(text.Length, 1024)];
        }
    }
}
