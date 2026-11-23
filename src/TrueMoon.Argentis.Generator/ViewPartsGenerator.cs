using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TrueMoon.Argentis.Generator;

/// <summary>Generates typed access to named controls through a view's existing part registry.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class ViewPartsGenerator : IIncrementalGenerator
{
    private const string AttributeName = "TrueMoon.Argentis.GenerateViewPartsAttribute";
    private const string ViewName = "TrueMoon.Argentis.View";
    private const string ContentMethodName = "BuildContent";
    private static readonly DiagnosticDescriptor UnsupportedView = new(
        "ARG005", "Unsupported view for generated parts",
        "'{0}' must be an accessible top-level non-generic View",
        "Argentis.Generation", DiagnosticSeverity.Error, isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor InvalidPart = new(
        "ARG006", "Unsupported generated view part",
        "A generated part must have a constant C# identifier and a public top-level non-generic control type",
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
        var constructors = context.SyntaxProvider.ForAttributeWithMetadataName(
            AttributeName, static (_, _) => true, static (input, cancellation) => ReadConstructors(input, cancellation));
        var contentMethods = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is MethodDeclarationSyntax method &&
                method.Identifier.ValueText == ContentMethodName && method.Modifiers.Any(SyntaxKind.OverrideKeyword),
            static (input, cancellation) => ReadContentMethod(input, cancellation));
        context.RegisterSourceOutput(constructors, Emit);
        context.RegisterSourceOutput(contentMethods, Emit);

        static void Emit(SourceProductionContext output, GeneratedView? view)
        {
            if (view is null) return;
            foreach (var diagnostic in view.Diagnostics) output.ReportDiagnostic(diagnostic);
            if (view.Source is { } source) output.AddSource(view.HintName, source);
        }
    }

    private static GeneratedView? ReadConstructors(GeneratorAttributeSyntaxContext input, CancellationToken cancellation)
    {
        var compilation = input.SemanticModel.Compilation;
        var view = compilation.GetTypeByMetadataName(ViewName);
        if (input.TargetSymbol is not INamedTypeSymbol owner || !SupportsExtensions(owner) || view is null || !DerivesFrom(owner, view))
            return new("", null, [Diagnostic.Create(UnsupportedView, input.TargetNode.GetLocation(), input.TargetSymbol.Name)]);

        if (owner.GetMembers(ContentMethodName).OfType<IMethodSymbol>().Any(method => OverridesBuildContent(method, view)))
            return null;

        var declarations = owner.InstanceConstructors.SelectMany(constructor => constructor.DeclaringSyntaxReferences)
            .Select(declaration => declaration.GetSyntax(cancellation)).OfType<ConstructorDeclarationSyntax>();
        return ReadParts(owner, view, compilation, declarations, input.TargetNode.GetLocation(), cancellation);
    }

    private static GeneratedView? ReadContentMethod(GeneratorSyntaxContext input, CancellationToken cancellation)
    {
        var compilation = input.SemanticModel.Compilation;
        var view = compilation.GetTypeByMetadataName(ViewName);
        var declaration = (MethodDeclarationSyntax)input.Node;
        if (view is null || input.SemanticModel.GetDeclaredSymbol(declaration, cancellation) is not IMethodSymbol method ||
            !OverridesBuildContent(method, view) || !SupportsExtensions(method.ContainingType))
            return null;

        return ReadParts(method.ContainingType, view, compilation, [declaration], declaration.GetLocation(), cancellation);
    }

    private static bool SupportsExtensions(INamedTypeSymbol owner) => owner is
    {
        ContainingType: null, IsGenericType: false, IsFileLocal: false,
        DeclaredAccessibility: Accessibility.Public or Accessibility.Internal
    };

    private static bool OverridesBuildContent(IMethodSymbol method, INamedTypeSymbol view)
    {
        for (var current = method.OverriddenMethod; current is not null; current = current.OverriddenMethod)
            if (SymbolEqualityComparer.Default.Equals(current.ContainingType, view)) return true;
        return false;
    }

    private static GeneratedView ReadParts(INamedTypeSymbol owner, INamedTypeSymbol view, Compilation compilation,
        IEnumerable<SyntaxNode> declarations, Location location, CancellationToken cancellation)
    {
        var elementExtensions = compilation.GetTypeByMetadataName("TrueMoon.Argentis.ElementExtensions");
        if (elementExtensions is null) return new("", null, []);
        var extensionType = owner.Name + "Parts";
        if (owner.ContainingNamespace.GetTypeMembers(extensionType).Any(type => type.Arity == 0))
        {
            return new("", null, [Diagnostic.Create(NameConflict, location, owner.Name, extensionType)]);
        }

        var parts = new Dictionary<string, string>(StringComparer.Ordinal);
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        foreach (var declaration in declarations)
        {
            var model = compilation.GetSemanticModel(declaration.SyntaxTree);
            var calls = declaration.DescendantNodes(static node => node is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
                .OfType<InvocationExpressionSyntax>();
            foreach (var call in calls)
            {
                if (call.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Named" } ||
                    model.GetSymbolInfo(call, cancellation).Symbol is not IMethodSymbol
                    {
                        ReducedFrom: { } original, ReturnType: INamedTypeSymbol partType
                    } || !SymbolEqualityComparer.Default.Equals(original.ContainingType, elementExtensions) ||
                    DerivesFrom(partType, view))
                {
                    continue;
                }

                if (call.ArgumentList.Arguments is not [{ Expression: var argument }] ||
                    model.GetConstantValue(argument, cancellation) is not { HasValue: true, Value: string name } ||
                    !SyntaxFacts.IsValidIdentifier(name) || SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ||
                    partType is not { DeclaredAccessibility: Accessibility.Public, ContainingType: null, IsGenericType: false })
                {
                    diagnostics.Add(Diagnostic.Create(InvalidPart, call.GetLocation()));
                    continue;
                }

                if (HasMember(owner, name))
                    diagnostics.Add(Diagnostic.Create(NameConflict, call.GetLocation(), owner.Name, name));
                else if (!parts.TryAdd(name, partType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
                    diagnostics.Add(Diagnostic.Create(DuplicateName, call.GetLocation(), owner.Name, name));
            }
        }

        var source = parts.Count == 0 ? null : Render(owner, extensionType, parts);
        return new($"{owner.ToDisplayString()}.ViewParts.g.cs", source, diagnostics.ToImmutable());
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

    private static string Render(INamedTypeSymbol owner, string extensionType, Dictionary<string, string> parts)
    {
        var source = new StringBuilder("// <auto-generated/>\n#nullable enable\n\n");
        if (!owner.ContainingNamespace.IsGlobalNamespace)
            source.Append("namespace ").Append(owner.ContainingNamespace.ToDisplayString()).AppendLine(";").AppendLine();

        var access = owner.DeclaredAccessibility == Accessibility.Public ? "public" : "internal";
        source.AppendLine("/// <summary>Provides typed access to the named parts of this view.</summary>");
        source.Append(access).Append(" static class ").AppendLine(extensionType).AppendLine("{");
        source.Append("    extension(").Append(owner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .AppendLine(" view)").AppendLine("    {");
        foreach (var (name, type) in parts.OrderBy(part => part.Key, StringComparer.Ordinal))
        {
            source.AppendLine("        /// <summary>Gets the live named part in this view's scope.</summary>");
            source.Append("        public ").Append(type).Append(' ').Append(name)
                .Append(" => view.GetRequiredPart<").Append(type).Append(">(")
                .Append(SymbolDisplay.FormatLiteral(name, quote: true)).AppendLine(");");
        }
        return source.AppendLine("    }").AppendLine("}").ToString();
    }

    private sealed record GeneratedView(string HintName, string? Source, ImmutableArray<Diagnostic> Diagnostics);
}
