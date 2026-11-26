using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TrueMoon.Argentis.Generator;

/// <summary>Generates typed named parts and inferred style roles from inline view content.</summary>
[Generator(LanguageNames.CSharp)]
public sealed partial class ViewPartsGenerator : IIncrementalGenerator
{
    private const string ViewName = "TrueMoon.Argentis.Views.View";
    private static readonly DiagnosticDescriptor InvalidPart = new(
        "ARG006", "Unsupported generated view part",
        "A generated part requires a constant C# identifier and a control type that, along with all containing types, is public or internal, non-generic, non-file-local, and accessible from the generated class",
        "Argentis.Generation", DiagnosticSeverity.Error, isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor DuplicateName = new(
        "ARG007", "Duplicate generated view part name",
        "View '{0}' declares more than one generated part named '{1}'",
        "Argentis.Generation", DiagnosticSeverity.Error, isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor NameConflict = new(
        "ARG008", "Generated view part name conflict",
        "View '{0}' cannot generate '{1}' because that name already exists",
        "Argentis.Generation", DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var views = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is TypeDeclarationSyntax type && IsCandidate(type),
            static (input, cancellation) => ReadView(input, cancellation));
        context.RegisterSourceOutput(views, static (output, view) =>
        {
            if (view is null) return;
            foreach (var diagnostic in view.Diagnostics) output.ReportDiagnostic(diagnostic);
            if (view.PartsSource is { } parts) output.AddSource($"{view.HintPrefix}.ViewParts.g.cs", parts);
            if (view.RolesSource is { } roles) output.AddSource($"{view.HintPrefix}.InferredViewRoles.g.cs", roles);
        });
    }

    private static bool IsCandidate(TypeDeclarationSyntax type) => type.Members.OfType<ConstructorDeclarationSyntax>()
        .Any(static constructor => Calls(constructor).Any(IsContentCall));

    private static bool IsContentCall(InvocationExpressionSyntax call) => call.Expression switch
    {
        IdentifierNameSyntax name => name.Identifier.ValueText == "Content",
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax or BaseExpressionSyntax } member =>
            member.Name.Identifier.ValueText == "Content",
        _ => false
    };

    private static IEnumerable<InvocationExpressionSyntax> Calls(SyntaxNode root) => root
        .DescendantNodesAndSelf(static node => node is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
        .OfType<InvocationExpressionSyntax>();

    private static GeneratedView? ReadView(GeneratorSyntaxContext input, CancellationToken cancellation)
    {
        var compilation = input.SemanticModel.Compilation;
        var view = compilation.GetTypeByMetadataName(ViewName);
        var declaration = (TypeDeclarationSyntax)input.Node;
        if (view is null || input.SemanticModel.GetDeclaredSymbol(declaration, cancellation) is not INamedTypeSymbol owner ||
            !SupportsExtensions(owner) || !DerivesFrom(owner, view)) return null;

        var declarations = owner.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax(cancellation))
            .OfType<TypeDeclarationSyntax>().ToArray();
        // Analyze each view once, even if multiple partial declarations are candidates.
        var first = declarations.First(IsCandidate);
        if (first.SyntaxTree != declaration.SyntaxTree || first.SpanStart != declaration.SpanStart)
            return null;
        var constructors = declarations.SelectMany(type => type.Members.OfType<ConstructorDeclarationSyntax>());
        var factories = ReadContentFactories(compilation, view, constructors, cancellation).ToArray();
        return factories.Length > 0 ? ReadContent(owner, view, compilation, factories, declaration.GetLocation(), cancellation) : null;
    }

