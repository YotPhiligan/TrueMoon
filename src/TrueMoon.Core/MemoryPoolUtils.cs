using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TrueMoon;

public static class MemoryPoolUtils
{
    public static void Return<T>(Memory<T> memory) => Return((ReadOnlyMemory<T>)memory);

    public static void Return<T>(ReadOnlyMemory<T> memory)
    {
        if (MemoryMarshal.TryGetArray(memory, out var segment) && segment.Array is { Length: > 0 })
        {
            ArrayPool<T>.Shared.Return(segment.Array, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
        else
        {
            throw new InvalidOperationException("Failed to return memory to array pool");
        }
    }
    
    public static bool TryReturn<T>(Memory<T> memory) => TryReturn((ReadOnlyMemory<T>)memory);

    public static bool TryReturn<T>(ReadOnlyMemory<T> memory)
    {
        try
        {
            if (MemoryMarshal.TryGetArray(memory, out var segment) && segment.Array is { Length: > 0 })
            {
                ArrayPool<T>.Shared.Return(segment.Array, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
                return true;
            }
        }
        catch (Exception e)
        {
            return false;
        }

        return false;
    }

    public static Memory<byte> Create(int len) => ArrayPool<byte>.Shared.Rent(len).AsMemory(0, len);
}