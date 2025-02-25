using System.Runtime.CompilerServices;

namespace TrueMoon.Cobalt;

public static class TypeUtils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static string GetTypeId<T>() => typeof(T).ToString();
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static string GetTypeId(Type type) => type.ToString();
}