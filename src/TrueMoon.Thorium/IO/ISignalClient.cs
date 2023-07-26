using System.Buffers;

namespace TrueMoon.Thorium.IO;

public interface ISignalClient<TService>
{
    Task<TResult> InvokeAsync<TResult>(byte methodCode, Action<IBufferWriter<byte>>? action, Func<IMemoryReadHandle,TResult> func, CancellationToken cancellationToken = default);

    Task InvokeAsync(byte methodCode, Action<IBufferWriter<byte>>? action,
        CancellationToken cancellationToken = default);
    
    TResult Invoke<TResult>(byte methodCode, Action<IBufferWriter<byte>>? action, Func<IMemoryReadHandle,TResult> func);

    void Invoke(byte methodCode, Action<IBufferWriter<byte>>? action);
}