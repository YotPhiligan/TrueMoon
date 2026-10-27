using System.Buffers;
using TrueMoon.Enerit.IO;

namespace TrueMoon.Aether.Diagnostics;

internal sealed class BoundedDiagnosticHandler(IInvocationServerHandler<IChildDiagnosticDelivery> inner) : IInvocationServerHandler<IChildDiagnosticDelivery>
{
    public async Task<(bool, Exception?)> HandleAsync(byte methodCode, ReadOnlyMemory<byte> readMemory, IBufferWriter<byte> writer, CancellationToken token = default)
    {
        if (readMemory.Length > ChildDiagnosticCodec.WireLimit) throw new InvalidDataException("Diagnostic envelope too large.");
        var response = new ArrayBufferWriter<byte>();
        var result = await inner.HandleAsync(methodCode, readMemory, response, token).ConfigureAwait(false);
        if (response.WrittenCount > 1024) throw new InvalidDataException("Diagnostic reply too large.");
        writer.Write(response.WrittenSpan); return result;
    }
}
