using TrueMoon.Thorium.IO;

namespace TrueMoon.Thorium;

public interface ISignalHandleWrapper
{
    void ProcessMessage(Guid code, IMemoryReadHandle readHandle, IMemoryWriteHandle? writeHandle, CancellationToken cancellationToken);
}

public interface ISignalHandleWrapper<TMessage> : ISignalHandleWrapper
{
    
}

public interface ISignalHandleWrapper<TMessage,TResponse> : ISignalHandleWrapper
{
    
}