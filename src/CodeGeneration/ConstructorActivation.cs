using System.Globalization;
using Microsoft.CodeAnalysis;

namespace TrueMoon.CodeGeneration;

internal enum ActivationConstructorFailure
{
    None,
    NoPublicConstructor,
    AmbiguousLongest,
    MultipleMarkedConstructors,
    MarkedConstructorNotPublic,
}

// Shared by Cobalt service generation and provider-neutral Argentis factory generation.
internal static class ConstructorActivation
{
    internal static IMethodSymbol? LongestPublicConstructor(INamedTypeSymbol type, out int ties)
    {
        IMethodSymbol? longest = null;
        ties = 0;
        foreach (var constructor in type.InstanceConstructors)
        {
            if (constructor.DeclaredAccessibility != Accessibility.Public) continue;
            if (longest is null || constructor.Parameters.Length > longest.Parameters.Length)
            {
                longest = constructor;
                ties = 1;
            }
            else if (constructor.Parameters.Length == longest.Parameters.Length) ties++;
        }
        return longest;
    }

    // [ViewConstructor] overrides the longest-public heuristic; a tie without it is ambiguous.
    internal static IMethodSymbol? SelectedConstructor(INamedTypeSymbol type, out ActivationConstructorFailure failure)
    {
        failure = ActivationConstructorFailure.None;
        IMethodSymbol? marked = null;
        foreach (var constructor in type.InstanceConstructors)
        {
            if (!constructor.GetAttributes().Any(attribute =>
                    attribute.AttributeClass?.ToDisplayString() == "TrueMoon.Alloy.Hosting.ViewConstructorAttribute")) continue;
            if (marked is not null)
            {
                failure = ActivationConstructorFailure.MultipleMarkedConstructors;
                return null;
            }
            marked = constructor;
        }

        if (marked is null)
        {
            var longest = LongestPublicConstructor(type, out var ties);
            if (longest is null) failure = ActivationConstructorFailure.NoPublicConstructor;
            else if (ties != 1) failure = ActivationConstructorFailure.AmbiguousLongest;
            else return longest;
            return null;
        }

        if (marked.DeclaredAccessibility != Accessibility.Public) failure = ActivationConstructorFailure.MarkedConstructorNotPublic;
        else return marked;
        return null;
    }

    internal static bool SetsRequiredMembers(IMethodSymbol constructor) =>
        !HasRequiredMembers(constructor.ContainingType) || constructor.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.ToDisplayString() == "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute");

    private static bool HasRequiredMembers(INamedTypeSymbol type) =>
        type.GetMembers().Any(member => member is IPropertySymbol { IsRequired: true } or IFieldSymbol { IsRequired: true }) ||
        type.BaseType is { } parent && HasRequiredMembers(parent);

    internal static string Argument(IParameterSymbol parameter, int index)
    {
        var type = TypeName(parameter.Type);
        if (!parameter.HasExplicitDefaultValue && parameter.NullableAnnotation != NullableAnnotation.Annotated)
            return $"context.Resolve<{type}>()";
        var fallback = parameter.HasExplicitDefaultValue ? DefaultValue(parameter) : $"default({type})";
        return $"(context.GetService(typeof({type})) is object dependency{index} ? ({type})dependency{index} : {fallback})";
    }

    private static string DefaultValue(IParameterSymbol parameter)
    {
        var value = parameter.ExplicitDefaultValue;
        if (value == null) return $"default({TypeName(parameter.Type)})";
        var literal = value switch
        {
            double number when double.IsNaN(number) => "global::System.Double.NaN",
            double number when double.IsPositiveInfinity(number) => "global::System.Double.PositiveInfinity",
            double number when double.IsNegativeInfinity(number) => "global::System.Double.NegativeInfinity",
            float number when float.IsNaN(number) => "global::System.Single.NaN",
            float number when float.IsPositiveInfinity(number) => "global::System.Single.PositiveInfinity",
            float number when float.IsNegativeInfinity(number) => "global::System.Single.NegativeInfinity",
            decimal number => number.ToString(CultureInfo.InvariantCulture) + "M",
            _ => Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatPrimitive(value, quoteStrings: true, useHexadecimalNumbers: false)
        };
        return $"({TypeName(parameter.Type)})({literal})";
    }

    // Preserve the CLR type of a dynamic dependency without introducing dynamic dispatch.
    internal static string TypeName(ITypeSymbol type) => type.TypeKind == TypeKind.Dynamic
        ? "global::System.Object" : type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
}
