using Microsoft.CodeAnalysis;

namespace TrueMoon.Thorium.Generator.Extensions;

public static class SymbolExtensions
{
    internal static (string? type, bool isVoid, bool isTask, ITypeSymbol? returnTypeSymbol) GetReturnDetails(this IMethodSymbol methodSymbol)
    {
        var isTask = $"{methodSymbol.ReturnType}" == "Task" || methodSymbol.ReturnType.ToDisplayString().Contains("Task");
        var isVoid = $"{methodSymbol.ReturnType}" == "void" || methodSymbol.ReturnType.ToDisplayString().Contains("void");

        ITypeSymbol? returnTypeSymbol = isVoid 
            ? default 
            : (isTask ? default : methodSymbol.ReturnType);
        
        string? returnType = isVoid 
            ? default 
            : (isTask ? default : $"{methodSymbol.ReturnType}");
        
        if (isTask && methodSymbol.ReturnType is INamedTypeSymbol { IsGenericType: true } v
                   && v.TypeArguments.Any())
        {
            returnTypeSymbol = v.TypeArguments[0];
            returnType = $"{returnTypeSymbol}";
        }

        return (returnType, isVoid, isTask, returnTypeSymbol);
    }
    
}