using Microsoft.CodeAnalysis;
using TrueMoon.Thorium.Generator.Utils;

namespace TrueMoon.Thorium.Generator.Extensions;

public static class GenerationContextExtensions
{
    internal static void GenerateSerializationExtensions(this GenerationContext context, ITypeSymbol type)
    {
        if (WellKnownTypes.Contains(type))
        {
            return;
        }
        
        if (context.CheckType(type))
        {
            return;
        }

        var handle = context.CreateSourceHandle(type as INamedTypeSymbol, ".SerializationExtensions");
        
        if (type.TypeKind == TypeKind.Struct)
        {
            handle.StructSerializer();
            return;
        }
        
        var members = type.GetMembers();
        handle.BeginSerializer();
        var typeValue = handle.TypeValueString;
        var typeStr = $"{handle.Symbol}";
        
        foreach (var member in members)
        {
            switch (member)
            {
                case IPropertySymbol property when property.SetMethod == null || property.DeclaredAccessibility != Accessibility.Public:
                    continue;
                case IPropertySymbol property:
                    handle.ProcessMemberTypeSerialization(property.Name, property.Type);
                    break;
                case IFieldSymbol field when field.IsReadOnly || field.IsConst || field.DeclaredAccessibility != Accessibility.Public:
                    continue;
                case IFieldSymbol field:
                    handle.ProcessMemberTypeSerialization(field.Name, field.Type);
                    break;
            }
        }
        handle.AppendLine("        }");
        handle.AppendLine("        else");
        handle.AppendLine("        {");
        handle.AppendLine("            SerializationUtils.WriteInstanceState(false, bufferWriter);");
        handle.AppendLine("        }");


        handle.AppendLine("    }");
    
        handle.AppendLine();
        
        handle.AppendLine("    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        handle.AppendLine($"    public static global::{typeValue} Deserialize(this global::{typeStr} instance, ReadOnlySpan<byte> span, ref int offset)");
        handle.AppendLine("    {");
        handle.AppendLine($"        global::{typeValue}? result = default;");
        handle.AppendLine("        if (SerializationUtils.ReadInstanceState(span[offset..], ref offset))");
        handle.AppendLine("        {");
        
        if (type is INamedTypeSymbol {IsRecord:true} p)
        {
            var recordParameters = string.Empty;
            
            var list = new List<string>();
            var constructorsCandidates =
                p.Constructors
                    .Where(t => !(t.Parameters.Length == 1 && t.Parameters.First().Type != p))
                    .ToList();
            
            if (constructorsCandidates.Any())
            {
                var constructor = constructorsCandidates
                    .OrderByDescending(t=>t.Parameters.Length)
                    .First();
                
                foreach (var constructorParameter in constructor.Parameters)
                {
                    var met = constructorParameter.MetadataName;
                    var itemName = handle.ProcessRecordMemberTypeDeserialization(met, constructorParameter.Type);
                    recordParameters += $"{met}: {itemName},";
                    list.Add(met);
                }
                
                recordParameters = recordParameters.TrimEnd(',', ' ');

                var initOnlyMembers = members
                    .Where(t => t is IPropertySymbol { SetMethod.IsInitOnly: true } && !list.Contains(t.MetadataName))
                    .Cast<IPropertySymbol>()
                    .ToList();
                
                if (initOnlyMembers.Count > 0)
                {
                    var propsList = new List<(string, string)>();
                    foreach (var initOnlyMember in initOnlyMembers)
                    {
                        var itemName = handle.ProcessRecordMemberTypeDeserialization(initOnlyMember.MetadataName, initOnlyMember.Type);
                        propsList.Add((initOnlyMember.MetadataName,itemName));
                        list.Add(initOnlyMember.MetadataName);
                    }
                    handle.AppendLine($"            result = new global::{typeValue}({recordParameters})");
                    handle.AppendLine("            {");
                    foreach (var valueTuple in propsList)
                    {
                        handle.AppendLine($"                {valueTuple.Item1} = {valueTuple.Item2},");
                    }
                    handle.AppendLine("            };");
                }
                else
                {
                    handle.AppendLine($"            result = new global::{typeValue}({recordParameters});");
                }
            }
            
            foreach (var member in members.Where(t=>!list.Contains(t.MetadataName)))
            {
                switch (member)
                {
                    case IPropertySymbol property when property.SetMethod == null || property.DeclaredAccessibility != Accessibility.Public:
                        continue;
                    case IPropertySymbol property:
                        handle.ProcessMemberTypeDeserialization(property.Name, property.Type);
                        break;
                    case IFieldSymbol field when field.IsReadOnly || field.IsConst || field.DeclaredAccessibility != Accessibility.Public:
                        continue;
                    case IFieldSymbol field:
                        handle.ProcessMemberTypeDeserialization(field.Name, field.Type);
                        break;
                }
            }
        }
        else if(type is INamedTypeSymbol pt)
        {
            var recordParameters = string.Empty;
            
            var list = new List<string>();
            if (pt.Constructors.Length > 0)
            {
                var constructor = pt.Constructors
                    .OrderByDescending(t => t.Parameters.Length)
                    .First();
                
                foreach (var constructorParameter in constructor.Parameters)
                {
                    var met = constructorParameter.MetadataName;
                    var itemName = handle.ProcessRecordMemberTypeDeserialization(met, constructorParameter.Type);
                    recordParameters += $"{met}: {itemName},";
                    list.Add(met);
                }
                
                recordParameters = recordParameters.TrimEnd(',', ' ');

                var initOnlyMembers = members
                    .Where(t => t is IPropertySymbol { SetMethod.IsInitOnly: true } && !list.Any(v=>
                        string.Equals(v, t.MetadataName, StringComparison.InvariantCultureIgnoreCase)))
                    .Cast<IPropertySymbol>()
                    .ToList();
                
                if (initOnlyMembers.Count > 0)
                {
                    var propsList = new List<(string, string)>();
                    foreach (var initOnlyMember in initOnlyMembers)
                    {
                        var itemName = handle.ProcessRecordMemberTypeDeserialization(initOnlyMember.MetadataName, initOnlyMember.Type);
                        propsList.Add((initOnlyMember.MetadataName,itemName));
                        list.Add(initOnlyMember.MetadataName);
                    }
                    handle.AppendLine($"            result = new global::{typeValue}({recordParameters})");
                    handle.AppendLine("            {");
                    foreach (var valueTuple in propsList)
                    {
                        handle.AppendLine($"                {valueTuple.Item1} = {valueTuple.Item2},");
                    }
                    handle.AppendLine("            };");
                }
                else
                {
                    handle.AppendLine($"            result = new global::{typeValue}({recordParameters});");
                }
            }
            
            foreach (var member in members.Where(t=>!list.Any(v=>
                         string.Equals(v, t.MetadataName, StringComparison.InvariantCultureIgnoreCase))))
            {
                switch (member)
                {
                    case IPropertySymbol property when property.SetMethod == null || property.DeclaredAccessibility != Accessibility.Public:
                        continue;
                    case IPropertySymbol property:
                        handle.ProcessMemberTypeDeserialization(property.Name, property.Type);
                        break;
                    case IFieldSymbol field when field.IsReadOnly || field.IsConst || field.DeclaredAccessibility != Accessibility.Public:
                        continue;
                    case IFieldSymbol field:
                        handle.ProcessMemberTypeDeserialization(field.Name, field.Type);
                        break;
                }
            }
        }
        else
        {
            handle.AppendLine($"            result = new global::{typeValue}();");
            foreach (var member in members)
            {
                switch (member)
                {
                    case IPropertySymbol property when property.SetMethod == null || property.DeclaredAccessibility != Accessibility.Public:
                        continue;
                    case IPropertySymbol property:
                        handle.ProcessMemberTypeDeserialization(property.Name, property.Type);
                        break;
                    case IFieldSymbol field when field.IsReadOnly || field.IsConst || field.DeclaredAccessibility != Accessibility.Public:
                        continue;
                    case IFieldSymbol field:
                        handle.ProcessMemberTypeDeserialization(field.Name, field.Type);
                        break;
                }
            }
        }
        
        handle.AppendLine("        };");
        handle.AppendLine("        return result;");

        handle.AppendLine("    }");
        
        handle.Complete();
    }
}