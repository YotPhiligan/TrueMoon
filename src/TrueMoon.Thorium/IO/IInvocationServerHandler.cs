using System.Buffers;

namespace TrueMoon.Thorium.IO;

public interface IInvocationServerHandler
{
    Task<(bool, Exception?)> HandleAsync(byte method, ReadOnlyMemory<byte> readMemory, IBufferWriter<byte> bufferWriter,
        CancellationToken cancellationToken = default);
}

public interface IInvocationServerHandler<TService> : IInvocationServerHandler
{
    
}