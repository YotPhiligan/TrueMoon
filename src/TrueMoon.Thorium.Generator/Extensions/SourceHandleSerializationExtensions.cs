using Microsoft.CodeAnalysis;
using TrueMoon.Thorium.Generator.Utils;

namespace TrueMoon.Thorium.Generator.Extensions;

public static class SourceHandleSerializationExtensions
{
    internal static void GenerateSerializationForTuple(this SourceHandle handle,
        INamedTypeSymbol type,
        string propertyName,
        string instanceName = "instance.", 
        string writer = "bufferWriter")
    {
        foreach (var element in type.TupleElements)
        {
            switch (element.Type)
            {
                case IArrayTypeSymbol ar:
                {
                    var arrayItemType = ar.ElementType;
                    handle.Context.GenerateSerializationExtensions(arrayItemType);
                    handle.GenerateSerializationForItems(arrayItemType, element.MetadataName, instanceName:$"{instanceName}{propertyName}.", writer:writer);
                    break;
                }
                case INamedTypeSymbol { TypeKind: TypeKind.Class or TypeKind.Interface or TypeKind.Error, IsGenericType: true, Name: "List" or "IReadOnlyList" } p:
                {
                    var listItemType = p.TypeArguments.First();
                    handle.Context.GenerateSerializationExtensions(listItemType);
                    handle.GenerateSerializationForItems(listItemType, element.MetadataName, instanceName:$"{instanceName}{propertyName}.", isCount:true,writer:writer);
                    break;
                }
                case INamedTypeSymbol
                {
                    TypeKind: TypeKind.Class or TypeKind.Interface or TypeKind.Error, IsGenericType: true,
                    Name: "Dictionary" or "IReadOnlyDictionary"
                } p:
                {
                    var keyType = p.TypeArguments[0];
                    var valueType = p.TypeArguments[1];
                    handle.Context.GenerateSerializationExtensions(keyType);
                    handle.Context.GenerateSerializationExtensions(valueType);
                    handle.GenerateSerializationForDictionary(keyType, valueType, element.MetadataName, instanceName:$"{instanceName}{propertyName}.",writer:writer);
                    break;
                }
                case { TypeKind: TypeKind.Class or TypeKind.Struct } when WellKnownTypes.NotContains(element.Type):
                    handle.Context.GenerateSerializationExtensions(element.Type);
                    handle.AppendLine($"                    {instanceName}{propertyName}.{element.MetadataName}.Serialize({writer});");
                    break;
                default:
                {
                    var b = element.Type switch
                    {
                        {} when element.Type.Name.Equals("string", StringComparison.InvariantCultureIgnoreCase) => "String",
                        {} when element.Type.Name.Contains("Memory") => "Bytes",
                        _ => ""
                    };
                    
                    if (element.Type is INamedTypeSymbol { NullableAnnotation: NullableAnnotation.Annotated })
                    {
                        handle.AppendLine($"                    if ({instanceName}{propertyName}.{element.MetadataName} != null)");
                        handle.AppendLine("                    {");
                        handle.AppendLine($"                        SerializationUtils.WriteInstanceState(true, {writer});");
                        handle.AppendLine(element.Type.TypeKind == TypeKind.Enum
                            ? $"            SerializationUtils.Write<int>((int){instanceName}{propertyName}.{element.MetadataName}.Value, {writer});"
                            : $"                        SerializationUtils.Write{b}({instanceName}{propertyName}.{element.MetadataName}.Value, {writer});");
                        handle.AppendLine("                    }");
                        handle.AppendLine("                    else");
                        handle.AppendLine("                    {");
                        handle.AppendLine($"                        SerializationUtils.WriteInstanceState(false, {writer});");
                        handle.AppendLine("                    }");
                    }
                    else
                    {
                        handle.AppendLine(element.Type.TypeKind == TypeKind.Enum
                            ? $"            SerializationUtils.Write<int>((int){instanceName}{propertyName}.{element.MetadataName}, {writer});"
                            : $"                        SerializationUtils.Write{b}({instanceName}{propertyName}.{element.MetadataName}, {writer});");
                    }

                    break;
                }
            }
        }
    }
    
