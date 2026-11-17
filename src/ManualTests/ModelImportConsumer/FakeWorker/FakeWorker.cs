using System.Runtime.InteropServices;

namespace TrueMoon.ManualTests.ModelImportConsumer;

// Acceptance-only external worker, deployed separately. Production has no test modes.
internal static partial class FakeWorker
{
    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine("TMIM/1 READY"); Console.Out.Flush();
        if (Console.ReadLine() != "GO") return 3;
        await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(args[0])!, "started"), Environment.ProcessId.ToString());
        var mode = (await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "mode.txt"))).Trim();
        if (mode == "memory")
        {
            var small = VirtualAlloc(0, 1024 * 1024, 0x3000, 4);
            if (small == 0) return 43;
            var oversized = VirtualAlloc(0, 512 * 1024 * 1024, 0x3000, 4);
            if (oversized != 0) { VirtualFree(oversized, 0, 0x8000); VirtualFree(small, 0, 0x8000); return 44; }
            return VirtualFree(small, 0, 0x8000) != 0 ? 42 : 45;
        }
        if (mode == "crash") return 23;
        if (mode == "malformed") { await File.WriteAllBytesAsync(args[1], new byte[] {1,2,3,4}); Console.WriteLine("TMIM/1 DONE"); return 0; }
        if (mode == "flood") { Console.Write(new string('X', 100000)); return 0; }
        await Task.Delay(Timeout.InfiniteTimeSpan); return 0;
    }
    [LibraryImport("kernel32", SetLastError = true)] private static partial nint VirtualAlloc(nint address, nuint bytes, uint allocation, uint protection);
    [LibraryImport("kernel32", SetLastError = true)] private static partial int VirtualFree(nint address, nuint bytes, uint freeType);
}
