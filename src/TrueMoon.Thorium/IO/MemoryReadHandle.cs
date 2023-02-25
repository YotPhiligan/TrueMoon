using System.IO.MemoryMappedFiles;

namespace TrueMoon.Thorium.IO;

public unsafe class MemoryReadHandle : IMemoryReadHandle,IDisposable
{
    private readonly MemoryMappedViewStream _view;
    private byte* _ptr = null;

    public MemoryReadHandle(MemoryMappedViewStream view)
    {
        _view = view;
        _view.SafeMemoryMappedViewHandle.AcquirePointer(ref _ptr);
    }

    public void Dispose()
    {
        _view.SafeMemoryMappedViewHandle.ReleasePointer();
        _view.Dispose();
    }

    public ReadOnlySpan<byte> GetData() => new Span<byte>(_ptr + _view.PointerOffset, (int)_view.Length);
}