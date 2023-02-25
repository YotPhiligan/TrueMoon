using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TrueMoon.Thorium.Signals;

namespace TrueMoon.Thorium.Utils;

internal static class SignalStorageUtils
{
    public static readonly byte DescriptorSize = (byte)Unsafe.SizeOf<SignalStorageDescriptor>();
    public static readonly byte SignalSize = (byte)Unsafe.SizeOf<Signal>();
    public static SignalStorageDescriptor GetDescriptor(MemoryMappedViewAccessor accessor)
    {
        //var size = Unsafe.SizeOf<SignalStorageDescriptor>();
        //Span<byte> messagesBytes = stackalloc byte[size];
        accessor.Read<SignalStorageDescriptor>(0, out var descriptor);
        return descriptor;
    }
}