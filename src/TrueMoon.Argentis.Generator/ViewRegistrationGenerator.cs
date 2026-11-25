using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using TrueMoon.CodeGeneration;

namespace TrueMoon.Argentis.Generator;

/// <summary>Generates constructor delegates for closed Hosting view registrations.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class ViewRegistrationGenerator : IIncrementalGenerator
{
    private const string RegistrationType = "TrueMoon.Alloy.Hosting.ViewRegistrationExtensions";
    private const string ViewType = "TrueMoon.Argentis.Views.View";
    private static readonly DiagnosticDescriptor InvalidActivation = new(
        "ARG020", "Unsupported generated view activation",
        "Cannot generate View activation for '{0}': {1}. Use a closed registration with a suitable constructor or register an explicit factory",
        "Argentis.Generation", DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var registrations = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is InvocationExpressionSyntax { Expression: var expression } && (expression switch
            {
                MemberAccessExpressionSyntax { Name: GenericNameSyntax name } => name.Identifier.ValueText == "View",
                MemberBindingExpressionSyntax { Name: GenericNameSyntax name } => name.Identifier.ValueText == "View",
                GenericNameSyntax name => name.Identifier.ValueText == "View",
                _ => false
            }),
            static (input, cancellation) => ReadRegistration(input, cancellation)).Collect();
        context.RegisterSourceOutput(registrations, static (output, input) => Generate(output, input));
    }

    private static ImmutableArray<Activation> ReadRegistration(GeneratorSyntaxContext input, CancellationToken cancellation)
    {
        var compilation = input.SemanticModel.Compilation;
        if (input.SemanticModel.GetOperation(input.Node, cancellation) is not IInvocationOperation { TargetMethod: var method } ||
            !SymbolEqualityComparer.Default.Equals((method.ReducedFrom ?? method).ContainingType,
                compilation.GetTypeByMetadataName(RegistrationType))) return [];

        var activations = ImmutableArray.CreateBuilder<Activation>(method.TypeArguments.Length);
        var location = input.Node.GetLocation();
        var viewType = compilation.GetTypeByMetadataName(ViewType);
        for (var i = 0; i < method.TypeArguments.Length; i++)
        {
            var type = method.TypeArguments[i];
            var name = ConstructorActivation.TypeName(type);
            string? error;
            string? factory = null;
            if (type is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false } concrete || !IsUsable(compilation, type))
                error = "use an accessible, closed, non-abstract class";
            else if (ConstructorActivation.LongestPublicConstructor(concrete, out var ties) is not { } constructor)
                error = "no public instance constructor";
            else if (ties != 1)
                error = "more than one longest public constructor";
            else if (constructor.Parameters.FirstOrDefault(parameter => parameter.RefKind != RefKind.None ||
                     !IsUsable(compilation, parameter.Type)) is { } invalid)
                error = $"dependency '{invalid.Name}' must be an accessible managed value passed by value";
            else if (!ConstructorActivation.SetsRequiredMembers(constructor))
                error = "the constructor must initialize required members with SetsRequiredMembers";
            else
            {
                error = null;
                factory = RenderFactory(name, constructor, i == 0 ? viewType : null);
            }
            activations.Add(new(name, factory, error is null ? null : Diagnostic.Create(InvalidActivation, location, name, error)));
        }
        return activations.MoveToImmutable();
    }

    private static string RenderFactory(string type, IMethodSymbol constructor, INamedTypeSymbol? viewType)
    {
        var childCount = 0;
        var parameters = constructor.Parameters;
        var arguments = new string[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            if (viewType is not null && IsConcreteView(parameter.Type, viewType))
            {
                childCount++;
                var factory = parameter.HasExplicitDefaultValue || parameter.NullableAnnotation == NullableAnnotation.Annotated
                    ? "TryCreate" : "Create";
                arguments[i] = $"children.{factory}<{ConstructorActivation.TypeName(parameter.Type)}>()";
            }
            else arguments[i] = ConstructorActivation.Argument(parameter, i);
        }
        var call = $"new {type}({string.Join(", ", arguments)})";
        return childCount == 0 ? $"static context => {call}"
            : $"static resolver => global::TrueMoon.Alloy.Hosting.Generation.ViewConstructor.Construct(resolver, {childCount}, static (context, children) => {call})";
    }

    private static bool IsConcreteView(ITypeSymbol type, INamedTypeSymbol viewType)
    {
        if (type is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false } named) return false;
        for (var current = named.BaseType; current is not null; current = current.BaseType)
            if (SymbolEqualityComparer.Default.Equals(current, viewType)) return true;
        return false;
    }

    private static bool IsUsable(Compilation compilation, ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol array => IsUsable(compilation, array.ElementType),
        INamedTypeSymbol named => !named.IsFileLocal && !named.IsRefLikeType && named.TypeKind != TypeKind.Error &&
            compilation.IsSymbolAccessibleWithin(named, compilation.Assembly) &&
            (named.ContainingType is null || IsUsable(compilation, named.ContainingType)) &&
            named.TypeArguments.All(argument => IsUsable(compilation, argument)),
        IDynamicTypeSymbol => true,
        _ => false
    };

    private static void Generate(SourceProductionContext output, ImmutableArray<ImmutableArray<Activation>> registrations)
    {
        var constructors = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var registration in registrations)
        {
            foreach (var activation in registration)
            {
                if (activation.Error is { } error) output.ReportDiagnostic(error);
                else constructors.TryAdd(activation.Type, activation.Factory!);
            }
        }
        if (constructors.Count == 0) return;

        var source = new StringBuilder("""
            // <auto-generated/>
            #nullable enable
            namespace TrueMoon.Argentis.Generated;

            file static class ViewRegistrations
            {
                [global::System.Runtime.CompilerServices.ModuleInitializer]
                internal static void Register()
                {
            """).AppendLine();
        foreach (var (type, factory) in constructors)
            source.Append("        global::TrueMoon.Alloy.Hosting.Generation.ViewActivation<").Append(type)
                .Append(">.Register(").Append(factory).AppendLine(");");
        source.AppendLine("    }").AppendLine("}");
        output.AddSource("ViewRegistrations.g.cs", source.ToString());
    }

    private sealed record Activation(string Type, string? Factory, Diagnostic? Error);
}
