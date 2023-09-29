using Microsoft.CodeAnalysis;
using TrueMoon.Thorium.Generator.Utils;

namespace TrueMoon.Thorium.Generator.Extensions;

public static class SourceHandleExtensions
{
    internal static void GenerateMethod(this SourceHandle handle,
        IMethodSymbol methodSymbol, 
        byte methodIndex)
    {
        var parameters = methodSymbol.Parameters;
        var anyParameters = parameters.Any();
        
        var str = "";
        string? cancellationTokenParameter = default;

        if (anyParameters)
        {
            foreach (var symbol in parameters)
            {
                if (symbol.Type is { TypeKind: TypeKind.Class or TypeKind.Struct, IsTupleType:false })
                {
                    handle.Context.GenerateSerializationExtensions(symbol.Type);
                }

                str += $"{GenerationUtils.GetTypeString(symbol.Type)} {symbol.MetadataName}{(symbol.IsOptional ? " = default" : string.Empty)}, ";

                if (symbol.Type.Name.Contains("CancellationToken"))
                {
                    cancellationTokenParameter = symbol.MetadataName;
                }
            }
        }

        str = str.TrimEnd();
        str = str.TrimEnd(',');

        var (returnType, isVoid, isTask, returnTypeSymbol) = methodSymbol.GetReturnDetails();

        var methodRetStr = isVoid 
            ? $"{methodSymbol.ReturnType}"
            : (isTask 
                ? (returnType is null 
                    ? "global::System.Threading.Tasks.Task"
                    : $"global::System.Threading.Tasks.Task<{GenerationUtils.GetTypeString(returnTypeSymbol)}>")
                : $"{GenerationUtils.GetTypeString(methodSymbol.ReturnType)}");
        handle.AppendLine($"    public {methodRetStr} {methodSymbol.Name}({str})");
        handle.AppendLine("    {");
        if (isTask)
        {
            handle.AppendLine($"        return _invocationClient.InvokeAsync({methodIndex}");
        }
        else
        {
            handle.AppendLine(isVoid
                ? $"        _invocationClient.Invoke({methodIndex}"
                : $"        return _invocationClient.Invoke({methodIndex}");
        }
        if (parameters.Length == 0 || (parameters.Length == 1 && cancellationTokenParameter != null))
        {
            handle.AppendLine("            , null");
        }
        else
        {
            handle.AppendLine("            , writer =>");
            handle.AppendLine("            {");
            foreach (var parameterSymbol in parameters.Where(parameterSymbol => !WellKnownTypes.IsCancellationToken(parameterSymbol.Type)))
            {
                switch (parameterSymbol.Type)
                {
                    case IArrayTypeSymbol ar:
                    {
                        var arrayItemType = ar.ElementType;
                        handle.GenerateSerializationForItems(arrayItemType, parameterSymbol.MetadataName, instanceName:"", writer:"writer");
                        break;
                    }
                    case INamedTypeSymbol { IsTupleType:true, TupleElements.IsEmpty:false } tupleSymbol:
                    {
                        handle.GenerateSerializationForTuple(tupleSymbol, parameterSymbol.MetadataName, instanceName:"", writer:"writer");
                        break;
                    }
                    case INamedTypeSymbol
                    {
                        TypeKind: TypeKind.Class or TypeKind.Interface or TypeKind.Error, IsGenericType: true,
                        Name: "List" or "IReadOnlyList"
                    } p:
                    {
                        var listItemType = p.TypeArguments.First();
                        handle.GenerateSerializationForItems(listItemType, parameterSymbol.MetadataName, isCount:true, instanceName:"", writer:"writer");
                        break;
                    }
                    case INamedTypeSymbol
                    {
                        TypeKind: TypeKind.Class or TypeKind.Interface or TypeKind.Error, IsGenericType: true,
                        Name: "Dictionary" or "IReadOnlyDictionary"
                    } pv:
                    {
                        var keyType = pv.TypeArguments[0];
                        var valueType = pv.TypeArguments[1];
                        handle.GenerateSerializationForDictionary(keyType, valueType, parameterSymbol.MetadataName, instanceName:"", writer:"writer");
                        break;
                    }
                    case { TypeKind: TypeKind.Class or TypeKind.Struct }
                        when WellKnownTypes.NotContains(parameterSymbol.Type):
                    {
                        handle.AppendLine($"                {parameterSymbol.MetadataName}.Serialize(writer);");
                        break;
                    }
                    default:
                    {
                        var b = parameterSymbol.Type switch
                        {
                            {} when parameterSymbol.Type.Name.Equals("string", StringComparison.InvariantCultureIgnoreCase) => "String",
                            {} when parameterSymbol.Type.Name.Contains("Memory") => "Bytes",
                            _ => ""
                        };
                
                        if (parameterSymbol.Type is INamedTypeSymbol { NullableAnnotation: NullableAnnotation.Annotated })
                        {
                            handle.AppendLine($"            if ({parameterSymbol.MetadataName} != null)");
                            handle.AppendLine("            {");
                            handle.AppendLine("                SerializationUtils.WriteInstanceState(true, writer);");
                            handle.AppendLine(parameterSymbol.Type.TypeKind == TypeKind.Enum
                                ? $"               SerializationUtils.Write<int>((int){parameterSymbol.MetadataName}.Value, writer);"
                                : $"               SerializationUtils.Write{b}({parameterSymbol.MetadataName}.Value, writer);");
                            handle.AppendLine("            }");
                            handle.AppendLine("            else");
                            handle.AppendLine("            {");
                            handle.AppendLine("                SerializationUtils.WriteInstanceState(false, writer);");
                            handle.AppendLine("            }");
                        }
                        else
                        {
                            handle.AppendLine(parameterSymbol.Type.TypeKind == TypeKind.Enum
                                ? $"           SerializationUtils.Write<int>((int){parameterSymbol.MetadataName}, writer);"
                                : $"           SerializationUtils.Write{b}({parameterSymbol.MetadataName}, writer);");
                        }
                        break;
                    }
                }
            }
            handle.AppendLine("            }");
        }

        if (!isVoid && returnType is not null)
        {
            handle.AppendLine("            , handle =>");
            handle.AppendLine("            {");
            handle.AppendLine("                var offset = 0;");
            handle.AppendLine($"                {GenerationUtils.GetTypeString(returnTypeSymbol)} result = default;");
            
            
            var isAnnotated = returnTypeSymbol.NullableAnnotation == NullableAnnotation.Annotated;
            
            if (returnTypeSymbol is INamedTypeSymbol {MetadataName:"Nullable`1", TypeArguments.Length:1} tp && isAnnotated)
            {
                returnTypeSymbol = tp.TypeArguments[0];
            }
            
            switch (returnTypeSymbol)
            {
                case IArrayTypeSymbol ar:
                {
                    var arrayItemType = ar.ElementType;
                    handle.Context.GenerateSerializationExtensions(arrayItemType);
                    var r = handle.GenerateDeserializationForItemsBase(arrayItemType, "result", spanSource:"handle.Span");
                    handle.AppendLine($"            result = {r};");
                    handle.AppendLine("            return result;");
                    break;
                }
                case INamedTypeSymbol { IsTupleType:true, TupleElements.IsEmpty:false } tupleSymbol:
                {
                    var r = handle.GenerateDeserializationForTuple(tupleSymbol, "result", spanSource:"handle.Span");
                    handle.AppendLine($"            result = {r};");
                    handle.AppendLine("            return result;");
                    break;
                }
                case INamedTypeSymbol
                {
                    TypeKind: TypeKind.Class or TypeKind.Interface or TypeKind.Error, IsGenericType: true, Name: "List" or "IReadOnlyList"
                } p:
                {
                    var listItemType = p.TypeArguments.First();
                    handle.Context.GenerateSerializationExtensions(listItemType);
                    var r = handle.GenerateDeserializationForItemsBase(listItemType, "result", true, spanSource:"handle.Span");
                    handle.AppendLine($"            result = {r};");
                    handle.AppendLine("            return result;");
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
                    var r = handle.GenerateDeserializationForDictionaryBase(keyType, valueType, "result", spanSource:"handle.Span");
                    handle.AppendLine($"            result = {r};");
                    handle.AppendLine("            return result;");
                    break;
                }
                case {TypeKind:TypeKind.Class or TypeKind.Struct} 
                    when WellKnownTypes.NotContains(returnType.TrimEnd('?')):
                    handle.Context.GenerateSerializationExtensions(returnTypeSymbol);
                    handle.AppendLine("                return result.Deserialize(handle.Span[offset..], ref offset);");
                    break;
                default:
                {
                    var b = returnTypeSymbol switch
                    {
                        {} when returnTypeSymbol.Name.Equals("string", StringComparison.InvariantCultureIgnoreCase) => "String",
                        {} when returnTypeSymbol.Name.Contains("Memory") => "Bytes",
                        INamedTypeSymbol { NullableAnnotation: NullableAnnotation.Annotated } => $"<{$"{GenerationUtils.GetTypeString(returnTypeSymbol)}".TrimEnd('?')}>",
                        _ => $"<{GenerationUtils.GetTypeString(returnTypeSymbol)}>"
                    };
                
                    if (isAnnotated)
                    {
                        handle.AppendLine("            if (SerializationUtils.ReadInstanceState(handle.Span[offset..], ref offset))");
                        handle.AppendLine("            {");
                        handle.AppendLine(returnTypeSymbol.TypeKind == TypeKind.Enum
                            ? $"                result = ({GenerationUtils.GetTypeString(returnTypeSymbol)})SerializationUtils.Read<int>(handle.Span[offset..], ref offset);"
                            : $"                result = SerializationUtils.Read{b}(handle.Span[offset..], ref offset);");
                        handle.AppendLine("            }");
                    }
                    else
                    {
                        handle.AppendLine(returnTypeSymbol.TypeKind == TypeKind.Enum
                            ? $"            result = ({GenerationUtils.GetTypeString(returnTypeSymbol)})SerializationUtils.Read<int>(handle.Span[offset..], ref offset);"
                            : $"            result = SerializationUtils.Read{b}(handle.Span[offset..], ref offset);");
                    }
                
                    handle.AppendLine("                return result;");
                    break;
                }
            }
            handle.AppendLine("            }");
        }
        
        handle.AppendLine(cancellationTokenParameter != null
            ? $"            , {cancellationTokenParameter});"
            : "            );");
        handle.AppendLine("    }");

        handle.AppendLine();
    }
    
