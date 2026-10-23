using System.IO.Pipes;

namespace TrueMoon.Enerit.IO.Pipes;

public abstract class PipeConectionHandler : IDisposable
{
    private readonly bool _isClient;
    private readonly object _streamGate = new();
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private PipeStream? _stream;
    private bool _disposed;
    protected PipeStream PipeStream => Volatile.Read(ref _stream) ?? throw new ObjectDisposedException(GetType().Name);
    protected bool IsDisposed { get { lock (_streamGate) { return _disposed; } } }
    public string Name { get; }

    public PipeConectionHandler(string name, bool isClient = true)
    {
        _isClient = isClient;
        Name = $"tm_{name}";
    }

    protected async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        PipeStream stream = _isClient
            ? new NamedPipeClientStream(".", Name, PipeDirection.InOut, PipeOptions.Asynchronous)
            : new NamedPipeServerStream(Name, PipeDirection.InOut, 64, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        lock (_streamGate)
        {
            if (_disposed)
            {
                stream.Dispose();
                throw new ObjectDisposedException(GetType().Name);
            }
            // Publish before connecting so Dispose can interrupt an outstanding connection.
            _stream = stream;
        }
        try
        {
            if (stream is NamedPipeClientStream client)
            {
                await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await ((NamedPipeServerStream)stream).WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            lock (_streamGate)
            {
                if (ReferenceEquals(_stream, stream)) { _stream = null; }
            }
            stream.Dispose();
            throw;
        }
    }

    protected void Connect(CancellationToken cancellationToken = default) =>
        ConnectAsync(cancellationToken).GetAwaiter().GetResult();

    /// <summary>Writes one complete frame without interleaving concurrent writers.</summary>
    /// <param name="frame">Header and payload of one invocation frame.</param>
    protected void WriteFrame(ReadOnlySpan<byte> frame)
    {
        _writeGate.Wait();
        try { PipeStream.Write(frame); }
        finally { _writeGate.Release(); }
    }

    // Cancellation after writing starts invalidates this connection: a partial frame cannot be replayed.
    protected async Task WriteFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken token,
        PipeStream? expected = null, Action? writing = null)
    {
        await _writeGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var stream = PipeStream;
            if (expected is not null && !ReferenceEquals(expected, stream)) throw new IOException("Invocation connection changed before send.");
            token.ThrowIfCancellationRequested();
            writing?.Invoke();
            try { await stream.WriteAsync(frame, token).ConfigureAwait(false); }
            catch { Reset(stream); throw; }
        }
        finally { _writeGate.Release(); }
    }

    public virtual void Dispose()
    {
        lock (_streamGate)
        {
            if (_disposed) { return; }
            _disposed = true;
        }
        Reset();
    }

    protected void Reset() => Reset(null);

    protected void Reset(PipeStream? expected)
    {
        PipeStream? stream;
        lock (_streamGate)
        {
            if (expected is not null && !ReferenceEquals(_stream, expected)) return;
            stream = _stream;
            _stream = null;
        }
        stream?.Dispose();
    }
}
