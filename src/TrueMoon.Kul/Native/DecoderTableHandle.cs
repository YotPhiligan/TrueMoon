using System.Runtime.InteropServices;
using MiniAudioEx.Native;

namespace TrueMoon.Kul.Native;

internal sealed unsafe class DecoderTableHandle : SafeHandle
{
    internal DecoderTableHandle() : base(0, true)
    {
        var backend = MiniAudioNative.ma_libvorbis_get_decoding_backend();
        if (backend == null) throw new NotSupportedException("The patched libvorbis backend is missing.");
        SetHandle((nint)NativeMemory.Alloc((nuint)IntPtr.Size));
        if (IsInvalid) throw new OutOfMemoryException();
        *(nint*)handle = (nint)backend;
    }
    public override bool IsInvalid => handle == 0;
    internal nint Pointer => DangerousGetHandle();
    protected override bool ReleaseHandle() { NativeMemory.Free((void*)handle); handle = 0; return true; }
}