    internal static void ProcessMemberTypeSerialization(this SourceHandle handle, string name, ITypeSymbol typeSymbol)
    {
        switch (typeSymbol)
        {
            case IArrayTypeSymbol ar:
            {
                var arrayItemType = ar.ElementType;
                handle.GenerateSerializationForItems(arrayItemType, name);
                break;
            }
            case INamedTypeSymbol
            {
                TypeKind: TypeKind.Class or TypeKind.Interface or TypeKind.Error, IsGenericType: true,
                Name: "List" or "IReadOnlyList"
            } p:
            {
                var listItemType = p.TypeArguments.First();
                handle.GenerateSerializationForItems(listItemType, name, true);
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
                handle.GenerateSerializationForDictionary(keyType, valueType, name);
                break;
            }
            case { TypeKind: TypeKind.Class or TypeKind.Struct } when WellKnownTypes.IsObject(typeSymbol):
                // TODO object
                //sb.AppendLine($"            instance.{name}.Serialize(bufferWriter);");
                break;
            case { TypeKind: TypeKind.Class or TypeKind.Struct } when WellKnownTypes.NotContains(typeSymbol):
                handle.Context.GenerateSerializationExtensions(typeSymbol);
                handle.AppendLine($"            instance.{name}.Serialize(bufferWriter);");
                break;
            default:
            {
                var b = typeSymbol switch
                {
                    {} when typeSymbol.Name.Equals("string", StringComparison.InvariantCultureIgnoreCase) => "String",
                    {} when typeSymbol.Name.Contains("Memory") => "Bytes",
                    _ => ""
                };
                
                if (typeSymbol is INamedTypeSymbol { NullableAnnotation: NullableAnnotation.Annotated })
                {
                    handle.AppendLine($"            if (instance.{name} != null)");
                    handle.AppendLine("            {");
                    handle.AppendLine("               SerializationUtils.WriteInstanceState(true, bufferWriter);");
                    if (WellKnownTypes.IsString(typeSymbol))
                    {
                        handle.AppendLine($"               SerializationUtils.Write{b}(instance.{name}, bufferWriter);");
                    }
                    else
                    {
                        handle.AppendLine(typeSymbol.TypeKind == TypeKind.Enum 
                            ? $"               SerializationUtils.Write<int>((int)instance.{name}.Value, bufferWriter);" 
                            : $"               SerializationUtils.Write{b}(instance.{name}.Value, bufferWriter);");
                    }
                    handle.AppendLine("            }");
                    handle.AppendLine("            else");
                    handle.AppendLine("            {");
                    handle.AppendLine("                SerializationUtils.WriteInstanceState(false, bufferWriter);");
                    handle.AppendLine("            }");
                }
                else
                {
                    handle.AppendLine(typeSymbol.TypeKind == TypeKind.Enum 
                        ? $"            SerializationUtils.Write<int>((int)instance.{name}, bufferWriter);" 
                        : $"            SerializationUtils.Write{b}(instance.{name}, bufferWriter);");
                }

                break;
            }
        }
    }

