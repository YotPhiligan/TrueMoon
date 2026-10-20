using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace TrueMoon.Aether.Gpu.Interop;

internal sealed class GpuNtHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    internal static int ActiveCount;
    internal GpuNtHandle(nint value) : base(true)
    { if (value is 0 or -1) throw new InvalidDataException("Invalid GPU NT handle."); SetHandle(value); Interlocked.Increment(ref ActiveCount); }
    internal ulong Token => checked((ulong)DangerousGetHandle());
    internal static GpuNtHandle Duplicate(SafeProcessHandle source, ulong token)
    {
        if (!GpuKernel.DuplicateHandle(source, checked((nint)token), -1, out var handle, 0, 0, 2)) throw new Win32Exception(Marshal.GetLastPInvokeError());
        return new(handle);
    }
    protected override bool ReleaseHandle()
    {
        var closed = GpuKernel.CloseHandle(handle);
        if (closed) Interlocked.Decrement(ref ActiveCount);
        return closed;
    }
}
internal static partial class GpuKernel
{
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DuplicateHandle(SafeProcessHandle sourceProcess, nint source, nint targetProcess, out nint target, uint access, int inherit, uint options);
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CloseHandle(nint handle);
}
