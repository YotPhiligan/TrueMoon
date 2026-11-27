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
    private static readonly DiagnosticDescriptor InvalidArgument = new(
        "ARG021", "Unsupported runtime view argument",
        "Cannot generate View activation for '{0}' with argument '{1}': {2}",
        "Argentis.Generation", DiagnosticSeverity.Error, isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor AmbiguousConstructor = new(
        "ARG024", "Ambiguous view constructor",
        "View '{0}' has more than one longest public constructor; mark exactly one with [ViewConstructor] or register an explicit factory",
        "Argentis.Generation", DiagnosticSeverity.Error, isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor MissingChildRegistration = new(
        "ARG025", "Missing child View registration",
        "Cannot generate View activation for '{0}': child View '{1}' is never registered with services.View<>() in this compilation",
        "Argentis.Generation", DiagnosticSeverity.Error, isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor MissingServiceRegistration = new(
        "ARG026", "Missing service registration",
        "Cannot generate View activation for '{0}': dependency '{1}' is never registered with services.Singleton<>(), Transient<>(), Instance<>() or View<>() in this compilation",
        "Argentis.Generation", DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var registrations = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is InvocationExpressionSyntax { Expression: var expression } && (expression switch
            {
                MemberAccessExpressionSyntax { Name: GenericNameSyntax name } => IsRegistration(name),
                MemberBindingExpressionSyntax { Name: GenericNameSyntax name } => IsRegistration(name),
                GenericNameSyntax name => IsRegistration(name),
                _ => false
            }),
            static (input, cancellation) => ReadRegistration(input, cancellation)).Collect();
        var declared = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax },
            static (input, cancellation) => ReadServiceRegistration(input, cancellation)).Collect();
        context.RegisterSourceOutput(registrations.Combine(declared), static (output, input) => Generate(output, input.Left, input.Right));
    }

    private static ImmutableArray<string> ReadServiceRegistration(GeneratorSyntaxContext input, CancellationToken cancellation)
    {
        if (input.SemanticModel.GetOperation(input.Node, cancellation) is not IInvocationOperation { TargetMethod: var method }) return [];
        var definition = method.OriginalDefinition;
        var containing = definition.ContainingType;
        var typeArguments = method.ReducedFrom is { TypeArguments.Length: > 0 } reduced ? reduced.TypeArguments : method.TypeArguments;
        if (typeArguments.Length == 0) return [];
        var ns = containing.ContainingNamespace?.ToDisplayString();
        if (ns == "TrueMoon.Alloy.Hosting" && containing.Name == "ViewRegistrationExtensions")
        {
            var registered = ImmutableArray.Create(ConstructorActivation.TypeName(typeArguments[0]));
            var factoryArguments = method.Name == "ViewWithArgs" ? typeArguments : ImmutableArray.Create(typeArguments[0]);
            return registered.Add(FactoryName(factoryArguments));
        }
        if (ns == "TrueMoon.Services" && containing.Name == "IServicesRegistrationContext" &&
            definition.Name is "Singleton" or "Transient" or "Instance")
        {
            if (typeArguments[0] is INamedTypeSymbol { TypeArguments.Length: > 0 } factory &&
                factory.OriginalDefinition.Name == "IFactory" &&
                factory.OriginalDefinition.ContainingNamespace?.ToDisplayString() is "TrueMoon" or "TrueMoon.Services" &&
                IsConcreteView(factory.TypeArguments[0], input.SemanticModel.Compilation.GetTypeByMetadataName(ViewType)))
                return [ConstructorActivation.TypeName(factory), ConstructorActivation.TypeName(factory.TypeArguments[0])];
            return [ConstructorActivation.TypeName(typeArguments[0])];
        }
        return [];
    }

    private static string FactoryName(ImmutableArray<ITypeSymbol> arguments) =>
        "global::TrueMoon.IFactory<" + string.Join(", ", arguments.Select(static argument => ConstructorActivation.TypeName(argument))) + ">";

    private static bool IsRegistration(GenericNameSyntax name) => name.Identifier.ValueText is "View" or "ViewWithArgs";

    private static ImmutableArray<Activation> ReadRegistration(GeneratorSyntaxContext input, CancellationToken cancellation)
    {
        var compilation = input.SemanticModel.Compilation;
        if (input.SemanticModel.GetOperation(input.Node, cancellation) is not IInvocationOperation { TargetMethod: var method } ||
            !SymbolEqualityComparer.Default.Equals((method.ReducedFrom ?? method).ContainingType,
                compilation.GetTypeByMetadataName(RegistrationType))) return [];

        var argumentType = method.Name == "ViewWithArgs" ? method.TypeArguments[1] : null;
        var count = argumentType is null ? method.TypeArguments.Length : 1;
        var activations = ImmutableArray.CreateBuilder<Activation>(count);
        var location = input.Node.GetLocation();
        var viewType = compilation.GetTypeByMetadataName(ViewType);
        for (var i = 0; i < count; i++)
        {
            var type = method.TypeArguments[i];
            var name = ConstructorActivation.TypeName(type);
            var activationType = name;
            string? error;
            string? factory = null;
            var children = ImmutableArray.CreateBuilder<string>();
            var services = ImmutableArray.CreateBuilder<(string Name, string? View)>();
            if (type is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false } concrete || !IsUsable(compilation, type))
                error = "use an accessible, closed, non-abstract class";
            else if (ConstructorActivation.SelectedConstructor(concrete, out var failure) is not { } constructor)
            {
                activations.Add(new(activationType, null, failure == ActivationConstructorFailure.AmbiguousLongest
                    ? Diagnostic.Create(AmbiguousConstructor, location, name)
                    : Diagnostic.Create(InvalidActivation, location, name, failure switch
                    {
                        ActivationConstructorFailure.NoPublicConstructor => "no public instance constructor",
                        ActivationConstructorFailure.MultipleMarkedConstructors => "more than one constructor is marked with ViewConstructor",
                        _ => "the ViewConstructor-marked constructor must be public",
                    })));
                continue;
            }
            else if (constructor.Parameters.FirstOrDefault(parameter => parameter.RefKind != RefKind.None ||
                     !IsUsable(compilation, parameter.Type)) is { } invalid)
                error = $"dependency '{invalid.Name}' must be an accessible managed value passed by value";
            else if (!ConstructorActivation.SetsRequiredMembers(constructor))
                error = "the constructor must initialize required members with SetsRequiredMembers";
            else
            {
                var argumentIndex = -1;
                if (argumentType is not null)
                {
                    var argumentName = ConstructorActivation.TypeName(argumentType);
                    var argumentError = FindArgument(compilation, constructor, argumentType, out argumentIndex);
                    if (argumentError is not null)
                    {
                        activations.Add(new(name, null, Diagnostic.Create(InvalidArgument, location, name, argumentName, argumentError)));
                        continue;
                    }
                    activationType = $"{name}, {argumentName}";
                }
                error = null;
                factory = RenderFactory(name, constructor, i == 0 ? viewType : null, argumentIndex);
                var childViewType = i == 0 ? viewType : null;
                foreach (var parameter in constructor.Parameters)
                {
                    if (parameter.Ordinal == argumentIndex || parameter.HasExplicitDefaultValue ||
                        parameter.NullableAnnotation == NullableAnnotation.Annotated) continue;
                    if (childViewType is not null && IsConcreteView(parameter.Type, childViewType))
                        children.Add(ConstructorActivation.TypeName(parameter.Type));
                    else services.Add((ConstructorActivation.TypeName(parameter.Type), null));
                }
            }
            activations.Add(new(activationType, factory, error is null ? null : Diagnostic.Create(InvalidActivation, location, name, error),
                location, children.ToImmutable(), services.ToImmutable()));
        }
        return activations.MoveToImmutable();
    }

    private static string? FindArgument(Compilation compilation, IMethodSymbol constructor, ITypeSymbol type, out int index)
    {
        index = -1;
        if (!IsUsable(compilation, type)) return "use an accessible, closed runtime argument type";
        foreach (var parameter in constructor.Parameters)
        {
            if (!SymbolEqualityComparer.Default.Equals(parameter.Type, type)) continue;
            if (index >= 0) return "more than one parameter of this type in the selected constructor";
            index = parameter.Ordinal;
        }
        return index < 0 ? "no parameter of this type in the selected constructor" : null;
    }

    private static string RenderFactory(string type, IMethodSymbol constructor, INamedTypeSymbol? viewType, int argumentIndex)
    {
        var childCount = 0;
        var parameters = constructor.Parameters;
        var arguments = new string[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            if (i == argumentIndex) arguments[i] = "argument";
            else if (viewType is not null && IsConcreteView(parameter.Type, viewType))
            {
                childCount++;
                var factory = parameter.HasExplicitDefaultValue || parameter.NullableAnnotation == NullableAnnotation.Annotated
                    ? "TryCreate" : "Create";
                arguments[i] = $"children.{factory}<{ConstructorActivation.TypeName(parameter.Type)}>()";
            }
            else arguments[i] = ConstructorActivation.Argument(parameter, i);
        }
        var call = $"new {type}({string.Join(", ", arguments)})";
        if (argumentIndex >= 0)
            return childCount == 0 ? $"static (context, argument) => {call}"
                : $"static (resolver, value) => global::TrueMoon.Alloy.Hosting.Generation.ViewConstructor.Construct(resolver, {childCount}, value, static (context, children, argument) => {call})";
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

    private static void Generate(SourceProductionContext output, ImmutableArray<ImmutableArray<Activation>> registrations,
        ImmutableArray<ImmutableArray<string>> declared)
    {
        var registered = declared.SelectMany(static types => types)
            .ToImmutableHashSet(StringComparer.Ordinal);
        var constructors = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var registration in registrations)
        {
            foreach (var activation in registration)
            {
                if (activation.Error is { } error)
                {
                    output.ReportDiagnostic(error);
                    continue;
                }
                foreach (var child in activation.Children)
                    if (!registered.Contains(child))
                        output.ReportDiagnostic(Diagnostic.Create(MissingChildRegistration, activation.Location,
                            activation.Type, Display(child)));
                foreach (var (service, view) in activation.Services)
                    if (!registered.Contains(service) && (view is null || !registered.Contains(view)))
                        output.ReportDiagnostic(Diagnostic.Create(MissingServiceRegistration, activation.Location,
                            activation.Type, Display(service)));
                constructors.TryAdd(activation.Type, activation.Factory!);
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

    private static string Display(string type) => type.StartsWith("global::", StringComparison.Ordinal) ? type[8..] : type;

    private sealed record Activation(string Type, string? Factory, Diagnostic? Error, Location? Location = null,
        ImmutableArray<string> Children = default, ImmutableArray<(string Name, string? View)> Services = default);
}