    internal static void GenerateSerializationForItems(this SourceHandle handle,
        ITypeSymbol arrayItemType, 
        string propertyName, 
        bool isCount = false, 
        string instanceName = "instance.", 
        string writer = "bufferWriter")
    {
        var sizeStr = isCount ? "Count" : "Length";
        handle.AppendLine($"            if ({instanceName}{propertyName} != null)");
        handle.AppendLine("            {");
        handle.AppendLine($"                SerializationUtils.WriteInstanceState(true, {writer});");
        handle.AppendLine($"                SerializationUtils.WriteElementsCount({instanceName}{propertyName}.{sizeStr}, {writer});");
        handle.AppendLine($"                for(var i = 0; i < {instanceName}{propertyName}.{sizeStr}; i++)");
        handle.AppendLine("                {");
        handle.AppendLine($"                    var item = {instanceName}{propertyName}[i];");
        switch (arrayItemType)
        {
            case { TypeKind: TypeKind.Class or TypeKind.Struct } when WellKnownTypes.NotContains(arrayItemType):
                handle.Context.GenerateSerializationExtensions(arrayItemType);
                handle.AppendLine($"                    item.Serialize({writer});");
                break;
            default:
            {
                var b = arrayItemType switch
                {
                    {} when arrayItemType.Name.Equals("string", StringComparison.InvariantCultureIgnoreCase) => "String",
                    {} when arrayItemType.Name.Contains("Memory") => "Bytes",
                    _ => ""
                };
                
                if (arrayItemType is INamedTypeSymbol { NullableAnnotation: NullableAnnotation.Annotated })
                {
                    handle.AppendLine($"                    if (item != null)");
                    handle.AppendLine("                    {");
                    handle.AppendLine($"                        SerializationUtils.WriteInstanceState(true, {writer});");
                    handle.AppendLine(arrayItemType.TypeKind == TypeKind.Enum
                        ? $"            SerializationUtils.Write<int>((int)item.Value, {writer});"
                        : $"                        SerializationUtils.Write{b}(item.Value, {writer});");
                    handle.AppendLine("                    }");
                    handle.AppendLine("                    else");
                    handle.AppendLine("                    {");
                    handle.AppendLine($"                        SerializationUtils.WriteInstanceState(false, {writer});");
                    handle.AppendLine("                    }");
                }
                else
                {
                    handle.AppendLine(arrayItemType.TypeKind == TypeKind.Enum
                        ? $"            SerializationUtils.Write<int>((int)item, {writer});"
                        : $"                        SerializationUtils.Write{b}(item, {writer});");
                }

                break;
            }
        }
        handle.AppendLine("                }");
        handle.AppendLine("            }");
        handle.AppendLine("            else");
        handle.AppendLine("            {");
        handle.AppendLine($"                SerializationUtils.WriteInstanceState(false, {writer});");
        handle.AppendLine("            }");
    }
    
    internal static void GenerateDeserializationForItems(this SourceHandle handle, 
        ITypeSymbol itemType, 
        string propertyName, 
        bool isList = false)
    {
        handle.GenerateDeserializationForItemsBase(itemType, propertyName, isList);
        handle.AppendLine($"            result.{propertyName} = {propertyName}Items;");
    }
    
