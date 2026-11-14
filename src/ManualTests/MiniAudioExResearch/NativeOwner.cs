using System.Runtime.InteropServices;
using MiniAudioEx.Native;

namespace MiniAudioExResearch;

// Pointer storage comes from the library allocator; uninitialization is conditional on successful init.
internal sealed class NativeOwner : SafeHandle
{
    private readonly ma_allocation_type type;
    private readonly long bytes;
    private bool initialized;
    internal static long LiveObjects;
    internal static long LiveObjectBytes;

    internal NativeOwner(ma_allocation_type type) : base(0, true)
    {
        this.type = type;
        bytes = checked((long)(nuint)MiniAudioNative.ma_get_size_of_type(type));
        SetHandle(MiniAudioNative.ma_allocate_type(type));
        if (IsInvalid) throw new OutOfMemoryException(type.ToString());
        Interlocked.Increment(ref LiveObjects);
        Interlocked.Add(ref LiveObjectBytes, bytes);
    }

    public override bool IsInvalid => handle == 0;
    internal nint Pointer => IsClosed ? throw new ObjectDisposedException(type.ToString()) : handle;
    internal bool HasInitialized => initialized;
    internal void Initialized() => initialized = true;

    protected override bool ReleaseHandle()
    {
        if (initialized)
        {
            switch (type)
            {
                case ma_allocation_type.sound: MiniAudioNative.ma_sound_uninit(new(handle)); break;
                case ma_allocation_type.engine: MiniAudioNative.ma_engine_uninit(new(handle)); break;
                case ma_allocation_type.resource_manager: MiniAudioNative.ma_resource_manager_uninit(new(handle)); break;
                case ma_allocation_type.context: MiniAudioNative.ma_context_uninit(new(handle)); break;
                case ma_allocation_type.device: MiniAudioNative.ma_device_uninit(new(handle)); break;
            }
        }
        MiniAudioNative.ma_deallocate_type(handle);
        Interlocked.Decrement(ref LiveObjects);
        Interlocked.Add(ref LiveObjectBytes, -bytes);
        handle = 0;
        return true;
    }
}
