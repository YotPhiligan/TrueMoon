using Microsoft.CodeAnalysis;

namespace TrueMoon.Enerit.Generator;

internal static class InvocationContractValidation
{
    private static readonly DiagnosticDescriptor Unsupported = new("TME001", "Unsupported invocation contract",
        "Service '{0}' is unsupported: {1}", "TrueMoon.Enerit", DiagnosticSeverity.Error, true);

    internal static bool Validate(INamedTypeSymbol service, IReadOnlyList<ISymbol> members, SourceProductionContext context)
    {
        string? error = service.TypeKind != TypeKind.Interface ? "a service must be an interface" : null;
        var methods = members.OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary).ToArray();
        if (methods.Length > 256) error = "method codes are limited to 256 methods";
        if (members.Any(m => m is IPropertySymbol or IEventSymbol)) error = "properties and events are not invocation methods";
        foreach (var method in methods)
        {
            if (error is not null) break;
            if (method.IsStatic || method.IsGenericMethod || method.ReturnsByRef || method.ReturnsByRefReadonly || method.Parameters.Any(p => p.RefKind != RefKind.None))
            { error = "static, generic and ref/out methods are unsupported"; break; }
            var returnType = method.ReturnType;
            if (returnType is INamedTypeSymbol { Name: "Task", ContainingNamespace: { } ns } task && ns.ToDisplayString() == "System.Threading.Tasks")
                returnType = task.IsGenericType ? task.TypeArguments[0] : null;
            if (returnType is not null && returnType.SpecialType != SpecialType.System_Void && !Supported(returnType, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default)))
                error = $"unsupported return payload '{returnType}'";
            foreach (var parameter in method.Parameters)
            {
                if (parameter.Type.ToDisplayString() == "System.Threading.CancellationToken") continue;
                if (!Supported(parameter.Type, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default)))
                    error = $"unsupported parameter payload '{parameter.Type}'";
            }
        }
        if (error is null) return true;
        context.ReportDiagnostic(Diagnostic.Create(Unsupported, service.Locations.FirstOrDefault(), service.ToDisplayString(), error));
        return false;
    }

    private static bool Supported(ITypeSymbol type, HashSet<ITypeSymbol> path)
    {
        if (type.SpecialType is SpecialType.System_Boolean or SpecialType.System_Byte or SpecialType.System_Int32 or
            SpecialType.System_Int64 or SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_String) return true;
        if (type.TypeKind == TypeKind.Enum) return ((INamedTypeSymbol)type).EnumUnderlyingType?.SpecialType == SpecialType.System_Int32;
        if (type is IArrayTypeSymbol array) return array.Rank == 1 && ElementSupported(array.ElementType, path);
        if (type is not INamedTypeSymbol named || path.Count >= 32) return false;
        if (named.ContainingNamespace.ToDisplayString() == "System")
        {
            if (named.Name is "Guid" or "DateTime" or "DateTimeOffset" or "TimeSpan") return true;
            if (named.Name is "Memory" or "ReadOnlyMemory") return named.TypeArguments.Length == 1 && named.TypeArguments[0].SpecialType == SpecialType.System_Byte;
            if (named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                return named.TypeArguments[0].IsValueType && named.TypeArguments[0].IsUnmanagedType && Supported(named.TypeArguments[0], path);
        }
        if (named.ContainingNamespace.ToDisplayString() == "System.Collections.Generic" &&
            named.Name is "List" or "IReadOnlyList" or "Dictionary" or "IReadOnlyDictionary")
            return named.TypeArguments.All(argument => ElementSupported(argument, path));
        if (named.IsGenericType || named.TypeKind != TypeKind.Class || named.IsAbstract || named.IsRecord ||
            named.SpecialType == SpecialType.System_Object || named.BaseType?.SpecialType != SpecialType.System_Object ||
            named.InstanceConstructors.Length != 1 || named.InstanceConstructors[0].Parameters.Length != 0 ||
            named.InstanceConstructors[0].DeclaredAccessibility != Accessibility.Public || !path.Add(named)) return false;
        try
        {
            foreach (var member in named.GetMembers())
            {
                if (member is IPropertySymbol { DeclaredAccessibility: Accessibility.Public } property &&
                    (property.IsStatic || property.IsIndexer || property.GetMethod?.DeclaredAccessibility != Accessibility.Public ||
                     property.SetMethod?.DeclaredAccessibility != Accessibility.Public || property.SetMethod.IsInitOnly || !Supported(property.Type, path))) return false;
                if (member is IFieldSymbol { DeclaredAccessibility: Accessibility.Public, IsConst: false } field &&
                    (field.IsStatic || field.IsReadOnly || !Supported(field.Type, path))) return false;
            }
            return true;
        }
        finally { path.Remove(named); }
    }

    private static bool ElementSupported(ITypeSymbol type, HashSet<ITypeSymbol> path)
    {
        // Nested collection/tuple elements need their own generated codec before they can be supported.
        if (type is IArrayTypeSymbol || type is INamedTypeSymbol { ContainingNamespace: { } ns } && ns.ToDisplayString() == "System.Collections.Generic") return false;
        return Supported(type, path);
    }
}
