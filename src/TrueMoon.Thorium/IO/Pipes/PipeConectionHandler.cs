using System.IO.Pipes;

namespace TrueMoon.Thorium.IO.Pipes;

public abstract class PipeConectionHandler : IDisposable
{
    private readonly bool _isClient;
    protected PipeStream PipeStream;
    protected readonly SemaphoreSlim WriteSync = new (1);
    protected readonly SemaphoreSlim ReadSync = new (1);
    
    public string Name { get; }
    
    public PipeConectionHandler(string name, bool isClient = true)
    {
        _isClient = isClient;
        Name = $"tmthorium_{name}";
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_isClient)
        {
            var stream = new NamedPipeClientStream(".", Name, PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough);
            await stream.ConnectAsync(cancellationToken);
            PipeStream = stream;
        }
        else
        {
            var stream = new NamedPipeServerStream(Name, PipeDirection.InOut, 64, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.WriteThrough);
            await stream.WaitForConnectionAsync(cancellationToken);
            PipeStream = stream;
        }
    }

    public void Dispose()
    {
        PipeStream.Dispose();
        WriteSync.Dispose();
        ReadSync.Dispose();
    }
}