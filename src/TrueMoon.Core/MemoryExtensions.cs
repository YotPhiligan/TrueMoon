namespace TrueMoon;

public static class MemoryExtensions
{
    public static void Return<T>(this Memory<T> memory) => MemoryPoolUtils.Return(memory);

    public static void Return<T>(this ReadOnlyMemory<T> memory) => MemoryPoolUtils.Return(memory);
    
    public static bool TryReturn<T>(this Memory<T> memory) => MemoryPoolUtils.TryReturn(memory);
    public static bool TryReturn<T>(this ReadOnlyMemory<T> memory) => MemoryPoolUtils.TryReturn(memory);
}