    private static IEnumerable<SyntaxNode> ReadContentFactories(Compilation compilation, INamedTypeSymbol view,
        IEnumerable<ConstructorDeclarationSyntax> constructors, CancellationToken cancellation)
    {
        var typedView = compilation.GetTypeByMetadataName("TrueMoon.Argentis.Views.View`1");
        var typedWindow = compilation.GetTypeByMetadataName("TrueMoon.Argentis.Windows.Window`1");

        foreach (var constructor in constructors)
        {
            var model = compilation.GetSemanticModel(constructor.SyntaxTree);
            foreach (var call in Calls(constructor).Where(IsContentCall))
            {
                // The body can already reference generated parts and roles that do not exist yet.
                var symbol = model.GetSymbolInfo(call, cancellation);
                var arguments = call.ArgumentList.Arguments;
                if (symbol.Symbol is IMethodSymbol method)
                {
                    if (!IsFrameworkContent(method, arguments.Count)) continue;
                }
                else
                {
                    var candidates = symbol.CandidateSymbols.IsEmpty
                        ? model.GetMemberGroup(call.Expression, cancellation) : symbol.CandidateSymbols;
                    if (candidates.OfType<IMethodSymbol>().Count(item => item.Parameters.Length == arguments.Count) != 1 ||
                        !candidates.OfType<IMethodSymbol>().Any(item => IsFrameworkContent(item, arguments.Count))) continue;
                }

                var argument = arguments.FirstOrDefault(static arg => arg.NameColon?.Name.Identifier.ValueText == "factory")
                    ?? arguments[arguments.Count - 1];
                if (argument.NameColon is { Name.Identifier.ValueText: not "factory" }) continue;
                var expression = argument.Expression;
                while (expression is ParenthesizedExpressionSyntax grouping) expression = grouping.Expression;
                if (expression is AnonymousFunctionExpressionSyntax factory) yield return factory.Body;
            }
        }

        bool IsFrameworkContent(IMethodSymbol method, int arguments)
        {
            if (method.Parameters.Length != arguments) return false;
            var declaringType = method.ContainingType.OriginalDefinition;
            return SymbolEqualityComparer.Default.Equals(declaringType, view) ||
                SymbolEqualityComparer.Default.Equals(declaringType, typedView) ||
                SymbolEqualityComparer.Default.Equals(declaringType, typedWindow);
        }
    }

    private static bool SupportsExtensions(INamedTypeSymbol owner) => owner is
    {
        ContainingType: null, IsGenericType: false, IsFileLocal: false,
        DeclaredAccessibility: Accessibility.Public or Accessibility.Internal
    };

    private static GeneratedView ReadContent(INamedTypeSymbol owner, INamedTypeSymbol view, Compilation compilation,
        IEnumerable<SyntaxNode> declarations, Location location, CancellationToken cancellation)
    {
        var elementExtensions = compilation.GetTypeByMetadataName("TrueMoon.Argentis.ElementExtensions");
        var buildExtensions = compilation.GetTypeByMetadataName("TrueMoon.Argentis.Views.UiBuildExtensions");
        var wrapped = compilation.GetTypeByMetadataName("TrueMoon.Argentis.Views.UiControl`2");
        var extensionType = owner.Name + "Parts";
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var parts = new Dictionary<string, (INamedTypeSymbol Type, Accessibility Accessibility)>(StringComparer.Ordinal);
        var canGenerateParts = elementExtensions is not null;
        if (canGenerateParts && owner.ContainingNamespace.GetTypeMembers(extensionType).Any(type => type.Arity == 0))
        {
            diagnostics.Add(Diagnostic.Create(NameConflict, location, owner.Name, extensionType));
            canGenerateParts = false;
        }
        var roleInference = new InferredRoles(compilation, owner, buildExtensions, wrapped);
        foreach (var declaration in declarations)
        {
            var model = compilation.GetSemanticModel(declaration.SyntaxTree);
            foreach (var call in Calls(declaration))
            {
                if (call.Expression is not MemberAccessExpressionSyntax member) continue;
                switch (member.Name.Identifier.ValueText)
                {
                    case "WithRole":
                        roleInference.Read(model, call, member, diagnostics, cancellation);
                        continue;
                    case "Named" when canGenerateParts:
                        break;
                    default:
                        continue;
                }

                var symbol = model.GetSymbolInfo(call, cancellation).Symbol ??
                    roleInference.BindAfterRole(model, call, member, cancellation);
                if (symbol is not IMethodSymbol { ReturnType: INamedTypeSymbol returnType } method)
                    continue;

                INamedTypeSymbol? partType;
                var declaringType = (method.ReducedFrom ?? method).ContainingType;
                if (wrapped is not null && buildExtensions is not null && SymbolEqualityComparer.Default.Equals(returnType.OriginalDefinition, wrapped) &&
                    (SymbolEqualityComparer.Default.Equals(declaringType, buildExtensions) ||
                     SymbolEqualityComparer.Default.Equals(declaringType.ContainingType, buildExtensions)))
                    partType = returnType.TypeArguments[0] as INamedTypeSymbol;
                else if (method.ReducedFrom is not null && SymbolEqualityComparer.Default.Equals(declaringType, elementExtensions))
                    partType = returnType;
                else
                    continue;
                if (partType is not null && DerivesFrom(partType, view)) continue;

                var accessibility = GetPartAccessibility(partType, compilation);
                if (call.ArgumentList.Arguments is not [{ Expression: var argument }] ||
                    model.GetConstantValue(argument, cancellation) is not { HasValue: true, Value: string name } ||
                    !SyntaxFacts.IsValidIdentifier(name) || SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ||
                    partType is null || accessibility == Accessibility.NotApplicable)
                {
                    diagnostics.Add(Diagnostic.Create(InvalidPart, call.GetLocation()));
                    continue;
                }

                if (HasMember(owner, name))
                    diagnostics.Add(Diagnostic.Create(NameConflict, call.GetLocation(), owner.Name, name));
                else if (!parts.TryAdd(name, (partType, accessibility)))
                    diagnostics.Add(Diagnostic.Create(DuplicateName, call.GetLocation(), owner.Name, name));
            }
        }

        var source = parts.Count == 0 ? null : Render(owner, extensionType, parts);
        return new(owner.ToDisplayString(), source, roleInference.Render(), diagnostics.ToImmutable());
    }

