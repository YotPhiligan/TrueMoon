using Microsoft.CodeAnalysis;

namespace TrueMoon.Thorium.Generator.Utils;

internal static class GenerationUtils
{
    internal static string GetTypeString(ITypeSymbol symbol)
    {
        switch (symbol)
        {
            case null:
                return default;
            case IArrayTypeSymbol ar:
            {
                return $"{GetTypeString(ar.ElementType)}[]";
            }
            case INamedTypeSymbol
            {
                TypeKind: TypeKind.Class or TypeKind.Interface or TypeKind.Error, IsGenericType: true,
                Name: "List" or "IReadOnlyList"
            } p:
            {
                var t = p.TypeArguments[0];
                var pStr = $"{p.OriginalDefinition}";
                var listTypeStr = pStr.Split('<')[0];
                return $"{listTypeStr}<{GetTypeString(t)}>";
            }
            case INamedTypeSymbol
            {
                TypeKind: TypeKind.Class or TypeKind.Interface or TypeKind.Error, IsGenericType: true,
                Name: "Dictionary" or "IReadOnlyDictionary"
            } pv:
            {
                var keyType = pv.TypeArguments[0];
                var valueType = pv.TypeArguments[1];
                var tStr = $"{pv.OriginalDefinition}";
                var dictionaryTypeStr = tStr.Split('<')[0];
                return $"{dictionaryTypeStr}<{GetTypeString(keyType)},{GetTypeString(valueType)}>";
            }
            case INamedTypeSymbol {NullableAnnotation:NullableAnnotation.Annotated, MetadataName:"Nullable`1", TypeArguments.Length:1} tp:
                var s = tp.TypeArguments[0];
                return WellKnownTypes.NotContains(s) ? $"global::{s}?" : $"{s}?";
            case INamedTypeSymbol { IsTupleType:true, TupleElements.IsEmpty:false } tupleSymbol:
                var str = "";
                foreach (var element in tupleSymbol.TupleElements)
                {
                    var elementName = element.IsExplicitlyNamedTupleElement
                        ? $" {element.MetadataName}"
                        : "";
                    if (element.Type is INamedTypeSymbol {NullableAnnotation:NullableAnnotation.Annotated, MetadataName:"Nullable`1", TypeArguments.Length:1} tp)
                    {
                        var sType = tp.TypeArguments[0];
                        //str += WellKnownTypes.NotContains(element.Type) ? $"global::{sType}?{elementName}," : $"{sType}?{elementName},";
                        str += $"{GetTypeString(sType)}?{elementName},";
                    }
                    else
                    {
                        //str += WellKnownTypes.NotContains(element.Type) ? $"global::{element.Type}{elementName}," : $"{element.Type}{elementName},";
                        str += $"{GetTypeString(element.Type)}{elementName},";
                    }
                }
                return $"({str.TrimEnd(',')})";
            default:
                return WellKnownTypes.NotContains(symbol) ? $"global::{symbol}" : $"{symbol}";
        }
    }
}