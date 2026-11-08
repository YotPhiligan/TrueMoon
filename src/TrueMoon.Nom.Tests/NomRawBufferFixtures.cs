using System.Buffers;
using System.Runtime.InteropServices;

namespace TrueMoon.Nom.Tests;

internal sealed class NomRawBufferMemory<T>(T[] values, int? logicalLength = null) : MemoryManager<T> where T : unmanaged
{
    internal Memory<T> View => CreateMemory(logicalLength ?? values.Length);
    internal T[] Values => values;
    internal int SpanRequests, PinRequests, DisposeCalls;
    internal bool IsDisposed;
    internal NomFaultGate? Gate;
    public override Span<T> GetSpan()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this); Interlocked.Increment(ref SpanRequests);
        if (Gate is { } gate)
        {
            gate.Entered.TrySetResult();
            if (!gate.Release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Raw owner test gate.");
        }
        return values;
    }
    public override MemoryHandle Pin(int elementIndex = 0) { PinRequests++; throw new NotSupportedException("Caller deliberately cannot pin."); }
    public override void Unpin() => throw new NotSupportedException();
    protected override void Dispose(bool disposing) { DisposeCalls++; IsDisposed = true; }
}

internal enum NomRawCode : ushort { Start = 0x1234, End = 0xfedc }
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct NomRawTriple { internal byte Tag; internal ushort Value; }
[StructLayout(LayoutKind.Sequential)]
internal struct NomRawVector { internal int X, Y, Z; }
[StructLayout(LayoutKind.Sequential)]
internal struct NomRawPadded { internal byte Tag; internal int Value; }

internal static class NomRawBufferFixtures
{
    internal static byte[] Bytes<T>(T[] values) where T : unmanaged => MemoryMarshal.AsBytes(values.AsSpan()).ToArray();
    internal static NomRawTriple[] Triples(int count) => Enumerable.Range(0, count).Select(i => new NomRawTriple { Tag = (byte)(i * 13), Value = (ushort)(i * 17 + 1) }).ToArray();
}
