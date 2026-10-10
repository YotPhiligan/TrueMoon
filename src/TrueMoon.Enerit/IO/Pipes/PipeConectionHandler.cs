using System.IO.Pipes;

namespace TrueMoon.Enerit.IO.Pipes;

public abstract class PipeConectionHandler : IDisposable
{
    private readonly bool _isClient;
    private readonly object _streamGate = new();
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

    public virtual void Dispose()
    {
        lock (_streamGate)
        {
            if (_disposed) { return; }
            _disposed = true;
        }
        Reset();
    }

    protected void Reset()
    {
        PipeStream? stream;
        lock (_streamGate)
        {
            stream = _stream;
            _stream = null;
        }
        stream?.Dispose();
    }
}