    internal static void ProcessMemberTypeDeserialization(this SourceHandle handle, string name, ITypeSymbol typeSymbol)
    {
        switch (typeSymbol)
        {
            case IArrayTypeSymbol ar:
            {
                var arrayItemType = ar.ElementType;
                handle.GenerateDeserializationForItems(arrayItemType, name);
                break;
            }
            case INamedTypeSymbol { TypeKind: TypeKind.Class or TypeKind.Interface or TypeKind.Error, IsGenericType: true, Name: "List" or "IReadOnlyList" } p:
            {
                var listItemType = p.TypeArguments.First();
                handle.GenerateDeserializationForItems(listItemType, name, true);
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
                handle.GenerateDeserializationForDictionary(keyType, valueType, name);
                break;
            }
            case { TypeKind: TypeKind.Class or TypeKind.Struct } when WellKnownTypes.IsObject(typeSymbol):
                // TODO object
                //sb.AppendLine($"            result.{name} = new object();");
                break;
            case { TypeKind: TypeKind.Class or TypeKind.Struct } when WellKnownTypes.NotContains(typeSymbol):
                handle.AppendLine($"            result.{name} = result.{name}.Deserialize(span, ref offset);");
                break;
            default:
            {
                var b = typeSymbol switch
                {
                    {} when typeSymbol.Name.Equals("string", StringComparison.InvariantCultureIgnoreCase) => "String",
                    {} when typeSymbol.Name.Contains("Memory") => "Bytes",
                    INamedTypeSymbol { NullableAnnotation: NullableAnnotation.Annotated } => $"<{$"{GenerationUtils.GetTypeString(typeSymbol)}".TrimEnd('?')}>",
                    _ => $"<{GenerationUtils.GetTypeString(typeSymbol)}>"
                };
                
                if (typeSymbol is INamedTypeSymbol { NullableAnnotation: NullableAnnotation.Annotated })
                {
                    handle.AppendLine("            if (SerializationUtils.ReadInstanceState(span[offset..], ref offset))");
                    handle.AppendLine("            {");
                    handle.AppendLine(typeSymbol.TypeKind == TypeKind.Enum
                        ? $"                result.{name} = ({GenerationUtils.GetTypeString(typeSymbol)})SerializationUtils.Read<int>(span[offset..], ref offset);"
                        : $"                result.{name} = SerializationUtils.Read{b}(span[offset..], ref offset);");
                    handle.AppendLine("            }");
                }
                else
                {
                    handle.AppendLine(typeSymbol.TypeKind == TypeKind.Enum
                        ? $"            result.{name} = ({GenerationUtils.GetTypeString(typeSymbol)})SerializationUtils.Read<int>(span[offset..], ref offset);"
                        : $"            result.{name} = SerializationUtils.Read{b}(span[offset..], ref offset);");
                }

                break;
            }
        }
    }
    
