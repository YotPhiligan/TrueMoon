using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace AetherSourceWatchPerformance;

// Whole-process accounting, including the sampler. ReadTransfer is logical IO, not physical disk traffic.
internal sealed partial class ProcessMetrics : IDisposable
{
    private readonly Process _process = Process.GetCurrentProcess();
    internal MetricPoint Capture()
    {
        if (GetProcessIoCounters(_process.SafeHandle, out var io) == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
        var memory = new MemoryCounters { Size = (uint)Marshal.SizeOf<MemoryCounters>() };
        if (K32GetProcessMemoryInfo(_process.SafeHandle, ref memory, memory.Size) == 0 ||
            GetProcessHandleCount(_process.SafeHandle, out var handles) == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
        return new(Stopwatch.GetTimestamp(), _process.TotalProcessorTime.TotalMilliseconds,
            GC.GetTotalAllocatedBytes(true), GC.GetTotalMemory(false), (long)memory.PrivateUsage, (long)memory.WorkingSet,
            (int)handles, io.ReadTransferCount, io.ReadOperationCount, io.OtherOperationCount,
            GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
    }
    internal void SamplePeak(MetricPoint point, ref long peakPrivate, ref long peakWorking)
    { peakPrivate = Math.Max(peakPrivate, point.PrivateBytes); peakWorking = Math.Max(peakWorking, point.WorkingSetBytes); }
    public void Dispose() => _process.Dispose();

    // Win32 BOOL is 32 bits; all six IO_COUNTERS fields are ULONGLONG. SafeHandle is borrowed from Process.
    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial int GetProcessIoCounters(SafeProcessHandle process, out IoCounters counters);
    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial int K32GetProcessMemoryInfo(SafeProcessHandle process, ref MemoryCounters counters, uint size);
    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial int GetProcessHandleCount(SafeProcessHandle process, out uint count);
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryCounters
    {
        public uint Size, PageFaultCount;
        public nuint PeakWorkingSet, WorkingSet, QuotaPeakPagedPoolUsage, QuotaPagedPoolUsage,
            QuotaPeakNonPagedPoolUsage, QuotaNonPagedPoolUsage, PagefileUsage, PeakPagefileUsage, PrivateUsage;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes;
        public readonly ulong ReadOperationCount => ReadOperations;
        public readonly ulong OtherOperationCount => OtherOperations;
        public readonly ulong ReadTransferCount => ReadBytes;
    }
}

internal sealed record MetricPoint(long Timestamp, double CpuMilliseconds, long AllocatedBytes, long ManagedBytes,
    long PrivateBytes, long WorkingSetBytes, int Handles, ulong ReadBytes, ulong ReadOperations, ulong OtherOperations, int Gen0, int Gen1, int Gen2);

internal sealed record MetricWindow(double ElapsedMilliseconds, double CpuMilliseconds, double CpuOneCorePercent,
    long AllocatedBytes, ulong ReadBytes, ulong ReadOperations, ulong OtherOperations, long ManagedBytesBefore,
    long ManagedBytesAfter, long PeakPrivateBytes, long PeakWorkingSetBytes, int HandlesBefore, int HandlesAfter, int Gen0, int Gen1, int Gen2)
{
    internal static MetricWindow Between(MetricPoint before, MetricPoint after, long peakPrivate, long peakWorking)
    {
        var elapsed = Stopwatch.GetElapsedTime(before.Timestamp, after.Timestamp).TotalMilliseconds;
        var cpu = after.CpuMilliseconds - before.CpuMilliseconds;
        return new(elapsed, cpu, cpu * 100 / elapsed, after.AllocatedBytes - before.AllocatedBytes,
            after.ReadBytes - before.ReadBytes, after.ReadOperations - before.ReadOperations, after.OtherOperations - before.OtherOperations,
            before.ManagedBytes, after.ManagedBytes, Math.Max(peakPrivate, after.PrivateBytes), Math.Max(peakWorking, after.WorkingSetBytes),
            before.Handles, after.Handles, after.Gen0 - before.Gen0, after.Gen1 - before.Gen1, after.Gen2 - before.Gen2);
    }
}