    internal static string GenerateDeserializationForItemsBase(this SourceHandle handle, 
        ITypeSymbol itemType, 
        string propertyName, 
        bool isList = false, 
        string spanSource = "span")
    {
        var typeString = GenerationUtils.GetTypeString(itemType);
        
        handle.AppendLine(isList ? $"            List<{typeString}> {propertyName}Items = default;" : $"            {typeString}[] {propertyName}Items = default;");

        handle.AppendLine($"            if (SerializationUtils.ReadInstanceState({spanSource}[offset..], ref offset))");
        handle.AppendLine("            {");
        handle.AppendLine($"                var count = SerializationUtils.ReadElementsCount({spanSource}[offset..], ref offset);");
        handle.AppendLine(isList
            ? $"                {propertyName}Items = new List<{typeString}>(count);"
            : $"                {propertyName}Items = new {typeString}[count];");
        handle.AppendLine("                for(var i = 0; i < count; i++)");
        handle.AppendLine("                {");
        handle.AppendLine($"                    {typeString} item = default;");
        
        switch (itemType)
        {
            case { TypeKind: TypeKind.Class or TypeKind.Struct } when WellKnownTypes.NotContains(itemType):
                handle.AppendLine($"            item = item.Deserialize({spanSource}, ref offset);");
                break;
            default:
            {
                var b = itemType switch
                {
                    {} when itemType.Name.Equals("string", StringComparison.InvariantCultureIgnoreCase) => "String",
                    {} when itemType.Name.Contains("Memory") => "Bytes",
                    INamedTypeSymbol { NullableAnnotation: NullableAnnotation.Annotated } => $"<{$"{typeString}".TrimEnd('?')}>",
                    _ => $"<{typeString}>"
                };
                
                if (itemType is INamedTypeSymbol { NullableAnnotation: NullableAnnotation.Annotated })
                {
                    handle.AppendLine($"            if (SerializationUtils.ReadInstanceState({spanSource}[offset..], ref offset))");
                    handle.AppendLine("            {");
                    handle.AppendLine(itemType.TypeKind == TypeKind.Enum
                        ? $"                item = ({typeString})SerializationUtils.Read<int>({spanSource}[offset..], ref offset);"
                        : $"                item = SerializationUtils.Read{b}({spanSource}[offset..], ref offset);");
                    handle.AppendLine("            }");
                }
                else
                {
                    handle.AppendLine(itemType.TypeKind == TypeKind.Enum
                        ? $"            item = ({typeString})SerializationUtils.Read<int>({spanSource}[offset..], ref offset);"
                        : $"            item = SerializationUtils.Read{b}({spanSource}[offset..], ref offset);");
                }
                break;
            }
        }

        handle.AppendLine(isList
            ? $"                    {propertyName}Items.Add(item);"
            : $"                    {propertyName}Items[i] = item;");

        handle.AppendLine("                }");
        handle.AppendLine("            }");
        
        return $"{propertyName}Items";
    }