    public static void BeginServiceGeneratedImplementation(this SourceHandle handle)
    {
        var fullname = handle.Symbol?.ToDisplayString();

        var implementationClassName = handle.ImplementationClassName;
        
        handle.AppendLine("// auto-generated by TrueMoon.Thorium.Generator");
        handle.AppendLine("using System;");
        handle.AppendLine("using System.Threading.Tasks;");
        handle.AppendLine("using TrueMoon.Thorium.IO;");
        handle.AppendLine($"using {handle.Context.NamespacePrefix};");
        handle.AppendLine();
        handle.AppendLine($"namespace {handle.Context.NamespacePrefix};");
        handle.AppendLine();

        handle.AppendLine($"// {fullname}");
        handle.AppendLine($"public sealed class {implementationClassName} : global::{fullname}");
        handle.AppendLine("{");
        handle.AppendLine($"    private readonly IInvocationClient<global::{fullname}> _invocationClient;");
        handle.AppendLine($"    public {implementationClassName}(IInvocationClient<global::{fullname}> invocationClient)");
        handle.AppendLine("    {");
        handle.AppendLine("        _invocationClient = invocationClient;");
        handle.AppendLine("    }");
        handle.AppendLine();
    }
    
    public static void BeginSerializer(this SourceHandle handle)
    {
        handle.AppendLine("// auto-generated by TrueMoon.Thorium.Generator");
        handle.AppendLine("using System.Buffers;");
        handle.AppendLine("using System.Collections.Generic;");
        handle.AppendLine("using TrueMoon.Thorium;");
        handle.AppendLine("using TrueMoon.Thorium.IO;");
        handle.AppendLine("using System.Runtime.CompilerServices;");
        handle.AppendLine($"using {handle.Context.NamespacePrefix};");
        handle.AppendLine();
        handle.AppendLine($"namespace {handle.Context.NamespacePrefix};");
        handle.AppendLine();

        var name = handle.Symbol.Name;
        
        handle.AppendLine($"// {handle.Symbol},  {name}");
        handle.AppendLine($"public static class {name}SerializationExtensions");
        handle.AppendLine("{");

        handle.AppendLine("    [MethodImpl(MethodImplOptions.AggressiveInlining)]");

        var typeStr = handle.Symbol.NullableAnnotation == NullableAnnotation.Annotated
            ? $"{handle.Symbol}"
            : $"{handle.Symbol}?";
        
        handle.AppendLine($"    public static void Serialize(this global::{typeStr} instance, IBufferWriter<byte> bufferWriter)");
        handle.AppendLine("    {");
        handle.AppendLine("        if (instance != null)"); 
        handle.AppendLine("        {");
        handle.AppendLine("            SerializationUtils.WriteInstanceState(true, bufferWriter);");
    }
    
