using Microsoft.CodeAnalysis;

namespace TrueMoon.Argentis.Generator;

internal sealed record FluentUiProperty(FluentExtensionReceiver Receiver, string FieldName, string Name, string ValueType)
{
    private const string PropertySuffix = "Property";

    internal static FluentUiProperty? TryRead(GeneratorAttributeSyntaxContext input)
    {
        var compilation = input.SemanticModel.Compilation;
        var uiProperty = compilation.GetTypeByMetadataName("TrueMoon.Argentis.UiProperty`1");
        if (input.TargetSymbol is IFieldSymbol
            {
                DeclaredAccessibility: Accessibility.Public, IsStatic: true, IsReadOnly: true,
                NullableAnnotation: not NullableAnnotation.Annotated,
                Type: INamedTypeSymbol { TypeArguments: [var valueType] } propertyType
            } field &&
            uiProperty is not null && SymbolEqualityComparer.Default.Equals(propertyType.OriginalDefinition, uiProperty) &&
            field.Name.Length > PropertySuffix.Length && field.Name.EndsWith(PropertySuffix, StringComparison.Ordinal) &&
            FluentExtensionReceiver.TryCreate(field.ContainingType, compilation) is { } receiver)
        {
            return new(receiver, field.Name, field.Name[..^PropertySuffix.Length],
                valueType.ToDisplayString(FluentExtensionReceiver.TypeFormat));
        }

        return null;
    }
}
