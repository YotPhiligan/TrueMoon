using System.Buffers;

namespace TrueMoon.Thorium.IO;

public interface IInvocationClient<TService> : IInvocationClient
{
    
}

public interface IInvocationClient
{
    Task<TResult> InvokeAsync<TResult>(byte methodCode, Action<IBufferWriter<byte>>? action, Func<ReadOnlyMemory<byte>,TResult> func, CancellationToken cancellationToken = default);

    Task InvokeAsync(byte methodCode, Action<IBufferWriter<byte>>? action,
        CancellationToken cancellationToken = default);
    
    TResult Invoke<TResult>(byte methodCode, Action<IBufferWriter<byte>>? action, Func<ReadOnlyMemory<byte>,TResult> func);

    void Invoke(byte methodCode, Action<IBufferWriter<byte>>? action);
}