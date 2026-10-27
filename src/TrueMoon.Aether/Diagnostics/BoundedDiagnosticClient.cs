using System.Buffers;
using TrueMoon.Enerit.IO;

namespace TrueMoon.Aether.Diagnostics;

internal sealed class BoundedDiagnosticClient(IInvocationClient<IChildDiagnosticDelivery> inner) : IInvocationClient<IChildDiagnosticDelivery>
{
    private static Action<IBufferWriter<byte>>? Bound(Action<IBufferWriter<byte>>? action)
    {
        if (action == null) return null;
        var bytes = new ArrayBufferWriter<byte>(); action(bytes);
        if (bytes.WrittenCount > ChildDiagnosticCodec.WireLimit) throw new InvalidDataException("Diagnostic envelope too large.");
        return writer => writer.Write(bytes.WrittenSpan);
    }
    public Task<TResult> InvokeAsync<TResult>(byte methodCode, Action<IBufferWriter<byte>>? action, Func<ReadOnlyMemory<byte>, TResult> result, CancellationToken token = default)
        => inner.InvokeAsync(methodCode, Bound(action), memory => { if (memory.Length > 1024) throw new InvalidDataException("Diagnostic reply too large."); return result(memory); }, token);
    public Task InvokeAsync(byte methodCode, Action<IBufferWriter<byte>>? action, CancellationToken token = default) => inner.InvokeAsync(methodCode, Bound(action), token);
    public TResult Invoke<TResult>(byte methodCode, Action<IBufferWriter<byte>>? action, Func<ReadOnlyMemory<byte>, TResult> result)
        => InvokeAsync(methodCode, action, result).GetAwaiter().GetResult();
    public void Invoke(byte methodCode, Action<IBufferWriter<byte>>? action) => InvokeAsync(methodCode, action).GetAwaiter().GetResult();
}
