namespace TrueMoon;

/// <summary>Returns memory rented by MemoryPoolUtils with ownership validation.</summary>
public static class MemoryExtensions
{
    /// <summary>Returns an active whole rental once; foreign, sliced or returned memory is rejected.</summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="memory">The whole logical rental.</param>
    public static void Return<T>(this Memory<T> memory) => MemoryPoolUtils.Return(memory);
    /// <summary>Returns an active whole rental once; foreign, sliced or returned memory is rejected.</summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="memory">The whole logical rental.</param>
    public static void Return<T>(this ReadOnlyMemory<T> memory) => MemoryPoolUtils.Return(memory);
    /// <summary>Attempts to return a whole rental once.</summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="memory">Memory to return.</param>
    /// <returns>True when an active whole rental was returned.</returns>
    public static bool TryReturn<T>(this Memory<T> memory) => MemoryPoolUtils.TryReturn(memory);
    /// <summary>Attempts to return a whole rental once.</summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="memory">Memory to return.</param>
    /// <returns>True when an active whole rental was returned.</returns>
    public static bool TryReturn<T>(this ReadOnlyMemory<T> memory) => MemoryPoolUtils.TryReturn(memory);
}