    internal static void GenerateSerializationForDictionary(this SourceHandle handle,
        ITypeSymbol keyType, 
        ITypeSymbol valueType, 
        string propertyName, 
        string instanceName = "instance.", 
        string writer = "bufferWriter")
    {
        handle.AppendLine($"            if ({instanceName}{propertyName} != null)");
        handle.AppendLine("            {");
        handle.AppendLine($"                SerializationUtils.WriteInstanceState(true, {writer});");
        handle.AppendLine($"                SerializationUtils.WriteElementsCount({instanceName}{propertyName}.Count, {writer});");
        handle.AppendLine($"                foreach (var pair in {instanceName}{propertyName})");
        handle.AppendLine("                {");
        handle.AppendLine("                    var key = pair.Key;");
        handle.AppendLine("                    var value = pair.Value;");
        switch (keyType)
        {
            case { TypeKind: TypeKind.Class or TypeKind.Struct } when WellKnownTypes.NotContains(keyType):
                handle.Context.GenerateSerializationExtensions(keyType);
                handle.AppendLine($"                    key.Serialize({writer});");
                break;
            default:
            {
                var b = keyType switch
                {
                    {} when keyType.Name.Equals("string", StringComparison.InvariantCultureIgnoreCase) => "String",
                    {} when keyType.Name.Contains("Memory") => "Bytes",
                    _ => ""
                };
                
                if (keyType is INamedTypeSymbol { NullableAnnotation: NullableAnnotation.Annotated })
                {
                    handle.AppendLine($"                    if (key != null)");
                    handle.AppendLine("                    {");
                    handle.AppendLine($"                        SerializationUtils.WriteInstanceState(true, {writer});");
                    handle.AppendLine(keyType.TypeKind == TypeKind.Enum
                        ? $"            SerializationUtils.Write<int>((int)key.Value, {writer});"
                        : $"                        SerializationUtils.Write{b}(key.Value, {writer});");
                    handle.AppendLine("                    }");
                    handle.AppendLine("                    else");
                    handle.AppendLine("                    {");
                    handle.AppendLine($"                        SerializationUtils.WriteInstanceState(false, {writer});");
                    handle.AppendLine("                    }");
                }
                else
                {
                    handle.AppendLine(keyType.TypeKind == TypeKind.Enum
                        ? $"            SerializationUtils.Write<int>((int)key, {writer});"
                        : $"                        SerializationUtils.Write{b}(key, {writer});");
                }

                break;
            }
        }
        switch (valueType)
        {
            case { TypeKind: TypeKind.Class or TypeKind.Struct } when WellKnownTypes.NotContains(valueType):
                handle.Context.GenerateSerializationExtensions(valueType);
                handle.AppendLine($"                    value.Serialize({writer});");
                break;
            default:
            {
                var b = valueType switch
                {
                    {} when valueType.Name.Equals("string", StringComparison.InvariantCultureIgnoreCase) => "String",
                    {} when valueType.Name.Contains("Memory") => "Bytes",
                    _ => ""
                };
                
                if (valueType is INamedTypeSymbol { NullableAnnotation: NullableAnnotation.Annotated })
                {
                    handle.AppendLine($"                    if (value != null)");
                    handle.AppendLine("                    {");
                    handle.AppendLine($"                        SerializationUtils.WriteInstanceState(true, {writer});");
                    handle.AppendLine(valueType.TypeKind == TypeKind.Enum
                        ? $"            SerializationUtils.Write<int>((int)value.Value, {writer});"
                        : $"                        SerializationUtils.Write{b}(value.Value, {writer});");
                    handle.AppendLine("                    }");
                    handle.AppendLine("                    else");
                    handle.AppendLine("                    {");
                    handle.AppendLine($"                        SerializationUtils.WriteInstanceState(false, {writer});");
                    handle.AppendLine("                    }");
                }
                else
                {
                    handle.AppendLine(valueType.TypeKind == TypeKind.Enum
                        ? $"            SerializationUtils.Write<int>((int)value, {writer});"
                        : $"                        SerializationUtils.Write{b}(value, {writer});");
                }

                break;
            }
        }
        handle.AppendLine("                }");
        handle.AppendLine("            }");
        handle.AppendLine("            else");
        handle.AppendLine("            {");
        handle.AppendLine($"                SerializationUtils.WriteInstanceState(false, {writer});");
        handle.AppendLine("            }");
    }
    
