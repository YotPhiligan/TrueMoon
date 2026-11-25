using Microsoft.CodeAnalysis;

namespace TrueMoon.Argentis.Generator;

internal sealed record FluentExtensionReceiver(string OwnerName, string OwnerType, bool IsSealed)
{
    internal static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    internal string ReturnType => IsSealed ? OwnerType : "T";

    internal string Declare(string name) => IsSealed ? $"extension({OwnerType} {name})" :
        $"extension<T>(T {name}) where T : {OwnerType}";

    internal string ControlType => $"global::TrueMoon.Argentis.Views.UiControl<{ReturnType}, TData>";

    internal string DeclareControl(string name) => IsSealed ? $"extension<TData>({ControlType} {name})" :
        $"extension<T, TData>({ControlType} {name}) where T : {OwnerType}";

    internal static FluentExtensionReceiver? TryCreate(INamedTypeSymbol owner, Compilation compilation)
    {
        if (owner is not { DeclaredAccessibility: Accessibility.Public, ContainingType: null, IsGenericType: false } ||
            compilation.GetTypeByMetadataName("TrueMoon.Argentis.Element") is not { } element)
        {
            return null;
        }

        for (INamedTypeSymbol? current = owner; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, element))
                return new(owner.ToDisplayString(), owner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), owner.IsSealed);
        }

        return null;
    }
}
