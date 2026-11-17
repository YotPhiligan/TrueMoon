using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace TrueMoon.Aether.Tooling.Importing;

// Windows x64 native isolation. Job attachment and queried limits precede the protocol's GO/native activation.
internal sealed partial class ModelImportJob : SafeHandleZeroOrMinusOneIsInvalid
{
    internal const nuint MemoryLimit = 512 * 1024 * 1024;
    public ModelImportJob() : base(true) { }
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
    internal static ModelImportJob Attach(Process process)
    {
        if (!OperatingSystem.IsWindows() || IntPtr.Size != 8 || Marshal.SizeOf<Extended>() != 144)
            throw new PlatformNotSupportedException("Model import requires Windows x64.");
        var job = CreateJobObjectW(0, 0);
        if (job.IsInvalid) { var error = Marshal.GetLastPInvokeError(); job.Dispose(); throw new Win32Exception(error); }
        try
        {
            var limits = new Extended { Basic = new() { Flags = 0x2000 | 0x100 | 0x8, ActiveProcesses = 1 }, ProcessMemory = MemoryLimit };
            if (SetInformationJobObject(job, 9, in limits, (uint)Marshal.SizeOf<Extended>()) == 0 ||
                AssignProcessToJobObject(job, process.SafeHandle) == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
            if (QueryInformationJobObject(job, 9, out var actual, (uint)Marshal.SizeOf<Extended>(), out _) == 0 ||
                actual.ProcessMemory != MemoryLimit || actual.Basic.Flags != limits.Basic.Flags || actual.Basic.ActiveProcesses != 1)
                throw new InvalidDataException("Model import Job limits not installed.");
            return job;
        }
        catch { job.Dispose(); throw; }
    }
    internal void Terminate()
    {
        if (TerminateJobObject(this, 125) == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
    }
    protected override bool ReleaseHandle() => CloseHandle(handle) != 0;
    [LibraryImport("kernel32", SetLastError = true)] private static partial ModelImportJob CreateJobObjectW(nint attributes, nint name);
    [LibraryImport("kernel32", SetLastError = true)] private static partial int SetInformationJobObject(ModelImportJob job, int informationClass, in Extended information, uint length);
    [LibraryImport("kernel32", SetLastError = true)] private static partial int QueryInformationJobObject(ModelImportJob job, int informationClass, out Extended information, uint length, out uint returned);
    [LibraryImport("kernel32", SetLastError = true)] private static partial int AssignProcessToJobObject(ModelImportJob job, SafeProcessHandle process);
    [LibraryImport("kernel32", SetLastError = true)] private static partial int TerminateJobObject(ModelImportJob job, uint exitCode);
    [LibraryImport("kernel32", SetLastError = true)] private static partial int CloseHandle(nint handle);
}
