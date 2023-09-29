using System.IO.Pipes;

namespace TrueMoon.Thorium.IO.Pipes;

public abstract class PipeConectionHandler : IDisposable
{
    private readonly bool _isClient;
    protected PipeStream PipeStream;
    
    public string Name { get; }
    
    public PipeConectionHandler(string name, bool isClient = true)
    {
        _isClient = isClient;
        Name = $"tm_{name}";
    }

    protected async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_isClient)
        {
            var stream = new NamedPipeClientStream(".", Name, PipeDirection.InOut,
                PipeOptions.Asynchronous);
            await stream.ConnectAsync(cancellationToken);
            PipeStream = stream;
        }
        else
        {
            var stream = new NamedPipeServerStream(Name, PipeDirection.InOut, 64, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            await stream.WaitForConnectionAsync(cancellationToken);
            PipeStream = stream;
        }
    }
    
    protected void Connect()
    {
        if (_isClient)
        {
            var stream = new NamedPipeClientStream(".", Name, PipeDirection.InOut,
                PipeOptions.Asynchronous);
            stream.Connect();
            PipeStream = stream;
        }
        else
        {
            var stream = new NamedPipeServerStream(Name, PipeDirection.InOut, 64, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            stream.WaitForConnection();
            PipeStream = stream;
        }
    }
    
    public void Dispose()
    {
        Reset();
    }

    protected void Reset()
    {
        try
        {
            PipeStream.Dispose();
            PipeStream = null;
        }
        catch (Exception)
        {
            //
        }
    }
}