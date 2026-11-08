using System.Buffers;
using System.Runtime.InteropServices;

namespace TrueMoon.Nom.Resources;

// Owned projection, borrowed selected destination. The byte reader owns its IO scratch,
// so this view never needs to pin the caller and is used only for synchronous Span copy.
internal sealed class RawDestinationMemory<T>(Memory<T> memory, int byteLength) : MemoryManager<byte> where T : unmanaged
{
    private Memory<T> destination = memory;
    private bool disposed;
    internal Memory<byte> View => CreateMemory(byteLength);
    public override Span<byte> GetSpan()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return MemoryMarshal.AsBytes(destination.Span);
    }
    public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException("Raw destination projection cannot be pinned.");
    public override void Unpin() => throw new NotSupportedException();
    protected override void Dispose(bool disposing) { if (disposing) { disposed = true; destination = default; } }
}
