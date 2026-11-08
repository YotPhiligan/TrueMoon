#if NETTERLOW
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ModelImportResearch;

internal sealed class ImportJob : SafeHandleZeroOrMinusOneIsInvalid
{
    internal const nuint MemoryLimit = 512 * 1024 * 1024;
    private ImportJob() : base(true) { }
    [StructLayout(LayoutKind.Sequential)]
    private struct Basic
    {
        internal long ProcessTime, JobTime;
        internal uint Flags;
        internal nuint MinWorkingSet, MaxWorkingSet;
        internal uint ActiveProcesses;
        internal nuint Affinity;
        internal uint Priority, Scheduling;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters { internal ulong ReadOps, WriteOps, OtherOps, ReadBytes, WriteBytes, OtherBytes; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Extended
    {
        internal Basic Basic;
        internal IoCounters Io;
        internal nuint ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
    }
    internal static ImportJob Attach(Process process)
    {
        if (!OperatingSystem.IsWindows() || IntPtr.Size != 8 || Marshal.SizeOf<Extended>() != 144)
            throw new PlatformNotSupportedException("Research Job profile requires Windows x64.");
        var job = CreateJobObjectW(0, null);
        if (job.IsInvalid) { job.Dispose(); throw new Win32Exception(Marshal.GetLastPInvokeError()); }
        try
        {
            var limits = new Extended { Basic = new() { Flags = 0x2000 | 0x100 | 0x8, ActiveProcesses = 1 }, ProcessMemory = MemoryLimit };
            if (!SetInformationJobObject(job, 9, ref limits, (uint)Marshal.SizeOf<Extended>()) ||
                !AssignProcessToJobObject(job, process.SafeHandle)) throw new Win32Exception(Marshal.GetLastPInvokeError());
            if (!QueryInformationJobObject(job, 9, out var actual, (uint)Marshal.SizeOf<Extended>(), out _) ||
                actual.ProcessMemory != MemoryLimit || actual.Basic.Flags != limits.Basic.Flags || actual.Basic.ActiveProcesses != 1)
                throw new InvalidDataException("Job limits not installed.");
            return job;
        }
        catch { job.Dispose(); throw; }
    }
    internal void Terminate()
    {
        if (!TerminateJobObject(this, 125)) throw new Win32Exception(Marshal.GetLastPInvokeError());
    }
    internal static void ProveCommitDenial()
    {
        var small = VirtualAlloc(0, 1024 * 1024, 0x3000, 4);
        if (small == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
        try
        {
            var oversized = VirtualAlloc(0, MemoryLimit, 0x3000, 4);
            if (oversized != 0) { VirtualFree(oversized, 0, 0x8000); throw new InvalidDataException("Job did not deny oversized commit."); }
        }
        finally { if (!VirtualFree(small, 0, 0x8000)) throw new Win32Exception(Marshal.GetLastPInvokeError()); }
    }
    protected override bool ReleaseHandle() => CloseHandle(handle);
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ImportJob CreateJobObjectW(nint attributes, string? name);
    [DllImport("kernel32", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(ImportJob job, int informationClass, ref Extended information, uint length);
    [DllImport("kernel32", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryInformationJobObject(ImportJob job, int informationClass, out Extended information, uint length, out uint returned);
    [DllImport("kernel32", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(ImportJob job, SafeProcessHandle process);
    [DllImport("kernel32", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateJobObject(ImportJob job, uint exitCode);
    [DllImport("kernel32", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);
    [DllImport("kernel32", SetLastError = true)]
    private static extern nint VirtualAlloc(nint address, nuint bytes, uint allocation, uint protection);
    [DllImport("kernel32", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool VirtualFree(nint address, nuint bytes, uint freeType);
}
#endif
