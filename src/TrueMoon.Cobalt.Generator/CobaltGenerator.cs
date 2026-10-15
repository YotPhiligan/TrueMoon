using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace TrueMoon.Cobalt.Generator;

[Generator(LanguageNames.CSharp)]
public class CobaltGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor InvalidType = Descriptor("COB001", "Unsupported service type",
        "Cobalt cannot generate '{0}': {1}. Use an accessible concrete reference type or an explicit runtime factory.");
    private static readonly DiagnosticDescriptor MissingConstructor = Descriptor("COB002", "No public constructor",
        "Cobalt cannot construct '{0}': no public instance constructor. Expose a public constructor or use an explicit runtime factory.");
    private static readonly DiagnosticDescriptor AmbiguousConstructor = Descriptor("COB003", "Ambiguous constructor",
        "Cobalt cannot choose a constructor for '{0}': {1} public constructors have the maximum parameter count ({2}). Keep one longest public constructor or use an explicit runtime factory.");
    private static readonly DiagnosticDescriptor UnsupportedParameter = Descriptor("COB004", "Unsupported constructor parameter",
        "Cobalt cannot inject parameter '{0}' of '{1}': {2}. Use a supported dependency type or an explicit runtime factory.");
    private static readonly DiagnosticDescriptor UnsupportedMapping = Descriptor("COB005", "Unsupported service mapping",
        "Cobalt cannot map '{0}' to '{1}': {2}. Use a compatible mapping or an explicit runtime factory.");
    private static readonly DiagnosticDescriptor RequiredMembers = Descriptor("COB006", "Required members need initialization",
        "Cobalt cannot construct '{0}': the selected constructor does not declare SetsRequiredMembers. Initialize required members in that constructor or use an explicit runtime factory.");

    private static DiagnosticDescriptor Descriptor(string id, string title, string message) =>
        new(id, title, message, "Cobalt.Generation", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var calls = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is InvocationExpressionSyntax call && RegistrationName(call.Expression) is
                "Singleton" or "Transient" or "Composite" or "TrySingleton" or "TryTransient",
            static (ctx, _) => (InvocationExpressionSyntax)ctx.Node).Collect();
        context.RegisterSourceOutput(context.CompilationProvider.Combine(calls),
            static (ctx, input) => Generate(input.Left, input.Right, ctx));
    }

    private static void Generate(Compilation compilation, ImmutableArray<InvocationExpressionSyntax> calls, SourceProductionContext context)
    {
        var generated = new HashSet<string>();
        var registrations = new StringBuilder();
        foreach (var call in calls)
        {
            var model = compilation.GetSemanticModel(call.SyntaxTree);
            if (model.GetOperation(call) is not IInvocationOperation invocation) continue;
            var method = invocation.TargetMethod;
            if (!IsRegistrationMethod(compilation, method)) continue;
            var tryRegistration = method.Name is "TrySingleton" or "TryTransient";
            // A factory owns construction even inside a generic registration module.
            if (!tryRegistration && method.Name != "Composite" && method.TypeArguments.Length > 0 && method.Parameters.Length != 0) continue;
            var unspecified = method.TypeArguments.FirstOrDefault(ContainsTypeParameter);
            if (unspecified != null)
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidType, call.GetLocation(), TypeName(unspecified),
                    "registration modules must use fully specified types; generic wrapper specialization is not inferred"));
                continue;
            }
            var lifetime = method.Name is "Transient" or "TryTransient" ? "Transient" : "Singleton";
            if (method.Name == "Composite")
            {
                if (method.TypeArguments[0] is not INamedTypeSymbol implementation) continue;
                if (!Add(implementation, implementation, "Singleton", null, call)) continue;
                foreach (var alias in method.TypeArguments.Skip(1).OfType<INamedTypeSymbol>())
                    Add(alias, implementation, "Singleton", $"context.Resolve<{TypeName(implementation)}>()", call);
                continue;
            }
            INamedTypeSymbol? service = null;
            INamedTypeSymbol? concrete = null;
            if (method.TypeArguments.Length > 0)
            {
                service = method.TypeArguments[0] as INamedTypeSymbol;
                concrete = method.TypeArguments.Length == 2 ? method.TypeArguments[1] as INamedTypeSymbol : service;
            }
            else
            {
                // Parameter binding also handles reordered named arguments and conditional access.
                service = TypeOperand(invocation.Arguments.FirstOrDefault(a => a.Parameter?.Name == "service")?.Value);
                concrete = TypeOperand(invocation.Arguments.FirstOrDefault(a => a.Parameter?.Name == "implementation")?.Value);
            }
            if (service != null && concrete != null) Add(service, concrete, lifetime, null, call);
        }
        if (generated.Count == 0) return;
        context.AddSource("CobaltResolversRegistration.g.cs", $@"
// auto-generated by TrueMoon.Cobalt.Generator
namespace {Namespace(compilation)};
public static class CobaltResolversRegistration
{{
    [global::System.Runtime.CompilerServices.ModuleInitializer]
    public static void Init()
    {{
{registrations}    }}
}}");

        bool Add(INamedTypeSymbol service, INamedTypeSymbol implementation, string lifetime, string? alias, InvocationExpressionSyntax call)
        {
            var constructor = Validate(compilation, service, implementation, call.GetLocation(), context);
            if (constructor == null) return false;
            var name = Identifier(TypeName(service)) + "_" + Identifier(TypeName(implementation)) + "_" + lifetime + (alias == null ? "Resolver" : "AliasResolver");
            if (!generated.Add(name)) return true;
            var open = service.IsUnboundGenericType;
            var source = open ? GenericSource(compilation, name, implementation, lifetime, constructor) : Source(compilation, name, service, implementation, lifetime, alias, constructor);
            context.AddSource(name + ".g.cs", source);
            registrations.AppendLine($"        global::TrueMoon.Cobalt.ServiceResolvers.Shared.Add(typeof({TypeName(service)}), typeof({TypeName(implementation)}), global::TrueMoon.Cobalt.ServiceLifetime.{lifetime}, () => new {name}());");
            return true;
        }
    }

    private static string? RegistrationName(ExpressionSyntax expression) => expression switch
    {
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
        MemberBindingExpressionSyntax member => member.Name.Identifier.ValueText,
        SimpleNameSyntax name => name.Identifier.ValueText,
        _ => null
    };

    private static bool IsRegistrationMethod(Compilation compilation, IMethodSymbol method)
    {
        var definition = (method.ReducedFrom ?? method).OriginalDefinition;
        if (method.Name is "TrySingleton" or "TryTransient")
            return SymbolEqualityComparer.Default.Equals(definition.ContainingType,
                compilation.GetTypeByMetadataName("TrueMoon.Services.ServicesRegistrationContextExtensions"));

        var contract = compilation.GetTypeByMetadataName("TrueMoon.Services.IServicesRegistrationContext");
        if (contract == null) return false;
        foreach (var member in contract.GetMembers(method.Name).OfType<IMethodSymbol>())
        {
            if (SymbolEqualityComparer.Default.Equals(definition, member) ||
                SymbolEqualityComparer.Default.Equals(definition,
                    method.ContainingType.FindImplementationForInterfaceMember(member)?.OriginalDefinition)) return true;
        }
        return false;
    }

    private static INamedTypeSymbol? TypeOperand(IOperation? operation)
    {
        while (operation is IConversionOperation conversion) operation = conversion.Operand;
        return operation is ITypeOfOperation typeOf ? typeOf.TypeOperand as INamedTypeSymbol : null;
    }

    private static IMethodSymbol? Validate(Compilation compilation, INamedTypeSymbol service, INamedTypeSymbol implementation,
        Location location, SourceProductionContext context)
    {
        foreach (var type in new[] { service, implementation })
        {
            if (type.TypeKind is not (TypeKind.Class or TypeKind.Interface) || !Accessible(compilation, type) ||
                (!type.IsUnboundGenericType && ContainsTypeParameter(type)))
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidType, location, TypeName(type),
                    "type must be an accessible, non-file-local class/interface with fully specified type arguments"));
                return null;
            }
        }
        if (implementation.TypeKind != TypeKind.Class || implementation.IsAbstract || implementation.IsStatic)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidType, location, TypeName(implementation), "implementation must be a non-abstract class"));
            return null;
        }
        var open = service.IsUnboundGenericType;
        var conversion = compilation.ClassifyCommonConversion(implementation, service);
        var mappingError = open != implementation.IsUnboundGenericType ? "both types must be closed or both must be open" :
            open ? OpenMappingError(service, implementation) :
            !(conversion.IsImplicit && (conversion.IsIdentity || conversion.IsReference)) ? "implementation is not assignable to the service" : null;
        if (mappingError != null)
        {
            context.ReportDiagnostic(Diagnostic.Create(UnsupportedMapping, location, TypeName(service), TypeName(implementation), mappingError));
            return null;
        }
        var definition = open ? implementation.OriginalDefinition : implementation;
        var constructors = definition.InstanceConstructors.Where(c => c.DeclaredAccessibility == Accessibility.Public)
            .OrderByDescending(c => c.Parameters.Length).ToArray();
        if (constructors.Length == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingConstructor, location, TypeName(implementation)));
            return null;
        }
        var constructor = constructors[0];
        var longestCount = constructors.Count(c => c.Parameters.Length == constructor.Parameters.Length);
        if (longestCount != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(AmbiguousConstructor, location, TypeName(implementation), longestCount, constructor.Parameters.Length));
            return null;
        }
        foreach (var parameter in constructor.Parameters)
        {
            var reason = parameter.RefKind != RefKind.None ? "ref, in and out parameters are not supported" :
                !Injectable(compilation, parameter.Type) ? "dependency cannot be resolved as an accessible managed value" : null;
            if (reason == null) continue;
            context.ReportDiagnostic(Diagnostic.Create(UnsupportedParameter, location, parameter.Name, TypeName(implementation), reason));
            return null;
        }
        if (HasRequiredMembers(definition) && !constructor.GetAttributes().Any(a =>
                a.AttributeClass?.ToDisplayString() == "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute"))
        {
            context.ReportDiagnostic(Diagnostic.Create(RequiredMembers, location, TypeName(implementation)));
            return null;
        }
        return constructor;
    }

    private static bool Accessible(Compilation compilation, ITypeSymbol type) =>
        compilation.IsSymbolAccessibleWithin(type, compilation.Assembly) &&
        (type is not INamedTypeSymbol named || (!named.IsFileLocal &&
            (named.ContainingType == null || Accessible(compilation, named.ContainingType)) &&
            named.TypeArguments.All(t => t.TypeKind == TypeKind.TypeParameter || Accessible(compilation, t))));

    private static bool ContainsTypeParameter(ITypeSymbol type) => type.TypeKind == TypeKind.TypeParameter ||
        type is IArrayTypeSymbol array && ContainsTypeParameter(array.ElementType) ||
        type is INamedTypeSymbol named && (named.TypeArguments.Any(ContainsTypeParameter) ||
            named.ContainingType != null && ContainsTypeParameter(named.ContainingType));

    private static bool Injectable(Compilation compilation, ITypeSymbol type) =>
        type.TypeKind is not (TypeKind.Pointer or TypeKind.FunctionPointer or TypeKind.Error) && !type.IsRefLikeType &&
        (type is not ITypeParameterSymbol parameter || !parameter.AllowsRefLikeType) &&
        (type.TypeKind == TypeKind.TypeParameter || Accessible(compilation, type)) &&
        (type is not IArrayTypeSymbol array || Injectable(compilation, array.ElementType));

    private static bool HasRequiredMembers(INamedTypeSymbol type) =>
        type.GetMembers().Any(m => m is IPropertySymbol { IsRequired: true } or IFieldSymbol { IsRequired: true }) ||
        type.BaseType != null && HasRequiredMembers(type.BaseType);

    private static string? OpenMappingError(INamedTypeSymbol service, INamedTypeSymbol implementation)
    {
        if (GenericContainingType(service) || GenericContainingType(implementation))
            return "open types inside generic containing types are not supported";
        if (service.Arity != implementation.Arity) return "open types must have the same number of type parameters";
        var definition = implementation.OriginalDefinition;
        var candidates = definition.AllInterfaces.Concat(BaseTypes(definition));
        return candidates.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, service.OriginalDefinition) &&
            candidate.TypeArguments.Length == definition.TypeParameters.Length &&
            candidate.TypeArguments.Zip(definition.TypeParameters).All(pair => SymbolEqualityComparer.Default.Equals(pair.First, pair.Second))) ? null :
            "open service must use implementation type parameters in the same order";
    }

    private static IEnumerable<INamedTypeSymbol> BaseTypes(INamedTypeSymbol type)
    {
        for (var current = type; current != null; current = current.BaseType) yield return current;
    }

    private static bool GenericContainingType(INamedTypeSymbol type)
    {
        for (var containing = type.ContainingType; containing != null; containing = containing.ContainingType)
            if (containing.Arity != 0) return true;
        return false;
    }

    private static string Argument(IParameterSymbol parameter, int index)
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

    private static string Source(Compilation compilation, string name, INamedTypeSymbol service, INamedTypeSymbol implementation, string lifetime, string? alias, IMethodSymbol constructor)
    {
        var arguments = string.Join(", ", constructor.Parameters.Select(Argument));
        var create = alias ?? $"new {TypeName(implementation)}({arguments})";
        var singleton = lifetime == "Singleton";
        return $@"
// auto-generated by TrueMoon.Cobalt.Generator
#nullable enable
namespace {Namespace(compilation)};
internal class {name} : global::TrueMoon.Cobalt.IResolver<{TypeName(service)}, {TypeName(implementation)}>, global::TrueMoon.Cobalt.IObjectResolver
{{
    {(singleton ? $"private readonly object _lock = new();\n    private {TypeName(service)}? _instance;" : "")}
    public global::TrueMoon.Cobalt.ServiceLifetime ServiceLifetime => global::TrueMoon.Cobalt.ServiceLifetime.{lifetime};
    public bool IsServiceDisposable => true;
    public {TypeName(service)} Resolve(global::TrueMoon.Services.IServiceResolver context)
    {{
        {(singleton ? $"lock (_lock) return _instance ??= {create};" : $"return {create};")}
    }}
    object? global::TrueMoon.Cobalt.IObjectResolver.Resolve(global::TrueMoon.Services.IServiceResolver context) => Resolve(context);
}}
";
    }

    private static string GenericSource(Compilation compilation, string name, INamedTypeSymbol implementation, string lifetime, IMethodSymbol constructor)
    {
        var singleton = lifetime == "Singleton";
        var optional = string.Join(", ", constructor.Parameters.Select(p => p.HasExplicitDefaultValue || p.NullableAnnotation == NullableAnnotation.Annotated ? "true" : "false"));
        return $@"
// auto-generated by TrueMoon.Cobalt.Generator
#nullable enable
namespace {Namespace(compilation)};
internal class {name} : global::TrueMoon.Cobalt.IGenericResolver
{{
    private readonly object _lock = new();
    {(singleton ? "private readonly global::System.Collections.Generic.Dictionary<global::System.Type, object> _instances = new();" : "")}
    public global::TrueMoon.Cobalt.ServiceLifetime ServiceLifetime => global::TrueMoon.Cobalt.ServiceLifetime.{lifetime};
    public bool IsServiceDisposable => true;
    public object? Resolve(global::System.Type[] arguments, global::TrueMoon.Services.IServiceResolver context)
    {{
        lock (_lock)
        {{
            var type = typeof({TypeName(implementation)}).MakeGenericType(arguments);
            {(singleton ? "if (_instances.TryGetValue(type, out var existing)) return existing;" : "")}
            // The unique longest public constructor was validated at the registration site.
            var constructor = global::System.Array.Find(type.GetConstructors(), candidate => candidate.GetParameters().Length == {constructor.Parameters.Length})!;
            var parameters = constructor.GetParameters();
            bool[] optional = [{optional}];
            var values = new object?[parameters.Length];
            for (var index = 0; index < parameters.Length; index++)
            {{
                var parameter = parameters[index];
                values[index] = context.GetService(parameter.ParameterType);
                if (values[index] == null)
                {{
                    if (parameter.HasDefaultValue) values[index] = parameter.DefaultValue;
                    else if (!optional[index])
                        throw new global::TrueMoon.Cobalt.ServiceResolvingException(parameter.ParameterType);
                }}
            }}
            var item = constructor.Invoke(global::System.Reflection.BindingFlags.DoNotWrapExceptions, binder: null, parameters: values, culture: null);
            {(singleton ? "_instances.Add(type, item);" : "")}
            return item;
        }}
    }}
}}
";
    }

    private static string Namespace(Compilation compilation) => Identifier(compilation.AssemblyName ?? "Assembly") + ".Cobalt.Generated";
    private static string TypeName(ITypeSymbol type) => type.TypeKind == TypeKind.Dynamic ? "global::System.Object" : type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    private static string Identifier(string value)
    {
        // Encode punctuation as well as underscores, so namespace/type/lifetime combinations never collide.
        var result = new StringBuilder("R");
        foreach (var character in value)
            if (char.IsLetterOrDigit(character)) result.Append(character);
            else result.Append('_').Append(((int)character).ToString("X4"));
        return result.ToString();
    }
}