    internal static string GenerateDeserializationForDictionaryBase(this SourceHandle handle, 
        ITypeSymbol keyType, 
        ITypeSymbol valueType, 
        string propertyName, 
        string spanSource = "span")
    {
        var keyTypeString = GenerationUtils.GetTypeString(keyType);
        var valueTypeString = GenerationUtils.GetTypeString(valueType);
        
        handle.AppendLine($"            Dictionary<{keyTypeString},{valueTypeString}> {propertyName}Dictionary = default;");

        handle.AppendLine($"            if (SerializationUtils.ReadInstanceState({spanSource}[offset..], ref offset))");
        handle.AppendLine("            {");
        handle.AppendLine($"                var count = SerializationUtils.ReadElementsCount({spanSource}[offset..], ref offset);");
        handle.AppendLine($"                {propertyName}Dictionary = new Dictionary<{keyTypeString},{valueTypeString}>(count);");
        handle.AppendLine("                for(var i = 0; i < count; i++)");
        handle.AppendLine("                {");
        handle.AppendLine($"                    {keyTypeString} key = default;");
        handle.AppendLine($"                    {valueTypeString} value = default;");
        
        switch (keyType)
        {
            case { TypeKind: TypeKind.Class or TypeKind.Struct } when WellKnownTypes.NotContains(keyType):
                handle.AppendLine($"            key = key.Deserialize({spanSource}, ref offset);");
                break;
            default:
            {
                var b = keyType switch
                {
                    {} when keyType.Name.Equals("string", StringComparison.InvariantCultureIgnoreCase) => "String",
                    {} when keyType.Name.Contains("Memory") => "Bytes",
                    INamedTypeSymbol { NullableAnnotation: NullableAnnotation.Annotated } => $"<{$"{keyTypeString}".TrimEnd('?')}>",
                    _ => $"<{keyTypeString}>"
                };
                
                if (keyType is INamedTypeSymbol { NullableAnnotation: NullableAnnotation.Annotated })
                {
                    handle.AppendLine($"            if (SerializationUtils.ReadInstanceState({spanSource}[offset..], ref offset))");
                    handle.AppendLine("            {");
                    handle.AppendLine(keyType.TypeKind == TypeKind.Enum
                        ? $"                key = ({keyTypeString})SerializationUtils.Read<int>({spanSource}[offset..], ref offset);"
                        : $"                key = SerializationUtils.Read{b}({spanSource}[offset..], ref offset);");
                    handle.AppendLine("            }");
                }
                else
                {
                    handle.AppendLine(keyType.TypeKind == TypeKind.Enum
                        ? $"            key = ({keyTypeString})SerializationUtils.Read<int>({spanSource}[offset..], ref offset);"
                        : $"            key = SerializationUtils.Read{b}({spanSource}[offset..], ref offset);");
                }
                break;
            }
        }
        
        switch (valueType)
        {
            case { TypeKind: TypeKind.Class or TypeKind.Struct } when WellKnownTypes.NotContains(valueType):
                handle.AppendLine($"            value = value.Deserialize({spanSource}, ref offset);");
                break;
            default:
            {
                var b = valueType switch
                {
                    {} when valueType.Name.Equals("string", StringComparison.InvariantCultureIgnoreCase) => "String",
                    {} when valueType.Name.Contains("Memory") => "Bytes",
                    INamedTypeSymbol { NullableAnnotation: NullableAnnotation.Annotated } => $"<{$"{valueTypeString}".TrimEnd('?')}>",
                    _ => $"<{valueTypeString}>"
                };
                
                if (valueType is INamedTypeSymbol { NullableAnnotation: NullableAnnotation.Annotated })
                {
                    handle.AppendLine($"            if (SerializationUtils.ReadInstanceState({spanSource}[offset..], ref offset))");
                    handle.AppendLine("            {");
                    handle.AppendLine(valueType.TypeKind == TypeKind.Enum
                        ? $"                value = ({valueTypeString})SerializationUtils.Read<int>({spanSource}[offset..], ref offset);"
                        : $"                value = SerializationUtils.Read{b}({spanSource}[offset..], ref offset);");
                    handle.AppendLine("            }");
                }
                else
                {
                    handle.AppendLine(valueType.TypeKind == TypeKind.Enum
                        ? $"            value = ({valueTypeString})SerializationUtils.Read<int>({spanSource}[offset..], ref offset);"
                        : $"            value = SerializationUtils.Read{b}({spanSource}[offset..], ref offset);");
                }
                break;
            }
        }
        
        handle.AppendLine($"                    {propertyName}Dictionary.Add(key, value);");
        handle.AppendLine("                }");
        handle.AppendLine("            }");
        
        return $"{propertyName}Dictionary";
    }
    