    private static Accessibility GetPartAccessibility(INamedTypeSymbol? type, Compilation compilation)
    {
        if (type is null) return Accessibility.NotApplicable;

        var accessibility = Accessibility.Public;
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current is not
                {
                    TypeKind: not TypeKind.Error, IsGenericType: false, IsFileLocal: false,
                    DeclaredAccessibility: Accessibility.Public or Accessibility.Internal
                })
                return Accessibility.NotApplicable;
            if (current.DeclaredAccessibility == Accessibility.Internal)
                accessibility = Accessibility.Internal;
        }

        return compilation.IsSymbolAccessibleWithin(type, compilation.Assembly)
            ? accessibility : Accessibility.NotApplicable;
    }

    private static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
            if (SymbolEqualityComparer.Default.Equals(current, baseType)) return true;
        return false;
    }

    private static bool HasMember(INamedTypeSymbol type, string name)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
            if (current.GetMembers(name).Length != 0) return true;
        return false;
    }

    private static string Render(INamedTypeSymbol owner, string extensionType,
        Dictionary<string, (INamedTypeSymbol Type, Accessibility Accessibility)> parts)
    {
        var source = new StringBuilder("// <auto-generated/>\n#nullable enable\n\n");
        if (!owner.ContainingNamespace.IsGlobalNamespace)
            source.Append("namespace ").Append(owner.ContainingNamespace.ToDisplayString()).AppendLine(";").AppendLine();

        var access = owner.DeclaredAccessibility == Accessibility.Public ? "public" : "internal";
        source.AppendLine("/// <summary>Provides typed access to the named parts of this view.</summary>");
        source.Append(access).Append(" static class ").AppendLine(extensionType).AppendLine("{");
        source.Append("    extension(").Append(owner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .AppendLine(" view)").AppendLine("    {");
        foreach (var (name, part) in parts.OrderBy(part => part.Key, StringComparer.Ordinal))
        {
            var type = part.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var partAccess = part.Accessibility == Accessibility.Public ? "public" : "internal";
            source.AppendLine("        /// <summary>Gets the live named part in this view's scope.</summary>");
            source.Append("        ").Append(partAccess).Append(' ').Append(type).Append(' ').Append(name)
                .Append(" => view.GetRequiredPart<").Append(type).Append(">(")
                .Append(SymbolDisplay.FormatLiteral(name, quote: true)).AppendLine(");");
        }
        return source.AppendLine("    }").AppendLine("}").ToString();
    }

    private sealed record GeneratedView(string HintPrefix, string? PartsSource, string? RolesSource, ImmutableArray<Diagnostic> Diagnostics);
}