    public static void BeginServiceHandler(this SourceHandle handle)
    {
        var name = handle.Symbol?.Name;
        var fullname = handle.Symbol?.ToDisplayString();

        var handlerImplementationClassName = (name.StartsWith("I") ? name.Substring(1) : name) + "InvocationServerHandler";
        
        handle.AppendLine("// auto-generated by TrueMoon.Thorium.Generator");
        handle.AppendLine("using System;");
        handle.AppendLine("using System.Buffers;");
        handle.AppendLine("using TrueMoon.Diagnostics;");
        handle.AppendLine("using System.Threading.Tasks;");
        handle.AppendLine("using TrueMoon.Thorium.IO;");
        
        handle.AppendLine();
        handle.AppendLine($"namespace {handle.Context.NamespacePrefix};");
        handle.AppendLine();

        handle.AppendLine($"// {fullname}");
        handle.AppendLine($"public sealed class {handlerImplementationClassName} : IInvocationServerHandler<global::{fullname}>");
        handle.AppendLine("{");
        handle.AppendLine($"    private readonly global::{fullname} _service;");
        handle.AppendLine($"    private readonly IEventsSource<{handlerImplementationClassName}> _eventsSource;");
        handle.AppendLine($"    public {handlerImplementationClassName}(global::{fullname} service, IEventsSource<{handlerImplementationClassName}> eventsSource)");
        handle.AppendLine("    {");
        handle.AppendLine("        _service = service;");
        handle.AppendLine("        _eventsSource = eventsSource;");
        handle.AppendLine("    }");
        handle.AppendLine();
        handle.AppendLine("    public async Task<(bool, Exception?)> HandleAsync(byte method, ReadOnlyMemory<byte> readHandle, IBufferWriter<byte> bufferWriter, CancellationToken cancellationToken = default)");
        handle.AppendLine("    {");
        handle.AppendLine("        switch (method)");
        handle.AppendLine("        {");
    }

    public static void StructSerializer(this SourceHandle handle)
    {
        handle.AppendLine("// auto-generated by TrueMoon.Thorium.Generator");
        handle.AppendLine("using System.Buffers;");
        handle.AppendLine("using System.Collections.Generic;");
        handle.AppendLine("using TrueMoon.Thorium;");
        handle.AppendLine("using TrueMoon.Thorium.IO;");
        handle.AppendLine("using System.Runtime.CompilerServices;");
        handle.AppendLine($"using {handle.Context.NamespacePrefix};");
        handle.AppendLine();
        handle.AppendLine($"namespace {handle.Context.NamespacePrefix};");
        handle.AppendLine();

        var name = handle.Symbol.Name;
        
        handle.AppendLine($"// {handle.Symbol},  {name}");
        handle.AppendLine($"public static class {name}SerializationExtensions");
        handle.AppendLine("{");

        handle.AppendLine("    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        
        handle.AppendLine($"    public static void Serialize(this {GenerationUtils.GetTypeString(handle.Symbol)} instance, IBufferWriter<byte> bufferWriter)");
        handle.AppendLine("    {");
        handle.AppendLine("            SerializationUtils.Write(instance, bufferWriter);"); 
        handle.AppendLine("    }");
        handle.AppendLine();
        handle.AppendLine("    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        handle.AppendLine($"    public static {GenerationUtils.GetTypeString(handle.Symbol)} Deserialize(this {GenerationUtils.GetTypeString(handle.Symbol)} instance, ReadOnlySpan<byte> span, ref int offset)");
        handle.AppendLine("    {");
        handle.AppendLine($"        return SerializationUtils.Read<{GenerationUtils.GetTypeString(handle.Symbol)}>(span[offset..], ref offset);");
        handle.AppendLine("    }");
        
        handle.Complete();
    }
}