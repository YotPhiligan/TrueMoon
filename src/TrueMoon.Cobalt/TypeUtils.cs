using System.Runtime.CompilerServices;

namespace TrueMoon.Cobalt;

internal static class TypeUtils
{
    private static readonly Type EnumerableType = typeof(IEnumerable<>);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static string GetTypeId<T>() => typeof(T).ToString();
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static string GetTypeId(Type type) => type.ToString();
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Type CreateEnumerableType(Type targetType)
    {
        var genericType = EnumerableType.MakeGenericType(targetType);
        return genericType;
    }
}