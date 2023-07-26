using System.Buffers;

namespace TrueMoon.Thorium.IO;

public interface ISignalServerHandler<TService>
{
    Task<bool> HandleAsync(byte method, IMemoryReadHandle readHandle, IBufferWriter<byte> bufferWriter,
        CancellationToken cancellationToken = default);
}