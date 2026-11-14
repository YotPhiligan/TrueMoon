using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MiniAudioEx.Native;

namespace MiniAudioExResearch;

// Research-only, file-only routing. The bundled backend leaks OggVorbis_File when probing WAV.
// Restrict its probes to the explicitly selected .ogg fixtures; malformed OGG still needs a native fix.
internal sealed unsafe class FileVorbisBackend : IDisposable
{
    private readonly ma_decoding_backend_vtable* original = MiniAudioNative.ma_libvorbis_get_decoding_backend();
    private ma_decoding_backend_vtable* routed;
    internal nint Table => (nint)routed;
    internal nint UserData => (nint)original;

    internal FileVorbisBackend()
    {
        if (original == null) throw new InvalidOperationException("libvorbis backend absent.");
        routed = (ma_decoding_backend_vtable*)NativeMemory.AllocZeroed((nuint)sizeof(ma_decoding_backend_vtable));
        routed->onInit = (nint)(delegate* unmanaged[Cdecl]<nint, nint, nint, nint, nint, nint, nint, nint, ma_result>)&OpenCallbacks;
        routed->onInitFile = (nint)(delegate* unmanaged[Cdecl]<nint, byte*, nint, nint, nint, ma_result>)&OpenUtf8;
        routed->onInitFileW = (nint)(delegate* unmanaged[Cdecl]<nint, char*, nint, nint, nint, ma_result>)&OpenWide;
        routed->onInitMemory = (nint)(delegate* unmanaged[Cdecl]<nint, nint, nuint, nint, nint, nint, ma_result>)&RejectMemory;
        routed->onUninit = (nint)(delegate* unmanaged[Cdecl]<nint, nint, nint, void>)&Uninit;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static ma_result OpenCallbacks(nint user, nint read, nint seek, nint tell, nint data, nint config, nint allocations, nint backend)
    {
        if (read == 0 || seek == 0) return ma_result.not_implemented;
        byte* header = stackalloc byte[4]; nuint count = 0;
        var result = ((delegate* unmanaged[Cdecl]<nint, byte*, nuint, nuint*, ma_result>)read)(data, header, 4, &count);
        var rewind = ((delegate* unmanaged[Cdecl]<nint, long, int, ma_result>)seek)(data, 0, 0);
        if (rewind != ma_result.success) return rewind;
        if (result != ma_result.success || count != 4 || header[0] != 'O' || header[1] != 'g' || header[2] != 'g' || header[3] != 'S')
            return ma_result.invalid_file;
        var source = (ma_decoding_backend_vtable*)user;
        return ((delegate* unmanaged[Cdecl]<nint, nint, nint, nint, nint, nint, nint, nint, ma_result>)source->onInit)
            (0, read, seek, tell, data, config, allocations, backend);
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static ma_result RejectMemory(nint a, nint b, nuint c, nint d, nint e, nint f) => ma_result.not_implemented;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static ma_result OpenWide(nint user, char* path, nint config, nint allocations, nint backend)
    {
        int length = 0; while (path != null && length < 32768 && path[length] != 0) length++;
        if (length < 4 || path[length - 4] != '.' || (path[length - 3] | 32) != 'o' ||
            (path[length - 2] | 32) != 'g' || (path[length - 1] | 32) != 'g') return ma_result.invalid_file;
        var source = (ma_decoding_backend_vtable*)user;
        return ((delegate* unmanaged[Cdecl]<nint, char*, nint, nint, nint, ma_result>)source->onInitFileW)(0, path, config, allocations, backend);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static ma_result OpenUtf8(nint user, byte* path, nint config, nint allocations, nint backend)
    {
        int length = 0; while (path != null && length < 32768 && path[length] != 0) length++;
        if (length < 4 || path[length - 4] != '.' || (path[length - 3] | 32) != 'o' ||
            (path[length - 2] | 32) != 'g' || (path[length - 1] | 32) != 'g') return ma_result.invalid_file;
        var source = (ma_decoding_backend_vtable*)user;
        return ((delegate* unmanaged[Cdecl]<nint, byte*, nint, nint, nint, ma_result>)source->onInitFile)(0, path, config, allocations, backend);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Uninit(nint user, nint backend, nint allocations)
    {
        var source = (ma_decoding_backend_vtable*)user;
        ((delegate* unmanaged[Cdecl]<nint, nint, nint, void>)source->onUninit)(0, backend, allocations);
    }

    public void Dispose()
    {
        if (routed == null) return;
        NativeMemory.Free(routed); routed = null;
    }
}