    internal static void GenerateDeserializationForDictionary(this SourceHandle handle, 
        ITypeSymbol keyType, 
        ITypeSymbol valueType, 
        string propertyName, 
        string spanSource = "span")
    {
        handle.GenerateDeserializationForDictionaryBase(keyType, valueType, propertyName, spanSource);
        handle.AppendLine($"            result.{propertyName} = {propertyName}Dictionary;");
    }
    
    internal static string GenerateDeserializationForTuple(this SourceHandle handle, 
        INamedTypeSymbol tupleType, 
        string propertyName, 
        string spanSource = "span")
    {
        var itemsStr = "";
        foreach (var element in tupleType.TupleElements)
        {
            var itemType = element.Type;
            var itemName = $"{propertyName}{element.MetadataName}";
            var typeString = GenerationUtils.GetTypeString(itemType);
            switch (itemType)
            {
                case IArrayTypeSymbol ar:
                {
                    var arrayItemType = ar.ElementType;
                    var s = handle.GenerateDeserializationForItemsBase(arrayItemType, itemName, spanSource:spanSource);
                    handle.AppendLine($"            {typeString} {itemName} = {s};");
                    break;
                }
                case INamedTypeSymbol { TypeKind: TypeKind.Class or TypeKind.Interface or TypeKind.Error, IsGenericType: true, Name: "List" or "IReadOnlyList" } p:
                {
                    var listItemType = p.TypeArguments.First();
                    var s = handle.GenerateDeserializationForItemsBase(listItemType, itemName, true,spanSource:spanSource);
                    handle.AppendLine($"            {typeString} {itemName} = {s};");
                    break;
                }
                case INamedTypeSymbol
                {
                    TypeKind: TypeKind.Class or TypeKind.Interface or TypeKind.Error, IsGenericType: true,
                    Name: "Dictionary" or "IReadOnlyDictionary"
                } p:
                {
                    var keyType = p.TypeArguments[0];
                    var valueType = p.TypeArguments[1];
                    var s = handle.GenerateDeserializationForDictionaryBase(keyType, valueType, itemName,spanSource:spanSource);
                    handle.AppendLine($"            {typeString} {itemName} = {s};");
                    break;
                }
                case { TypeKind: TypeKind.Class or TypeKind.Struct } when WellKnownTypes.IsObject(itemType):
                    // TODO object
                    handle.AppendLine($"            var {itemName} = new object();");
                    break;
                case { TypeKind: TypeKind.Class or TypeKind.Struct } when WellKnownTypes.NotContains(itemType):
                    handle.AppendLine($"            {typeString} {itemName} = default;");
                    handle.AppendLine($"            {itemName} = {itemName}.Deserialize({spanSource}[offset..], ref offset);");
                    break;
                default:
                {
                    var b = itemType switch
                    {
                        {} when itemType.Name.Equals("string", StringComparison.InvariantCultureIgnoreCase) => "String",
                        {} when itemType.Name.Contains("Memory") => "Bytes",
                        INamedTypeSymbol { NullableAnnotation: NullableAnnotation.Annotated } => $"<{$"{typeString}".TrimEnd('?')}>",
                        _ => $"<{typeString}>"
                    };
                
                    if (itemType is INamedTypeSymbol { NullableAnnotation: NullableAnnotation.Annotated })
                    {
                        handle.AppendLine($"            {typeString} {itemName} = default;");
                        handle.AppendLine($"            if (SerializationUtils.ReadInstanceState({spanSource}[offset..], ref offset))");
                        handle.AppendLine("            {");
                        handle.AppendLine(itemType.TypeKind == TypeKind.Enum
                            ? $"                {itemName} = ({typeString})SerializationUtils.Read<int>({spanSource}[offset..], ref offset);"
                            : $"                {itemName} = SerializationUtils.Read{b}({spanSource}[offset..], ref offset);");
                        handle.AppendLine("            }");
                    }
                    else
                    {
                        handle.AppendLine(itemType.TypeKind == TypeKind.Enum
                            ? $"            {typeString} {itemName} = ({typeString})SerializationUtils.Read<int>({spanSource}[offset..], ref offset);"
                            : $"            {typeString} {itemName} = SerializationUtils.Read{b}({spanSource}[offset..], ref offset);");
                    }
                    break;
                }
            }

            itemsStr += $"{itemName},";
        }

        var resultName = $"{propertyName}Tuple";
        handle.AppendLine($"{GenerationUtils.GetTypeString(tupleType)} {resultName} = ({itemsStr.TrimEnd(',')});");
        
        return resultName;
    }
    
