using System.Runtime.InteropServices;
using MiniAudioEx.Native;

namespace TrueMoon.Kul.Native;

// Each native child retains its parents, including during SafeHandle finalization.
internal sealed class NativeHandle : SafeHandle
{
    private readonly ma_allocation_type type;
    private readonly SafeHandle[] parents = [];
    private bool initialized;
    internal static int LiveCount;

    internal NativeHandle(ma_allocation_type type, params SafeHandle[] parents) : base(0, true)
    {
        this.type = type;
        this.parents = (SafeHandle[])parents.Clone();
        int retained = 0;
        try
        {
            foreach (var parent in this.parents)
            {
                bool success = false;
                parent.DangerousAddRef(ref success);
                if (success) retained++;
            }
            SetHandle(MiniAudioNative.ma_allocate_type(type));
            if (IsInvalid) throw new OutOfMemoryException(type.ToString());
            Interlocked.Increment(ref LiveCount);
        }
        catch { for (int i = 0; i < retained; i++) this.parents[i].DangerousRelease(); throw; }
    }
    public override bool IsInvalid => handle == 0;
    internal nint Pointer => IsClosed ? throw new ObjectDisposedException(nameof(NativeHandle)) : handle;
    internal bool IsInitialized => initialized;
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
            }
        }
        MiniAudioNative.ma_deallocate_type(handle);
        handle = 0;
        Interlocked.Decrement(ref LiveCount);
        foreach (var parent in parents) parent.DangerousRelease();
        return true;
    }
}
