using Microsoft.CodeAnalysis;

namespace TrueMoon.Thorium.Generator.Utils;

internal static class WellKnownTypes
{
    private static readonly IReadOnlyList<string> Types = new[]
    {
        "object",
        "Object",
        "bool",
        "Bool",
        "int",
        "Int32",
        "float",
        "Float",
        "byte",
        "Byte",
        "long",
        "Int64",
        "string",
        "String",
        "double",
        "Double",
        "CancellationToken",
        "Memory<byte>",
        "ReadOnlyMemory<byte>",
        "Memory",
        "ReadOnlyMemory",
        "Nullable",
        "DateTime",
        "DateTimeOffset",
        "TimeSpan",
        "Guid",
    };
    
    internal static bool Contains(ITypeSymbol symbol) 
        => !NotContains(symbol);
    internal static bool NotContains(ITypeSymbol symbol) 
        => !Types.Contains(symbol.Name) && !Types.Contains($"{symbol}");
    internal static bool NotContains(string symbol) 
        => !Types.Contains(symbol) && !Types.Contains($"{symbol}");
    
    internal static bool IsCancellationToken(ITypeSymbol symbol) 
        => symbol.Name == "CancellationToken" || $"{symbol}" == "CancellationToken";
    
    internal static bool IsObject(ITypeSymbol symbol) 
        => symbol.Name == "Object" || $"{symbol}" == "Object" || symbol.Name == "object" || $"{symbol}" == "object";
    
    internal static bool IsNullable(ITypeSymbol symbol) 
        => $"{symbol}".Contains("System.Nullable");

    public static bool IsString(ITypeSymbol symbol)
        => string.Equals(symbol.Name, "String", StringComparison.InvariantCultureIgnoreCase) 
           || string.Equals($"{symbol}", "String", StringComparison.InvariantCultureIgnoreCase);
}