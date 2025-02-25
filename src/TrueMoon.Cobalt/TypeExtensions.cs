using System.Runtime.CompilerServices;

namespace TrueMoon.Cobalt;

public static class TypeExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static string GetTypeId(this Type type) => TypeUtils.GetTypeId(type);
}