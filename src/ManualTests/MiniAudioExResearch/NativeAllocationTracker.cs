using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MiniAudioEx.Native;

namespace MiniAudioExResearch;

// Diagnostic allocator. Its unmanaged ledger remains alive until every native owner drains.
// This observes allocations routed through the configured callbacks, not arbitrary OS/codec heaps.
internal sealed unsafe class NativeAllocationTracker : IDisposable
{
    internal static readonly List<object> CleanupFailures = [];
    [StructLayout(LayoutKind.Sequential)]
    private struct Ledger
    {
        internal long Bytes, Blocks, PeakBytes, Allocations, FailAt;
        internal Header* Head;
        internal int Lock;
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    private struct Header
    {
        internal nuint Size;
        internal Header* Previous;
        internal Header* Next;
    }

    private Ledger* ledger = (Ledger*)NativeMemory.AllocZeroed((nuint)sizeof(Ledger));
    internal long Bytes => Interlocked.Read(ref ledger->Bytes);
    internal long Blocks => Interlocked.Read(ref ledger->Blocks);
    internal long PeakBytes => Interlocked.Read(ref ledger->PeakBytes);
    internal long Allocations => Interlocked.Read(ref ledger->Allocations);
    internal void FailNextAllocation() => ledger->FailAt = Allocations + 1;

    internal ma_allocation_callbacks Callbacks => new()
    {
        pUserData = (nint)ledger,
        onMalloc = (nint)(delegate* unmanaged[Cdecl]<nuint, nint, nint>)&Allocate,
        onRealloc = (nint)(delegate* unmanaged[Cdecl]<nint, nuint, nint, nint>)&Reallocate,
        onFree = (nint)(delegate* unmanaged[Cdecl]<nint, nint, void>)&Free
    };

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static nint Allocate(nuint size, nint user) => AllocateCore(size, (Ledger*)user);

    private static nint AllocateCore(nuint size, Ledger* state)
    {
        long sequence = Interlocked.Increment(ref state->Allocations);
        if (sequence == Interlocked.Read(ref state->FailAt) || size > (nuint)(long.MaxValue - sizeof(Header)))
            return 0;
        // Avoid an exception crossing native code, including an allocator OOM.
        Header* header;
        try { header = (Header*)NativeMemory.Alloc(size + (nuint)sizeof(Header)); }
        catch { return 0; }
        if (header == null) return 0;
        header->Size = size;
        while (Interlocked.CompareExchange(ref state->Lock, 1, 0) != 0) Thread.SpinWait(1);
        header->Previous = null; header->Next = state->Head;
        if (state->Head != null) state->Head->Previous = header;
        state->Head = header;
        Volatile.Write(ref state->Lock, 0);
        long current = Interlocked.Add(ref state->Bytes, (long)size);
        Interlocked.Increment(ref state->Blocks);
        long peak;
        while (current > (peak = Interlocked.Read(ref state->PeakBytes)) &&
               Interlocked.CompareExchange(ref state->PeakBytes, current, peak) != peak) { }
        return (nint)(header + 1);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Free(nint pointer, nint user) => FreeCore(pointer, (Ledger*)user);

    private static void FreeCore(nint pointer, Ledger* state)
    {
        if (pointer == 0) return;
        var header = (Header*)pointer - 1;
        while (Interlocked.CompareExchange(ref state->Lock, 1, 0) != 0) Thread.SpinWait(1);
        if (header->Previous != null) header->Previous->Next = header->Next;
        else state->Head = header->Next;
        if (header->Next != null) header->Next->Previous = header->Previous;
        Volatile.Write(ref state->Lock, 0);
        Interlocked.Add(ref state->Bytes, -(long)header->Size);
        Interlocked.Decrement(ref state->Blocks);
        NativeMemory.Free(header);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static nint Reallocate(nint pointer, nuint size, nint user)
    {
        var state = (Ledger*)user;
        if (size == 0) { FreeCore(pointer, state); return 0; }
        nint replacement = AllocateCore(size, state);
        if (replacement == 0) return 0; // realloc failure preserves the original allocation.
        if (pointer != 0)
        {
            nuint oldSize = ((Header*)pointer - 1)->Size;
            NativeMemory.Copy((void*)pointer, (void*)replacement, nuint.Min(oldSize, size));
            FreeCore(pointer, state);
        }
        return replacement;
    }

    public void Dispose()
    {
        if (ledger == null) return;
        if (Bytes != 0 || Blocks != 0)
        {
            // Preserve the primary failure during stack unwinding; the final report also fails this condition.
            CleanupFailures.Add(new { Bytes, Blocks, PeakBytes, Allocations, Outstanding = Outstanding() });
            Console.Error.WriteLine($"Native allocations remain: {Blocks} blocks / {Bytes} bytes; ledger retained.");
            return;
        }
        NativeMemory.Free(ledger);
        ledger = null;
    }

    internal object[] Outstanding()
    {
        var result = new List<object>();
        for (Header* header = ledger->Head; header != null; header = header->Next)
            result.Add(new { Bytes = (ulong)header->Size });
        return result.ToArray();
    }
}