    internal static string ProcessRecordMemberTypeDeserialization(this SourceHandle handle, 
        string name, 
        ITypeSymbol typeSymbol)
    {
        switch (typeSymbol)
        {
            case IArrayTypeSymbol ar:
            {
                var arrayItemType = ar.ElementType;
                return handle.GenerateDeserializationForItemsBase(arrayItemType, name);
            }
            case INamedTypeSymbol { TypeKind: TypeKind.Class or TypeKind.Interface or TypeKind.Error, IsGenericType: true, Name: "List" or "IReadOnlyList" } p:
            {
                var listItemType = p.TypeArguments.First();
                return handle.GenerateDeserializationForItemsBase(listItemType, name, true);
            }
            case INamedTypeSymbol
            {
                TypeKind: TypeKind.Class or TypeKind.Interface or TypeKind.Error, IsGenericType: true,
                Name: "Dictionary" or "IReadOnlyDictionary"
            } p:
            {
                var keyType = p.TypeArguments[0];
                var valueType = p.TypeArguments[1];
                return handle.GenerateDeserializationForDictionaryBase(keyType, valueType, name);
            }
            case { TypeKind: TypeKind.Class or TypeKind.Struct } when WellKnownTypes.IsObject(typeSymbol):
                // TODO object
                handle.AppendLine($"            var {name} = new object();");
                return name;
            case { TypeKind: TypeKind.Class or TypeKind.Struct } when WellKnownTypes.NotContains(typeSymbol):
                handle.AppendLine($"            var {name} = result.{name}.Deserialize(span[offset..], ref offset);");
                return name;
            default:
            {
                var typeString = GenerationUtils.GetTypeString(typeSymbol);
                var b = typeSymbol switch
                {
                    {} when typeSymbol.Name.Equals("string", StringComparison.InvariantCultureIgnoreCase) => "String",
                    {} when typeSymbol.Name.Contains("Memory") => "Bytes",
                    INamedTypeSymbol { NullableAnnotation: NullableAnnotation.Annotated } => $"<{$"{typeString}".TrimEnd('?')}>",
                    _ => $"<{typeString}>"
                };
                
                if (typeSymbol is INamedTypeSymbol { NullableAnnotation: NullableAnnotation.Annotated })
                {
                    handle.AppendLine($"            {typeString} {name} = default;");
                    handle.AppendLine("            if (SerializationUtils.ReadInstanceState(span[offset..], ref offset))");
                    handle.AppendLine("            {");

                    handle.AppendLine(typeSymbol.TypeKind == TypeKind.Enum
                        ? $"                {name} = ({typeString})SerializationUtils.Read<int>(span[offset..], ref offset);"
                        : $"                {name} = SerializationUtils.Read{b}(span[offset..], ref offset);");

                    handle.AppendLine("            }");
                }
                else
                {
                    handle.AppendLine(typeSymbol.TypeKind == TypeKind.Enum
                        ? $"                var {name} = ({typeString})SerializationUtils.Read<int>(span[offset..], ref offset);"
                        : $"                var {name} = SerializationUtils.Read{b}(span[offset..], ref offset);");
                }

                return name;
            }
        }
    }
}